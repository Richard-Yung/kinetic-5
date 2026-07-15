using System;
using System.Collections.Generic;
using UnityEngine;

namespace KINETICS5.Data
{
    /// <summary>Preset d'environnement pour une région. Équivalent runtime : <see cref="EnvironmentPresetDto"/>.</summary>
    [Serializable]
    public sealed class EnvironmentPreset
    {
        public ShipType ShipType = ShipType.CargoShip;
        public Lighting Lighting = Lighting.Dim;
        public Atmosphere Atmosphere = Atmosphere.Vacuum;
    }

    /// <summary>
    /// Région / vaisseau hébergeant plusieurs missions.
    /// Asset authoring éditeur ; équivalent runtime : <see cref="RegionDto"/>
    /// chargé depuis <c>Resources/Data/regions.json</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "Region", menuName = "KINETICS 5/Region", order = 14)]
    public sealed class RegionSO : ScriptableObject
    {
        [Header("Identité")]
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        [TextArea(2, 8)] public string Description = string.Empty;
        [Tooltip("Nom de la scène Unity de fond de carte de la région.")]
        public string SceneName = string.Empty;
        public int RecommendedLevel = 1;

        [Header("Contenu")]
        [Tooltip("Missions référencées (résolues via DataLoader au runtime).")]
        public List<MissionSO> Missions = new();

        [Header("Ambiance")]
        [Tooltip("Couleur ambiante dominante de la région.")]
        public Color AmbientColor = new(0.102f, 0.631f, 0.808f, 1f); // #1AA1CE
        public EnvironmentPreset Environment = new();
    }
}
