using System;
using System.Collections.Generic;
using UnityEngine;

namespace KINETICS5.Data
{
    /// <summary>
    /// Compétence active d'un agent (authoring ScriptableObject).
    /// Équivalent runtime : <see cref="AbilityDto"/>.
    /// </summary>
    [Serializable]
    public sealed class AbilityData
    {
        [Tooltip("Identifiant unique de la compétence (ex: vulcan_aegis).")]
        public string Id = string.Empty;
        public string Name = string.Empty;
        [TextArea(2, 6)] public string Description = string.Empty;
        public AbilityEffectType EffectType = AbilityEffectType.Damage;
        [Tooltip("Temps de recharge en secondes.")]
        [Range(0f, 180f)] public float Cooldown = 5f;
        [Tooltip("Magnitude de l'effet (dégâts, soin, % de réduction, etc.).")]
        public float Magnitude = 1f;
    }

    /// <summary>
    /// Nœud de talent de l'arbre d'éveil d'un agent.
    /// Équivalent runtime : <see cref="TalentNodeDto"/>.
    /// </summary>
    [Serializable]
    public sealed class TalentNode
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        [TextArea(2, 6)] public string Description = string.Empty;
        public TalentType Type = TalentType.Stat;
        [Tooltip("Coût en points de talent.")]
        [Range(1, 10)] public int Cost = 1;
        [Tooltip("Ids des nœuds requis pour débloquer celui-ci.")]
        public List<string> RequiredNodeIds = new();
        public AbilityEffectType EffectType = AbilityEffectType.Heal;
        public float Magnitude = 1f;
    }

    /// <summary>
    /// Agent (personnage jouable) KINETICS 5.
    /// Asset authoring éditeur ; équivalent runtime data-driven : <see cref="AgentDto"/>
    /// chargé depuis <c>Resources/Data/agents.json</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "Agent", menuName = "KINETICS 5/Agent", order = 10)]
    public sealed class AgentSO : ScriptableObject
    {
        [Header("Identité")]
        [Tooltip("Identifiant unique (ex: vulcan, xen, jolt, xano).")]
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public AgentClass Class = AgentClass.Tank;
        [Tooltip("Niveau joueur requis pour débloquer cet agent.")]
        public int UnlockLevel = 1;

        [Header("Lore")]
        [TextArea(2, 8)] public string Description = string.Empty;
        [TextArea] public string Motto = string.Empty;
        public Sprite? Portrait;
        public GameObject? ModelPrefab;

        [Header("Statistiques de base")]
        public int BaseHealth = 3000;
        public int BaseShield = 1500;
        [Tooltip("Multiplicateur de vitesse (1 = référence).")]
        [Range(0.1f, 3f)] public float BaseSpeed = 1f;
        public int BasePower = 2000;
        [Tooltip("Couleur de thème de l'agent (palette KINETICS 5).")]
        public Color ThemeColor = new(0.102f, 0.631f, 0.808f, 1f); // #1AA1CE

        [Header("Compétences")]
        public List<AbilityData> Abilities = new();

        [Header("Arbre d'Éveil")]
        public List<TalentNode> AwakeningTree = new();
    }
}
