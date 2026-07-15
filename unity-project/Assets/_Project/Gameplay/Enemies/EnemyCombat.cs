using System;
using System.Collections;
using KINETICS5.Core;
using KINETICS5.Data;
using KINETICS5.Gameplay.Combat;
using UnityEngine;

namespace KINETICS5.Gameplay.Enemies
{
    /// <summary>
    /// Type d'attaque ennemie. Détermine le pattern de télégraphe, le VFX et la logique de hit.
    /// </summary>
    public enum EnemyAttackType
    {
        /// <summary>Coup de mêlée instantané (portée courte, dégâts directs).</summary>
        Melee,
        /// <summary>Tir hitscan avec tracer VFX (portée longue, dégâts directs).</summary>
        Ranged,
        /// <summary>Charge : dash vers le joueur, dégâts au contact.</summary>
        Charge,
        /// <summary>Slam au sol : AoE circulaire avec télégraphe sol.</summary>
        AoESlam,
        /// <summary>Lancer de grenade : projectile parabolique + explosion AoE différée.</summary>
        GrenadeToss
    }

    /// <summary>
    /// Logique d'attaque des ennemis. Trois familles :
    /// <list type="bullet">
    ///   <item><b>Mêlée</b> : check de distance + applique dégâts directs au joueur.</item>
    ///   <item><b>Tir ranged</b> : hitscan + tracer VFX poolé (1 frame, mobile-friendly).</item>
    ///   <item><b>Spécial</b> : charge, slam AoE, grenade — avec télégraphe de 1s.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <b>Cooldown</b> par attaque, dérivé de <see cref="EnemyDto.AttackRate"/>.
    /// <b>Télégraphe</b> : VFX de windup 1s avant l'attaque effective (préviens le joueur).
    /// <b>Dégâts au joueur</b> : routés via <see cref="PlayerContext.DamagePlayer"/>
    /// (zéro couplage à <c>PlayerStats</c>).
    /// </remarks>
    [DisallowMultipleComponent]
    public class EnemyCombat : MonoBehaviour
    {
        [Header("Configuration attaque")]
        [Tooltip("Type d'attaque principale (override EnemyDto.Class pour choisir le pattern).")]
        [SerializeField] private EnemyAttackType _primaryAttack = EnemyAttackType.Ranged;
        [Tooltip("Type d'attaque spéciale (cooldown long, dégâts élevés).")]
        [SerializeField] private EnemyAttackType _specialAttack = EnemyAttackType.AoESlam;
        [Tooltip("Probabilité de déclencher l'attaque spéciale si cooldown prêt (0..1).")]
        [Range(0f, 1f)][SerializeField] private float _specialChance = 0.15f;
        [Tooltip("Multiplicateur de cooldown de l'attaque spéciale (vs primaire).")]
        [SerializeField] private float _specialCooldownMult = 4f;
        [Tooltip("Multiplicateur de dégâts de l'attaque spéciale.")]
        [SerializeField] private float _specialDamageMult = 2.5f;

        [Header("Télégraphe")]
        [Tooltip("Durée du windup (télégraphe) avant l'attaque effective (secondes).")]
        [Range(0.1f, 3f)][SerializeField] private float _telegraphDuration = 1f;
        [Tooltip("Prefab du VFX de télégraphe (poolé). Doit implémenter IPooledItem.")]
        [SerializeField] private Component _telegraphVfxPrefab;
        [Tooltip("Id du pool VFX télégraphe (doit être pré-enregistré dans ObjectPooler).")]
        [SerializeField] private string _telegraphPoolId = "VFX_Telegraph";

        [Header("Tracer ranged")]
        [Tooltip("Prefab du tracer (ligne/trace de balle). Poolé.")]
        [SerializeField] private Component _tracerPrefab;
        [Tooltip("Id du pool tracer.")]
        [SerializeField] private string _tracerPoolId = "VFX_Tracer";
        [Tooltip("Durée d'affichage du tracer (secondes).")]
        [SerializeField] private float _tracerDuration = 0.08f;

        [Header("Slam AoE")]
        [Tooltip("Rayon du slam AoE (mètres).")]
        [SerializeField] private float _slamRadius = 6f;
        [Tooltip("Hauteur d'effet du slam (pour les cibles aériennes).")]
        [SerializeField] private float _slamHeight = 3f;

        [Header("Grenade")]
        [Tooltip("Prefab de grenade (poolé). Doit implémenter IPooledItem et avoir un Rigidbody.")]
        [SerializeField] private Component _grenadePrefab;
        [Tooltip("Id du pool grenade.")]
        [SerializeField] private string _grenadePoolId = "EnemyGrenade";
        [Tooltip("Rayon d'explosion de la grenade (mètres).")]
        [SerializeField] private float _grenadeExplosionRadius = 5f;
        [Tooltip("Délai avant explosion après impact (secondes).")]
        [SerializeField] private float _grenadeFuseTime = 1.5f;

