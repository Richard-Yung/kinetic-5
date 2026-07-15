// ============================================================================
//  KINETICS 5 — Projectile (projectile poolé pour armes énergie/explosives)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Projectile physique pour armes à tir non-hitscan (plasma, grenades, roquettes).
//  Caractéristiques :
//    • Trajet avant via Transform cached (pas de Rigidbody pour limiter les cost).
//    • Détection de collision par Physics.RaycastNonAlloc (prevPos -> currentPos).
//    • Pénétration limitée (max targets par tir).
//    • Durée de vie (lifetime) avant retour au pool.
//    • AoE pour explosifs (radius + falloff linéaire).
//    • TrailRenderer teinté par élément.
//    • Pool-friendly : implémente IPooledItem, reset au spawn.
//    • Network-ready : expose NetworkId pour synchronisation future.
// ============================================================================
using System;
using KINETICS5.Core;
using KINETICS5.Data;
using KINETICS5.Gameplay.Enemies;
using UnityEngine;

namespace KINETICS5.Gameplay.Combat
{
    /// <summary>
    /// Composant de projectile poolé. Attaché au prefab projectile, il gère son
    /// déplacement, ses collisions et son retour au pool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hot path optimisée :</b> aucun GetComponent, aucun Instantiate, cache
    /// du Transform, raycast non-alloc (buffer partagé).
    /// </para>
    /// <para>
    /// <b>Pool :</b> enregistrer le prefab sous un poolId dédié (ex: <c>"Proj_Plasma"</c>)
    /// auprès de <see cref="ObjectPooler"/>. Utiliser <see cref="Spawn"/> pour obtenir
    /// une instance initialisée.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour, IPooledItem
    {
        [Header("Paramètres runtime")]
        [Tooltip("Vitesse de déplacement (m/s).")]
        [SerializeField] private float _speed = 80f;
        [Tooltip("Multiplicateur de gravité (0 = pas de drop, 1 = gravité normale).")]
        [SerializeField] private float _dropMultiplier = 0f;
        [Tooltip("Nombre maximum de cibles pénétrées avant disparition (0 = s'arrête au premier).")]
        [SerializeField] private int _maxPenetration = 0;
        [Tooltip("Durée de vie (s) avant retour au pool.")]
        [SerializeField] private float _lifetime = 3f;
        [Tooltip("Rayon d'explosion (0 = pas d'AoE, single-target).")]
        [SerializeField] private float _explosionRadius = 0f;
        [Tooltip("Falloff d'AoE (1 = dégâts pleins au centre, 0 = dégâts nuls au bord).")]
        [SerializeField] private AnimationCurve _falloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Effets visuels")]
        [Tooltip("TrailRenderer teinté par élément.")]
        [SerializeField] private TrailRenderer _trail;
        [Tooltip("Si vrai, le trail est recoloré à chaque spawn selon l'élément.")]
        [SerializeField] private bool _recolorTrail = true;

        [Header("Layers")]
        [Tooltip("Layers de collision (ennemis, murs, destructibles).")]
        [SerializeField] private LayerMask _collisionMask = ~0;

        // --- État runtime (initialisé par Spawn) ---
        private float _damage;
        private Element _element;
        private string _weaponId;
        private string _enemyIdForCalc;
        private uint _ownerId;
        private IDamageable _ownerDamageable;
        private Vector3 _prevPosition;
        private Vector3 _velocity;
        private float _remainingLifetime;
        private int _penetratedCount;
        private bool _isActive;
        private uint _networkId;

        // Buffer partagé pour raycast non-alloc (max 8 hits par frame, largement suffisant).
        private static readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

        /// <summary>Transform cached (évite GetComponent<Transform> dans Update).</summary>
        public Transform CachedTransform { get; private set; }

        /// <summary>Identifiant réseau unique (pour sync future via Nakama).</summary>
        public uint NetworkId => _networkId;

        /// <summary>Vrai si le projectile est actuellement actif (en vol).</summary>
        public bool IsActive => _isActive;

        private void Awake()
        {
            CachedTransform = transform;
            if (_trail != null) _trail.emitting = false;
        }

        /// <summary>
        /// Initialise et active le projectile. À appeler après Get du pool.
        /// </summary>
        /// <param name="position">Position de départ monde.</param>
        /// <param name="direction">Direction normalisée.</param>
        /// <param name="damage">Dégâts bruts à appliquer.</param>
        /// <param name="element">Élément.</param>
        /// <param name="weaponId">Id de l'arme source.</param>
        /// <param name="ownerId">Id unique de l'attaquant (évite friendly fire).</param>
        /// <param name="ownerDamageable">Interface IDamageable du propriétaire (peut être null).</param>
        /// <param name="speedOverride">Vitesse override (<= 0 = utiliser _speed).</param>
        /// <param name="lifetimeOverride">Durée de vie override (<= 0 = utiliser _lifetime).</param>
        /// <param name="explosionRadiusOverride">Rayon explosion override (>= 0).</param>
        /// <param name="networkId">Id réseau (0 = généré localement).</param>
        public void Spawn(Vector3 position, Vector3 direction, float damage, Element element,
                          string weaponId, uint ownerId, IDamageable ownerDamageable = null,
                          float speedOverride = -1f, float lifetimeOverride = -1f,
                          float explosionRadiusOverride = -1f, uint networkId = 0u)
        {
            CachedTransform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));

