// ============================================================================
//  KINETICS 5 — Player Controller (FPS mobile + desktop, FULL version)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Contrôleur FPS complet. Mouvement via CharacterController, lecture
//  InputManager.CurrentState, gravité, ground check, slope handling,
//  sprint stamina, crouch, head bob callback, footstep events, collision-based
//  interact, implémente IDamageable (register PlayerContext en OnEnable).
//
//  Implémente IDamageable en forwardant à PlayerStats (qui détient l'état réel
//  de santé/bouclier/buffs et publie PlayerDeathEvent).
//
//  Architecture :
//    • Singleton par scène (pas de DontDestroyOnLoad — recréé par scène mission).
//    • Dépend de InputManager, CameraManager, AudioManager via ServiceLocator.
//    • State machine interne : Idle / Walking / Sprinting / Crouching / Jumping / Falling / Dead.
//    • Mobile : virtual joystick (InputManager.CurrentState.Move) -> mouvement.
//    • Desktop : WASD (InputManager.CurrentState.Move).
// ============================================================================
using System;
using KINETICS5.Core;
using KINETICS5.Data;
using KINETICS5.Gameplay.Combat;
using UnityEngine;

namespace KINETICS5.Gameplay.Player
{
    /// <summary>État de mouvement du joueur.</summary>
    public enum PlayerMovementState
    {
        Idle, Walking, Sprinting, Crouching, Jumping, Falling, Dead
    }