        [Header("Charge")]
        [Tooltip("Distance de charge (mètres).")]
        [SerializeField] private float _chargeDistance = 12f;
        [Tooltip("Vitesse de charge (m/s).")]
        [SerializeField] private float _chargeSpeed = 18f;

        [Header("Audio (optionnel)")]
        [Tooltip("Clip de son d'attaque (via AudioManager.PlaySfx). Laisser null si non géré ici.")]
        [SerializeField] private AudioClip _attackSfx;
        [Tooltip("Clip de son de télégraphe.")]
        [SerializeField] private AudioClip _telegraphSfx;

        /// <summary>Vrai si l'ennemi peut attaquer maintenant (cooldown écoulé et pas déjà en windup).</summary>
        public bool CanAttack => _cooldownTimer <= 0f && !_isInWindup && !_isAttacking;

        private EnemyController _controller;
        private EnemyDto _data;
        private float _damageMult = 1f;
        private float _cooldownTimer;
        private float _baseCooldown = 1f;
        private bool _isInWindup;
        private bool _isAttacking;

        // Cache du mute de layer pour IgnoreRaycast des tracers.
        private static readonly int EnemyLayerMask = -1; // -1 = tous ; à filtrer via LayerMask field en prod.

        /// <summary>Initialise le combat avec le contrôleur propriétaire et le scaling de dégâts.</summary>
        public void Initialize(EnemyController controller, float damageMult = 1f)
        {
            _controller = controller;
            _data = controller.Data;
            _damageMult = Mathf.Max(0.1f, damageMult);
            _baseCooldown = _data != null && _data.AttackRate > 0f ? 1f / _data.AttackRate : 1f;
            _cooldownTimer = 0f;
            _isInWindup = false;
            _isAttacking = false;
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }
        }

        /// <summary>
        /// Tente de déclencher une attaque. Ne fait rien si <see cref="CanAttack"/> est faux.
        /// Peut déclencher l'attaque spéciale avec une probabilité configurable.
        /// </summary>
        public void TryAttack()
        {
            if (!CanAttack || _data == null) return;

            bool useSpecial = UnityEngine.Random.value < _specialChance;
            EnemyAttackType type = useSpecial ? _specialAttack : _primaryAttack;
            float cooldown = useSpecial ? _baseCooldown * _specialCooldownMult : _baseCooldown;
            float damageMult = useSpecial ? _specialDamageMult : 1f;

            StartCoroutine(ExecuteAttackCoroutine(type, cooldown, damageMult));
        }

        /// <summary>Force une attaque spécifique (utilisé par les boss via BossPhaseManager).</summary>
        public void ForceAttack(EnemyAttackType type, float cooldownOverride = 0f, float damageMult = 1f)
        {
            if (_isInWindup || _isAttacking) return;
            float cd = cooldownOverride > 0f ? cooldownOverride : _baseCooldown;
            StartCoroutine(ExecuteAttackCoroutine(type, cd, damageMult));
        }

