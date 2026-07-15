using System;
using System.Collections.Generic;
using UnityEngine;

namespace KINETICS5.Data
{
    /// <summary>
    /// Paramètres de projectile (authoring ScriptableObject).
    /// Équivalent runtime : <see cref="ProjectileDto"/>.
    /// </summary>
    [Serializable]
    public sealed class ProjectileData
    {
        [Tooltip("Vitesse du projectile (m/s).")]
        public float Speed = 300f;
        [Tooltip("Chute gravitationnelle appliquée au projectile.")]
        public float Drop = 0f;
        [Tooltip("Pénétration d'ennemis (0 = aucun, 1 = illimité normalisé).")]
        [Range(0f, 1f)] public float Penetration = 0f;
    }

    /// <summary>
    /// Arme KINETICS 5 (primaire, secondaire ou tactique).
    /// Asset authoring éditeur ; équivalent runtime : <see cref="WeaponDto"/>
    /// chargé depuis <c>Resources/Data/weapons.json</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "KINETICS 5/Weapon", order = 11)]
    public sealed class WeaponSO : ScriptableObject
    {
        [Header("Identité")]
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public WeaponCategory Category = WeaponCategory.Primary;
        public WeaponType Type = WeaponType.AssaultRifle;
        public Rarity Rarity = Rarity.Common;
        public Element Element = Element.Kinetic;

        [Header("Attributs combat (pourcentages 0..100)")]
        [Range(0, 5000)] public int Power = 1000;
        [Range(0f, 10f)] public float ReloadTime = 2.5f;
        [Range(0f, 100f)] public float DamagePct = 50f;
        [Range(0f, 100f)] public float FireRatePct = 50f;
        [Range(0f, 100f)] public float AccuracyPct = 50f;
        [Range(0f, 100f)] public float StabilityPct = 50f;
        [Tooltip("Rayon d'explosion (armes tactiques uniquement).")]
        [Range(0f, 100f)] public float ExplosionRadiusPct = 0f;
        [Tooltip("Temps de fuse (grenades). 0 = détonation à l'impact.")]
        [Range(0f, 30f)] public float FuseTime = 0f;

        [Header("Magasin & portée")]
        [Range(1, 500)] public int MagazineSize = 30;
        [Range(1f, 2000f)] public float Range = 100f;
        public List<FireMode> FireModes = new() { FireMode.Auto };

        [Header("Projectile")]
        public ProjectileData Projectile = new();

        [Header("Ressources")]
        public Sprite? Icon;
        public GameObject? ModelPrefab;
    }
}
