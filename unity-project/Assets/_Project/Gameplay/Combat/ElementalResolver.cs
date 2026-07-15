// ============================================================================
//  KINETICS 5 — Elemental Resolver (matrice élémentaire + effets de statut)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Calcul centralisé des multiplicateurs élémentaires et de l'application
//  des effets de statut (Burn, Freeze, Shock, etc.). Consommé par PlayerCombat,
//  EnemyCombat, Projectile, DischargeSystem.
//
//  Règles de la matrice (résonance élémentaire) :
//    • Kinetic  vs shields   : ×1.5  (perce les boucliers)
//    • Energy   vs armor     : ×1.3  (bypass partiel d'armure)
//    • Cryo     vs Energy    : ×1.4  (extinction plasma)
//    • Volt     vs shields   : ×2.0  (surtension)
//    • Explosive universal   : ×1.1  (toujours légèrement bonus)
//
//  Effets de statut :
//    • Burn    (Flame/Heat)  : 5% PV/s pendant 4s, max 3 stacks (refresh)
//    • Freeze  (Cryo)        : ralentit 50% pendant 3s
//    • Shock   (Volt)        : étourdit 1s + chain-lightning (5m, 30% dégâts)
//    • Corrode (Energy)      : -10% armure pendant 5s, max 3 stacks
//    • Concussion (Explosive): 20% d'étourdissement, 0.3s
// ============================================================================
using System;
using KINETICS5.Data;
using UnityEngine;

namespace KINETICS5.Gameplay.Combat
{
    /// <summary>
    /// Type d'effet de statut appliqué par une attaque élémentaire.
    /// </summary>
    public enum StatusEffectType
    {
        /// <summary>Aucun effet (coup sec).</summary>
        None,
        /// <summary>Brûlure : dégâts sur la durée (5% PV/s, 4s, max 3 stacks).</summary>
        Burn,
        /// <summary>Gel : ralentit la cible de 50% pendant 3s.</summary>
        Freeze,
        /// <summary>Électrocution : étourdit 1s + chain-lightning sur cibles proches.</summary>
        Shock,
        /// <summary>Corrosion : réduit l'armure de 10% par stack pendant 5s.</summary>
        Corrode,
        /// <summary>Commotion : étourdissement court (0.3s) suite à une explosion.</summary>
        Concussion
    }

    /// <summary>
    /// Effet de statut élémentaire appliqué à une cible.
    /// Struct readonly pour zero-allocation lors du passage dans le bus d'événements.
    /// </summary>
    public readonly struct StatusEffect
    {
        /// <summary>Type d'effet.</summary>
        public readonly StatusEffectType Type;
        /// <summary>Durée totale en secondes.</summary>
        public readonly float Duration;
        /// <summary>Magnitude (fraction de PV/s pour Burn, % de ralentissement pour Freeze, etc.).</summary>
        public readonly float Magnitude;
        /// <summary>Nombre de stacks actuels (plafonné par <see cref="MaxStacks"/>).</summary>
        public readonly int Stacks;
        /// <summary>Nombre maximum de stacks empilables.</summary>
        public readonly int MaxStacks;
        /// <summary>Élément source de l'effet.</summary>
        public readonly Element SourceElement;

        /// <summary>True si l'effet est inactif (type None).</summary>
        public bool IsEmpty => Type == StatusEffectType.None;

        /// <summary>Constructeur complet.</summary>
        public StatusEffect(StatusEffectType type, float duration, float magnitude, int stacks, int maxStacks, Element sourceElement)
        {
            Type = type;
            Duration = Mathf.Max(0f, duration);
            Magnitude = magnitude;
            Stacks = Mathf.Max(1, stacks);
            MaxStacks = Mathf.Max(1, maxStacks);
            SourceElement = sourceElement;
        }

        /// <summary>Crée un effet de statut vide (None).</summary>
        public static StatusEffect None => new(StatusEffectType.None, 0f, 0f, 0, 0, Element.Kinetic);
    }