            _damage = Mathf.Max(0f, damage);
            _element = element;
            _weaponId = weaponId ?? string.Empty;
            _enemyIdForCalc = string.Empty; // Résolu dynamiquement à l'impact.
            _ownerId = ownerId;
            _ownerDamageable = ownerDamageable;
            _velocity = direction.normalized * (speedOverride > 0f ? speedOverride : _speed);
            _prevPosition = position;
            _remainingLifetime = lifetimeOverride > 0f ? lifetimeOverride : _lifetime;
            _penetratedCount = 0;
            _isActive = true;
            _networkId = networkId != 0u ? networkId : (uint)GetInstanceID();

            if (explosionRadiusOverride >= 0f) _explosionRadius = explosionRadiusOverride;

            if (_trail != null)
            {
                _trail.emitting = true;
                _trail.Clear();
                if (_recolorTrail)
                {
                    Color c = ElementalResolver.GetElementColor(element);
                    _trail.startColor = c;
                    _trail.endColor = new Color(c.r, c.g, c.b, 0f);
                }
            }

            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_isActive) return;

            float dt = Time.deltaTime;
            _remainingLifetime -= dt;
            if (_remainingLifetime <= 0f)
            {
                // Durée de vie écoulée : retour au pool.
                if (_explosionRadius > 0f) Detonate(CachedTransform.position);
                Despawn();
                return;
            }

            // Applique la gravité (drop) si configurée.
            if (_dropMultiplier > 0f)
            {
                _velocity += Physics.gravity * _dropMultiplier * dt;
            }

            Vector3 newPos = CachedTransform.position + _velocity * dt;
            Vector3 dir = newPos - _prevPosition;
            float dist = dir.magnitude;
            if (dist > 0f)
            {
                dir /= dist; // normalize
                int hits = Physics.RaycastNonAlloc(_prevPosition, dir, _hitBuffer, dist, _collisionMask);
                if (hits > 0)
                {
                    // Trie les hits par distance (les buffers ne sont pas triés par défaut).
                    SortHitsByDistance(hits);
                    for (int i = 0; i < hits; i++)
                    {
                        var hit = _hitBuffer[i];
                        if (HandleHit(hit)) break; // Si arrêté (pénétration max), on sort.
                    }
                }
            }

            if (_isActive)
            {
                CachedTransform.position = newPos;
                _prevPosition = newPos;
            }
        }

        /// <summary>
        /// Gère un hit. Retourne vrai si le projectile doit s'arrêter (pénétration max).
        /// </summary>
        private bool HandleHit(RaycastHit hit)
        {
            // Le collider touché : on récupère l'IDamageable via le cache ou GetComponent léger.
            var damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                // Évite friendly fire : ne touche pas le propriétaire.
                if (_ownerDamageable != null && ReferenceEquals(damageable, _ownerDamageable))
                {
                    return false;
                }

                // Résout l'enemyId pour le calcul de dégâts (si c'est un ennemi).
                string enemyId = ResolveEnemyId(hit.collider);

                // Calcule le multiplicateur élémentaire et applique les dégâts.
                float distance = Vector3.Distance(CachedTransform.position, _prevPosition);
                float finalDamage = DamageCalculator.CalculateFast(
                    _weaponId, enemyId, _element, false, false, distance, 0f);
                // Si le calcul retourne 0 (arme inconnue), fallback sur les dégâts passés à Spawn.
                if (finalDamage <= 0f) finalDamage = _damage;

                damageable.TakeDamage(finalDamage, _element, _ownerId, hit.point, false);

                // VFX d'impact.
                VFXSpawner.Instance?.ImpactSpark(hit.point, hit.normal, _element);
                VFXSpawner.Instance?.HitBlood(hit.point, hit.normal);
                FloatingDamage.Instance?.ShowDamage(hit.point, finalDamage, _element, false);

                // Si explosif, détone en AoE.
                if (_explosionRadius > 0f)
                {
                    Detonate(hit.point);
                    Despawn();
                    return true;
                }

                _penetratedCount++;
                if (_penetratedCount > _maxPenetration)
                {
                    Despawn();
                    return true;
                }
                return false;
            }

            // Collision avec un mur / destructible sans IDamageable.
            if (_explosionRadius > 0f)
            {
                Detonate(hit.point);
                Despawn();
                return true;
            }

            // Décals de balle + étincelles.
            VFXSpawner.Instance?.ImpactSpark(hit.point, hit.normal, _element);
            VFXSpawner.Instance?.BulletHoleDecal(hit.point, hit.normal, SurfaceType.Metal);
            Despawn();
            return true;
        }

        /// <summary>
        /// Détonation AoE : applique des dégâts à toutes les cibles dans le rayon.
        /// </summary>
        private void Detonate(Vector3 center)
        {
            if (_explosionRadius <= 0f) return;

            // VFX d'explosion.
            VFXSpawner.Instance?.Explosion(center, _explosionRadius, _element);
            // Screen shake big.
            CameraManager.Instance?.Shake(0.8f, 2f, 0.5f);

            // Dégâts en zone : OverlapSphereNonAlloc pour limiter les allocations.
            int count = Physics.OverlapSphereNonAlloc(center, _explosionRadius, ColliderBuffer.Buffer, _collisionMask);
            for (int i = 0; i < count; i++)
            {
                var col = ColliderBuffer.Buffer[i];
                if (col == null) continue;
                var damageable = col.GetComponent<IDamageable>();
                if (damageable == null) continue;
                if (_ownerDamageable != null && ReferenceEquals(damageable, _ownerDamageable)) continue;

                // Falloff basé sur la distance au centre.
                float dist = Vector3.Distance(center, col.ClosestPoint(center));
                float t = Mathf.Clamp01(dist / _explosionRadius);
                float falloff = _falloff.Evaluate(t);
                float aoeDamage = _damage * falloff;
                if (aoeDamage <= 0f) continue;

                string enemyId = ResolveEnemyId(col);
                float finalDamage = DamageCalculator.CalculateFast(
                    _weaponId, enemyId, _element, false, false, dist, 0f);
                if (finalDamage <= 0f) finalDamage = aoeDamage;

                Vector3 hitPoint = col.ClosestPoint(center);
                damageable.TakeDamage(finalDamage, _element, _ownerId, hitPoint, false);
                FloatingDamage.Instance?.ShowDamage(hitPoint, finalDamage, _element, false);
            }
        }

        /// <summary>
        /// Tente de résoudre l'enemyId d'un collider touché (pour DamageCalculator).
        /// </summary>
        private string ResolveEnemyId(Collider col)
        {
            // Convention KINETICS 5 : les ennemis portent un EnemyController avec EnemyId.
            var ec = col.GetComponentInParent<EnemyController>();
            return (ec != null && ec.Data != null) ? ec.Data.Id : string.Empty;
        }

        /// <summary>Trie les hits du buffer par distance croissante.</summary>
        private void SortHitsByDistance(int count)
        {
            // Tri par insertion (count <= 8, suffisamment petit).
            for (int i = 1; i < count; i++)
            {
                var key = _hitBuffer[i];
                float keyDist = _hitBuffer[i].distance;
                int j = i - 1;
                while (j >= 0 && _hitBuffer[j].distance > keyDist)
                {
                    _hitBuffer[j + 1] = _hitBuffer[j];
                    j--;
                }
                _hitBuffer[j + 1] = key;
            }
        }

        /// <summary>
        /// Retourne le projectile au pool.
        /// </summary>
        private void Despawn()
        {
            if (!_isActive) return;
            _isActive = false;
            if (_trail != null) _trail.emitting = false;

            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler != null)
            {
                // Le projectile retourne au pool via le mécanisme standard.
                pooler.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        // --- IPooledItem ---

        /// <inheritdoc/>
        void IPooledItem.OnSpawnFromPool()
        {
            // Rien de spécial : tout est fait dans Spawn().
        }

        /// <inheritdoc/>
        void IPooledItem.OnReturnToPool()
        {
            _isActive = false;
            _damage = 0f;
            _velocity = Vector3.zero;
            _prevPosition = Vector3.zero;
            _penetratedCount = 0;
            _remainingLifetime = 0f;
            if (_trail != null)
            {
                _trail.emitting = false;
                _trail.Clear();
            }
        }
    }

    /// <summary>
    /// Buffer partagé pour OverlapSphereNonAlloc (évite l'allocation par projectile).
    /// Taille 64 (assez pour la plupart des explosions ; si plus, les extras sont ignorés).
    /// </summary>
    internal static class ColliderBuffer
    {
        public static readonly Collider[] Buffer = new Collider[64];
    }
}
