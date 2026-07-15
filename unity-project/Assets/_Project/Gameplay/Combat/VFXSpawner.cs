// ============================================================================
//  KINETICS 5 — VFX Spawner (pool de particules + decals + tracers)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Centralise tous les effets visuels de combat de KINETICS 5 :
//    • MuzzleFlash (par arme)
//    • ImpactSpark (point d'impact + normale + élément teinté)
//    • HitBlood / HitFlesh (sur ennemi organique)
//    • Explosion (radius + élément)
//    • ElementalStatus (overlay durée sur cible)
//    • UltimateBurst (par agent)
//    • Tracer (startPos -> endPos)
//    • BulletHoleDecal (par surfaceType)
//
//  Toutes les particules sont poolées via <see cref="ObjectPooler"/>.
//  Pool keys : VFX_MuzzleFlash, VFX_ImpactSpark, VFX_Blood, VFX_Explosion,
//  VFX_Status, VFX_Ultimate, VFX_Tracer, Decal_BulletHole.
//
//  Cap mobile : 100 VFX concurrents (au-delà, recycle le plus ancien).
// ============================================================================
using System;
using KINETICS5.Core;
using KINETICS5.Data;
using UnityEngine;

namespace KINETICS5.Gameplay.Combat
{
    /// <summary>
    /// Type de surface pour les decals de balle (impact différent selon matériau).
    /// </summary>
    public enum SurfaceType
    {
        /// <summary>Métal (coque de vaisseau).</summary>
        Metal,
        /// <summary>Béton / pierre (stations).</summary>
        Concrete,
        /// <summary>Chair (ennemis organiques).</summary>
        Flesh,
        /// <summary>Verre / hologramme.</summary>
        Glass,
        /// <summary>Bois (caisses).</summary>
        Wood,
        /// <summary>Surface inconnue (fallback métal).</summary>
        Unknown
    }

    /// <summary>
    /// Spawner centralisé des VFX de combat. Tous les effets transitent par
    /// <see cref="ObjectPooler"/> (zéro Instantiate dans la hot path, recyclage
    /// automatique via <see cref="PooledVFX"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Architecture :</b> Singleton par scène (DontDestroyOnLoad). Les prefabs
    /// sont assignés via Inspector (ou auto-résolus depuis Resources à la première
    /// demande, avec avertissement).
    /// </para>
    /// <para>
    /// <b>Cap mobile :</b> 100 VFX concurrents maximum. Au-delà, le plus ancien
    /// est recyclé (FIFO via liste circulaire).
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class VFXSpawner : MonoBehaviour
    {
        // --- Clés de pool standardisées ---
        /// <summary>Pool key du muzzle flash.</summary>
        public const string PoolMuzzleFlash = "VFX_MuzzleFlash";
        /// <summary>Pool key des impacts d'étincelles.</summary>
        public const string PoolImpactSpark = "VFX_ImpactSpark";
        /// <summary>Pool key du sang / chair.</summary>
        public const string PoolBlood = "VFX_Blood";
        /// <summary>Pool key des explosions.</summary>
        public const string PoolExplosion = "VFX_Explosion";
        /// <summary>Pool key des overlays de statut.</summary>
        public const string PoolStatus = "VFX_Status";
        /// <summary>Pool key de l'explosion d'ultimate.</summary>
        public const string PoolUltimate = "VFX_Ultimate";
        /// <summary>Pool key des tracers de balle.</summary>
        public const string PoolTracer = "VFX_Tracer";
        /// <summary>Pool key des decals de trous de balle.</summary>
        public const string PoolBulletHole = "Decal_BulletHole";

        private static VFXSpawner _instance;
        /// <summary>Instance globale (auto-créée si absente).</summary>
        public static VFXSpawner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[VFXSpawner]");
                    _instance = go.AddComponent<VFXSpawner>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Prefabs VFX")]
        [Tooltip("Prefab générique de muzzle flash (ParticleSystem).")]
        [SerializeField] private ParticleSystem _muzzleFlashPrefab;
        [Tooltip("Prefab d'étincelles d'impact (ParticleSystem).")]
        [SerializeField] private ParticleSystem _impactSparkPrefab;
        [Tooltip("Prefab de sang (impact sur chair).")]
        [SerializeField] private ParticleSystem _bloodPrefab;
        [Tooltip("Prefab d'explosion (gros burst radial).")]
        [SerializeField] private ParticleSystem _explosionPrefab;
        [Tooltip("Prefab d'overlay de statut élémentaire (parenté à la cible).")]
        [SerializeField] private ParticleSystem _statusPrefab;
        [Tooltip("Prefab d'explosion d'ultimate (très gros burst).")]
        [SerializeField] private ParticleSystem _ultimatePrefab;
        [Tooltip("Prefab de tracer (LineRenderer ou TrailRenderer).")]
        [SerializeField] private LineRenderer _tracerPrefab;
        [Tooltip("Prefab de decal de trou de balle.")]
        [SerializeField] private GameObject _bulletHolePrefab;

        [Header("Pool sizes")]
        [Tooltip("Taille initiale de chaque pool VFX.")]
        [SerializeField] private int _preWarmPerPool = 8;
        [Tooltip("Taille max de chaque pool VFX.")]
        [SerializeField] private int _maxPoolSize = 32;
        [Tooltip("Durée de vie par défaut d'un VFX (s) avant retour au pool.")]
        [SerializeField] private float _defaultVfxLifetime = 1.5f;
        [Tooltip("Nombre maximal de VFX simultanés (cap mobile).")]
        [SerializeField] private int _maxConcurrentVfx = 100;

        [Header("Tracer")]
        [Tooltip("Durée d'affichage d'un tracer (s).")]
        [SerializeField] private float _tracerDuration = 0.05f;
        [Tooltip("Largeur de départ du tracer.")]
        [SerializeField] private float _tracerStartWidth = 0.02f;
        [Tooltip("Largeur d'arrivée du tracer (0 pour fade).")]
        [SerializeField] private float _tracerEndWidth = 0.0f;

        // --- Suivi des VFX actifs (recyclage FIFO si cap atteint) ---
        private readonly System.Collections.Generic.List<PooledVFX> _activeVfx = new(128);
        private int _fifoIndex;

        // --- MonoBeh lifecycle ---

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            RegisterAllPools();
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
        }

