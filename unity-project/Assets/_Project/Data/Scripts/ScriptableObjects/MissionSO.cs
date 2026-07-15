using System;
using System.Collections.Generic;
using UnityEngine;

namespace KINETICS5.Data
{
    /// <summary>Objectif individuel d'une mission. Équivalent runtime : <see cref="MissionObjectiveDto"/>.</summary>
    [Serializable]
    public sealed class MissionObjective
    {
        public string Id = string.Empty;
        [TextArea(2, 4)] public string Description = string.Empty;
        public ObjectiveKind Kind = ObjectiveKind.Reach;
        [Tooltip("Id de la cible (ennemi, objet, zone...) si applicable.")]
        public string TargetId = string.Empty;
        public int RequiredCount = 1;
        public int RewardXp;
        public int RewardCr;
    }

    /// <summary>Vague d'apparition d'ennemis. Équivalent runtime : <see cref="EnemySpawnWaveDto"/>.</summary>
    [Serializable]
    public sealed class EnemySpawnWave
    {
        public string Id = string.Empty;
        [Tooltip("Délai en secondes avant le début de cette vague.")]
        public float Delay;
        [Tooltip("Id de l'ennemi (EnemySO.Id) à faire apparaître.")]
        public string EnemyId = string.Empty;
        public int Count = 1;
        public Vector3 SpawnPoint = Vector3.zero;
    }

    /// <summary>Phase d'un combat de boss. Équivalent runtime : <see cref="BossPhaseDto"/>.</summary>
    [Serializable]
    public sealed class BossPhaseData
    {
        public string Id = string.Empty;
        public int Phase = 1;
        [Tooltip("Id de l'ennemi boss (EnemySO.Id) pour cette phase.")]
        public string EnemyId = string.Empty;
        [Tooltip("Seuil de PV (0..1) déclenchant la phase suivante.")]
        [Range(0f, 1f)] public float HealthThresholdPct = 0.5f;
        [Tooltip("Timer d'enrage en secondes (0 = aucun).")]
        public float EnrageTimer;
        [TextArea(2, 4)] public string Description = string.Empty;
    }

    /// <summary>Entrée de table de loot. Équivalent runtime : <see cref="LootTableEntryDto"/>.</summary>
    [Serializable]
    public sealed class LootTableEntry
    {
        public string ItemId = string.Empty;
        [Range(0f, 100f)] public float DropChancePct;
        public int MinQty = 1;
        public int MaxQty = 1;
    }

    /// <summary>Récompenses d'une mission. Équivalent runtime : <see cref="RewardDataDto"/>.</summary>
    [Serializable]
    public sealed class RewardData
    {
        public int Xp;
        public int Cr;
        public List<LootTableEntry> LootTable = new();
    }

    /// <summary>Configuration d'environnement d'une mission. Équivalent runtime : <see cref="EnvironmentDataDto"/>.</summary>
    [Serializable]
    public sealed class EnvironmentData
    {
        public ShipType ShipType = ShipType.CargoShip;
        public Lighting Lighting = Lighting.Dim;
        public Atmosphere Atmosphere = Atmosphere.Vacuum;
    }

    /// <summary>
    /// Mission KINETICS 5.
    /// Asset authoring éditeur ; équivalent runtime : <see cref="MissionDto"/>
    /// chargé depuis <c>Resources/Data/missions.json</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "Mission", menuName = "KINETICS 5/Mission", order = 12)]
    public sealed class MissionSO : ScriptableObject
    {
        [Header("Identité")]
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        [TextArea(2, 8)] public string Description = string.Empty;
        public MissionType Type = MissionType.Extraction;
        [Tooltip("Id de la région hôte.")]
        public string Region = string.Empty;
        public int RecommendedPower = 2000;

        [Header("Objectifs & vagues")]
        public List<MissionObjective> Objectives = new();
        public List<EnemySpawnWave> Waves = new();
        public List<BossPhaseData> BossPhases = new();

        [Header("Contraintes")]
        [Tooltip("Limite de temps en secondes (0 = aucune).")]
        public int TimeLimit;
        [Tooltip("Vrai si une approche furtive est possible.")]
        public bool StealthOptional;

        [Header("Récompenses")]
        public RewardData Rewards = new();

        [Header("Scène & environnement")]
        [Tooltip("Nom de la scène Unity à charger.")]
        public string SceneName = string.Empty;
        public EnvironmentData Environment = new();
    }
}
