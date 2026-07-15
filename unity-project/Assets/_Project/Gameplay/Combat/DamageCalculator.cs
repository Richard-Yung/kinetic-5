// ============================================================================
//  KINETICS 5 — Damage Calculator (formule de dégâts)
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Calcul centralisé des dégâts d'un tir / ability / explosion.
//  Formule :
//    baseDamage = weapon.DamagePct × AGENT_POWER_SCALE (× 0.5 pour balance)
//    elementMult = 1.5 si element == enemyWeakness, 0.5 si == enemyResistance, 1 sinon
//    headshotMult = 2.0 (headshot) / 1.0
//    critMult = 1.5 (critique) / 1.0 (critique ET headshot non cumulables : max)
//    distanceMult = lerp(1.0, 0.3, dist / weapon.Range)  // falloff linéaire
//    armorMult = 1 - enemy.Armor * 0.01  (0..0.95)
//    finalDamage = baseDamage × elementMult × headshotMult × critMult × distanceMult × armorMult
//
//  Plafonné à 9999 (anti-abus). Jamais négatif. Arrondi à l'entier le plus proche.
// ============================================================================
using System;
using UnityEngine;
using KINETICS5.Data;

namespace KINETICS5.Gameplay.Combat
{
    /// <summary>Statistiques d'entrée du calcul de dégâts.</summary>
    public readonly struct DamageInput
    {
        public readonly string WeaponId;
        public readonly string EnemyId;
        public readonly Element Element;
        public readonly bool IsHeadshot;
        public readonly bool IsCritical;
        public readonly float Distance;

        public DamageInput(string weaponId, string enemyId, Element element, bool isHeadshot, bool isCritical, float distance)
        {
            WeaponId = weaponId; EnemyId = enemyId; Element = element;
            IsHeadshot = isHeadshot; IsCritical = isCritical; Distance = distance;
        }
    }

    /// <summary>Résultat du calcul de dégâts (décomposé pour debug/affichage).</summary>
    public readonly struct DamageResult
    {
        public readonly float BaseDamage;
        public readonly float ElementMultiplier;
        public readonly float HeadshotMultiplier;
        public readonly float CritMultiplier;
        public readonly float DistanceMultiplier;
        public readonly float ArmorMultiplier;
        public readonly float FinalDamage;
        public readonly bool IsLethal;
        public readonly bool IsOverkill;

        public DamageResult(float baseDamage, float elemMult, float hsMult, float critMult,
                            float distMult, float armorMult, float finalDamage,
                            bool isLethal, bool isOverkill)
        {
            BaseDamage = baseDamage; ElementMultiplier = elemMult;
            HeadshotMultiplier = hsMult; CritMultiplier = critMult;
            DistanceMultiplier = distMult; ArmorMultiplier = armorMult;
            FinalDamage = finalDamage; IsLethal = isLethal; IsOverkill = isOverkill;
        }
    }

    /// <summary>
    /// Calculateur statique de dégâts. Utilisé par PlayerCombat, EnemyAI, MatchManager
    /// (validation), DamageNumber (affichage), AntiCheatValidator (bornes).
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>Échelle de puissance agent (ramène DamagePct à des PV raisonnables).</summary>
        public const float AgentPowerScale = 0.5f;
        /// <summary>Plafond absolu (anti-abus, anti-overflow).</summary>
        public const float DamageCap = 9999f;
        /// <summary>Headshot = ×2.0.</summary>
        public const float HeadshotMultiplier = 2.0f;
        /// <summary>Critique = ×1.5.</summary>
        public const float CritMultiplier = 1.5f;
        /// <summary>Bonus élémentaire (weakness).</summary>
        public const float WeaknessMultiplier = 1.5f;
        /// <summary>Pénalité élémentaire (resistance).</summary>
        public const float ResistanceMultiplier = 0.5f;
        /// <summary>Falloff à distance max de l'arme.</summary>
        public const float MinDistanceMultiplier = 0.3f;

        /// <summary>Calcule le résultat complet d'un coup.</summary>
        public static DamageResult Calculate(in DamageInput input, float enemyCurrentHealth, float enemyArmorPct)
        {
            var weapon = DataLoader.GetWeapon(input.WeaponId);
            var enemy = DataLoader.GetEnemy(input.EnemyId);

            float baseDamage = (weapon?.DamagePct ?? 0f) * AgentPowerScale;
            float elemMult = GetElementMultiplier(input.Element, enemy?.Weakness ?? Element.Kinetic, enemy?.Resistance ?? Element.Kinetic);
            float hsMult = input.IsHeadshot ? HeadshotMultiplier : 1f;
            float critMult = input.IsCritical ? CritMultiplier : 1f;
            // Headshot + crit non cumulables : on prend le max.
            float compositeMult = Mathf.Max(hsMult, critMult);
            float distMult = GetDistanceMultiplier(input.Distance, weapon?.Range ?? 100f);
            float armorMult = Mathf.Clamp(1f - Mathf.Clamp01(enemyArmorPct * 0.01f), 0.05f, 1f);

            float final = baseDamage * elemMult * compositeMult * distMult * armorMult;
            final = Mathf.Clamp(final, 0f, DamageCap);
            final = Mathf.Round(final);

            bool isLethal = final >= enemyCurrentHealth && enemyCurrentHealth > 0;
            bool isOverkill = final > enemyCurrentHealth + enemyCurrentHealth * 0.5f;

            return new DamageResult(
                baseDamage, elemMult, hsMult, critMult, distMult, armorMult,
                final, isLethal, isOverkill);
        }

        /// <summary>Calcul rapide sans structure d'entrée (hot path combat).</summary>
        public static float CalculateFast(string weaponId, string enemyId, Element element, bool isHeadshot, bool isCritical, float distance, float enemyArmorPct)
        {
            var weapon = DataLoader.GetWeapon(weaponId);
            var enemy = DataLoader.GetEnemy(enemyId);
            float baseDamage = (weapon?.DamagePct ?? 0f) * AgentPowerScale;
            float elemMult = GetElementMultiplier(element, enemy?.Weakness ?? Element.Kinetic, enemy?.Resistance ?? Element.Kinetic);
            float compositeMult = Mathf.Max(isHeadshot ? HeadshotMultiplier : 1f, isCritical ? CritMultiplier : 1f);
            float distMult = GetDistanceMultiplier(distance, weapon?.Range ?? 100f);
            float armorMult = Mathf.Clamp(1f - Mathf.Clamp01(enemyArmorPct * 0.01f), 0.05f, 1f);
            return Mathf.Clamp(Mathf.Round(baseDamage * elemMult * compositeMult * distMult * armorMult), 0f, DamageCap);
        }

        /// <summary>Multiplicateur élémentaire selon faiblesse/résistance ennemi.</summary>
        public static float GetElementMultiplier(Element attackElement, Element enemyWeakness, Element enemyResistance)
        {
            if (attackElement == enemyWeakness)   return WeaknessMultiplier;
            if (attackElement == enemyResistance) return ResistanceMultiplier;
            return 1f;
        }

        /// <summary>Multiplicateur de distance (falloff linéaire).</summary>
        public static float GetDistanceMultiplier(float distance, float weaponRange)
        {
            if (weaponRange <= 0f) return 1f;
            float t = Mathf.Clamp01(distance / weaponRange);
            return Mathf.Lerp(1f, MinDistanceMultiplier, t);
        }
    }
}