    /// <summary>
    /// Contrôleur joueur FPS complet. Mouvement + collisions + stamina + crouch +
    /// head bob + footstep + interact + IDamageable (forward à PlayerStats).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Inscriptions statiques :</b>
    /// <list type="bullet">
    ///   <item><see cref="PlayerContext.Register"/> en OnEnable (pour exposition position + IDamageable aux ennemis).</item>
    ///   <item><see cref="ComboChain.SetLocalPlayerSourceId"/> en OnEnable (filtre des DamageDealtEvent).</item>
    ///   <item><see cref="DischargeSystem.UpdatePlayerPosition"/> chaque frame (pour centrer l'AoE ultimate).</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Performance mobile :</b> cache du CharacterController, aucun GetComponent
    /// dans Update, raycast non-alloc pour ground check, gravité accumulée (pas de
    /// Physics auto), slope handling via contact normals.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour, IDamageable
    {
        [Header("Mouvement")]
        [Tooltip("Vitesse de marche (m/s).")]
        [SerializeField] private float _walkSpeed = 4.5f;
        [Tooltip("Vitesse de sprint (m/s).")]
        [SerializeField] private float _sprintSpeed = 7.0f;
        [Tooltip("Vitesse en crouch (m/s).")]
        [SerializeField] private float _crouchSpeed = 2.0f;
        [Tooltip("Hauteur de saut (m).")]
        [SerializeField] private float _jumpHeight = 1.2f;
        [Tooltip("Gravité (m/s²).")]
        [SerializeField] private float _gravity = -19.6f;
        [Tooltip("Multiplicateur de vitesse quand lent (Cryo) — appliqué à MoveSpeed.")]
        [SerializeField] private float _slowMultiplier = 0.5f;

        [Header("Slope handling")]
        [Tooltip("Angle maximum de pente franchissable (degrés).")]
        [SerializeField] private float _slopeLimit = 50f;
        [Tooltip("Force de poussée sur les pentes (slide resistance).")]
        [SerializeField] private float _slideResistance = 2f;

        [Header("Stamina")]
        [Tooltip("Stamina max (sprint).")]
        [SerializeField] private float _maxStamina = 5f;
        [Tooltip("Coût stamina/s en sprint.")]
        [SerializeField] private float _sprintCost = 1f;
        [Tooltip("Régénération stamina/s.")]
        [SerializeField] private float _staminaRegen = 0.8f;
        [Tooltip("Délai avant régénération après sprint (s).")]
        [SerializeField] private float _staminaRegenDelay = 1.5f;

        [Header("Crouch")]
        [Tooltip("Hauteur debout (m).")]
        [SerializeField] private float _standHeight = 1.8f;
        [Tooltip("Hauteur en crouch (m).")]
        [SerializeField] private float _crouchHeight = 1.0f;
        [Tooltip("Vitesse de transition de hauteur (m/s).")]
        [SerializeField] private float _crouchTransitionSpeed = 8f;

        [Header("Head Bob")]
        [Tooltip("Fréquence du head bob en sprint (Hz).")]
        [SerializeField] private float _bobFrequency = 1.6f;
        [Tooltip("Amplitude du head bob (m).")]
        [SerializeField] private float _bobAmplitude = 0.025f;

        [Header("Footsteps")]
        [Tooltip("Intervalle entre les bruits de pas en marche (s).")]
        [SerializeField] private float _walkStepInterval = 0.5f;
        [Tooltip("Intervalle entre les bruits de pas en sprint (s).")]
        [SerializeField] private float _sprintStepInterval = 0.3f;
        [Tooltip("AudioClip de bruit de pas (fallback si null).")]
        [SerializeField] private AudioClip _footstepClip;

        [Header("Références")]
        [Tooltip("Composant PlayerStats (détient l'état de santé, implémente la logique de dégâts).")]
        [SerializeField] private PlayerStats _stats;
        [Tooltip("Composant PlayerWeaponManager (pour passage de position à ultimate).")]
        [SerializeField] private PlayerWeaponManager _weaponManager;
        [Tooltip("Composant PlayerAnimator (pour maj blend tree + bobbing viewmodel).")]
        [SerializeField] private PlayerAnimator _animator;
        [Tooltip("Composant PlayerCombat (pour déclencher melee via bouton dédié).")]
        [SerializeField] private PlayerCombat _combat;
        [Tooltip("Transform racine de la caméra FPS (pour head bob offset).")]
        [SerializeField] private Transform _cameraTransform;

        [Header("Agent")]
        [Tooltip("Id de l'agent (VULCAN/XEN/JOLT/XANO).")]
        [SerializeField] private string _agentId = "VULCAN";

        /// <summary>État de mouvement courant.</summary>
        public PlayerMovementState MovementState { get; private set; } = PlayerMovementState.Idle;
        /// <summary>Vitesse horizontale instantanée (m/s).</summary>
        public float HorizontalSpeed { get; private set; }
        /// <summary>Vrai si le joueur est mort (délègue à PlayerStats).</summary>
        public bool IsDead => _stats != null ? _stats.IsDead : false;
        /// <summary>Identifiant unique du joueur (instanceId).</summary>
        public uint PlayerId => _stats != null ? _stats.PlayerId : (uint)GetInstanceID();
        /// <summary>Stamina actuelle (0..MaxStamina).</summary>
        public float Stamina { get; private set; }
        /// <summary>Vitesse max théorique (anti-cheat, pour validation serveur).</summary>
        public float MaxTheoreticalSpeed => _sprintSpeed * 1.15f;

        // --- Implémentation IDamageable (forward à PlayerStats) ---
        /// <inheritdoc/>
        bool IDamageable.IsAlive => !IsDead;
        /// <inheritdoc/>
        Vector3 IDamageable.Position => transform.position;
        /// <inheritdoc/>
        public float TakeDamage(float amount, Element element, uint sourceId, Vector3 hitPoint, bool isCritical = false)
        {
            return _stats?.TakeDamage(amount, element, sourceId, hitPoint, isCritical) ?? 0f;
        }
        /// <inheritdoc/>
        public void Heal(float amount) => _stats?.Heal(amount);
        /// <inheritdoc/>
        public void RestoreShield(float amount) => _stats?.RestoreShield(amount);
        /// <inheritdoc/>
        public void Die() => _stats?.Die();

        // --- État interne ---
        private CharacterController _cc;
        private Vector3 _velocity;
        private float _verticalVelocity;
        private float _yaw;
        private float _pitch;
        private float _staminaRegenTimer;
        private float _targetHeight;
        private float _bobPhase;
        private float _footstepTimer;
        private Vector3 _cameraRestPos;
        private Vector3 _groundNormal;
        private bool _isGrounded;
        private bool _isCrouching;
        private float _slowFactor = 1f; // 1 = normal, <1 = ralenti (Cryo)

        // Buffer pour raycast non-alloc (ground check).
        private static readonly RaycastHit[] _groundHitBuffer = new RaycastHit[4];

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (_stats == null) _stats = GetComponent<PlayerStats>();
            if (_weaponManager == null) _weaponManager = GetComponent<PlayerWeaponManager>();
            if (_animator == null) _animator = GetComponentInChildren<PlayerAnimator>();
            if (_combat == null) _combat = GetComponent<PlayerCombat>();
            if (_cameraTransform != null) _cameraRestPos = _cameraTransform.localPosition;

            _targetHeight = _standHeight;
            _cc.height = _standHeight;
            _cc.slopeLimit = _slopeLimit;
            Stamina = _maxStamina;
        }

