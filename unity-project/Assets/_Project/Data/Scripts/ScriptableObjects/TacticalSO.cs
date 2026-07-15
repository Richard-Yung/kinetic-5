using System;
using UnityEngine;

namespace KINETICS5.Data
{
    /// <summary>
    /// Objet tactique (grenade, gadget, piège, leurre) KINETICS 5.
    /// Définition séparée de <see cref="WeaponSO"/> pour clarifier l'authoring
    /// des gadgets à effet non-létal (EMP, leurres, pièges).
    /// Au runtime, les armes de catégorie <c>Tactical</c> dans
    /// <c>Resources/Data/weapons.json</c> constituent la source de données
    /// principale du loadout ; ce SO sert de schéma d'authoring complémentaire.
    /// </summary>
    [CreateAssetMenu(fileName = "Tactical", menuName = "KINETICS 5/Tactical Item", order = 15)]
    public sealed class TacticalSO : ScriptableObject
    {
        [Header("Identité")]
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        [TextArea(2, 6)] public string Description = string.Empty;
        public TacticalEffectType EffectType = TacticalEffectType.Damage;
        public Rarity Rarity = Rarity.Common;

        [Header("Effet")]
        [Range(0, 5000)] public int Power = 1000;
        [Tooltip("Temps de fuse avant détonation (s). 0 = à l'impact.")]
        [Range(0f, 30f)] public float FuseTime = 3f;
        [Tooltip("Rayon d'explosion en mètres.")]
        [Range(0f, 50f)] public float ExplosionRadius = 8f;
        [Tooltip("Magnitude de l'effet (dégâts, durée d'étourdissement, etc.).")]
        public float Magnitude = 1f;
        [Tooltip("Durée de l'effet en secondes (slow, stun, EMP...).")]
        public float Duration;

        [Header("Ressources")]
        public Sprite? Icon;
        public GameObject? ModelPrefab;
    }
}