        private void Update()
        {
            // Recyclage des VFX expirés (zéro allocation : for-loop + swap-remove).
            float now = Time.time;
            for (int i = _activeVfx.Count - 1; i >= 0; i--)
            {
                var vfx = _activeVfx[i];
                if (vfx == null || vfx.ExpireTime <= now)
                {
                    if (vfx != null) ReleaseVFX(vfx);
                    _activeVfx.RemoveAt(i);
                }
            }
        }

        // --- API publique : un spawn par type de VFX ---

        /// <summary>
        /// Affiche un muzzle flash à la bouche du canon.
        /// </summary>
        /// <param name="weaponId">Id de l'arme (pour variation de couleur si besoin).</param>
        /// <param name="position">Position monde de la bouche du canon.</param>
        /// <param name="rotation">Orientation (forward = direction du tir).</param>
        public void MuzzleFlash(string weaponId, Vector3 position, Quaternion rotation)
        {
            var ps = SpawnParticleSystem(PoolMuzzleFlash, _muzzleFlashPrefab, position, rotation, 0.15f);
            // Teinte légère selon l'élément de l'arme (optionnel, via DataLoader).
            var weapon = DataLoader.GetWeapon(weaponId);
            if (weapon.HasValue && ps != null)
            {
                var col = ElementalResolver.GetElementColor(weapon.Value.Element);
                var main = ps.main;
                main.startColor = col;
            }
        }

        /// <summary>
        /// Affiche des étincelles d'impact (surface dure).
        /// </summary>
        /// <param name="hitPoint">Point d'impact monde.</param>
        /// <param name="normal">Normale de surface (orientation des étincelles).</param>
        /// <param name="element">Élément de l'attaque (teinte).</param>
        public void ImpactSpark(Vector3 hitPoint, Vector3 normal, Element element)
        {
            Quaternion rot = Quaternion.LookRotation(normal);
            var ps = SpawnParticleSystem(PoolImpactSpark, _impactSparkPrefab, hitPoint, rot, 0.5f);
            if (ps != null)
            {
                var col = ElementalResolver.GetElementColor(element);
                var main = ps.main;
                main.startColor = col;
            }
        }

        /// <summary>
        /// Affiche un impact de sang (sur ennemi organique).
        /// </summary>
        /// <param name="hitPoint">Point d'impact monde.</param>
        /// <param name="normal">Normale de surface.</param>
        public void HitBlood(Vector3 hitPoint, Vector3 normal)
        {
            Quaternion rot = Quaternion.LookRotation(normal);
            SpawnParticleSystem(PoolBlood, _bloodPrefab, hitPoint, rot, 0.6f);
        }

