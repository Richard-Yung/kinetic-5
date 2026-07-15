using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace KINETICS5.Data
{
    // =================================================================================
    //  DTO (Data Transfer Objects) — formes POCO désérialisées depuis les fichiers
    //  JSON de Resources/Data/. Ces classes constituent la source de vérité runtime
    //  (data-driven) du projet. Les ScriptableObjects (dossier ScriptableObjects/)
    //  en sont l'équivalent éditeur pour l'authoring visuel.
    //  Convention JSON : camelCase pour les clés, enums en PascalCase.
    // =================================================================================

    /// <summary>
    /// Vecteur 3 sérialisable en JSON sous la forme <c>{ "x": 0, "y": 0, "z": 0 }</c>.
    /// Utilisé pour les points d'apparition des vagues ennemies.
    /// </summary>
    [Serializable]
    public sealed class Vector3Dto
    {
        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }
        [JsonProperty("z")] public float Z { get; set; }

        /// <summary>Convertit ce DTO en <see cref="Vector3"/> Unity.</summary>
        public Vector3 ToVector3() => new(X, Y, Z);
    }

    // ---------------------------------------------------------------------------------
    //  AGENTS
    // ---------------------------------------------------------------------------------

    /// <summary>Compétence active d'un agent.</summary>
    [Serializable]
    public sealed class AbilityDto
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("name")] public string Name { get; set; } = string.Empty;
        [JsonProperty("description")] public string Description { get; set; } = string.Empty;
        [JsonProperty("cooldown")] public float Cooldown { get; set; }
        [JsonProperty("effectType")] public AbilityEffectType EffectType { get; set; }
        [JsonProperty("magnitude")] public float Magnitude { get; set; }
    }

    /// <summary>Nœud de talent dans l'arbre d'éveil d'un agent.</summary>
    [Serializable]
    public sealed class TalentNodeDto
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("name")] public string Name { get; set; } = string.Empty;
        [JsonProperty("description")] public string Description { get; set; } = string.Empty;
        [JsonProperty("type")] public TalentType Type { get; set; }
        [JsonProperty("cost")] public int Cost { get; set; } = 1;
        [JsonProperty("requiredNodeIds")] public List<string> RequiredNodeIds { get; set; } = new();
        [JsonProperty("effectType")] public AbilityEffectType EffectType { get; set; }
        [JsonProperty("magnitude")] public float Magnitude { get; set; }
    }

    /// <summary>Agent (personnage jouable) KINETICS 5.</summary>
    [Serializable]
    public sealed class AgentDto
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("displayName")] public string DisplayName { get; set; } = string.Empty;
        [JsonProperty("class")] public AgentClass Class { get; set; }
        [JsonProperty("unlockLevel")] public int UnlockLevel { get; set; } = 1;
        [JsonProperty("level")] public int Level { get; set; } = 1;
        [JsonProperty("description")] public string Description { get; set; } = string.Empty;
        [JsonProperty("motto")] public string Motto { get; set; } = string.Empty;
        [JsonProperty("portrait")] public string Portrait { get; set; } = string.Empty;
        [JsonProperty("modelPrefab")] public string ModelPrefab { get; set; } = string.Empty;
        [JsonProperty("baseHealth")] public int BaseHealth { get; set; }
        [JsonProperty("baseShield")] public int BaseShield { get; set; }
        [JsonProperty("baseSpeed")] public float BaseSpeed { get; set; } = 1f;
        [JsonProperty("basePower")] public int BasePower { get; set; }
        [JsonProperty("themeColor")] [JsonConverter(typeof(HexColorConverter))]
        public Color ThemeColor { get; set; } = new(0.102f, 0.631f, 0.808f, 1f);
        [JsonProperty("abilities")] public List<AbilityDto> Abilities { get; set; } = new();
        [JsonProperty("awakeningTree")] public List<TalentNodeDto> AwakeningTree { get; set; } = new();
    }

    // ---------------------------------------------------------------------------------
    //  ARMES
    // ---------------------------------------------------------------------------------

    /// <summary>Paramètres de projectile d'une arme à tir non-hitscan.</summary>
    [Serializable]
    public sealed class ProjectileDto
    {
        [JsonProperty("speed")] public float Speed { get; set; } = 300f;
        [JsonProperty("drop")] public float Drop { get; set; }
        [JsonProperty("penetration")] public float Penetration { get; set; }
    }

    /// <summary>Arme (primaire, secondaire ou tactique) du loadout.</summary>
    [Serializable]
    public sealed class WeaponDto
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("displayName")] public string DisplayName { get; set; } = string.Empty;
        [JsonProperty("category")] public WeaponCategory Category { get; set; }
        [JsonProperty("type")] public WeaponType Type { get; set; }
        [JsonProperty("power")] public int Power { get; set; }
        [JsonProperty("reloadTime")] public float ReloadTime { get; set; }
        [JsonProperty("damagePct")] public float DamagePct { get; set; }
        [JsonProperty("fireRatePct")] public float FireRatePct { get; set; }
        [JsonProperty("accuracyPct")] public float AccuracyPct { get; set; }
        [JsonProperty("stabilityPct")] public float StabilityPct { get; set; }
        [JsonProperty("explosionRadiusPct")] public float ExplosionRadiusPct { get; set; }
        [JsonProperty("fuseTime")] public float FuseTime { get; set; }
        [JsonProperty("rarity")] public Rarity Rarity { get; set; }
        [JsonProperty("icon")] public string Icon { get; set; } = string.Empty;
        [JsonProperty("modelPrefab")] public string ModelPrefab { get; set; } = string.Empty;
        [JsonProperty("projectile")] public ProjectileDto Projectile { get; set; } = new();
        [JsonProperty("fireModes")] public List<FireMode> FireModes { get; set; } = new();
        [JsonProperty("magazineSize")] public int MagazineSize { get; set; } = 30;
        [JsonProperty("range")] public float Range { get; set; } = 100f;
        [JsonProperty("element")] public Element Element { get; set; }

        /// <summary>Vrai si cette entrée d'arme est un objet tactique (grenade/gadget).</summary>
        [JsonIgnore]
        public bool IsTactical => Category == WeaponCategory.Tactical;
    }

    // ---------------------------------------------------------------------------------
    //  MISSIONS
    // ---------------------------------------------------------------------------------

    /// <summary>Objectif individuel d'une mission.</summary>
    [Serializable]
    public sealed class MissionObjectiveDto
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("description")] public string Description { get; set; } = string.Empty;
        [JsonProperty("kind")] public ObjectiveKind Kind { get; set; }
        [JsonProperty("targetId")] public string TargetId { get; set; } = string.Empty;
        [JsonProperty("requiredCount")] public int RequiredCount { get; set; } = 1;
        [JsonProperty("rewardXp")] public int RewardXp { get; set; }
        [JsonProperty("rewardCr")] public int RewardCr { get; set; }
    }

    /// <summary>Vague d'apparition d'ennemis.</summary>
    [Serializable]
    public sealed class EnemySpawnWaveDto
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("delay")] public float Delay { get; set; }
        [JsonProperty("enemyId")] public string EnemyId { get; set; } = string.Empty;
        [JsonProperty("count")] public int Count { get; set; } = 1;
        [JsonProperty("spawnPoint")] public Vector3Dto SpawnPoint { get; set; } = new();
    }

    /// <summary>Phase d'un combat de boss (multi-phases).</summary>
    [Serializable]
    public sealed class BossPhaseDto
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("phase")] public int Phase { get; set; } = 1;
        [JsonProperty("enemyId")] public string EnemyId { get; set; } = string.Empty;
        [JsonProperty("healthThresholdPct")] public float HealthThresholdPct { get; set; } = 1f;
        [JsonProperty("enrageTimer")] public float EnrageTimer { get; set; }
        [JsonProperty("description")] public string Description { get; set; } = string.Empty;
    }

    /// <summary>Entrée de table de loot.</summary>
    [Serializable]
    public sealed class LootTableEntryDto
    {
        [JsonProperty("itemId")] public string ItemId { get; set; } = string.Empty;
        [JsonProperty("dropChancePct")] public float DropChancePct { get; set; }
        [JsonProperty("minQty")] public int MinQty { get; set; } = 1;
        [JsonProperty("maxQty")] public int MaxQty { get; set; } = 1;
    }

    /// <summary>Récompenses d'une mission.</summary>
    [Serializable]
    public sealed class RewardDataDto
    {
        [JsonProperty("xp")] public int Xp { get; set; }
        [JsonProperty("cr")] public int Cr { get; set; }
        [JsonProperty("lootTable")] public List<LootTableEntryDto> LootTable { get; set; } = new();
    }

    /// <summary>Configuration d'environnement d'une mission.</summary>
    [Serializable]
    public sealed class EnvironmentDataDto
    {
        [JsonProperty("shipType")] public ShipType ShipType { get; set; }
        [JsonProperty("lighting")] public Lighting Lighting { get; set; }
        [JsonProperty("atmosphere")] public Atmosphere Atmosphere { get; set; }
    }

    /// <summary>Mission KINETICS 5.</summary>
    [Serializable]
    public sealed class MissionDto
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("displayName")] public string DisplayName { get; set; } = string.Empty;
        [JsonProperty("description")] public string Description { get; set; } = string.Empty;
        [JsonProperty("type")] public MissionType Type { get; set; }
        [JsonProperty("region")] public string Region { get; set; } = string.Empty;
        [JsonProperty("recommendedPower")] public int RecommendedPower { get; set; }
        [JsonProperty("objectives")] public List<MissionObjectiveDto> Objectives { get; set; } = new();
        [JsonProperty("waves")] public List<EnemySpawnWaveDto> Waves { get; set; } = new();
        [JsonProperty("bossPhases")] public List<BossPhaseDto> BossPhases { get; set; } = new();
        [JsonProperty("timeLimit")] public int TimeLimit { get; set; }
        [JsonProperty("stealthOptional")] public bool StealthOptional { get; set; }
        [JsonProperty("rewards")] public RewardDataDto Rewards { get; set; } = new();
        [JsonProperty("sceneName")] public string SceneName { get; set; } = string.Empty;
        [JsonProperty("environment")] public EnvironmentDataDto Environment { get; set; } = new();
    }

    // ---------------------------------------------------------------------------------
    //  ENNEMIS
    // ---------------------------------------------------------------------------------

    /// <summary>Drop de loot d'un ennemi vaincu.</summary>
    [Serializable]
    public sealed class LootDropDto
    {
        [JsonProperty("itemId")] public string ItemId { get; set; } = string.Empty;
        [JsonProperty("dropChancePct")] public float DropChancePct { get; set; }
        [JsonProperty("minQty")] public int MinQty { get; set; } = 1;
        [JsonProperty("maxQty")] public int MaxQty { get; set; } = 1;
    }

    /// <summary>Archétype d'ennemi.</summary>
    [Serializable]
    public sealed class EnemyDto
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("displayName")] public string DisplayName { get; set; } = string.Empty;
        [JsonProperty("class")] public EnemyClass Class { get; set; }
        [JsonProperty("baseHealth")] public int BaseHealth { get; set; }
        [JsonProperty("baseShield")] public int BaseShield { get; set; }
        [JsonProperty("baseDamage")] public int BaseDamage { get; set; }
        [JsonProperty("moveSpeed")] public float MoveSpeed { get; set; } = 3f;
        [JsonProperty("attackRange")] public float AttackRange { get; set; } = 20f;
        [JsonProperty("attackRate")] public float AttackRate { get; set; } = 1f;
        [JsonProperty("behavior")] public AIBehavior Behavior { get; set; } = AIBehavior.Aggressive;
        [JsonProperty("weakness")] public Element Weakness { get; set; }
        [JsonProperty("resistance")] public Element Resistance { get; set; }
        [JsonProperty("icon")] public string Icon { get; set; } = string.Empty;
        [JsonProperty("modelPrefab")] public string ModelPrefab { get; set; } = string.Empty;
        [JsonProperty("xpReward")] public int XpReward { get; set; }
        [JsonProperty("crReward")] public int CrReward { get; set; }
        [JsonProperty("lootTable")] public List<LootDropDto> LootTable { get; set; } = new();

        /// <summary>Vrai si cet ennemi est un boss (multi-phases, grosse récompense).</summary>
        [JsonIgnore]
        public bool IsBoss => Class == EnemyClass.Boss;
    }

    // ---------------------------------------------------------------------------------
    //  RÉGIONS
    // ---------------------------------------------------------------------------------

    /// <summary>Preset d'environnement pour une région.</summary>
    [Serializable]
    public sealed class EnvironmentPresetDto
    {
        [JsonProperty("shipType")] public ShipType ShipType { get; set; }
        [JsonProperty("lighting")] public Lighting Lighting { get; set; }
        [JsonProperty("atmosphere")] public Atmosphere Atmosphere { get; set; }
    }

    /// <summary>Région / vaisseau hébergeant plusieurs missions.</summary>
    [Serializable]
    public sealed class RegionDto
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("displayName")] public string DisplayName { get; set; } = string.Empty;
        [JsonProperty("description")] public string Description { get; set; } = string.Empty;
        [JsonProperty("sceneName")] public string SceneName { get; set; } = string.Empty;
        [JsonProperty("recommendedLevel")] public int RecommendedLevel { get; set; } = 1;
        /// <summary>Ids de missions référencées (résolues via <see cref="DataLoader"/>).</summary>
        [JsonProperty("missions")] public List<string> Missions { get; set; } = new();
        [JsonProperty("ambientColor")] [JsonConverter(typeof(HexColorConverter))]
        public Color AmbientColor { get; set; } = new(0.102f, 0.631f, 0.808f, 1f);
        [JsonProperty("environment")] public EnvironmentPresetDto Environment { get; set; } = new();
    }

    // ---------------------------------------------------------------------------------
    //  OBJETS TACTIQUES (grenades / gadgets)
    //  Note : les armes tactiques apparaissent aussi dans weapons.json (category=Tactical)
    //  pour le loadout unifié. Ce DTO dédié porte les champs spécifiques aux gadgets.
    // ---------------------------------------------------------------------------------

    /// <summary>Objet tactique (grenade, gadget, piège, leurres).</summary>
    [Serializable]
    public sealed class TacticalDto
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("displayName")] public string DisplayName { get; set; } = string.Empty;
        [JsonProperty("description")] public string Description { get; set; } = string.Empty;
        [JsonProperty("effectType")] public TacticalEffectType EffectType { get; set; } = TacticalEffectType.Damage;
        [JsonProperty("power")] public int Power { get; set; }
        [JsonProperty("fuseTime")] public float FuseTime { get; set; } = 3f;
        [JsonProperty("explosionRadius")] public float ExplosionRadius { get; set; } = 8f;
        [JsonProperty("magnitude")] public float Magnitude { get; set; } = 1f;
        [JsonProperty("duration")] public float Duration { get; set; }
        [JsonProperty("rarity")] public Rarity Rarity { get; set; }
        [JsonProperty("icon")] public string Icon { get; set; } = string.Empty;
        [JsonProperty("modelPrefab")] public string ModelPrefab { get; set; } = string.Empty;
    }

    // ---------------------------------------------------------------------------------
    //  PROGRESSION (courbe XP)
    // ---------------------------------------------------------------------------------

    /// <summary>Palier de niveau de la courbe de progression.</summary>
    [Serializable]
    public sealed class ProgressionLevelDto
    {
        [JsonProperty("level")] public int Level { get; set; }
        /// <summary>XP nécessaire pour passer du niveau courant au suivant.</summary>
        [JsonProperty("xpRequired")] public int XpRequired { get; set; }
        /// <summary>XP total cumulé requis pour atteindre ce niveau.</summary>
        [JsonProperty("totalXp")] public int TotalXp { get; set; }
    }

    /// <summary>Courbe de progression joueur (XP → niveau).</summary>
    [Serializable]
    public sealed class ProgressionCurveDto
    {
        [JsonProperty("maxLevel")] public int MaxLevel { get; set; } = 60;
        [JsonProperty("levels")] public List<ProgressionLevelDto> Levels { get; set; } = new();
    }
}