    /// <summary>
    /// Solveur élémentaire statique. Fournit :
    /// <list type="bullet">
    ///   <item><see cref="GetMultiplier"/> : multiplicateur de dégâts selon le couple attaque/défense.</item>
    ///   <item><see cref="ApplyStatus"/> : détermine l'effet de statut à infliger selon l'élément et une chance.</item>
    ///   <item><see cref="RefreshStacks"/> : règle de rafraîchissement des stacks (durée reset, stacks ++).</item>
    ///   <item><see cref="ComputeTickDamage"/> : dégâts par tick pour les DoT (Burn).</item>
    /// </list>
    /// Aucun état mutable : thread-safe et IL2CPP-friendly.
    /// </summary>
    public static class ElementalResolver
    {
        // --- Multiplicateurs de base (matrice élémentaire) ---
        // Indexé par [attaque, défense]. 0 = pas de bonus/malus spécial (×1.0).

        /// <summary>Kinetic vs Shield : perce les boucliers.</summary>
        public const float KineticVsShieldMult = 1.5f;
        /// <summary>Energy vs Armor : bypass partiel de l'armure.</summary>
        public const float EnergyVsArmorMult = 1.3f;
        /// <summary>Cryo vs Energy : éteint le plasma.</summary>
        public const float CryoVsEnergyMult = 1.4f;
        /// <summary>Volt vs Shield : surtension (plus efficace que Kinetic).</summary>
        public const float VoltVsShieldMult = 2.0f;
        /// <summary>Explosive : bonus universel léger.</summary>
        public const float ExplosiveUniversalMult = 1.1f;

        // --- Paramètres des effets de statut ---
        /// <summary>Durée par défaut de la brûlure (s).</summary>
        public const float BurnDuration = 4f;
        /// <summary>Dégâts de brûlure par seconde, en fraction des PV max de la cible.</summary>
        public const float BurnDpsFraction = 0.05f;
        /// <summary>Stacks max pour la brûlure.</summary>
        public const int BurnMaxStacks = 3;

        /// <summary>Durée du gel (s).</summary>
        public const float FreezeDuration = 3f;
        /// <summary>Ralentissement appliqué par le gel (50% = 0.5).</summary>
        public const float FreezeSlowFraction = 0.5f;
        /// <summary>Stacks max pour le gel (généralement 1).</summary>
        public const int FreezeMaxStacks = 1;

        /// <summary>Durée du shock (s).</summary>
        public const float ShockDuration = 1f;
        /// <summary>Portée du chain-lightning (m).</summary>
        public const float ShockChainRadius = 5f;
        /// <summary>Fraction de dégâts propagés aux cibles proches.</summary>
        public const float ShockChainDamageFraction = 0.3f;
        /// <summary>Stacks max pour le shock.</summary>
        public const int ShockMaxStacks = 1;

        /// <summary>Durée de corrosion (s).</summary>
        public const float CorrodeDuration = 5f;
        /// <summary>Réduction d'armure par stack (10% = 0.1).</summary>
        public const float CorrodeArmorReductionPerStack = 0.1f;
        /// <summary>Stacks max pour la corrosion.</summary>
        public const int CorrodeMaxStacks = 3;

        /// <summary>Durée de la commotion (s).</summary>
        public const float ConcussionDuration = 0.3f;
        /// <summary>Stacks max pour la commotion.</summary>
        public const int ConcussionMaxStacks = 1;

        /// <summary>
        /// Retourne le multiplicateur de dégâts à appliquer selon l'élément d'attaque
        /// et l'élément de défense de la cible (ou la nature de sa protection principale).
        /// </summary>
        /// <param name="attack">Élément de l'attaque (balle, projectile, explosion).</param>
        /// <param name="defense">Élément de défense (souvent le type de protection dominante : shield/armor).</param>
        /// <returns>Multiplicateur (1.0 = neutre).</returns>
        public static float GetMultiplier(Element attack, Element defense)
        {
            // Le tableau de résonance est volontairement petit ; les bonus spéciaux
            // sont des conditions explicites (lecture claire, optimisation compile-time).
            return (attack, defense) switch
            {
                (Element.Kinetic, Element.Energy)    => KineticVsShieldMult,   // boucliers énergétiques
                (Element.Energy, Element.Kinetic)    => EnergyVsArmorMult,     // armure cinétique
                (Element.Cryo, Element.Energy)       => CryoVsEnergyMult,      // plasma vs cryo
                (Element.Volt, Element.Energy)       => VoltVsShieldMult,      // boucliers vs volt
                (Element.Explosive, _)               => ExplosiveUniversalMult,// universel
                (_, _)                                => 1f
            };
        }