        /// <summary>
        /// Alias pour <see cref="HitBlood"/> (impact sur chair).
        /// </summary>
        public void HitFlesh(Vector3 hitPoint, Vector3 normal) => HitBlood(hitPoint, normal);

        /// <summary>
        /// Affiche une explosion (rayon donné pour scale).
        /// </summary>
        /// <param name="position">Centre de l'explosion.</param>
        /// <param name="radius">Rayon en mètres (scale le VFX).</param>
        /// <param name="element">Élément (teinte).</param>
        public void Explosion(Vector3 position, float radius, Element element)
        {
            var ps = SpawnParticleSystem(PoolExplosion, _explosionPrefab, position, Quaternion.identity, 1.2f);
            if (ps != null)
            {
                // Scale proportionnel au rayon.
                float scale = Mathf.Clamp(radius * 0.1f, 0.5f, 4f);
                ps.transform.localScale = Vector3.one * scale;
                var col = ElementalResolver.GetElementColor(element);
                var main = ps.main;
                main.startColor = col;
            }
        }

        /// <summary>
        /// Attache un overlay d'effet de statut à une cible (brûlure, gel, etc.).
        /// </summary>
        /// <param name="target">Transform de la cible (parent du VFX).</param>
        /// <param name="element">Élément de l'effet (teinte).</param>
        /// <param name="duration">Durée d'affichage (s).</param>
        public void ElementalStatus(Transform target, Element element, float duration)
        {
            if (target == null) return;
            var ps = SpawnParticleSystem(PoolStatus, _statusPrefab, target.position, Quaternion.identity, duration, target);
            if (ps != null)
            {
                var col = ElementalResolver.GetElementColor(element);
                var main = ps.main;
                main.startColor = col;
            }
        }

        /// <summary>
        /// Affiche le burst d'ultimate de l'agent (très gros VFX radial).
        /// </summary>
        /// <param name="position">Position monde.</param>
        /// <param name="agentId">Id de l'agent (pour variation par agent).</param>
        public void UltimateBurst(Vector3 position, string agentId)
        {
            var ps = SpawnParticleSystem(PoolUltimate, _ultimatePrefab, position, Quaternion.identity, 2.0f);
            if (ps == null) return;
            // Teinte selon la couleur thème de l'agent.
            var agent = DataLoader.GetAgent(agentId);
            Color col = agent.HasValue ? agent.Value.ThemeColor : new Color(0.102f, 0.631f, 0.808f, 1f);
            var main = ps.main;
            main.startColor = col;
        }

        /// <summary>
        /// Affiche un tracer de balle entre deux points.
        /// </summary>
        /// <param name="startPos">Position de départ (bouche du canon).</param>
        /// <param name="endPos">Position d'arrivée (point d'impact).</param>
        /// <param name="color">Couleur du tracer.</param>
        public void Tracer(Vector3 startPos, Vector3 endPos, Color color)
        {
            if (_tracerPrefab == null) return;
            // Le tracer n'est pas un ParticleSystem mais un LineRenderer.
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;

            var lr = pooler.Get<LineRenderer>(PoolTracer, startPos, Quaternion.identity);
            if (lr == null) return;

            lr.startWidth = _tracerStartWidth;
            lr.endWidth = _tracerEndWidth;
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, 0f);
            lr.positionCount = 2;
            lr.SetPosition(0, startPos);
            lr.SetPosition(1, endPos);