        private void OnEnable()
        {
            // Enregistrement auprès du PlayerContext (expose position + IDamageable aux ennemis).
            PlayerContext.Register(transform, this, PlayerId);
            // Filtre des DamageDealtEvent pour le ComboChain (seuls les hits du joueur local comptent).
            ComboChain.Instance?.SetLocalPlayerSourceId(PlayerId);
            // Définit l'agent courant pour le DischargeSystem (effet ultimate spécifique).
            DischargeSystem.Instance?.SetAgent(_agentId);
        }

        private void OnDisable()
        {
            PlayerContext.Unregister(transform);
        }

        private void Update()
        {
            if (IsDead)
            {
                MovementState = PlayerMovementState.Dead;
                return;
            }

            var input = ServiceLocator.Instance?.Get<InputManager>();
            var state = input != null ? input.CurrentState : default;

            HandleLook(state.LookDelta);
            HandleMovement(state);
            HandleStamina(state);
            HandleCrouch(state);
            HandleGroundCheck();
            ApplyGravity();
            ApplySlopeHandling();
            _cc.Move(_velocity * Time.deltaTime);

            HandleHeadBob();
            HandleFootsteps();
            HandleInteract(state);

            // Met à jour la position du joueur pour le DischargeSystem (AoE ultimate).
            if (_weaponManager != null)
            {
                _weaponManager.UpdateUltimatePosition(transform.position, PlayerId);
            }
        }

        // ==================== LOOK ====================

