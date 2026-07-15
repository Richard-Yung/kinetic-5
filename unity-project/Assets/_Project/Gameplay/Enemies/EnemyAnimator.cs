using KINETICS5.Data;
using UnityEngine;

namespace KINETICS5.Gameplay.Enemies
{
    /// <summary>
    /// Wrapper d'animation pour ennemis. Pilote un <c>Animator</c> Unity via paramètres
    /// standardisés (Blend Tree 2D pour idle/walk/run, triggers pour attaques et hit,
    /// booléen pour stun et mort). Supporte des override controllers par <see cref="EnemyClass"/>.
    /// </summary>
    /// <remarks>
    /// <b>Conventions de paramètres Animator :</b>
    /// <list type="bullet">
    ///   <item><c>MoveSpeed</c> (float 0..1) : pilotage du blend tree idle→walk→run.</item>
    ///   <item><c>State</c> (int 0..6) : état <see cref="EnemyState"/> cast en int.</item>
    ///   <item><c>IsDead</c> (bool) : déclenche la couche Death.</item>
    ///   <item><c>IsStunned</c> (bool) : gel des actions, hit reaction prolongée.</item>
    ///   <item><c>HitTrigger</c> (trigger) : réaction de coup court.</item>
    ///   <item><c>AttackWindup</c>/<c>AttackRelease</c> (trigger) : phases d'attaque.</item>
    /// </list>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimator : MonoBehaviour
    {
        [Header("Références")]
        [Tooltip("Animator Unity (auto-résolu si sur le même GameObject).")]
        [SerializeField] private Animator _animator;

        [Header("Blend tree")]
        [Tooltip("Vitesse de transition du blend tree (0..1).")]
        [Range(0.1f, 10f)][SerializeField] private float _blendLerpSpeed = 4f;
        [Tooltip("Seuil de vitesse en dessous duquel l'ennemi est considéré Idle (m/s).")]
        [SerializeField] private float _idleThreshold = 0.1f;
        [Tooltip("Seuil de vitesse au-dessus duquel l'ennemi est en Run (m/s).")]
        [SerializeField] private float _runThreshold = 4f;

        [Header("Override par classe (optionnel)")]
        [Tooltip("Override controller pour la classe Grunt (si null, animator par défaut).")]
        [SerializeField] private AnimatorOverrideController _gruntOverride;
        [Tooltip("Override controller pour la classe Soldier.")]
        [SerializeField] private AnimatorOverrideController _soldierOverride;
        [Tooltip("Override controller pour la classe Elite.")]
        [SerializeField] private AnimatorOverrideController _eliteOverride;
        [Tooltip("Override controller pour la classe Sniper.")]
        [SerializeField] private AnimatorOverrideController _sniperOverride;
        [Tooltip("Override controller pour la classe Heavy.")]
        [SerializeField] private AnimatorOverrideController _heavyOverride;
        [Tooltip("Override controller pour la classe Drone.")]
        [SerializeField] private AnimatorOverrideController _droneOverride;
        [Tooltip("Override controller pour la classe Boss.")]
        [SerializeField] private AnimatorOverrideController _bossOverride;

        // IDs de paramètres (hashés une fois pour éviter les allocations string par frame).
        private static readonly int SpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int StateHash = Animator.StringToHash("State");
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
        private static readonly int IsStunnedHash = Animator.StringToHash("IsStunned");
        private static readonly int HitTriggerHash = Animator.StringToHash("HitTrigger");
        private static readonly int AttackWindupHash = Animator.StringToHash("AttackWindup");
        private static readonly int AttackReleaseHash = Animator.StringToHash("AttackRelease");
        private static readonly int AlertHash = Animator.StringToHash("Alert");

        private float _currentBlendSpeed;
        private bool _isStunned;
        private bool _isDead;
        private EnemyClass _appliedOverride = (EnemyClass)(-1);

        private void Reset()
        {
            _animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
        }

        /// <summary>
        /// Applique l'override controller correspondant à la classe de l'ennemi.
        /// À appeler après Initialize du contrôleur.
        /// </summary>
        public void ApplyClassOverride(EnemyClass cls)
        {
            if (_animator == null || cls == _appliedOverride) return;
            AnimatorOverrideController ovc = cls switch
            {
                EnemyClass.Grunt => _gruntOverride,
                EnemyClass.Soldier => _soldierOverride,
                EnemyClass.Elite => _eliteOverride,
                EnemyClass.Sniper => _sniperOverride,
                EnemyClass.Heavy => _heavyOverride,
                EnemyClass.Drone => _droneOverride,
                EnemyClass.Boss => _bossOverride,
                _ => null
            };
            if (ovc != null)
            {
                _animator.runtimeAnimatorController = ovc;
            }
            _appliedOverride = cls;
        }

        /// <summary>Tick d'état : met à jour les paramètres du blend tree.</summary>
        /// <param name="state">État courant de la machine à états.</param>
        /// <param name="moveSpeed">Vitesse de mouvement en m/s.</param>
        /// <param name="hasTarget">Vrai si un joueur est en vue.</param>
        /// <param name="isDead">Vrai si mort.</param>
        public void TickState(EnemyState state, float moveSpeed, bool hasTarget, bool isDead)
        {
            if (_animator == null) return;

            // Blend tree : normalize 0..1 (idle → walk → run).
            float targetBlend;
            if (isDead) targetBlend = 0f;
            else if (moveSpeed <= _idleThreshold) targetBlend = 0f;
            else if (moveSpeed >= _runThreshold) targetBlend = 1f;
            else targetBlend = (moveSpeed - _idleThreshold) / (_runThreshold - _idleThreshold);

            _currentBlendSpeed = Mathf.Lerp(_currentBlendSpeed, targetBlend,
                                            Time.deltaTime * _blendLerpSpeed);

            _animator.SetFloat(SpeedHash, _currentBlendSpeed);
            _animator.SetInteger(StateHash, (int)state);
            _animator.SetBool(IsDeadHash, isDead);
            _animator.SetBool(AlertHash, hasTarget);
        }

        /// <summary>Réinitialise l'animator à l'état Idle (pour les ennemis poolés réutilisés).</summary>
        public void ResetState()
        {
            _currentBlendSpeed = 0f;
            _isStunned = false;
            _isDead = false;
            if (_animator == null) return;
            _animator.Rebind();
            _animator.SetFloat(SpeedHash, 0f);
            _animator.SetInteger(StateHash, (int)EnemyState.Idle);
            _animator.SetBool(IsDeadHash, false);
            _animator.SetBool(IsStunnedHash, false);
            _animator.SetBool(AlertHash, false);
            _animator.ResetTrigger(HitTriggerHash);
            _animator.ResetTrigger(AttackWindupHash);
            _animator.ResetTrigger(AttackReleaseHash);
        }

        // =================================================================================
        //  HOOKS D'ÉTAT (appelés par EnemyController.EnterState)
        // =================================================================================

        /// <summary>Joue l'animation Idle.</summary>
        public void PlayIdle() { /* géré par TickState via State int */ }

        /// <summary>Joue l'animation Patrouille.</summary>
        public void PlayPatrol() { /* géré par blend tree */ }

        /// <summary>Joue l'animation Alerte (passe en stance vigilante).</summary>
        public void PlayAlert()
        {
            if (_animator != null) _animator.SetTrigger(AlertHash);
        }

        /// <summary>Joue l'animation Poursuite.</summary>
        public void PlayChase() { /* géré par blend tree */ }

        /// <summary>Joue l'animation Stance d'attaque.</summary>
        public void PlayAttackStance()
        {
            // Rien : l'attaque est déclenchée par PlayAttackWindup.
        }

        /// <summary>Joue l'animation Fuite.</summary>
        public void PlayFlee() { /* géré par blend tree (vitesse élevée) */ }

        /// <summary>Joue l'animation de Mort.</summary>
        public void PlayDeath()
        {
            if (_animator == null) return;
            _isDead = true;
            _animator.SetBool(IsDeadHash, true);
        }

        // =================================================================================
        //  HOOKS DE COMBAT (appelés par EnemyCombat)
        // =================================================================================

        /// <summary>Joue le windup d'attaque (télégraphe anim).</summary>
        /// <param name="type">Type d'attaque (pour spécialisation anim futur).</param>
        public void PlayAttackWindup(EnemyAttackType type)
        {
            if (_animator != null) _animator.SetTrigger(AttackWindupHash);
        }

        /// <summary>Joue la libération d'attaque (coup/tir effectif).</summary>
        public void PlayAttackRelease(EnemyAttackType type)
        {
            if (_animator != null) _animator.SetTrigger(AttackReleaseHash);
        }

        /// <summary>Joue la réaction de coup (hit reaction courte).</summary>
        public void PlayHitReaction()
        {
            if (_animator == null || _isDead) return;
            _animator.SetTrigger(HitTriggerHash);
        }

        // =================================================================================
        //  STUN
        // =================================================================================

        /// <summary>Active ou désactive l'état Stunned (gel des actions).</summary>
        public void SetStunned(bool stunned)
        {
            if (_isStunned == stunned) return;
            _isStunned = stunned;
            if (_animator != null) _animator.SetBool(IsStunnedHash, stunned);
        }

        /// <summary>Vrai si l'ennemi est actuellement étourdi.</summary>
        public bool IsStunned => _isStunned;
    }
}
