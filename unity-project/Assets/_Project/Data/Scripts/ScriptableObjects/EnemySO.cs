using System;
using System.Collections.Generic;
using UnityEngine;

namespace KINETICS5.Data
{
    /// <summary>Drop de loot d'un ennemi vaincu. Équivalent runtime : <see cref="LootDropDto"/>.</summary>
    [Serializable]
    public sealed class LootDrop
    {
        public string ItemId = string.Empty;
        [Range(0f, 100f)] public float DropChancePct;
        public int MinQty = 1;
        public int MaxQty = 1;
    }

    /// <summary>
    /// Archétype d'ennemi KINETICS 5.
    /// Asset authoring éditeur ; équivalent runtime : <see cref="EnemyDto"/>
    /// chargé depuis <c>Resources/Data/enemies.json</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "KINETICS 5/Enemy", order = 13)]
    public sealed class EnemySO : ScriptableObject
    {
        [Header("Identité")]
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public EnemyClass Class = EnemyClass.Soldier;
        public Sprite? Icon;
        public GameObject? ModelPrefab;

        [Header("Statistiques")]
        public int BaseHealth = 1000;
        public int BaseShield;
        public int BaseDamage = 50;
        [Range(0.1f, 20f)] public float MoveSpeed = 3f;
        [Range(0f, 200f)] public float AttackRange = 20f;
        [Range(0.1f, 10f)] public float AttackRate = 1f;

        [Header("IA & éléments")]
        public AIBehavior Behavior = AIBehavior.Aggressive;
        [Tooltip("Élément qui inflige des dégâts bonus à cet ennemi.")]
        public Element Weakness = Element.Kinetic;
        [Tooltip("Élément auquel cet ennemi résiste.")]
        public Element Resistance = Element.Kinetic;

        [Header("Récompenses")]
        public int XpReward = 100;
        public int CrReward = 50;
        public List<LootDrop> LootTable = new();
    }
}
