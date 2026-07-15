using System;
using UnityEngine;
using Cinemachine;
using Cysharp.Threading.Tasks;

namespace KINETICS5.Core
{
    /// <summary>
    /// Gestionnaire de caméra FPS première personne pour KINETICS 5.
    /// - Caméra virtuelle Cinemachine (POV) pour le regard.
    /// - Camera shake (bruit de Perlin) paramétrable par amplitude/fréquence/durée.
    /// - Recoil kick à chaque tir (rotation instantanée + récupération amortie).
    /// - Zoom d'aim (FOV dynamique).
    /// - Head bob en mouvement (sinusoïdes couplées).
    /// - Look smooth via damping Cinemachine.
    /// - Look tactile mobile via delta InputManager.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraManager : MonoBehaviour
    {
        private static CameraManager _instance;
        /// <summary>Instance globale.</summary>
        public static CameraManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[CameraManager]");
                    var cam = go.AddComponent<Camera>();
                    _instance = go.AddComponent<CameraManager>();
                }
                return _instance;
            }
        }

        [Header("Cinemachine")]
        [Tooltip("Caméra virtuelle POV liée au joueur.")]
        [SerializeField] private CinemachineVirtualCamera _fpsVirtualCamera;
        [Tooltip("Brain Camemain reliant la virtual camera.")]
        [SerializeField] private CinemachineBrain _cinemachineBrain;

        [Header("Look")]
        [Tooltip("Sensibilité X (yaw) en degrés par unité d'input.")]
        [Range(0.1f, 5f)][SerializeField] private float _yawSensitivity = 1.5f;
        [Tooltip("Sensibilité Y (pitch) en degrés par unité d'input.")]
        [Range(0.1f, 5f)][SerializeField] private float _pitchSensitivity = 1.5f;
        [Tooltip("Angle minimal de pitch (vers le bas).")]
        [SerializeField] private float _pitchMin = -85f;
        [Tooltip("Angle maximal de pitch (vers le haut).")]
        [SerializeField] private float _pitchMax = 85f;

        [Header("Recoil Kick")]
        [Tooltip("Recul vertical appliqué par tir (degrés).")]
        [Range(0f, 5f)][SerializeField] private float _recoilKick = 1.2f;
        [Tooltip("Recul horizontal aléatoire par tir (degrés).")]
        [Range(0f, 3f)][SerializeField] private float _recoilKickHorizontal = 0.4f;
        [Tooltip("Vitesse de récupération du recul (degrés/sec).")]
        [Range(1f, 30f)][SerializeField] private float _recoilRecovery = 8f;

        [Header("Shake (Perlin)")]
        [Tooltip("Default gain du bruit de Perlin (idle).")]
        [Range(0f, 1f)][SerializeField] private float _idleShakeGain = 0.15f;

        [Header("Aim Zoom")]
        [Tooltip("FOV de base (degrés).")]
        [Range(60f, 90f)][SerializeField] private float _baseFov = 75f;
        [Tooltip("FOV en visée (degrés).")]
        [Range(30f, 70f)][SerializeField] private float _aimFov = 50f;
        [Tooltip("Vitesse de transition FOV.")]
        [Range(1f, 30f)][SerializeField] private float _fovLerpSpeed = 12f;

        [Header("Head Bob")]
        [Tooltip("Amplitude verticale du bob (mètres).")]
        [Range(0f, 0.05f)][SerializeField] private float _bobAmplitude = 0.018f;
        [Tooltip("Fréquence du bob (Hz à vitesse max).")]
        [Range(0.5f, 4f)][SerializeField] private float _bobFrequency = 1.6f;
        [Tooltip("Seuil de vitesse de déplacement déclenchant le bob (m/s).")]
        [SerializeField] private float _bobSpeedThreshold = 0.5f;

        // --- Etat interne ---
        private CinemachinePOV _pov;
        private CinemachineBasicMultiChannelPerlin _perlin;
        private float _currentFov;
        private float _recoilYaw, _recoilPitch;
        private float _bobPhase;
        private Vector3 _camLocalRest;
        private Transform _followTarget;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            if (_cinemachineBrain == null) _cinemachineBrain = GetComponent<CinemachineBrain>() ?? gameObject.AddComponent<CinemachineBrain>();
            if (_fpsVirtualCamera != null)
            {
                _pov = _fpsVirtualCamera.GetCinemachineComponent<CinemachinePOV>();
                _perlin = _fpsVirtualCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
                if (_pov == null) _pov = _fpsVirtualCamera.AddCinemachineComponent<CinemachinePOV>();
                if (_perlin == null) _perlin = _fpsVirtualCamera.AddCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
                _pov.m_HorizontalAxis.m_InputAxisName = string.Empty;
                _pov.m_VerticalAxis.m_InputAxisName = string.Empty;
                _pov.m_HorizontalAxis.m_MaxValue = 360f;
                _pov.m_HorizontalAxis.m_MinValue = -360f;
                _pov.m_HorizontalAxis.m_Wrap = true;
                _pov.m_VerticalAxis.m_MaxValue = _pitchMax;
                _pov.m_VerticalAxis.m_MinValue = _pitchMin;
                _pov.m_VerticalAxis.m_Wrap = false;
            }
            _currentFov = _baseFov;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void LateUpdate()
        {
            if (_pov == null) return;
            var input = InputManager.Instance?.CurrentState ?? default;

            // 1) Look input (delta tactile ou souris) -> axes POV.
            _pov.m_HorizontalAxis.m_InputAxisValue = input.LookDelta.x * _yawSensitivity;
            _pov.m_VerticalAxis.m_InputAxisValue = -input.LookDelta.y * _pitchSensitivity;

            // 2) Recoil: applique un kick et récupère en smooth.
            if (Mathf.Abs(_recoilPitch) > 0.001f || Mathf.Abs(_recoilYaw) > 0.001f)
            {
                _pov.m_VerticalAxis.Value -= _recoilPitch * Time.unscaledDeltaTime * _recoilRecovery;
                _pov.m_HorizontalAxis.Value += _recoilYaw * Time.unscaledDeltaTime * _recoilRecovery;
                _recoilPitch = Mathf.Lerp(_recoilPitch, 0f, Time.unscaledDeltaTime * _recoilRecovery);
                _recoilYaw = Mathf.Lerp(_recoilYaw, 0f, Time.unscaledDeltaTime * _recoilRecovery);
            }

            // 3) Aim zoom: FOV dynamique.
            float targetFov = input.AimHeld ? _aimFov : _baseFov;
            _currentFov = Mathf.Lerp(_currentFov, targetFov, Time.unscaledDeltaTime * _fovLerpSpeed);
            if (_fpsVirtualCamera != null) _fpsVirtualCamera.m_Lens.FieldOfView = _currentFov;

            // 4) Head bob: sinusoïde couplée à la vitesse de déplacement.
            var move = input.Move;
            float speedMag = move.magnitude; // 0..1 normalisé (joystick), suffisant pour le bob
            if (speedMag > 0.05f)
            {
                _bobPhase += Time.unscaledDeltaTime * _bobFrequency * (0.5f + speedMag);
                float bobY = Mathf.Sin(_bobPhase * 2f) * _bobAmplitude * speedMag;
                float bobX = Mathf.Cos(_bobPhase) * _bobAmplitude * 0.5f * speedMag;
                if (_followTarget != null)
                {
                    var lp = _followTarget.localPosition;
                    _followTarget.localPosition = new Vector3(_camLocalRest.x + bobX, _camLocalRest.y + bobY, _camLocalRest.z);
                }
            }
            else if (_followTarget != null)
            {
                _followTarget.localPosition = Vector3.Lerp(_followTarget.localPosition, _camLocalRest, Time.unscaledDeltaTime * 8f);
            }

            // 5) Idle shake gain (légèrement présent même sans action).
            if (_perlin != null && _perlin.m_AmplitudeGain <= 0.001f) _perlin.m_AmplitudeGain = _idleShakeGain;
        }

        /// <summary>Définit la cible suivie par la caméra FPS (root du joueur).</summary>
        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
            if (_fpsVirtualCamera != null) _fpsVirtualCamera.Follow = target;
            if (target != null) _camLocalRest = target.localPosition;
        }

        /// <summary>Ajoute un recul (kick) à la caméra sur le prochain tir.</summary>
        public void AddRecoilKick(float multiplier = 1f)
        {
            _recoilPitch += _recoilKick * multiplier;
            _recoilYaw += UnityEngine.Random.Range(-_recoilKickHorizontal, _recoilKickHorizontal) * multiplier;
        }

        /// <summary>
        /// Déclenche un camera shake ponctuel (explosion, gros dégât, ultime).
        /// </summary>
        /// <param name="amplitude">0..10 typique.</param>
        /// <param name="frequency">Hz du bruit de Perlin.</param>
        /// <param name="duration">Durée en secondes.</param>
        public void Shake(float amplitude, float frequency, float duration)
        {
            if (_perlin == null) return;
            _perlin.m_AmplitudeGain = amplitude;
            _perlin.m_FrequencyGain = frequency;
            this.DelayedShakeReset(duration).Forget();
        }

        private async Cysharp.Threading.Tasks.UniTaskVoid DelayedShakeReset(float seconds)
        {
            await Cysharp.Threading.Tasks.UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true);
            if (_perlin != null) _perlin.m_AmplitudeGain = _idleShakeGain;
        }

        /// <summary>Bascule entre FOV base et FOV aim (override manuel, ex: sniper).</summary>
        public void OverrideFov(float fov, float duration = 0f)
        {
            _currentFov = fov;
        }

        /// <summary>Remet la caméra en orientation zéro (utile après respawn).</summary>
        public void ResetLook()
        {
            if (_pov == null) return;
            _pov.m_HorizontalAxis.Value = 0f;
            _pov.m_VerticalAxis.Value = 0f;
            _recoilPitch = 0f; _recoilYaw = 0f;
        }
    }
}
