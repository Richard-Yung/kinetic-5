using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;
using KINETICS5.Data;

namespace KINETICS5.UI
{
    /// <summary>
    /// Écran de chargement de mission — PDF page 2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spécifications PDF page 2</b> :
    /// <list type="bullet">
    /// <item>Tip text rotatif ("RARE LOOT IS OFTEN HIDDEN IN HIGH-RISK HIGH ZONES - BE PREPARED BEFORE ENTERING").</item>
    /// <item>Barre de progression 0-100% (segmentée).</item>
    /// <item>Carte de prévisualisation de mission.</item>
    /// <item>Messages de log de chargement.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/Screens/LoadingScreen")]
    [DisallowMultipleComponent]
    public sealed class LoadingScreen : UIScreen
    {
        [Header("Logo / Titre")]
        [SerializeField] private TMP_Text _missionTitle;
        [SerializeField] private TMP_Text _missionType;

        [Header("Aperçu mission")]
        [Tooltip("Image de preview de la mission.")]
        [SerializeField] private Image _missionPreview;
        [Tooltip("Carte de mission (KCard).")]
        [SerializeField] private KCard _missionCard;
        [Tooltip("Texte descriptif court de la mission.")]
        [SerializeField] private TMP_Text _missionDescription;

        [Header("Progression")]
        [Tooltip("Barre de progression segmentée 0-100%.")]
        [SerializeField] private KProgressBar _progressBar;
        [Tooltip("Texte Audiowide pourcentage (55%).")]
        [SerializeField] private TMP_Text _progressText;

        [Header("Tips")]
        [Tooltip("Texte TMP du tip rotatif.")]
        [SerializeField] private TMP_Text _tipText;
        [Tooltip("Liste de clés de tips de localisation.")]
        [SerializeField] private string[] _tipKeys =
        {
            "loading.tip.loot",
            "loading.tip.stealth",
            "loading.tip.ammo",
            "loading.tip.ultimate"
        };
        [Tooltip("Texte fallback si clé introuvable.")]
        [SerializeField] private string[] _tipFallbacks =
        {
            "RARE LOOT IS OFTEN HIDDEN IN HIGH-RISK HIGH ZONES - BE PREPARED BEFORE ENTERING",
            "STEALTH APPROACHES AVOID ENEMY REINFORCEMENTS - BUT NOT BOSS PHASES",
            "ALWAYS KEEP RESERVE AMMO FOR SECONDARY ENGAGEMENTS",
            "ULTIMATE CHARGE BUILDS FASTER ON HEADSHOTS"
        };
        [Tooltip("Durée d'affichage d'un tip (secondes).")]
        [SerializeField] private float _tipDuration = 5f;

        [Header("Logs")]
        [Tooltip("Conteneur des messages de log (scroll vertical).")]
        [SerializeField] private RectTransform _logContainer;
        [Tooltip("Prefab d'une ligne de log.")]
        [SerializeField] private GameObject _logEntryPrefab;
        [Tooltip("Nombre max de lignes affichées.")]
        [SerializeField] private int _maxLogEntries = 8;

        [Header("Progression simulée")]
        [Tooltip("Vrai si la barre progresse automatiquement (démo).")]
        [SerializeField] private bool _simulateProgress = true;
        [Tooltip("Durée totale simulée (secondes).")]
        [SerializeField] private float _simulatedDuration = 6f;
        [Tooltip("Messages de log émis à des paliers de progression (0..1).")]
        [SerializeField] private LoadingLogEntry[] _logMilestones;

        /// <summary>Entrée de log associée à un palier de progression.</summary>
        [System.Serializable]
        public struct LoadingLogEntry
        {
            [Range(0f, 1f)] public float Threshold;
            [TextArea] public string Message;
        }

        private float _simulatedStartTime;
        private float _currentProgress;
        private int _currentTipIndex;
        private float _nextTipSwap;
        private readonly List<GameObject> _logEntries = new(16);
        private readonly HashSet<float> _emittedThresholds = new();
        private string _pendingMissionId;

        protected override void Awake()
        {
            _screenType = ScreenType.Loading;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_progressBar != null)
            {
                _progressBar.SetType(StatBarType.Power);
                _progressBar.SetRange(0f, 1f);
                _progressBar.Value = 0f;
            }
            if (_tipText != null)
            {
                _tipText.font = ThemeManager.Instance.GetFont(FontRole.Body);
                _tipText.color = ThemeManager.White;
            }
        }

        protected override void OnShow(object payload)
        {
            _pendingMissionId = payload as string;
            _simulatedStartTime = Time.unscaledTime;
            _currentProgress = 0f;
            _emittedThresholds.Clear();
            _currentTipIndex = 0;
            _nextTipSwap = 0f;
            RefreshMissionPreview(_pendingMissionId);
            ShowTip(0);
            TrackClick("loading_show");
        }