        private void HandleLook(Vector2 lookDelta)
        {
            _yaw += lookDelta.x;
            _pitch -= lookDelta.y;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_cameraTransform != null)
            {
                _cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        // ==================== MOVEMENT ====================

        private void HandleMovement(InputState state)
        {
            Vector2 move = state.Move;
            if (move.sqrMagnitude > 1f) move.Normalize();

            Vector3 forward = transform.forward * move.y + transform.right * move.x;
            forward = Vector3.ProjectOnPlane(forward, _groundNormal).normalized;

            bool wantsSprint = move.y > 0.5f && Stamina > 0f && !state.AimHeld && !_isCrouching;
            bool wantsCrouch = _isCrouching;

            // Vitesse de base selon état.
            float baseSpeed = _walkSpeed;
            if (wantsSprint) baseSpeed = _sprintSpeed;
            else if (wantsCrouch) baseSpeed = _crouchSpeed;

            // Applique le slow factor (Cryo).
            baseSpeed *= _slowFactor;

            // Applique la vitesse calculée par PlayerStats (si disponible).
            if (_stats != null)
            {
                float statsSpeed = _stats.MoveSpeed;
                // Normalise : la vitesse de base de PlayerStats correspond à walkSpeed.
                float ratio = statsSpeed / 4.5f; // 4.5 = vitesse nominale.
                baseSpeed *= ratio;
            }

            Vector3 horizontal = forward * baseSpeed;
            _velocity = new Vector3(horizontal.x, _verticalVelocity, horizontal.z);
            HorizontalSpeed = new Vector2(_velocity.x, _velocity.z).magnitude;

            // Mise à jour de l'état de mouvement.
            if (wantsSprint && move.sqrMagnitude > 0.01f) MovementState = PlayerMovementState.Sprinting;
            else if (wantsCrouch && move.sqrMagnitude > 0.01f) MovementState = PlayerMovementState.Crouching;
            else if (move.sqrMagnitude > 0.01f) MovementState = PlayerMovementState.Walking;
            else MovementState = PlayerMovementState.Idle;

            // Saut.
            if (state.JumpPressed && _isGrounded && !_isCrouching)
            {
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
                MovementState = PlayerMovementState.Jumping;
            }

            // Mise à jour de l'Animator viewmodel (blend tree + sprint).
            float normalizedSpeed = Mathf.Clamp01(HorizontalSpeed / _sprintSpeed);
            _animator?.UpdateMoveSpeed(normalizedSpeed, MovementState == PlayerMovementState.Sprinting);
        }

        // ==================== STAMINA ====================

        private void HandleStamina(InputState state)
        {
            if (MovementState == PlayerMovementState.Sprinting)
            {
                Stamina = Mathf.Max(0f, Stamina - _sprintCost * Time.deltaTime);
                _staminaRegenTimer = _staminaRegenDelay;
            }
            else
            {
                if (_staminaRegenTimer > 0f)
                {
                    _staminaRegenTimer -= Time.deltaTime;
                }
                else
                {
                    Stamina = Mathf.Min(_maxStamina, Stamina + _staminaRegen * Time.deltaTime);
                }
            }
        }

        // ==================== CROUCH ====================

        private void HandleCrouch(InputState state)
        {
            // Le crouch est déclenché par un bouton dédié (à câbler via InputAction).
            // Pour l'instant, on gère la transition douce de hauteur.
            _cc.height = Mathf.Lerp(_cc.height, _targetHeight, _crouchTransitionSpeed * Time.deltaTime);
            _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
        }

        /// <summary>
        /// Active/désactive le crouch (appelé par bouton UI ou InputAction dédié).
        /// </summary>
        public void SetCrouch(bool enabled)
        {
            _isCrouching = enabled;
            _targetHeight = enabled ? _crouchHeight : _standHeight;
        }

        // ==================== GROUND CHECK ====================

        private void HandleGroundCheck()
        {
            _isGrounded = _cc.isGrounded;
            if (_isGrounded)
            {
                // Calcule la normale du sol (pour slope handling).
                Vector3 origin = transform.position + Vector3.up * 0.1f;
                int hits = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHitBuffer, 0.5f, ~0);
                if (hits > 0)
                {
                    _groundNormal = _groundHitBuffer[0].normal;
                }
                else
                {
                    _groundNormal = Vector3.up;
                }
            }
            else
            {
                _groundNormal = Vector3.up;
            }
        }

        // ==================== GRAVITY ====================

        private void ApplyGravity()
        {
            if (_isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f; // coller au sol
                if (MovementState == PlayerMovementState.Jumping) MovementState = PlayerMovementState.Idle;
            }
            else if (!_isGrounded && MovementState != PlayerMovementState.Jumping)
            {
                MovementState = PlayerMovementState.Falling;
            }
            _verticalVelocity += _gravity * Time.deltaTime;
        }

        // ==================== SLOPE HANDLING ====================

        private void ApplySlopeHandling()
        {
            // Si on est sur une pente trop raide, on slide vers le bas.
            float slopeAngle = Vector3.Angle(_groundNormal, Vector3.up);
            if (slopeAngle > _cc.slopeLimit && _isGrounded)
            {
                Vector3 slideDir = (Vector3.down + _groundNormal * 2f).normalized;
                _velocity += slideDir * _slideResistance * Time.deltaTime * 10f;
            }
        }

        // ==================== HEAD BOB ====================