        private IEnumerator ExecuteAttackCoroutine(EnemyAttackType type, float cooldown, float damageMult)
        {
            _isInWindup = true;
            _cooldownTimer = cooldown;

            // 1. Télégraphe (windup VFX + SFX).
            Vector3 target = AcquireTargetPosition();
            SpawnTelegraph(type, target);
            PlaySfx(_telegraphSfx);
            _controller.Animator?.PlayAttackWindup(type);

            yield return new WaitForSeconds(_telegraphDuration);

            _isInWindup = false;
            _isAttacking = true;

            // 2. Exécution effective.
            try
            {
                ExecuteDamage(type, target, damageMult);
                _controller.Animator?.PlayAttackRelease(type);
                PlaySfx(_attackSfx);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EnemyCombat] Erreur exécution attaque {type}: {ex}");
            }
            finally
            {
                _isAttacking = false;
            }
        }

        private Vector3 AcquireTargetPosition()
        {
            if (PlayerContext.TryGetPlayer(out var t))
            {
                return t.position;
            }
            return _controller != null ? _controller.LastKnownPlayerPosition : transform.position;
        }

        // =================================================================================
        //  EXÉCUTION DES ATTAQUES
        // =================================================================================

        private void ExecuteDamage(EnemyAttackType type, Vector3 target, float damageMult)
        {
            float baseDamage = _data?.BaseDamage ?? 10f;
            float damage = baseDamage * _damageMult * damageMult;
            Element element = _data?.Weakness ?? Element.Kinetic;
            // Note : l'élément de l'ennemi (faiblesse du joueur vis-à-vis de l'ennemi) n'est pas
            // défini dans EnemySO. On utilise Kinetic par défaut ; un futur champ EnemyDto.DamageElement
            // permettrait de spécialiser (plasma, cryo, etc.).
            Vector3 hitPoint = target;

            switch (type)
            {
                case EnemyAttackType.Melee:
                    ExecuteMelee(damage, element, ref hitPoint);
                    break;
                case EnemyAttackType.Ranged:
                    ExecuteRanged(damage, element, target, ref hitPoint);
                    break;
                case EnemyAttackType.Charge:
                    StartCoroutine(ExecuteCharge(damage, element, target));
                    break;
                case EnemyAttackType.AoESlam:
                    ExecuteSlam(damage, element, target);
                    break;
                case EnemyAttackType.GrenadeToss:
                    ExecuteGrenadeToss(damage, element, target);
                    break;
            }
        }

        /// <summary>Attaque mêlée : check distance + dégâts directs si à portée.</summary>
        private void ExecuteMelee(float damage, Element element, ref Vector3 hitPoint)
        {
            const float meleeRange = 3f;
            if (!PlayerContext.TryGetPlayer(out var player)) return;
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist > meleeRange) return;

            hitPoint = player.position + Vector3.up * 1.2f;
            PlayerContext.DamagePlayer(damage, hitPoint, _controller.InstanceId, element, false);
        }

        /// <summary>Attaque ranged hitscan : raycast + tracer VFX.</summary>
        private void ExecuteRanged(float damage, Element element, Vector3 target, ref Vector3 hitPoint)
        {
            Vector3 origin = transform.position + Vector3.up * 1.4f;
            Vector3 dir = (target + Vector3.up * 1.2f) - origin;
            float dist = Mathf.Min(dir.magnitude, _data?.AttackRange ?? 50f);
            if (dist < 0.1f) return;
            dir /= dir.magnitude;

            // Raycast : si hit joueur, applique dégâts ; sinon, tracer simple.
            if (Physics.Raycast(origin, dir, out var hit, dist, EnemyLayerMask, QueryTriggerInteraction.Ignore))
            {
                hitPoint = hit.point;
                if (hit.collider.CompareTag("Player"))
                {
                    PlayerContext.DamagePlayer(damage, hit.point, _controller.InstanceId, element, false);
                }
                SpawnTracer(origin, hit.point);
            }
            else
            {
                Vector3 end = origin + dir * dist;
                SpawnTracer(origin, end);
            }
        }

        /// <summary>Charge : dash vers la position cible + dégâts au contact.</summary>
        private IEnumerator ExecuteCharge(float damage, Element element, Vector3 target)
        {
            Vector3 start = transform.position;
            Vector3 dir = (target - start);
            dir.y = 0f;
            float totalDist = Mathf.Min(dir.magnitude, _chargeDistance);
            if (totalDist < 0.1f) yield break;
            dir /= dir.magnitude;

            float traveled = 0f;
            bool hitPlayer = false;
            while (traveled < totalDist)
            {
                float step = _chargeSpeed * Time.deltaTime;
                Vector3 next = transform.position + dir * step;
                transform.position = next;
                traveled += step;

                // Hit detection : si le joueur est dans un rayon de 1.5m, dégâts.
                if (!hitPlayer && PlayerContext.TryGetPlayer(out var player))
                {
                    float dist = Vector3.Distance(transform.position, player.position);
                    if (dist < 1.5f)
                    {
                        PlayerContext.DamagePlayer(damage, player.position + Vector3.up, _controller.InstanceId, element, true);
                        hitPlayer = true;
                    }
                }
                yield return null;
            }
        }

        /// <summary>Slam AoE : dégâts dans un rayon autour de la cible.</summary>
        private void ExecuteSlam(float damage, Element element, Vector3 center)
        {
            if (PlayerContext.TryGetPlayer(out var player))
            {
                Vector3 flat = new(player.position.x, center.y, player.position.z);
                float dist = Vector3.Distance(flat, center);
                float verticalDelta = Mathf.Abs(player.position.y - center.y);
                if (dist <= _slamRadius && verticalDelta <= _slamHeight)
                {
                    // Falloff linéaire : 100% au centre, 30% au bord.
                    float falloff = Mathf.Lerp(1f, 0.3f, dist / _slamRadius);
                    Vector3 hitPoint = player.position + Vector3.up * 0.5f;
                    PlayerContext.DamagePlayer(damage * falloff, hitPoint, _controller.InstanceId, element, false);
                }
            }
            // VFX d'explosion (poolé si configuré).
            SpawnSlamVfx(center);
        }

        /// <summary>Lancer de grenade : spawn projectile parabolique + explosion différée.</summary>
        private void ExecuteGrenadeToss(float damage, Element element, Vector3 target)
        {
            if (_grenadePrefab == null)
            {
                // Fallback : simule une explosion immédiate à la cible.
                ExecuteSlam(damage, element, target);
                return;
            }
            // Pour simplicité et zéro dépendance Rigidbody, on simule la parabole via coroutine.
            StartCoroutine(GrenadeParabolaCoroutine(damage, element, target));
        }

        private IEnumerator GrenadeParabolaCoroutine(float damage, Element element, Vector3 target)
        {
            Vector3 start = transform.position + Vector3.up * 1.5f;
            // Spawn via pool si configuré, sinon primitive sphere temporaire.
            GameObject grenadeObj;
            if (ObjectPooler.Instance != null && !string.IsNullOrEmpty(_grenadePoolId))
            {
                var proj = ObjectPooler.Instance.Get<Component>(_grenadePoolId, start, Quaternion.identity);
                grenadeObj = proj != null ? proj.gameObject : null;
            }
            else
            {
                grenadeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                grenadeObj.transform.localScale = Vector3.one * 0.2f;
                grenadeObj.transform.position = start;
            }
            if (grenadeObj == null) yield break;

            // Parabole : 1s de vol avec apex à mi-chemin.
            float flightTime = 1f;
            float elapsed = 0f;
            float arcHeight = 4f;
            Vector3 initial = start;
            Vector3 final = target;

            while (elapsed < flightTime && grenadeObj != null)
            {
                float t = elapsed / flightTime;
                Vector3 pos = Vector3.Lerp(initial, final, t);
                pos.y += arcHeight * Mathf.Sin(t * Mathf.PI); // parabole
                grenadeObj.transform.position = pos;
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Explosion.
            if (grenadeObj != null)
            {
                Vector3 explosionPos = grenadeObj.transform.position;
                if (ObjectPooler.Instance != null && !string.IsNullOrEmpty(_grenadePoolId))
                {
                    // Le projectile est poolé : on le retourne.
                    var comp = grenadeObj.GetComponent<Component>();
                    if (comp != null) ObjectPooler.Instance.Release(comp);
                }
                else if (grenadeObj != null)
                {
                    Destroy(grenadeObj);
                }
                yield return new WaitForSeconds(_grenadeFuseTime);
                ExecuteSlam(damage, element, explosionPos);
            }
        }

        // =================================================================================
        //  VFX SPAWN (poolé)
        // =================================================================================

        private void SpawnTelegraph(EnemyAttackType type, Vector3 target)
        {
            if (_telegraphVfxPrefab == null || ObjectPooler.Instance == null ||
                string.IsNullOrEmpty(_telegraphPoolId)) return;
            Vector3 origin = type == EnemyAttackType.AoESlam || type == EnemyAttackType.GrenadeToss
                ? target
                : transform.position + Vector3.up * 1.2f;
            var vfx = ObjectPooler.Instance.Get<Component>(_telegraphPoolId, origin, Quaternion.identity);
            if (vfx != null)
            {
                // Auto-release après la durée du télégraphe.
                StartCoroutine(ReleaseAfter(vfx, _telegraphDuration + 0.1f));
            }
        }

        private void SpawnTracer(Vector3 from, Vector3 to)
        {
            if (_tracerPrefab == null || ObjectPooler.Instance == null || string.IsNullOrEmpty(_tracerPoolId))
            {
                // Fallback : Debug.DrawLine en scène (visible seulement en éditeur).
                return;
            }
            Vector3 mid = (from + to) * 0.5f;
            Quaternion rot = Quaternion.LookRotation((to - from).normalized);
            var tracer = ObjectPooler.Instance.Get<Component>(_tracerPoolId, mid, rot);
            if (tracer != null)
            {
                float length = Vector3.Distance(from, to);
                tracer.transform.localScale = new Vector3(0.05f, 0.05f, length);
                StartCoroutine(ReleaseAfter(tracer, _tracerDuration));
            }
        }

        private void SpawnSlamVfx(Vector3 center)
        {
            // Réutilise le pool télégraphe si pas de pool dédié (acceptable pour mobile).
            if (_telegraphVfxPrefab == null || ObjectPooler.Instance == null ||
                string.IsNullOrEmpty(_telegraphPoolId)) return;
            var vfx = ObjectPooler.Instance.Get<Component>(_telegraphPoolId, center, Quaternion.Euler(90f, 0f, 0f));
            if (vfx != null)
            {
                vfx.transform.localScale = Vector3.one * _slamRadius;
                StartCoroutine(ReleaseAfter(vfx, 0.5f));
            }
        }

        private IEnumerator ReleaseAfter(Component obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null && ObjectPooler.Instance != null)
            {
                ObjectPooler.Instance.Release(obj);
            }
        }

        private void PlaySfx(AudioClip clip)
        {
            if (clip == null || AudioManager.Instance == null) return;
            AudioManager.Instance.PlaySfx(clip, transform.position);
        }
    }
}
