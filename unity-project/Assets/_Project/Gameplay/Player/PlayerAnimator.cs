// ============================================================================
//  KINETICS 5 — Player Animator (viewmodel FPS : bras + arme première personne)
//  Task 2-b — Player & Combat (retry)
// ----------------------------------------------------------------------------
//  Animator du viewmodel FPS (bras + arme visible à l'écran) :
//    • Animations Fire / Reload / Switch / Idle / Sprint / Melee
//    • Animation events pour VFX/SFX (OnFireVFX, OnReloadSound, OnSwitchEnd)
//    • Blend trees pour weapon sway (idle/walk/run)
//    • Sway procédural basé sur le delta de regard (Mouse delta ou swipe mobile)
//    • Animation d'inspect (touche T)
//
//  Consomme InputManager.CurrentState pour le sway procédural et le sprint.
// ============================================================================
using System;
using KINETICS5.Core;
using UnityEngine;

namespace KINETICS5.Gameplay.Player
{
    /// <summary>
    /// État d'animation du viewmodel.
    /// </summary>
    public enum ViewmodelState
    {
        /// <summary>Au repos (arme baissée).</summary>
        Idle,
        /// <summary>En marche.</summary>
        Walking,
        /// <summary>En sprint (arme baissée, FOV légèrement élargi).</summary>
        Sprinting,
        /// <summary>En cours de reload.</summary>
        Reloading,
        /// <summary>En cours de switch d'arme.</summary>
        Switching,
        /// <summary>Animation de coup de crosse (melee).</summary>
        Melee,
        /// <summary>Animation d'inspection de l'arme.</summary>
        Inspecting
    }

    /// <summary>
    /// Animator du viewmodel FPS (bras + arme). Wraps un <see cref="Animator"/>
    /// Unity avec des triggers et des blend trees.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Architecture :</b>
    /// <list type="bullet">
    ///   <item>Animator Unity (assigné via Inspector) avec paramètres :
    ///     <c>MoveSpeed</c> (float 0..1, blend tree), <c>Fire</c> (trigger),
    ///     <c>Reload</c> (trigger), <c>Switch</c> (trigger), <c>Melee</c> (trigger),
    ///     <c>Inspect</c> (bool), <c>Sprint</c> (bool).</item>
    ///   <item>Animation events : <c>OnFireVFX</c>, <c>OnReloadSound</c>,
    ///     <c>OnSwitchEnd</c>, <c>OnMeleeHit</c>.</item>
    ///   <item>Sway procédural : rotation du viewmodel basée sur InputManager.LookDelta.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Performance mobile :</b> cache des Animator string-to-hash (static readonly),
    /// aucun GetComponent dans Update.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        // Hash des paramètres Animator (calculés une fois, IL2CPP-friendly).
        private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
        private static readonly int HashFire = Animator.StringToHash("Fire");
        private static readonly int HashReload = Animator.StringToHash("Reload");
        private static readonly int HashSwitch = Animator.StringToHash("Switch");
        private static readonly int HashMelee = Animator.StringToHash("Melee");
        private static readonly int HashInspect = Animator.StringToHash("Inspect");
        private static readonly int HashSprint = Animator.StringToHash("Sprint");
        private static readonly int HashReloadDuration = Animator.StringToHash("ReloadDuration");
        private static readonly int HashSwitchDuration = Animator.StringToHash("SwitchDuration");

        [Header("Références")]
        [Tooltip("Animator Unity du viewmodel (bras + arme).")]
        [SerializeField] private Animator _animator;
        [Tooltip("Transform racine du viewmodel (pour sway procédural).")]
        [SerializeField] private Transform _viewmodelRoot;
        [Tooltip("Camera FPS (pour calcul du sway directionnel).")]
        [SerializeField] private Transform _cameraTransform;

        [Header("Sway procédural")]
        [Tooltip("Amplitude du sway en degrés (rotation X/Y du viewmodel).")]
        [SerializeField] private float _swayAmplitude = 2f;
        [Tooltip("Vitesse de récupération du sway (degrés/s).")]
        [SerializeField] private float _swayRecovery = 6f;
        [Tooltip("Vitesse de réponse du sway (lerp factor).")]
        [SerializeField] private float _swayResponsiveness = 8f;
        [Tooltip("Amplitude du bobbing vertical en mouvement (m).")]
        [SerializeField] private float _bobAmplitude = 0.02f;
        [Tooltip("Fréquence du bobbing (Hz).")]
        [SerializeField] private float _bobFrequency = 1.6f;