        /// <summary>
        /// Détermine si un effet de statut doit être appliqué selon l'élément et une chance.
        /// </summary>
        /// <param name="element">Élément de l'attaque.</param>
        /// <param name="chance">Chance d'application 0..1.</param>
        /// <returns>Effet de statut à appliquer, ou <see cref="StatusEffect.None"/> si l'élément n'a pas d'effet ou que la chance échoue.</returns>
        public static StatusEffect ApplyStatus(Element element, float chance)
        {
            if (chance <= 0f) return StatusEffect.None;

            StatusEffectType type = GetStatusTypeForElement(element);
            if (type == StatusEffectType.None) return StatusEffect.None;

            // Jet de chance (UnityEngine.Random valeur 0..1).
            if (UnityEngine.Random.value > chance) return StatusEffect.None;

            return CreateDefaultEffect(type, element);
        }

        /// <summary>
        /// Détermine le type d'effet de statut associé à chaque élément.
        /// </summary>
        /// <param name="element">Élément source.</param>
        /// <returns>Type d'effet, ou <see cref="StatusEffectType.None"/> si l'élément n'inflige pas de statut.</returns>
        public static StatusEffectType GetStatusTypeForElement(Element element)
        {
            return element switch
            {
                Element.Explosive => StatusEffectType.Concussion,
                Element.Cryo      => StatusEffectType.Freeze,
                Element.Volt      => StatusEffectType.Shock,
                Element.Energy    => StatusEffectType.Corrode,
                // Kinetic n'inflige pas de statut par défaut (coups physiques secs).
                _                  => StatusEffectType.None
            };
        }

        /// <summary>
        /// Crée un effet de statut avec les paramètres par défaut pour le type donné.
        /// </summary>
        /// <param name="type">Type d'effet.</param>
        /// <param name="sourceElement">Élément source.</param>
        /// <returns>Effet fraîchement créé (1 stack).</returns>
        public static StatusEffect CreateDefaultEffect(StatusEffectType type, Element sourceElement)
        {
            return type switch
            {
                StatusEffectType.Burn        => new StatusEffect(type, BurnDuration, BurnDpsFraction, 1, BurnMaxStacks, sourceElement),
                StatusEffectType.Freeze      => new StatusEffect(type, FreezeDuration, FreezeSlowFraction, 1, FreezeMaxStacks, sourceElement),
                StatusEffectType.Shock       => new StatusEffect(type, ShockDuration, ShockChainDamageFraction, 1, ShockMaxStacks, sourceElement),
                StatusEffectType.Corrode     => new StatusEffect(type, CorrodeDuration, CorrodeArmorReductionPerStack, 1, CorrodeMaxStacks, sourceElement),
                StatusEffectType.Concussion  => new StatusEffect(type, ConcussionDuration, 0f, 1, ConcussionMaxStacks, sourceElement),
                _                             => StatusEffect.None
            };
        }

        /// <summary>
        /// Règle de gestion des stacks : rafraîchit la durée (max entre durée restante et nouvelle durée),
        /// incrémente les stacks (plafonné par <see cref="StatusEffect.MaxStacks"/>).
        /// </summary>
        /// <param name="current">Effet actuellement actif sur la cible (peut être None).</param>
        /// <param name="newEffect">Nouvel effet à appliquer.</param>
        /// <returns>Effet fusionné.</returns>
        public static StatusEffect RefreshStacks(in StatusEffect current, in StatusEffect newEffect)
        {
            if (newEffect.IsEmpty) return current;
            if (current.IsEmpty || current.Type != newEffect.Type)
            {
                // Nouvel effet ou type différent : remplace.
                return newEffect;
            }
            // Même type : refresh durée, incrémente stacks (cap maxStacks).
            int newStacks = Mathf.Min(current.MaxStacks, current.Stacks + 1);
            // Durée = max(restante, nouvelle) — ici on suppose que current.Duration est la durée restante.
            float maxDuration = Mathf.Max(current.Duration, newEffect.Duration);
            return new StatusEffect(newEffect.Type, maxDuration, newEffect.Magnitude, newStacks, newEffect.MaxStacks, newEffect.SourceElement);
        }

