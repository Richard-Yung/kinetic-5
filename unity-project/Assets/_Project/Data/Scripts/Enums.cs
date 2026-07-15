using System;

namespace KINETICS5.Data
{
    /// <summary>
    /// Énumérations partagées du domaine KINETICS 5.
    /// Toutes les énumérations sont centralisées dans ce fichier afin de garantir
    /// la cohérence entre les ScriptableObjects (authoring éditeur), les fichiers
    /// JSON (<c>Resources/Data/*.json</c>) et le runtime (<see cref="DataLoader"/>).
    /// Les valeurs JSON doivent utiliser la forme PascalCase (ex: <c>"Tank"</c>,
    /// <c>"AssaultRifle"</c>) — la désérialisation Newtonsoft est insensible à la casse.
    /// </summary>

    /// <summary>Classe d'agent (personnage jouable).</summary>
    public enum AgentClass
    {
        /// <summary>Tank : haute défense, mobilité réduite (ex: VULCAN).</summary>
        Tank,
        /// <summary>Assault : DPS polyvalent, mobilité élevée (ex: XEN).</summary>
        Assault,
        /// <summary>Recon : furtif, éliminations silencieuses (ex: XANO).</summary>
        Recon,
        /// <summary>Support : soin, EMP, utilitaire (ex: JOLT).</summary>
        Support
    }

    /// <summary>Emplacement d'arme dans le loadout.</summary>
    public enum WeaponCategory
    {
        /// <summary>Arme principale (fusil, sniper, lourd...).</summary>
        Primary,
        /// <summary>Arme secondaire (pistolet).</summary>
        Secondary,
        /// <summary>Arme tactique (grenade, gadget, piège).</summary>
        Tactical
    }

    /// <summary>Type technique d'arme.</summary>
    public enum WeaponType
    {
        AssaultRifle,
        SMG,
        Shotgun,
        Sniper,
        Heavy,
        Pistol,
        Grenade,
        Trap,
        Special
    }

    /// <summary>Rareté d'un objet (impacte le loot et le power score).</summary>
    public enum Rarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>Élément de dégâts (résonance / faiblesses ennemies).</summary>
    public enum Element
    {
        /// <summary>Cinétique : balles physiques, résistance courante.</summary>
        Kinetic,
        /// <summary>Énergie : plasma, bypass partiel d'armure.</summary>
        Energy,
        /// <summary>Explosif : dégâts de zone.</summary>
        Explosive,
        /// <summary>Cryo : ralentit et fragilise.</summary>
        Cryo,
        /// <summary>Volt : étourdit et désactive la tech.</summary>
        Volt
    }

    /// <summary>Mode de tir d'une arme.</summary>
    public enum FireMode
    {
        /// <summary>Coup par coup.</summary>
        Single,
        /// <summary>Rafale courte (ex: 3 coups).</summary>
        Burst,
        /// <summary>Tir automatique continu.</summary>
        Auto
    }

    /// <summary>Type de mission.</summary>
    public enum MissionType
    {
        Extraction,
        Sabotage,
        Survival,
        Assassination,
        Recon,
        Defense,
        BossRush
    }

    /// <summary>Nature d'un objectif de mission.</summary>
    public enum ObjectiveKind
    {
        Reach,
        Eliminate,
        Collect,
        Sabotage,
        Escort,
        Defend,
        Extract,
        Assassinate,
        Scan,
        Survive
    }

    /// <summary>Classe d'ennemi (détermine comportement et récompenses).</summary>
    public enum EnemyClass
    {
        Grunt,
        Soldier,
        Elite,
        Sniper,
        Heavy,
        Drone,
        Boss
    }

    /// <summary>Comportement d'IA de l'ennemi.</summary>
    public enum AIBehavior
    {
        Patrol,
        Aggressive,
        Defensive,
        Flanking,
        Berserker,
        Sniper
    }

    /// <summary>Effet produit par une compétence d'agent.</summary>
    public enum AbilityEffectType
    {
        Damage,
        Heal,
        Shield,
        SpeedBuff,
        DamageReduction,
        Stun,
        Cloak,
        EMP,
        Slow,
        Pull,
        Knockback,
        DamageOverTime
    }

    /// <summary>Effet produit par un objet tactique (grenade/gadget).</summary>
    public enum TacticalEffectType
    {
        Damage,
        Stun,
        Slow,
        Trap,
        Decoy,
        EMP
    }

    /// <summary>Type de vaisseau / décor de la mission.</summary>
    public enum ShipType
    {
        CargoShip,
        HeavyCruiser,
        OrbitalStation,
        DroneFactory,
        Derelict,
        Carrier,
        Flagship
    }

    /// <summary>Configuration d'éclairage de l'environnement.</summary>
    public enum Lighting
    {
        Dim,
        Emergency,
        Industrial,
        Neon,
        Storm,
        Void
    }

    /// <summary>Atmosphère / post-FX volumétrique de l'environnement.</summary>
    public enum Atmosphere
    {
        Vacuum,
        Toxic,
        Ice,
        Steam,
        Smoke,
        VoidStorm
    }

    /// <summary>Type de nœud de talent dans l'arbre d'éveil d'un agent.</summary>
    public enum TalentType
    {
        /// <summary>Bonus de statistique passif.</summary>
        Stat,
        /// <summary>Amélioration d'une compétence existante.</summary>
        Ability,
        /// <summary>Effet passif conditionnel.</summary>
        Passive,
        /// <summary>Amélioration de l'ultimate.</summary>
        Ultimate
    }

    /// <summary>
    /// Utilitaires d'analyse de chaînes vers les énumérations du domaine.
    /// Tolérants à la casse et aux valeurs inconnues (retourne une valeur par défaut).
    /// </summary>
    public static class EnumParser
    {
        /// <summary>
        /// Tente de convertir une chaîne en énumération ; insensible à la casse.
        /// </summary>
        /// <param name="value">Chaîne source (ex: "Tank").</param>
        /// <param name="defaultValue">Valeur retournée si l'analyse échoue.</param>
        /// <typeparam name="T">Type d'énumération.</typeparam>
        /// <returns>Valeur énumérée ou <paramref name="defaultValue"/>.</returns>
        public static T Parse<T>(string? value, T defaultValue = default!) where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return Enum.TryParse(value, ignoreCase: true, out T result) ? result : defaultValue;
        }
    }
}