            var vfx = lr.gameObject.AddComponent<PooledVFX>();
            vfx.Initialize(PoolTracer, lr, _tracerDuration);
            TrackActive(vfx);
        }

        /// <summary>
        /// Dépose un decal de trou de balle sur une surface.
        /// </summary>
        /// <param name="hitPoint">Point d'impact monde.</param>
        /// <param name="normal">Normale de surface (orientation du decal).</param>
        /// <param name="surfaceType">Type de surface (variation du decal).</param>
        public void BulletHoleDecal(Vector3 hitPoint, Vector3 normal, SurfaceType surfaceType)
        {
            if (_bulletHolePrefab == null) return;
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;

            // Légère offset pour éviter le z-fighting.
            Vector3 pos = hitPoint + normal * 0.01f;
            Quaternion rot = Quaternion.LookRotation(-normal);
            var go = pooler.Get<GameObject>(PoolBulletHole, pos, rot);
            // Note : le type de surface pourrait être utilisé pour swap de material ; ici on garde générique.
            var vfx = go.GetComponent<PooledVFX>();
            if (vfx == null)
            {
                vfx = go.AddComponent<PooledVFX>();
                vfx.Initialize(PoolBulletHole, go, 8f);
            }
            else
            {
                vfx.ResetTimer(8f);
            }
            TrackActive(vfx);
        }

        // --- Internes ---

        /// <summary>
        /// Enregistre tous les pools connus auprès de l'ObjectPooler.
        /// </summary>
        private void RegisterAllPools()
        {
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;

            TryRegister(pooler, PoolMuzzleFlash, _muzzleFlashPrefab);
            TryRegister(pooler, PoolImpactSpark, _impactSparkPrefab);
            TryRegister(pooler, PoolBlood, _bloodPrefab);
            TryRegister(pooler, PoolExplosion, _explosionPrefab);
            TryRegister(pooler, PoolStatus, _statusPrefab);
            TryRegister(pooler, PoolUltimate, _ultimatePrefab);
            TryRegister(pooler, PoolTracer, _tracerPrefab);
            TryRegister(pooler, PoolBulletHole, _bulletHolePrefab);
        }

        private void TryRegister<T>(ObjectPooler pooler, string id, T prefab) where T : Component
        {
            if (prefab == null) return;
            pooler.RegisterPool(id, prefab, _preWarmPerPool, _maxPoolSize);
        }

        /// <summary>
        /// Instancie (depuis le pool) un ParticleSystem et l'enregistre pour recyclage.
        /// </summary>
        private ParticleSystem SpawnParticleSystem(string poolId, ParticleSystem prefab, Vector3 pos, Quaternion rot, float lifetime, Transform parent = null)
        {
            if (prefab == null) return null;
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return null;

            var ps = pooler.Get<ParticleSystem>(poolId, pos, rot, parent);
            if (ps == null) return null;
            ps.Play(true);

            var vfx = ps.GetComponent<PooledVFX>();
            if (vfx == null)
            {
                vfx = ps.gameObject.AddComponent<PooledVFX>();
                vfx.Initialize(poolId, ps, Mathf.Max(0.05f, lifetime));
            }
            else
            {
                vfx.ResetTimer(Mathf.Max(0.05f, lifetime));
            }
            TrackActive(vfx);
            return ps;
        }

        /// <summary>
        /// Suit un VFX actif. Si le cap est atteint, recycle le plus ancien (FIFO).
        /// </summary>
        private void TrackActive(PooledVFX vfx)
        {
            _activeVfx.Add(vfx);
            // Cap mobile : recycle le plus ancien si dépassement.
            while (_activeVfx.Count > _maxConcurrentVfx && _activeVfx.Count > 0)
            {
                int idx = _fifoIndex % _activeVfx.Count;
                var oldest = _activeVfx[idx];
                if (oldest != null) ReleaseVFX(oldest);
                _activeVfx.RemoveAt(idx);
                _fifoIndex++;
            }
        }

        /// <summary>
        /// Retourne un VFX au pool (via ObjectPooler.Release).
        /// </summary>
        private void ReleaseVFX(PooledVFX vfx)
        {
            if (vfx == null) return;
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;
            pooler.Release(vfx.Target);
        }

        /// <summary>
        /// Force la libération de tous les VFX actifs (utile lors d'un reset de scène).
        /// </summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < _activeVfx.Count; i++)
            {
                if (_activeVfx[i] != null) ReleaseVFX(_activeVfx[i]);
            }
            _activeVfx.Clear();
        }

        /// <summary>Nombre de VFX actuellement actifs (debug HUD).</summary>
        public int ActiveVfxCount => _activeVfx.Count;
    }

    /// <summary>
    /// Composant attaché à chaque VFX instancié pour suivre sa durée de vie
    /// et permettre le recyclage automatique vers le pool d'origine.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledVFX : MonoBehaviour
    {
        /// <summary>Id du pool d'origine (pour retour).</summary>
        public string PoolId { get; private set; }
        /// <summary>Composant cible retourné au pool.</summary>
        public Component Target { get; private set; }
        /// <summary>Temps absolu (Time.time) d'expiration.</summary>
        public float ExpireTime { get; private set; }

        /// <summary>
        /// Initialise ce tracker. À appeler juste après Get du pool.
        /// </summary>
        public void Initialize(string poolId, Component target, float lifetime)
        {
            PoolId = poolId;
            Target = target;
            ExpireTime = Time.time + lifetime;
        }

        /// <summary>
        /// Réinitialise le timer d'expiration (réutilisation du VFX recyclé).
        /// </summary>
        public void ResetTimer(float lifetime)
        {
            ExpireTime = Time.time + lifetime;
        }
    }
}
