using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace KINETICS5.Core
{
    /// <summary>
    /// Gestionnaire de temps global de KINETICS 5.
    /// - Hitstop (freeze frames 3..8) sur gros impacts / parades pour accentuer le "feel".
    /// - Slow-motion (0.3x pendant 0.5s) sur ultimates / parades / cinématiques courtes.
    /// - Transitions douces de TimeScale (pas de jerk pour l'utilisateur).
    /// - TimeScale UI indépendant (toujours 1x) pour que menus restent fluides en pause.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimeManager : MonoBehaviour
    {
        private static TimeManager _instance;
        public static TimeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[TimeManager]");
                    _instance = go.AddComponent<TimeManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Hitstop")]
        [Tooltip("Durée par défaut d'un hitstop en secondes.")]
        [Range(0.02f, 0.2f)][SerializeField] private float _defaultHitstopDuration = 0.06f;
        [Tooltip("TimeScale appliqué pendant un hitstop (0 = gel total).")]
        [Range(0f, 0.5f)][SerializeField] private float _hitstopTimeScale = 0.02f;

        [Header("Slow-motion")]
        [Tooltip("TimeScale en slow-motion (0.3 = 30%).")]
        [Range(0.05f, 0.95f)][SerializeField] private float _slowMotionScale = 0.3f;
        [Tooltip("Durée par défaut du slow-mo en secondes.")]
        [Range(0.1f, 3f)][SerializeField] private float _slowMotionDuration = 0.5f;

        [Header("Transitions")]
        [Tooltip("Vitesse de transition vers un nouveau TimeScale (1 = instantané, 0.1 = doux).")]
        [Range(0.05f, 5f)][SerializeField] private float _timeScaleLerpSpeed = 3f;

        /// <summary>TimeScale gameplay cible (sans compter l'effet de transition).</summary>
        public float TargetGameplayScale { get; private set; } = 1f;
        /// <summary>TimeScale UI (toujours 1, exposé pour les anims UI).</summary>
        public float UIScale => 1f;
        /// <summary>Vrai si un hitstop est en cours.</summary>
        public bool IsHitstopActive { get; private set; }
        /// <summary>Vrai si un slow-mo est en cours.</summary>
        public bool IsSlowMotionActive { get; private set; }
        /// <summary>Vrai si le gameplay est en pause (TimeScale gelé à 0).</summary>
        public bool IsPaused { get; private set; }

        private float _hitstopTimer;
        private float _slowMoTimer;
        private float _slowMoOriginalScale = 1f;

        // File d'attente des hitstops (cas où plusieurs impacts arrivent simultanément).
        private readonly List<(float duration, float scale)> _hitstopQueue = new(4);

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            Time.timeScale = 1f;
            _instance = null;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Décrémente le hitstop.
            if (_hitstopTimer > 0f)
            {
                _hitstopTimer -= dt;
                if (_hitstopTimer <= 0f)
                {
                    IsHitstopActive = false;
                    // Passe au hitstop suivant dans la file.
                    if (_hitstopQueue.Count > 0)
                    {
                        var next = _hitstopQueue[0];
                        _hitstopQueue.RemoveAt(0);
                        StartHitstopInternal(next.duration, next.scale);
                    }
                }
            }

            // Décrémente le slow-mo.
            if (_slowMoTimer > 0f)
            {
                _slowMoTimer -= dt;
                if (_slowMoTimer <= 0f)
                {
                    IsSlowMotionActive = false;
                    TargetGameplayScale = _slowMoOriginalScale;
                }
            }

            // Calcule la cible réelle: priorité pause > hitstop > slow-mo > normal.
            float target = TargetGameplayScale;
            if (IsPaused) target = 0f;
            else if (IsHitstopActive) target = _hitstopTimeScale;
            else if (IsSlowMotionActive) target = _slowMotionScale;

            // Lerp doux pour éviter les saccades perceptuelles.
            Time.timeScale = Mathf.Lerp(Time.timeScale, target, dt * _timeScaleLerpSpeed);
            if (Mathf.Abs(Time.timeScale - target) < 0.005f) Time.timeScale = target;
        }

        // --- API publique ---

        /// <summary>
        /// Déclenche un hitstop (freeze frames). Si un hitstop est déjà en cours,
        /// met en file d'attente (max 2, plus = ignoré).
        /// </summary>
        /// <param name="duration">Durée en secondes (0.04 à 0.15 typique).</param>
        /// <param name="scale">TimeScale pendant le hitstop (0..0.5).</param>
        public void TriggerHitstop(float duration = -1f, float scale = -1f)
        {
            float d = duration < 0f ? _defaultHitstopDuration : duration;
            float s = scale < 0f ? _hitstopTimeScale : scale;
            if (IsHitstopActive)
            {
                if (_hitstopQueue.Count < 2) _hitstopQueue.Add((d, s));
                return;
            }
            StartHitstopInternal(d, s);
        }

        /// <summary>
        /// Déclenche un slow-motion. Si déjà actif, prolonge de la durée spécifiée.
        /// </summary>
        public void TriggerSlowMotion(float duration = -1f, float scale = -1f)
        {
            float d = duration < 0f ? _slowMotionDuration : duration;
            if (scale > 0f) _slowMotionScale = scale;
            if (!IsSlowMotionActive)
            {
                _slowMotionOriginalScale = TargetGameplayScale;
                IsSlowMotionActive = true;
            }
            _slowMoTimer = Mathf.Max(_slowMoTimer, d);
        }

        /// <summary>Met en pause le gameplay (TimeScale -> 0, UI reste 1x).</summary>
        public void SetGameplayPaused(bool paused)
        {
            IsPaused = paused;
            if (paused) TargetGameplayScale = 0f;
            else TargetGameplayScale = 1f;
            Debug.Log($"[TimeManager] Pause={paused}.");
        }

        /// <summary>Définit un TimeScale de gameplay personnalisé (override).</summary>
        public void SetGameplayScale(float scale)
        {
            TargetGameplayScale = Mathf.Clamp01(scale);
        }

        /// <summary>Annule tous les effets (hitstop + slow-mo) immédiatement.</summary>
        public void CancelAllEffects()
        {
            _hitstopTimer = 0f;
            _slowMoTimer = 0f;
            IsHitstopActive = false;
            IsSlowMotionActive = false;
            _hitstopQueue.Clear();
            if (!IsPaused) TargetGameplayScale = 1f;
        }

        private void StartHitstopInternal(float duration, float scale)
        {
            _hitstopTimer = duration;
            _hitstopTimeScale = scale;
            IsHitstopActive = true;
        }
    }
}
