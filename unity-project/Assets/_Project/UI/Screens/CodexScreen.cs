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
    /// Codex / base de données lore.
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/CodexScreen")]
    [DisallowMultipleComponent]
    public sealed class CodexScreen : UIScreen
    {
        [Header("Tabs")]
        [Tooltip("Boutons de tab (AGENTS, ENEMIES, WEAPONS, REGIONS).")]
        [SerializeField] private KButton[] _tabButtons;
        [Tooltip("Panneaux.")]
        [SerializeField] private RectTransform[] _tabPanels;

        [Header("Liste")]
        [Tooltip("Conteneur de la liste des entrées du codex.")]
        [SerializeField] private RectTransform _listContainer;
        [Tooltip("Prefab d'une ligne d'entrée codex.")]
        [SerializeField] private GameObject _entryPrefab;

        [Header("Détails")]
        [Tooltip("Panneau de détails.")]
        [SerializeField] private RectTransform _detailsPanel;
        [Tooltip("Texte titre.")]
        [SerializeField] private TMP_Text _detailsTitle;
        [Tooltip("Texte corps.")]
        [SerializeField] private TMP_Text _detailsBody;

        [Header("Navigation")]
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;
        [Tooltip("Texte compteur unlocked/total.")]
        [SerializeField] private TMP_Text _unlockedCountText;

        private int _currentTab;
        private readonly List<GameObject> _entries = new(32);

        protected override void Awake()
        {
            _screenType = ScreenType.Codex;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int idx = i;
                var label = idx switch { 0 => "AGENTS", 1 => "ENEMIES", 2 => "WEAPONS", 3 => "REGIONS", _ => "AGENTS" };
                _tabButtons[i].SetText(label);
                _tabButtons[i].OnKClick += _ => SelectTab(idx);
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
            TrackClick("codex_show");
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
            TrackClick($"codex_tab_{index}");
        }

        private void BuildList()
        {
            ClearEntries();
            if (_listContainer == null || _entryPrefab == null) return;

            switch (_currentTab)
            {
                case 0: BuildAgents(); break;
                case 1: BuildEnemies(); break;
                case 2: BuildWeapons(); break;
                case 3: BuildRegions(); break;
            }
            UpdateUnlockedCount();
        }

        private void BuildAgents()
        {
            foreach (var a in DataLoader.GetAllAgents()) CreateEntry(a.DisplayName, a.Description);
        }
        private void BuildEnemies()
        {
            foreach (var e in DataLoader.GetAllEnemies()) CreateEntry(e.DisplayName, e.IsBoss ? $"Boss — {e.BaseHealth} HP" : $"Enemy — {e.Class}");
        }
        private void BuildWeapons()
        {
            foreach (var w in DataLoader.GetAllWeapons()) CreateEntry(w.DisplayName, $"{w.Category} • {w.Type}");
        }
        private void BuildRegions()
        {
            foreach (var r in DataLoader.GetAllRegions()) CreateEntry(r.DisplayName, r.Description);
        }

        private void CreateEntry(string title, string subtitle)
        {
            var entry = Instantiate(_entryPrefab, _listContainer);
            _entries.Add(entry);
            var texts = entry.GetComponentsInChildren<TMP_Text>();
            if (texts.Length >= 2)
            {
                texts[0].text = title;
                texts[0].font = ThemeManager.Instance.GetFont(FontRole.Display);
                texts[0].color = ThemeManager.Main;
                texts[1].text = subtitle;
                texts[1].font = ThemeManager.Instance.GetFont(FontRole.Body);
                texts[1].color = ThemeManager.TextMuted;
            }
            var btn = entry.GetComponentInChildren<KButton>();
            if (btn != null)
            {
                string t = title, s = subtitle;
                btn.OnKClick += _ => ShowDetails(t, s);
            }
        }

        private void ShowDetails(string title, string body)
        {
            if (_detailsPanel != null) _detailsPanel.gameObject.SetActive(true);
            if (_detailsTitle != null) { _detailsTitle.text = title; _detailsTitle.color = ThemeManager.Main; }
            if (_detailsBody != null) { _detailsBody.text = body; _detailsBody.color = ThemeManager.White; }
            TrackClick($"codex_detail_{title}");
        }

        private void UpdateUnlockedCount()
        {
            if (_unlockedCountText != null)
            {
                _unlockedCountText.text = $"{_entries.Count} ENTRIES";
                _unlockedCountText.color = ThemeManager.SubGreen;
            }
        }

        private void OnBack()
        {
            TrackClick("codex_back");
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
