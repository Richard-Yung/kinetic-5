using System;
using System.Collections.Generic;
using KINETICS5.Core;
using KINETICS5.Data;
using KINETICS5.Gameplay.Combat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KINETICS5.Gameplay.Enemies
{
    /// <summary>
    /// États possibles de la machine à états finie d'un ennemi KINETICS 5.
    /// </summary>
    public enum EnemyState
    {
        /// <summary>Aucun ordre, attend un stimulus.</summary>
        Idle,
        /// <summary>Patrouille entre waypoints.</summary>
        Patrol,
        /// <summary>A repéré le joueur, mais pas encore engagé (recherche/observation).</summary>
        Alert,
        /// <summary>Poursuivit activement le joueur.</summary>
        Chase,
        /// <summary>À portée d'attaque, déclenche tirs/coups.</summary>
        Attack,
        /// <summary>Recule pour échapper au joueur (basse PV ou sniper).</summary>
        Flee,
        /// <summary>Mort (loot drop puis cleanup différé).</summary>
        Dead
    }

    /// <summary>
    /// Contrôleur de base d'un ennemi KINETICS 5. Composant racine qui orchestre :
    /// <list type="bullet">
    ///   <item>Résolution des données depuis <see cref="DataLoader"/> (par EnemyId).</item>
    ///   <item>Initialisation du <see cref="HealthComponent"/> avec scaling de difficulté.</item>
    ///   <item>Machine à états <see cref="EnemyState"/> (Idle/Patrol/Alert/Chase/Attack/Flee/Dead).</item>
    ///   <item>Acquisition de la position joueur via <see cref="PlayerContext"/> (zéro FindObject).</item>
    ///   <item>Orientation progressive (LookAt lissée) vers la cible.</item>
    ///   <item>Hooks d'animation via <see cref="EnemyAnimator"/>.</item>
    ///   <item>Mort → drop de loot (LootDropSystem), publication <c>EnemyKilledEvent</c>, cleanup.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance mobile :</b> tick IA à fréquence réduite (1 frame sur 2 par défaut),
    /// cache des positions, aucun <c>GameObject.Find</c>, pooling-friendly via
    /// <see cref="IPooledItem"/> (l'ennemi se reset au spawn au lieu d'être réinstancié).
    /// </para>
    /// <para>
    /// <b>Pool friendly :</b> implémente <see cref="Core.IPooledItem"/> pour réutilisation
    /// via <c>ObjectPooler</c>. Le cycle Spawn→Death→Release est géré par
    /// <see cref="EnemySpawner"/>.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HealthComponent))]
    public class EnemyController : MonoBehaviour, IPooledItem, IDamageable
    {
        [Header("Résolution data")]
        [Tooltip("Id de l'ennemi tel que défini dans Resources/Data/enemies.json (ex: \"grunt_mk1\").")]
        [SerializeField] private string _enemyId = string.Empty;
        [Tooltip("EnemySO direct (override du DataLoader ; utile pour les tests éditeur).")]
        [SerializeField] private EnemySO _enemySoOverride;

        [Header("Composants")]
        [SerializeField] private HealthComponent _health;
        [SerializeField] private EnemyAnimator _animator;
        [SerializeField] private EnemyAI _ai;
        [SerializeField] private EnemyCombat _combat;
        [Tooltip("Caractère optionnel : si null, le transform local est utilisé pour le mouvement.")]
        [SerializeField] private CharacterController _characterController;

        [Header("Patrouille")]
        [Tooltip("Points de patrouille (en mode AIBehavior.Patrol).")]
        [SerializeField] private Transform[] _patrolWaypoints = Array.Empty<Transform>();
        [Tooltip("Rayon de tolérance pour considérer un waypoint atteint.")]
        [SerializeField] private float _waypointTolerance = 1.2f;

        [Header("Détection")]
        [Tooltip("Distance de détection du joueur (en mètres).")]
        [SerializeField] private float _detectionRange = 35f;
        [Tooltip("Champ de vision horizontal en degrés.")]
        [Range(30f, 360f)][SerializeField] private float _fieldOfView = 220f;
        [Tooltip("Distance à laquelle l'ennemi perd la poursuite.")]
        [SerializeField] private float _loseTargetRange = 60f;
        [Tooltip("Seuil de PV (% max) en dessous duquel l'ennemi peut fuir (comportement Berserker/Sniper).")]
        [Range(0f, 1f)][SerializeField] private float _fleeHealthThreshold = 0.25f;

        [Header("Rotation")]
        [Tooltip("Vitesse de rotation (deg/sec) pour faire face à la cible.")]
        [SerializeField] private float _turnSpeed = 540f;

        [Header("Tick IA")]
        [Tooltip("Une frame sur N pour le tick IA (1 = chaque frame, 3 = 1/3 fréquence).")]
        [Range(1, 8)][SerializeField] private int _aiTickEveryNFrames = 2;

        [Header("Cleanup")]
        [Tooltip("Délai après mort avant retour au pool / destruction.")]
        [SerializeField] private float _corpseCleanupDelay = 4f;

        /// <summary>État courant de la machine à états.</summary>
        public EnemyState State { get; private set; } = EnemyState.Idle;

        /// <summary>Données ennemi résolues (runtime).</summary>
        public EnemyDto Data { get; private set; }

        /// <summary>Identifiant d'instance unique (pour les events).</summary>
        public uint InstanceId { get; private set; }

        /// <summary>Dernière position connue du joueur (mise à jour si LOS).</summary>
        public Vector3 LastKnownPlayerPosition { get; private set; }

        /// <summary>Vrai si le joueur est actuellement visible (LOS + FOV + range).</summary>
        public bool HasPlayerInSight { get; private set; }

        /// <summary>Vrai si l'ennemi est actif (spawné, pas mort).</summary>
        public bool IsActive { get; private set; }

        /// <summary>Waypoints de patrouille exposés pour l'IA.</summary>
        public Transform[] PatrolWaypoints => _patrolWaypoints;

        /// <summary>Tolérance waypoint.</summary>
        public float WaypointTolerance => _waypointTolerance;

        /// <summary>Index du waypoint courant (muté par l'IA).</summary>
        public int CurrentWaypointIndex { get; set; }

        /// <summary>Composant santé (pour hooks externes).</summary>
        public HealthComponent Health => _health;

        /// <summary>Animator wrapper (peut être null si l'ennemi n'a pas d'anim).</summary>
        public EnemyAnimator Animator => _animator;

        /// <summary>Wrapper IA (peut être null en mode "dummy" de test).</summary>
        public EnemyAI AI => _ai;

        /// <summary>Wrapper combat (peut être null si ennemi pacifique).</summary>
        public EnemyCombat Combat => _combat;

        /// <summary>Vrai si l'ennemi est mort (état <see cref="EnemyState.Dead"/>).</summary>
        public bool IsDead => State == EnemyState.Dead;

        // --- État runtime interne ---
        private static uint _nextInstanceId = 1u;
        private int _tickFrameCounter;
        private float _stateTimer;
        private bool _isPooled;
        private bool _deathTriggered;
        private EnemySpawner _owningSpawner;

        // --- Buffer réutilisé pour les checks LOS (zéro alloc) ---
        private static readonly RaycastHit[] LosHits = new RaycastHit[8];

        private void Reset()
        {
            _health = GetComponent<HealthComponent>();
            _animator = GetComponentInChildren<EnemyAnimator>();
            _ai = GetComponent<EnemyAI>();
            _combat = GetComponent<EnemyCombat>();
            _characterController = GetComponent<CharacterController>();
        }

        /// <summary>Awake protégé virtual pour permettre à <see cref="BossController"/> d'étendre l'init.</summary>
        protected virtual void Awake()
        {
            InstanceId = _nextInstanceId++;
            if (_health == null) _health = GetComponent<HealthComponent>();
            // Hooks de santé
            if (_health != null)
            {
                _health.OnDied += HandleDeath;
            }
        }

        protected virtual void OnEnable()
        {
            // Ne pas réinitialiser ici : OnSpawnFromPool gère l'init des ennemis poolés.
            // Pour les ennemis placés à la main en scène, Initialize() doit être appelé
            // par le spawner ou manuellement.
        }

        protected virtual void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDied -= HandleDeath;
            }
        }

        /// <summary>Update virtual pour que <see cref="BossController"/> puisse ajouter du tick spécifique.</summary>
        protected virtual void Update()
        {
            if (!IsActive || State == EnemyState.Dead) return;

            // Tick IA à fréquence réduite pour économiser le CPU mobile.
            _tickFrameCounter++;
            if (_tickFrameCounter >= _aiTickEveryNFrames)
            {
                _tickFrameCounter = 0;
                TickAI();
            }

            // Le LookAt reste à chaque frame pour rester fluide.
            UpdateFacing();
        }

        // =================================================================================
        //  IPooledItem
        // =================================================================================

        /// <inheritdoc/>
        void IPooledItem.OnSpawnFromPool()
        {
            _isPooled = true;
            _deathTriggered = false;
            IsActive = true;
            InitializeFromData();
        }

        /// <inheritdoc/>
        void IPooledItem.OnReturnToPool()
        {
            _isPooled = true;
            IsActive = false;
            State = EnemyState.Idle;
            _deathTriggered = false;
            _owningSpawner = null;
            if (_health != null) _health.ResetHealth();
            if (_animator != null) _animator.ResetState();
        }

        // =================================================================================
        //  INITIALISATION
        // =================================================================================

        /// <summary>
        /// Initialise l'ennemi à partir des données résolues via <see cref="DataLoader"/>.
        /// </summary>
        /// <param name="spawner">Spawner propriétaire (pour notifier la mort).</param>
        /// <param name="difficultyHpMult">Multiplicateur de PV (difficulté).</param>
        /// <param name="difficultyDmgMult">Multiplicateur de dégâts.</param>
        public void Initialize(EnemySpawner spawner, float difficultyHpMult = 1f, float difficultyDmgMult = 1f)
        {
            _owningSpawner = spawner;
            IsActive = true;
            InitializeFromData(difficultyHpMult, difficultyDmgMult);
        }

        private void InitializeFromData(float hpMult = 1f, float dmgMult = 1f)
        {
            // Résolution data : priorité SO override, sinon DataLoader.GetEnemy.
            if (_enemySoOverride != null)
            {
                Data = EnemyDtoFromSO(_enemySoOverride);
            }
            else if (!string.IsNullOrEmpty(_enemyId))
            {
                Data = DataLoader.GetEnemy(_enemyId);
                if (Data == null)
                {
                    Debug.LogError($"[EnemyController] EnemyId '{_enemyId}' introuvable dans DataLoader.");
                    IsActive = false;
                    return;
                }
            }
            else
            {
                Debug.LogError($"[EnemyController] Ni _enemyId ni _enemySoOverride défini sur {gameObject.name}.", this);
                IsActive = false;
                return;
            }

            // Initialisation santé avec scaling.
            if (_health != null)
            {
                _health.Initialize(Data.BaseHealth, Data.BaseShield, hpMult,
                                   Data.Weakness, Data.Resistance);
            }

            // Initialisation IA.
            if (_ai != null)
            {
                _ai.Initialize(this);
            }

            // Initialisation combat.
            if (_combat != null)
            {
                _combat.Initialize(this, dmgMult);
            }

            // Animator reset (state machine au repos).
            if (_animator != null)
            {
                _animator.ResetState();
            }

            State = EnemyState.Patrol;
            _stateTimer = 0f;
            _deathTriggered = false;
        }

        private static EnemyDto EnemyDtoFromSO(EnemySO so)
        {
            // Conversion minimale : pour les tests éditeur sans JSON.
            var dto = new EnemyDto
            {
                Id = so.Id,
                DisplayName = so.DisplayName,
                Class = so.Class,
                BaseHealth = so.BaseHealth,
                BaseShield = so.BaseShield,
                BaseDamage = so.BaseDamage,
                MoveSpeed = so.MoveSpeed,
                AttackRange = so.AttackRange,
                AttackRate = so.AttackRate,
                Behavior = so.Behavior,
                Weakness = so.Weakness,
                Resistance = so.Resistance,
                Icon = string.Empty,
                ModelPrefab = string.Empty,
                XpReward = so.XpReward,
                CrReward = so.CrReward,
            };
            foreach (var l in so.LootTable)
            {
                dto.LootTable.Add(new LootDropDto
                {
                    ItemId = l.ItemId,
                    DropChancePct = l.DropChancePct,
                    MinQty = l.MinQty,
                    MaxQty = l.MaxQty
                });
            }
            return dto;
        }

        // =================================================================================
        //  MACHINE À ÉTATS
        // =================================================================================

        private void TickAI()
        {
            _stateTimer += Time.deltaTime * _aiTickEveryNFrames;

            // Acquisition de la cible (zéro FindObject, via PlayerContext).
            UpdateTargetAcquisition();

            switch (State)
            {
                case EnemyState.Idle:
                    TickIdle();
                    break;
                case EnemyState.Patrol:
                    TickPatrol();
                    break;
                case EnemyState.Alert:
                    TickAlert();
                    break;
                case EnemyState.Chase:
                    TickChase();
                    break;
                case EnemyState.Attack:
                    TickAttack();
                    break;
                case EnemyState.Flee:
                    TickFlee();
                    break;
                case EnemyState.Dead:
                    // Rien : attend cleanup.
                    break;
            }

            // Hook animation : met à jour les paramètres du blend tree.
            if (_animator != null)
            {
                _animator.TickState(State, _ai != null ? _ai.CurrentMoveSpeed : 0f,
                                    HasPlayerInSight, IsDead);
            }
        }

        private void TickIdle()
        {
            if (HasPlayerInSight)
            {
                TransitionTo(EnemyState.Alert);
                return;
            }
            if (_patrolWaypoints.Length > 0 && (Data?.Behavior == AIBehavior.Patrol))
            {
                TransitionTo(EnemyState.Patrol);
            }
        }

        private void TickPatrol()
        {
            if (HasPlayerInSight)
            {
                TransitionTo(EnemyState.Alert);
                return;
            }
            _ai?.TickPatrol();
        }

        private void TickAlert()
        {
            // Court délai d'alerte avant la poursuite (1s).
            if (_stateTimer >= 1f || HasPlayerInSight)
            {
                TransitionTo(EnemyState.Chase);
            }
            else if (!HasPlayerInSight && _stateTimer >= 3f)
            {
                TransitionTo(EnemyState.Patrol);
            }
        }

        private void TickChase()
        {
            if (!HasPlayerInSight && _stateTimer > 4f)
            {
                // Perte de cible → retour patrouille.
                TransitionTo(EnemyState.Patrol);
                return;
            }
            // À portée d'attaque ?
            float distSq = (LastKnownPlayerPosition - transform.position).sqrMagnitude;
            float range = Data?.AttackRange ?? 20f;
            if (distSq <= range * range)
            {
                TransitionTo(EnemyState.Attack);
                return;
            }
            _ai?.TickChase();
        }

        private void TickAttack()
        {
            float distSq = (LastKnownPlayerPosition - transform.position).sqrMagnitude;
            float range = Data?.AttackRange ?? 20f;
            if (distSq > range * range * 1.2f)
            {
                TransitionTo(EnemyState.Chase);
                return;
            }
            // Fuite si bas PV et comportement Flee-enabled.
            if (_health != null && _health.CurrentHealth <= _health.MaxHealth * _fleeHealthThreshold
                && (Data?.Behavior == AIBehavior.Berserker || Data?.Behavior == AIBehavior.Sniper))
            {
                // Le berserker enrage plutôt qu'il ne fuit, sauf si configuré.
                if (Data?.Behavior == AIBehavior.Sniper)
                {
                    TransitionTo(EnemyState.Flee);
                    return;
                }
            }
            _ai?.TickAttack();
            _combat?.TryAttack();
        }

        private void TickFlee()
        {
            // S'éloigne du joueur jusqu'à portée de sniper safe.
            _ai?.TickFlee();
            float distSq = (LastKnownPlayerPosition - transform.position).sqrMagnitude;
            float safeRange = (Data?.AttackRange ?? 80f) * 0.85f;
            if (distSq >= safeRange * safeRange)
            {
                TransitionTo(EnemyState.Attack);
            }
        }

        /// <summary>Transition d'état avec hook d'enter/exit.</summary>
        public void TransitionTo(EnemyState newState)
        {
            if (State == newState) return;
            ExitState(State);
            State = newState;
            _stateTimer = 0f;
            EnterState(newState);
        }

        private void EnterState(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Idle:
                    _animator?.PlayIdle();
                    break;
                case EnemyState.Patrol:
                    _animator?.PlayPatrol();
                    break;
                case EnemyState.Alert:
                    _animator?.PlayAlert();
                    break;
                case EnemyState.Chase:
                    _animator?.PlayChase();
                    break;
                case EnemyState.Attack:
                    _animator?.PlayAttackStance();
                    break;
                case EnemyState.Flee:
                    _animator?.PlayFlee();
                    break;
                case EnemyState.Dead:
                    _animator?.PlayDeath();
                    break;
            }
        }

        private void ExitState(EnemyState state)
        {
            // Hook futur : VFX de sortie d'état (ex: désactivation du halo d'alerte).
        }

        // =================================================================================
        //  ACQUISITION CIBLE + LOS
        // =================================================================================

        private void UpdateTargetAcquisition()
        {
            if (!PlayerContext.TryGetPlayer(out var playerTransform))
            {
                HasPlayerInSight = false;
                return;
            }

            Vector3 toPlayer = playerTransform.position - transform.position;
            float dist = toPlayer.magnitude;
            if (dist > _detectionRange)
            {
                HasPlayerInSight = false;
                return;
            }

            // Champ de vision (skip si drone ou 360°).
            if (_fieldOfView < 360f)
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                Vector3 flatTo = toPlayer;
                flatTo.y = 0f;
                if (forward.sqrMagnitude > 0.001f && flatTo.sqrMagnitude > 0.001f)
                {
                    float angle = Vector3.Angle(forward, flatTo);
                    if (angle > _fieldOfView * 0.5f)
                    {
                        HasPlayerInSight = false;
                        return;
                    }
                }
            }

            // LOS via raycast (utilise un buffer poolé).
            bool los = HasLineOfSight(playerTransform.position + Vector3.up * 1.2f,
                                      transform.position + Vector3.up * 1.2f);
            if (los)
            {
                HasPlayerInSight = true;
                LastKnownPlayerPosition = playerTransform.position;
            }
            else
            {
                HasPlayerInSight = false;
            }
        }

        /// <summary>
        /// Vérifie la ligne de vue entre deux points via raycast (zéro allocation,
        /// utilise un buffer statique).
        /// </summary>
        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.01f) return true;
            dir /= dist; // normalize

            int mask = ~0; // tous les layers ; en prod, filtrer via LayerMask.
            int hitCount = Physics.RaycastNonAlloc(from, dir, LosHits, dist, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = LosHits[i];
                if (hit.collider == null) continue;
                // Ignore l'ennemi lui-même et les autres ennemis (pas d'occlusion par congénères).
                if (hit.collider.transform == transform) continue;
                if (hit.collider.CompareTag("Enemy")) continue;
                // Tout autre collider = blocage.
                return false;
            }
            return true;
        }

        // =================================================================================
        //  ORIENTATION
        // =================================================================================

        private void UpdateFacing()
        {
            if (State == EnemyState.Dead) return;
            // Face au joueur en Chase/Attack ; face au mouvement en Patrol/Flee.
            Vector3 lookTarget;
            if (State == EnemyState.Chase || State == EnemyState.Attack || State == EnemyState.Alert)
            {
                if (!PlayerContext.TryGetPlayer(out _)) return;
                lookTarget = LastKnownPlayerPosition;
            }
            else
            {
                return; // L'IA gère l'orientation via mouvement.
            }

            Vector3 dir = lookTarget - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * Time.deltaTime);
        }

        // =================================================================================
        //  MORT
        // =================================================================================

        private void HandleDeath(HealthComponent health, uint killerId)
        {
            if (_deathTriggered) return;
            _deathTriggered = true;
            IsActive = false;
            TransitionTo(EnemyState.Dead);

            // Loot drop.
            if (Data != null && LootDropSystem.HasInstance)
            {
                LootDropSystem.Instance.SpawnLoot(Data, transform.position);
            }

            // Publication EnemyKilledEvent (zero-alloc).
            int xp = Data?.XpReward ?? 0;
            int cr = Data?.CrReward ?? 0;
            var bus = GameEventBus.Instance;
            if (bus != null)
            {
                bus.Publish(new EnemyKilledEvent(InstanceId, killerId, xp, cr, transform.position));
            }

            // Telemetry : le TelemetryLogger peut souscrire à EnemyKilledEvent sur le bus global.
            // Pas d'appel direct ici pour éviter le couplage et les soucis d'API.

            // Notification du spawner propriétaire (pour suivi de vague).
            _owningSpawner?.NotifyEnemyDeath(this);

            // Cleanup différé : retour pool si poolé, sinon destruction.
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(CleanupAfterDelay(_corpseCleanupDelay));
            }
        }

        private System.Collections.IEnumerator CleanupAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (_isPooled && ObjectPooler.Instance != null)
            {
                // Retour au pool. Le spawner garde le compte via NotifyEnemyDeath.
                ObjectPooler.Instance.Release(gameObject.GetComponent<EnemyController>());
            }
            else
            {
                if (gameObject != null) Destroy(gameObject);
            }
        }

        // =================================================================================
        //  IDamageable (délègue à HealthComponent)
        // =================================================================================

        /// <inheritdoc/>
        bool IDamageable.IsAlive => State != EnemyState.Dead && _health != null && _health.IsAlive;

        /// <inheritdoc/>
        Vector3 IDamageable.Position => transform.position;

        /// <inheritdoc/>
        float IDamageable.TakeDamage(float amount, Element element, uint sourceId, Vector3 hitPoint, bool isCritical)
        {
            if (_health == null) return 0f;
            // Hit reaction anim si significatif.
            if (amount > 5f && _animator != null && State != EnemyState.Dead)
            {
                _animator.PlayHitReaction();
            }
            return _health.TakeDamage(amount, element, sourceId, hitPoint, isCritical);
        }

        /// <inheritdoc/>
        void IDamageable.Heal(float amount) => _health?.Heal(amount);

        /// <inheritdoc/>
        void IDamageable.RestoreShield(float amount) => _health?.RestoreShield(amount);

        /// <inheritdoc/>
        void IDamageable.Die() => _health?.Die();

        // =================================================================================
        //  GIZMOS (édition seulement)
        // =================================================================================

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Champ de détection.
            Gizmos.color = new Color(0.1f, 0.63f, 0.81f, 0.25f); // KINETICS cyan
            UnityEditor.Handles.DrawWireArc(transform.position, Vector3.up, transform.forward,
                                            _fieldOfView * 0.5f, _detectionRange);
            UnityEditor.Handles.DrawWireArc(transform.position, Vector3.up, transform.forward,
                                            -_fieldOfView * 0.5f, _detectionRange);
            Gizmos.color = new Color(0.1f, 0.63f, 0.81f, 0.6f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * _detectionRange);

            // Portée d'attaque.
            if (Data != null)
            {
                Gizmos.color = new Color(0.996f, 0f, 0.133f, 0.3f); // KINETICS rouge
                Gizmos.DrawWireSphere(transform.position, Data.AttackRange);
            }

            // Waypoints.
            if (_patrolWaypoints != null)
            {
                Gizmos.color = new Color(0.42f, 0.96f, 0.17f, 0.8f); // KINETICS vert
                foreach (var wp in _patrolWaypoints)
                {
                    if (wp != null) Gizmos.DrawSphere(wp.position, 0.4f);
                }
            }
        }
#endif
    }
}
