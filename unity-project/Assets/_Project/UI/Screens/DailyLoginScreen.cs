using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Récompense de connexion quotidienne.
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/DailyLoginScreen")]
    [DisallowMultipleComponent]
    public sealed class DailyLoginScreen : UIScreen
    {
        [Header("Header")]
        [Tooltip("Texte titre.")]
        [SerializeField] private TMP_Text _titleText;
        [Tooltip("Texte streak counter (e.g. STREAK: 5 days).")]
        [SerializeField] private TMP_Text _streakText;

        [Header("Calendar")]
        [Tooltip("Conteneur des cellules de récompense (7 jours).")]
        [SerializeField] private RectTransform _calendarContainer;
        [Tooltip("Prefab d'une cellule jour.")]
        [SerializeField] private GameObject _dayCellPrefab;
        [Tooltip("Nombre de jours affichés (7).")]
        [SerializeField] private int _totalDays = 7;
        [Tooltip("Jour courant (1..7).")]
        [SerializeField] private int _currentDay = 3;

        [Header("Actions")]
        [Tooltip("Bouton CLAIM.")]
        [SerializeField] private KButton _claimButton;
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;

        private readonly List<GameObject> _cells = new(8);

        protected override void Awake()
        {
            _screenType = ScreenType.DailyLogin;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_titleText != null)
            {
                _titleText.text = "DAILY REWARD";
                _titleText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _titleText.color = ThemeManager.Main;
            }
            if (_streakText != null)
            {
                _streakText.text = $"STREAK: {_currentDay} DAYS";
                _streakText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _streakText.color = ThemeManager.SubYellow;
            }
            if (_claimButton != null)
            {
                _claimButton.SetLocalizationKey("daily.claim", "CLAIM");
                _claimButton.OnKClick += _ => OnClaim();
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }
        }

        protected override void OnShow(object payload)
        {
            BuildCalendar();
            TrackClick("dailylogin_show");
        }

        protected override void OnHide()
        {
            foreach (var c in _cells) { if (c != null) Destroy(c); }
            _cells.Clear();
        }

        private void BuildCalendar()
        {
            if (_calendarContainer == null || _dayCellPrefab == null) return;
            string[] rewards = { "100 CR", "200 CR", "1 GEM", "500 CR", "300 XP", "2 GEMS", "EPIC LOOT" };
            for (int i = 1; i <= _totalDays; i++)
            {
                var cell = Instantiate(_dayCellPrefab, _calendarContainer);
                _cells.Add(cell);
                var texts = cell.GetComponentsInChildren<TMP_Text>();
                if (texts.Length >= 2)
                {
                    texts[0].text = $"DAY {i}";
                    texts[0].font = ThemeManager.Instance.GetFont(FontRole.Display);
                    texts[0].color = i == _currentDay ? ThemeManager.SubYellow : ThemeManager.White;
                    texts[1].text = rewards[(i - 1) % rewards.Length];
                    texts[1].font = ThemeManager.Instance.GetFont(FontRole.Mono);
                    texts[1].color = ThemeManager.Main;
                }
                var img = cell.GetComponentInChildren<Image>();
                if (img != null)
                {
                    if (i < _currentDay) img.color = new Color(0.2f, 0.4f, 0.2f, 1f); // déjà claimed
                    else if (i == _currentDay) img.color = ThemeManager.Main; // aujourd'hui
                    else img.color = ThemeManager.Surface; // futur
                }
            }
        }

        private void OnClaim()
        {
            TrackClick($"dailylogin_claim_{_currentDay}");
            TelemetryLogger.Instance?.Track("daily_claim", new() { { "day", _currentDay } });
            // Avance le compteur.
            _currentDay = Mathf.Min(_totalDays, _currentDay + 1);
            if (_streakText != null) _streakText.text = $"STREAK: {_currentDay} DAYS";
            BuildCalendar();
        }

        private void OnBack()
        {
            TrackClick("dailylogin_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        public override bool HandleBack() { OnBack(); return true; }
    }
}