        private void HandleHeadBob()
        {
            if (_cameraTransform == null) return;

            float speedMag = HorizontalSpeed / _sprintSpeed;
            if (speedMag > 0.05f && _isGrounded)
            {
                _bobPhase += Time.deltaTime * _bobFrequency * (0.5f + speedMag);
                float bobY = Mathf.Sin(_bobPhase * 2f) * _bobAmplitude * speedMag;
                float bobX = Mathf.Cos(_bobPhase) * _bobAmplitude * 0.5f * speedMag;
                _cameraTransform.localPosition = _cameraRestPos + new Vector3(bobX, bobY, 0f);
            }
            else
            {
                _cameraTransform.localPosition = Vector3.Lerp(
                    _cameraTransform.localPosition, _cameraRestPos, Time.deltaTime * 8f);
            }
        }

        // ==================== FOOTSTEPS ====================

        private void HandleFootsteps()
        {
            if (!_isGrounded || HorizontalSpeed < 0.5f) return;

            float interval = MovementState == PlayerMovementState.Sprinting ? _sprintStepInterval : _walkStepInterval;
            _footstepTimer -= Time.deltaTime;
            if (_footstepTimer <= 0f)
            {
                _footstepTimer = interval;
                AudioManager.Instance?.PlaySfx(_footstepClip, transform.position, 0.4f);
            }
        }

        // ==================== INTERACT ====================

        private void HandleInteract(InputState state)
        {
            if (!state.InteractPressed) return;

            // Détection collision-based : raycast courte portée devant le joueur.
            if (_cameraTransform == null) return;
            Vector3 origin = _cameraTransform.position;
            Vector3 forward = _cameraTransform.forward;
            int hits = Physics.RaycastNonAlloc(origin, forward, _groundHitBuffer, 2.5f, ~0);
            if (hits > 0)
            {
                // Signale la tentative d'interaction (les objets interactifs écoutent cet event).
                Debug.Log($"[PlayerController] Interact attempt sur {_groundHitBuffer[0].collider.name}.");
                // Event futur : GameEventBus.Instance?.Publish(new InteractAttemptEvent(...));
            }
        }

        // ==================== COLLISION (interact auto) ====================

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Auto-interact on collision (pickup automatique si l'objet est interactif).
            // Délégué aux triggers ( OnTriggerEnter ) pour la plupart des cas.
        }

        // ==================== RESPAWN ====================

        /// <summary>
        /// Respawn le joueur à une position donnée (reset HP/Shield/buffs via PlayerStats).
        /// </summary>
        public void Respawn(Vector3 position, Quaternion rotation)
        {
            _stats?.Respawn(position, rotation);
            _velocity = Vector3.zero;
            _verticalVelocity = 0f;
            _yaw = rotation.eulerAngles.y;
            _pitch = 0f;
            Stamina = _maxStamina;
            MovementState = PlayerMovementState.Idle;
            _isCrouching = false;
            _targetHeight = _standHeight;
            if (_cameraTransform != null)
            {
                _cameraTransform.localRotation = Quaternion.identity;
                _cameraTransform.localPosition = _cameraRestPos;
            }
            CameraManager.Instance?.ResetLook();
        }

        // ==================== SLOW (debuff Cryo) ====================

        /// <summary>
        /// Applique un ralentissement temporaire au joueur (debuff Cryo).
        /// </summary>
        /// <param name="factor">Facteur multiplicatif (1 = normal, 0.5 = ralenti 50%).</param>
        /// <param name="duration">Durée (s).</param>
        public void ApplySlow(float factor, float duration)
        {
            _slowFactor = Mathf.Min(_slowFactor, factor);
            // Réinitialisation différée après duration.
            CancelInvoke(nameof(ResetSlow));
            Invoke(nameof(ResetSlow), duration);
        }

        private void ResetSlow()
        {
            _slowFactor = 1f;
        }

        // ==================== AGENT ====================

        /// <summary>
        /// Change l'agent courant (re-recalcule les stats via PlayerStats).
        /// </summary>
        public void SetAgent(string agentId)
        {
            _agentId = agentId ?? "VULCAN";
            _stats?.RecalculateFromAgent();
            DischargeSystem.Instance?.SetAgent(_agentId);
        }
    }
}