        [Header("Inspect")]
        [Tooltip("Touche clavier pour inspecter l'arme (desktop).")]
        [SerializeField] private KeyCode _inspectKey = KeyCode.T;

        /// <summary>État courant du viewmodel.</summary>
        public ViewmodelState State { get; private set; } = ViewmodelState.Idle;

        // Cache pour sway procédural.
        private Vector2 _currentSway;
        private Vector2 _targetSway;
        private Vector3 _viewmodelRestPos;
        private Quaternion _viewmodelRestRot;
        private float _bobPhase;

        /// <summary>Événement déclenché par l'animation Fire (frame du muzzle flash).</summary>
        public event Action OnFireVFX;
        /// <summary>Événement déclenché par l'animation Reload (frame du son).</summary>
        public event Action OnReloadSound;
        /// <summary>Événement déclenché par l'animation Switch (fin d'animation).</summary>
        public event Action OnSwitchEnd;
        /// <summary>Événement déclenché par l'animation Melee (frame de l'impact).</summary>
        public event Action OnMeleeHit;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_viewmodelRoot == null) _viewmodelRoot = transform;
            _viewmodelRestPos = _viewmodelRoot.localPosition;
            _viewmodelRestRot = _viewmodelRoot.localRotation;
        }

        private void Update()
        {
            UpdateSway();
            UpdateBob();
            UpdateInspectInput();
        }

        // ==================== API PUBLIQUE ====================

        /// <summary>
        /// Met à jour la vitesse de déplacement (blend tree idle/walk/run).
        /// </summary>
        /// <param name="normalizedSpeed">Vitesse normalisée 0..1 (0 = immobile, 1 = sprint).</param>
        /// <param name="isSprinting">Vrai si le joueur sprinte.</param>
        public void UpdateMoveSpeed(float normalizedSpeed, bool isSprinting)
        {
            if (_animator == null) return;
            _animator.SetFloat(HashMoveSpeed, Mathf.Clamp01(normalizedSpeed));
            _animator.SetBool(HashSprint, isSprinting);
            if (State == ViewmodelState.Idle || State == ViewmodelState.Walking || State == ViewmodelState.Sprinting)
            {
                State = isSprinting ? ViewmodelState.Sprinting :
                        (normalizedSpeed > 0.05f ? ViewmodelState.Walking : ViewmodelState.Idle);
            }
        }

        /// <summary>
        /// Déclenche l'animation de tir.
        /// </summary>
        public void PlayFire()
        {
            if (_animator == null) return;
            _animator.SetTrigger(HashFire);
        }

        /// <summary>
        /// Déclenche l'animation de reload.
        /// </summary>
        /// <param name="duration">Durée du reload (s) pour override d'Animator.</param>
        public void PlayReload(float duration)
        {
            if (_animator == null) return;
            _animator.SetFloat(HashReloadDuration, duration);
            _animator.SetTrigger(HashReload);
            State = ViewmodelState.Reloading;
        }

        /// <summary>
        /// Déclenche l'animation de switch d'arme.
        /// </summary>
        /// <param name="duration">Durée du switch (s).</param>
        public void PlaySwitch(float duration)
        {
            if (_animator == null) return;
            _animator.SetFloat(HashSwitchDuration, duration);
            _animator.SetTrigger(HashSwitch);
            State = ViewmodelState.Switching;
        }

        /// <summary>
        /// Déclenche l'animation de melee (coup de crosse).
        /// </summary>
        public void PlayMelee()
        {
            if (_animator == null) return;
            _animator.SetTrigger(HashMelee);
            State = ViewmodelState.Melee;
        }

        /// <summary>
        /// Bascule l'état d'inspection de l'arme.
        /// </summary>
        public void SetInspect(bool isInspecting)
        {
            if (_animator == null) return;
            _animator.SetBool(HashInspect, isInspecting);
            if (isInspecting) State = ViewmodelState.Inspecting;
            else if (State == ViewmodelState.Inspecting) State = ViewmodelState.Idle;
        }

        /// <summary>
        /// Remet l'état à Idle (à appeler à la fin d'une animation one-shot).
        /// </summary>
        public void ResetToIdle()
        {
            State = ViewmodelState.Idle;
        }

        // ==================== ANIMATION EVENTS ====================

        // Ces méthodes sont appelées par les AnimationEvents dans l'Animator.
        // Elles forward aux events C# pour découpler PlayerCombat de l'Animator.

        /// <summary>Animation Event : déclenche le VFX/SFX du tir.</summary>
        private void OnFireVFXEvent()
        {
            OnFireVFX?.Invoke();
        }

        /// <summary>Animation Event : déclenche le son de reload.</summary>
        private void OnReloadSoundEvent()
        {
            OnReloadSound?.Invoke();
        }

        /// <summary>Animation Event : fin d'animation de switch.</summary>
        private void OnSwitchEndEvent()
        {
            if (State == ViewmodelState.Switching) State = ViewmodelState.Idle;
            OnSwitchEnd?.Invoke();
        }

        /// <summary>Animation Event : frame d'impact du melee.</summary>
        private void OnMeleeHitEvent()
        {
            OnMeleeHit?.Invoke();
            if (State == ViewmodelState.Melee) State = ViewmodelState.Idle;
        }

        // ==================== SWAY PROCÉDURAL ====================

        /// <summary>
        /// Calcule le sway procédural basé sur le delta de regard (InputManager).
        /// </summary>
        private void UpdateSway()
        {
            if (_viewmodelRoot == null) return;

            var input = InputManager.Instance?.CurrentState ?? default;
            Vector2 lookDelta = input.LookDelta;

            // Le sway inverse le delta (le viewmodel "retarde" derrière la caméra).
            _targetSway.x = Mathf.Clamp(-lookDelta.y * _swayAmplitude * 0.1f, -_swayAmplitude, _swayAmplitude);
            _targetSway.y = Mathf.Clamp(-lookDelta.x * _swayAmplitude * 0.1f, -_swayAmplitude, _swayAmplitude);

            // Lerp doux vers la cible (responsiveness).
            _currentSway = Vector2.Lerp(_currentSway, _targetSway, Time.deltaTime * _swayResponsiveness);

            // Récupération : ramène progressivement à zéro si pas d'input.
            if (lookDelta.sqrMagnitude < 0.001f)
            {
                _currentSway = Vector2.Lerp(_currentSway, Vector2.zero, Time.deltaTime * _swayRecovery);
            }

            // Applique la rotation locale du viewmodel.
            Quaternion swayRot = Quaternion.Euler(_currentSway.x, _currentSway.y, 0f);
            _viewmodelRoot.localRotation = _viewmodelRestRot * swayRot;
        }

        /// <summary>
        /// Bobbing vertical en mouvement (sinusoïde couplée à la vitesse).
        /// </summary>
        private void UpdateBob()
        {
            if (_viewmodelRoot == null) return;

            var input = InputManager.Instance?.CurrentState ?? default;
            float speedMag = input.Move.magnitude;

            if (speedMag > 0.05f)
            {
                _bobPhase += Time.deltaTime * _bobFrequency * (0.5f + speedMag);
                float bobY = Mathf.Sin(_bobPhase * 2f) * _bobAmplitude * speedMag;
                float bobX = Mathf.Cos(_bobPhase) * _bobAmplitude * 0.5f * speedMag;
                _viewmodelRoot.localPosition = _viewmodelRestPos + new Vector3(bobX, bobY, 0f);
            }
            else
            {
                _viewmodelRoot.localPosition = Vector3.Lerp(
                    _viewmodelRoot.localPosition, _viewmodelRestPos, Time.deltaTime * 8f);
            }
        }

        /// <summary>
        /// Lit l'input d'inspection (touche T desktop).
        /// </summary>
        private void UpdateInspectInput()
        {
            if (State == ViewmodelState.Switching || State == ViewmodelState.Reloading || State == ViewmodelState.Melee) return;
#if !ENABLE_INPUT_SYSTEM
            if (Input.GetKey(_inspectKey))
            {
                SetInspect(true);
            }
            else if (State == ViewmodelState.Inspecting)
            {
                SetInspect(false);
            }
#endif
        }
    }
}
