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
    /// Classement global/amis/crew.
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/LeaderboardScreen")]
    [DisallowMultipleComponent]
    public sealed class LeaderboardScreen : UIScreen
    {
        [Header("Tabs")]
        [Tooltip("Boutons de tab (GLOBAL, FRIENDS, CREW).")]
        [SerializeField] private KButton[] _tabButtons;
        [Tooltip("Panneaux.")]
        [SerializeField] private RectTransform[] _tabPanels;

        [Header("Liste")]
        [Tooltip("Conteneur de la liste de classement.")]
        [SerializeField] private RectTransform _listContainer;
        [Tooltip("Prefab d'une ligne de classement.")]
        [SerializeField] private GameObject _entryPrefab;
        [Tooltip("Nombre max de lignes.")]
        [SerializeField] private int _maxEntries = 100;

        [Header("Header")]
        [Tooltip("Texte timer saison.")]
        [SerializeField] private TMP_Text _seasonTimer;
        [Tooltip("Texte rang du joueur courant.")]
        [SerializeField] private TMP_Text _playerRankText;

        [Header("Navigation")]
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;

        private int _currentTab;
        private readonly List<GameObject> _entries = new(64);

        protected override void Awake()
        {
            _screenType = ScreenType.Leaderboard;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int idx = i;
                var label = idx switch { 0 => "GLOBAL", 1 => "FRIENDS", 2 => "CREW", _ => "GLOBAL" };
                _tabButtons[i].SetText(label);
                _tabButtons[i].OnKClick += _ => SelectTab(idx);
            }
            if (_seasonTimer != null)
            {
                _seasonTimer.text = "Season ends in 14d 06h";
                _seasonTimer.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _seasonTimer.color = ThemeManager.SubYellow;
            }
            if (_playerRankText != null)
            {
                _playerRankText.text = "YOUR RANK: #247";
                _playerRankText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _playerRankText.color = ThemeManager.Main;
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }
        }

        protected override void OnShow(object payload)
        {
            SelectTab(0);
            TrackClick("leaderboard_show");
        }

        protected override void OnHide()
        {
            ClearEntries();
        }

        private void SelectTab(int index)
        {
            _currentTab = index;
            for (int i = 0; i < _tabPanels.Length; i++)
            {
                if (_tabPanels[i] != null) _tabPanels[i].gameObject.SetActive(i == index);
            }
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] != null) _tabButtons[i].SetSelected(i == index);
            }
            BuildList();
            TrackClick($"leaderboard_tab_{index}");
        }

        private void BuildList()
        {
            ClearEntries();
            if (_listContainer == null || _entryPrefab == null) return;
            for (int i = 1; i <= 20; i++)
            {
                var entry = Instantiate(_entryPrefab, _listContainer);
                _entries.Add(entry);
                var texts = entry.GetComponentsInChildren<TMP_Text>();
                if (texts.Length >= 4)
                {
                    texts[0].text = $"#{i}";
                    texts[0].font = ThemeManager.Instance.GetFont(FontRole.Display);
                    texts[0].color = i <= 3 ? ThemeManager.SubYellow : ThemeManager.White;
                    texts[1].text = $"Operative-{1000 + i}";
                    texts[1].font = ThemeManager.Instance.GetFont(FontRole.Body);
                    texts[1].color = ThemeManager.White;
                    texts[2].text = $"{50000 - i * 250}";
                    texts[2].font = ThemeManager.Instance.GetFont(FontRole.Mono);
                    texts[2].color = ThemeManager.Main;
                    texts[3].text = $"{82 - i}";
                    texts[3].font = ThemeManager.Instance.GetFont(FontRole.Mono);
                    texts[3].color = ThemeManager.TextMuted;
                }
                // Highlight joueur.
                if (i == 7)
                {
                    var img = entry.GetComponent<Image>();
                    if (img != null) img.color = new Color(ThemeManager.Main.r, ThemeManager.Main.g, ThemeManager.Main.b, 0.2f);
                }
            }
        }

        private void OnBack()
        {
            TrackClick("leaderboard_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        public override bool HandleBack() { OnBack(); return true; }

        private void ClearEntries()
        {
            foreach (var e in _entries) { if (e != null) Destroy(e); }
            _entries.Clear();
        }
    }
}
