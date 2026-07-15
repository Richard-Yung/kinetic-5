using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Cysharp.Threading.Tasks;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace KINETICS5.Core
{
    /// <summary>
    /// État d'entrée consommé par PlayerController / CameraManager.
    /// Struct volontairement simple, lue sans allocation dans Update.
    /// </summary>
    public struct InputState
    {
        /// <summary>Vecteur de déplacement 2D (joystick gauche ou WASD), normalisé -1..1.</summary>
        public Vector2 Move;
        /// <summary>Delta de regard pour cette frame (swipe droit ou souris), en pixels/frame.</summary>
        public Vector2 LookDelta;
        /// <summary>Vrai si le joueur appuie sur Fire.</summary>
        public bool FireHeld;
        /// <summary>Vrai si le joueur vise (AIM maintenu).</summary>
        public bool AimHeld;
        /// <summary>Impulsion Reload (true pendant 1 frame).</summary>
        public bool ReloadPressed;
        /// <summary>Impulsion Jump (true pendant 1 frame).</summary>
        public bool JumpPressed;
        /// <summary>Impulsion Grenade (true pendant 1 frame).</summary>
        public bool GrenadePressed;
        /// <summary>Impulsion Switch weapon (true pendant 1 frame).</summary>
        public bool SwitchPressed;
        /// <summary>Impulsion Interact (true pendant 1 frame).</summary>
        public bool InteractPressed;
        /// <summary>Périphérique actif (debug).</summary>
        public InputDeviceType ActiveDevice;
    }

    public enum InputDeviceType { Mobile, Desktop, Gamepad }

    /// <summary>
    /// Gestionnaire d'entrées multi-périphériques pour KINETICS 5.
    /// - Mobile: joystick virtuel gauche + 6 boutons + swipe droit.
    /// - Desktop: WASD + mouse look + clics.
    /// - Gamepad: sticks + boutons standards.
    /// Expose <see cref="CurrentState"/> consommé par PlayerController.
    /// Supporte le rebind des contrôles et le haptic feedback mobile.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InputManager : MonoBehaviour
    {
        private static InputManager _instance;
        public static InputManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[InputManager]");
                    _instance = go.AddComponent<InputManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Configuration Mobile")]
        [Tooltip("Zone tactile gauche pour le joystick (0..1 proportion écran).")]
        [Range(0.1f, 0.5f)][SerializeField] private float _leftZoneRatio = 0.4f;
        [Tooltip("Sensibilité du swipe droit (degrés par pixel).")]
        [Range(0.05f, 1f)][SerializeField] private float _lookSensitivity = 0.18f;
        [Tooltip("Rayon du joystick virtuel flottant (pixels).")]
        [Range(40f, 200f)][SerializeField] private float _joystickRadius = 110f;
        [Tooltip("Active le haptic feedback sur mobile.")]
        [SerializeField] private bool _hapticsEnabled = true;

        [Header("Configuration Desktop")]
        [Tooltip("Sensibilité souris (degrés par pixel).")]
        [Range(0.05f, 1f)][SerializeField] private float _mouseSensitivity = 0.22f;

        [Header("Référence Input System")]
        [Tooltip("Asset Input System (généré par le Input Actions Editor).")]
        [SerializeField] private InputActionAsset _actionsAsset;

        /// <summary>État d'entrée courant (lecture par PlayerController).</summary>
        public InputState CurrentState;
        /// <summary>État précédent (détection de fronts montants).</summary>
        private InputState _prevState;

        private InputAction _moveAction, _lookAction, _fireAction, _aimAction,
                            _reloadAction, _jumpAction, _grenadeAction, _switchAction, _interactAction;

        // Joystick virtuel mobile.
        private int _joystickTouchId = -1;
        private int _lookTouchId = -1;
        private Vector2 _joystickOrigin;
        private Vector2 _joystickCurrent;
        private Vector2 _lastLookPos;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnhancedTouchSupport.Enable();
            SetupActions();
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            DisableActions();
            EnhancedTouchSupport.Disable();
            _instance = null;
        }

        private void OnEnable()
        {
            EnableActions();
        }

        private void OnDisable()
        {
            DisableActions();
        }

        /// <summary>Indique si un périphérique mobile (tactile) est actif.</summary>
        public bool IsTouchDevice => Touchscreen.current != null && Gamepad.current == null;

        private void Update()
        {
            CurrentState = default;
            var device = DetectDevice();
            CurrentState.ActiveDevice = device;
            switch (device)
            {
                case InputDeviceType.Mobile: UpdateMobile(); break;
                case InputDeviceType.Gamepad: UpdateGamepad(); break;
                default: UpdateDesktop(); break;
            }
            // Détection fronts montants (impulsions).
            CurrentState.ReloadPressed = _reloadAction?.WasPressedThisFrame() ?? false;
            CurrentState.JumpPressed = _jumpAction?.WasPressedThisFrame() ?? false;
            CurrentState.GrenadePressed = _grenadeAction?.WasPressedThisFrame() ?? false;
            CurrentState.SwitchPressed = _switchAction?.WasPressedThisFrame() ?? false;
            CurrentState.InteractPressed = _interactAction?.WasPressedThisFrame() ?? false;
            _prevState = CurrentState;
        }

        // --- Détection périphérique ---
        private InputDeviceType DetectDevice()
        {
            if (Gamepad.current != null) return InputDeviceType.Gamepad;
            if (Touchscreen.current != null && Touchscreen.current.enabled) return InputDeviceType.Mobile;
            return InputDeviceType.Desktop;
        }

        // --- Mobile ---
        private void UpdateMobile()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            var leftZoneMaxX = screen.x * _leftZoneRatio;

            // Joystick gauche flottant: premier touch dans la zone gauche.
            Vector2 move = Vector2.zero;
            Vector2 look = Vector2.zero;
            for (int i = 0; i < Touch.activeTouches.Count; i++)
            {
                var t = Touch.activeTouches[i];
                var x = t.screenPosition.x;
                bool inLeftZone = x < leftZoneMaxX;

                if (inLeftZone)
                {
                    if (_joystickTouchId == -1 && t.phase == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        _joystickTouchId = t.touchId;
                        _joystickOrigin = t.screenPosition;
                    }
                    if (_joystickTouchId == t.touchId)
                    {
                        if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended || t.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                        {
                            _joystickTouchId = -1; move = Vector2.zero;
                        }
                        else
                        {
                            _joystickCurrent = t.screenPosition;
                            var delta = (_joystickCurrent - _joystickOrigin) / _joystickRadius;
                            move = Vector2.ClampMagnitude(delta, 1f);
                        }
                    }
                }
                else
                {
                    // Zone droite: swipe look + boutons (boutons gérés par UI).
                    if (_lookTouchId == -1 && t.phase == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        _lookTouchId = t.touchId;
                        _lastLookPos = t.screenPosition;
                    }
                    if (_lookTouchId == t.touchId)
                    {
                        if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended || t.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                        {
                            _lookTouchId = -1; look = Vector2.zero;
                        }
                        else
                        {
                            look = (t.screenPosition - _lastLookPos) * _lookSensitivity;
                            _lastLookPos = t.screenPosition;
                        }
                    }
                }
            }
            CurrentState.Move = move;
            CurrentState.LookDelta = look;
            // Boutons mobiles (Fire/Aim/etc.) câblés via UI Button -> SetButtonState.
            CurrentState.FireHeld = _fireHeld;
            CurrentState.AimHeld = _aimHeld;
        }

        // --- Desktop ---
        private void UpdateDesktop()
        {
            CurrentState.Move = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            var mouseDelta = Mouse.current?.delta.ReadValue() ?? Vector2.zero;
            CurrentState.LookDelta = mouseDelta * _mouseSensitivity;
            CurrentState.FireHeld = _fireAction?.IsPressed() ?? false;
            CurrentState.AimHeld = _aimAction?.IsPressed() ?? false;
        }

        // --- Gamepad ---
        private void UpdateGamepad()
        {
            CurrentState.Move = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            var stickR = Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;
            CurrentState.LookDelta = stickR * _lookSensitivity * 50f; // amplitude jeu
            CurrentState.FireHeld = Gamepad.current?.rightTrigger.isPressed ?? false;
            CurrentState.AimHeld = Gamepad.current?.leftTrigger.isPressed ?? false;
        }

        // --- API pour boutons UI mobile ---
        private bool _fireHeld, _aimHeld;
        /// <summary>Appelé par EventTrigger des boutons UI mobile: Fire/Aim maintenus.</summary>
        public void SetButtonHeld(string buttonId, bool held)
        {
            switch (buttonId)
            {
                case "Fire": _fireHeld = held; if (held) TriggerHaptic(15); break;
                case "Aim": _aimHeld = held; break;
            }
        }

        /// <summary>Déclenche une vibration haptique courte (mobile uniquement).</summary>
        public void TriggerHaptic(int durationMs = 20, float amplitude = 0.6f)
        {
            if (!_hapticsEnabled) return;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                gamepad.SetMotorSpeeds(amplitude, amplitude);
                this.DelayedResetGamepad(durationMs).Forget();
            }
        }

        /// <summary>Active/désactive les haptics (paramètre utilisateur).</summary>
        public void SetHaptics(bool enabled) => _hapticsEnabled = enabled;

        // --- Setup Input Actions ---
        private void SetupActions()
        {
            if (_actionsAsset == null)
            {
                Debug.LogWarning("[InputManager] Aucun InputActionAsset assigné: actions en lecture seule désactivées.");
                return;
            }
            var gameplay = _actionsAsset.FindActionMap("Gameplay", throwIfNotFound: false);
            if (gameplay == null) { Debug.LogError("[InputManager] ActionMap 'Gameplay' introuvable."); return; }
            _moveAction = gameplay.FindAction("Move", false);
            _lookAction = gameplay.FindAction("Look", false);
            _fireAction = gameplay.FindAction("Fire", false);
            _aimAction = gameplay.FindAction("Aim", false);
            _reloadAction = gameplay.FindAction("Reload", false);
            _jumpAction = gameplay.FindAction("Jump", false);
            _grenadeAction = gameplay.FindAction("Grenade", false);
            _switchAction = gameplay.FindAction("Switch", false);
            _interactAction = gameplay.FindAction("Interact", false);
        }

        private void EnableActions()
        {
            _actionsAsset?.Enable();
        }

        private void DisableActions()
        {
            _actionsAsset?.Disable();
        }

        /// <summary>Rebind une action via le système Unity Input (UI rebind).</summary>
        public void PerformInteractiveRebind(string actionName, int bindingIndex, Action onComplete, Action onCancel)
        {
            if (_actionsAsset == null) return;
            var map = _actionsAsset.FindActionMap("Gameplay");
            var action = map?.FindAction(actionName, false);
            if (action == null) { onCancel?.Invoke(); return; }
            var rebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(_ => { onComplete?.Invoke(); _.Dispose(); })
                .OnCancel(_ => { onCancel?.Invoke(); _.Dispose(); })
                .Start();
        }
    }

    /// <summary>Extensions utilitaires pour InputManager.</summary>
    internal static class InputManagerExtensions
    {
        public static async Cysharp.Threading.Tasks.UniTaskVoid DelayedResetGamepad(this MonoBehaviour mb, int ms)
        {
            await Cysharp.Threading.Tasks.UniTask.Delay(ms, ignoreTimeScale: true);
            Gamepad.current?.SetMotorSpeeds(0f, 0f);
        }
    }
}