        /// <summary>
        /// Calcule les dégâts par tick (par seconde) pour un effet de statut DoT.
        /// </summary>
        /// <param name="effect">Effet actif.</param>
        /// <param name="targetMaxHealth">PV max de la cible (pour les DoT en %).</param>
        /// <returns>Dégâts par seconde.</returns>
        public static float ComputeTickDamage(in StatusEffect effect, float targetMaxHealth)
        {
            if (effect.IsEmpty || effect.Type != StatusEffectType.Burn) return 0f;
            return effect.Magnitude * targetMaxHealth * effect.Stacks;
        }

        /// <summary>
        /// Calcule la réduction d'armure totale (fraction 0..1) pour un effet Corrode actif.
        /// </summary>
        /// <param name="effect">Effet Corrode actif.</param>
        /// <returns>Fraction de réduction (0 = pas de réduction, 0.3 = -30% armure).</returns>
        public static float ComputeArmorReduction(in StatusEffect effect)
        {
            if (effect.IsEmpty || effect.Type != StatusEffectType.Corrode) return 0f;
            return effect.Magnitude * effect.Stacks;
        }

        /// <summary>
        /// Calcule le facteur de ralentissement (0..1) à appliquer à la vitesse de déplacement.
        /// </summary>
        /// <param name="effect">Effet Freeze actif.</param>
        /// <returns>Facteur multiplicatif de vitesse (1 = normal, 0.5 = ralenti 50%).</returns>
        public static float ComputeSlowFactor(in StatusEffect effect)
        {
            if (effect.IsEmpty || effect.Type != StatusEffectType.Freeze) return 1f;
            return 1f - effect.Magnitude; // Magnitude = 0.5 => facteur 0.5
        }

        /// <summary>
        /// Tique un effet de statut (décrémente la durée restante).
        /// </summary>
        /// <param name="effect">Effet à ticker.</param>
        /// <param name="deltaTime">Temps écoulé (s).</param>
        /// <returns>Effet mis à jour (avec durée restante), ou None si expiré.</returns>
        public static StatusEffect Tick(in StatusEffect effect, float deltaTime)
        {
            if (effect.IsEmpty) return StatusEffect.None;
            float remaining = effect.Duration - deltaTime;
            if (remaining <= 0f) return StatusEffect.None;
            return new StatusEffect(effect.Type, remaining, effect.Magnitude, effect.Stacks, effect.MaxStacks, effect.SourceElement);
        }

        /// <summary>
        /// Couleur d'affichage (HUD/VFX) associée à un élément.
        /// </summary>
        /// <param name="element">Élément.</param>
        /// <returns>Couleur Unity (palette KINETICS 5).</returns>
        public static Color GetElementColor(Element element)
        {
            return element switch
            {
                Element.Kinetic    => new Color(1f, 1f, 1f, 1f),         // #FFFFFF
                Element.Energy     => new Color(0.102f, 0.631f, 0.808f, 1f), // #1AA1CE cyan
                Element.Cryo       => new Color(0.376f, 0.647f, 0.980f, 1f), // #60A5FA bleu
                Element.Volt       => new Color(1f, 0.906f, 0.208f, 1f), // #FFE735 jaune
                Element.Explosive  => new Color(0.976f, 0.451f, 0.086f, 1f), // #F97316 orange
                _                   => Color.white
            };
        }

        /// <summary>
        /// Couleur d'affichage pour un effet de statut (couleur de l'icône HUD).
        /// </summary>
        /// <param name="type">Type d'effet.</param>
        /// <returns>Couleur Unity.</returns>
        public static Color GetStatusColor(StatusEffectType type)
        {
            return type switch
            {
                StatusEffectType.Burn        => new Color(0.976f, 0.451f, 0.086f, 1f), // orange
                StatusEffectType.Freeze      => new Color(0.376f, 0.647f, 0.980f, 1f), // bleu clair
                StatusEffectType.Shock       => new Color(1f, 0.906f, 0.208f, 1f),     // jaune
                StatusEffectType.Corrode     => new Color(0.424f, 0.957f, 0.173f, 1f), // #6CF42E vert
                StatusEffectType.Concussion  => new Color(0.996f, 0.0f, 0.133f, 1f),   // #FE0022 rouge
                _                             => Color.white
            };
        }
    }
}