        protected override void OnHide()
        {
            // Réinitialise pour prochaine ouverture.
            if (_progressBar != null) _progressBar.Value = 0f;
            if (_progressText != null) _progressText.text = "0%";
        }

        private void Update()
        {
            if (!IsVisible) return;

            // Rotation des tips.
            if (Time.unscaledTime > _nextTipSwap)
            {
                _currentTipIndex = (_currentTipIndex + 1) % Mathf.Max(1, _tipKeys.Length);
                ShowTip(_currentTipIndex);
                _nextTipSwap = Time.unscaledTime + _tipDuration;
            }

            // Progression simulée.
            if (_simulateProgress)
            {
                var t = Mathf.Clamp01((Time.unscaledTime - _simulatedStartTime) / _simulatedDuration);
                SetProgress(t);
                if (t >= 1f)
                {
                    // Fin du chargement : transition vers HUD (via GameManager).
                    _simulateProgress = false;
                    AddLogEntry("Loading complete.");
                    GameManager.Instance?.OnMissionLoaded();
                }
            }

            // Émission des logs milestones.
            EmitMilestoneLogs();
        }

        // =================================================================================
        //  API PUBLIQUE
        // =================================================================================

        /// <summary>Définit la progression (0..1) et met à jour l'UI.</summary>
        public void SetProgress(float normalized)
        {
            _currentProgress = Mathf.Clamp01(normalized);
            if (_progressBar != null) _progressBar.Value = _currentProgress;
            if (_progressText != null)
            {
                _progressText.text = $"{Mathf.RoundToInt(_currentProgress * 100f)}%";
                _progressText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _progressText.color = ThemeManager.Main;
            }
        }

        /// <summary>Ajoute une ligne de log au chargement.</summary>
        public void AddLogEntry(string message)
        {
            if (_logContainer == null || _logEntryPrefab == null) return;
            var entry = Instantiate(_logEntryPrefab, _logContainer);
            var txt = entry.GetComponentInChildren<TMP_Text>();
            if (txt != null)
            {
                txt.text = $"> {message}";
                txt.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                txt.color = ThemeManager.SubGreen;
            }
            _logEntries.Add(entry);

            // Fade-in.
            var cg = entry.GetComponent<CanvasGroup>();
            if (cg == null) cg = entry.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.DOFade(1f, 0.2f).SetUpdate(true);

            // Limite le nombre de lignes.
            while (_logEntries.Count > _maxLogEntries)
            {
                var oldest = _logEntries[0];
                _logEntries.RemoveAt(0);
                if (oldest != null) Destroy(oldest);
            }
        }

        // =================================================================================
        //  IMPLÉMENTATION INTERNE
        // =================================================================================

        private void RefreshMissionPreview(string missionId)
        {
            var mission = string.IsNullOrEmpty(missionId) ? null : DataLoader.GetMission(missionId);
            if (mission == null)
            {
                // Fallback : prend la première mission.
                var all = DataLoader.GetAllMissions();
                if (all.Count > 0) mission = all[0];
            }
            if (mission == null) return;

            if (_missionTitle != null)
            {
                _missionTitle.text = mission.DisplayName;
                _missionTitle.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _missionTitle.color = ThemeManager.White;
            }
            if (_missionType != null)
            {
                _missionType.text = mission.Type.ToString().ToUpperInvariant();
                _missionType.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _missionType.color = ThemeManager.Main;
            }
            if (_missionDescription != null)
            {
                _missionDescription.text = mission.Description;
                _missionDescription.font = ThemeManager.Instance.GetFont(FontRole.Body);
                _missionDescription.color = ThemeManager.TextMuted;
            }
            if (_missionCard != null) _missionCard.Bind(mission);
        }

        private void ShowTip(int index)
        {
            if (_tipText == null) return;
            if (_tipKeys == null || _tipKeys.Length == 0) return;
            index = Mathf.Clamp(index, 0, _tipKeys.Length - 1);
            var key = _tipKeys[index];
            var fallback = (_tipFallbacks != null && index < _tipFallbacks.Length)
                ? _tipFallbacks[index]
                : string.Empty;
            _tipText.text = L(key, fallback);
            // Animation fade-in.
            _tipText.DOKill();
            _tipText.alpha = 0f;
            _tipText.DOFade(1f, 0.4f).SetUpdate(true);
        }

        private void EmitMilestoneLogs()
        {
            if (_logMilestones == null) return;
            foreach (var entry in _logMilestones)
            {
                if (_emittedThresholds.Contains(entry.Threshold)) continue;
                if (_currentProgress >= entry.Threshold)
                {
                    _emittedThresholds.Add(entry.Threshold);
                    AddLogEntry(entry.Message);
                }
            }
        }
    }
}
