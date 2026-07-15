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
    /// Succès / Achievements.
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/AchievementsScreen")]
    [DisallowMultipleComponent]
    public sealed class AchievementsScreen : UIScreen
    {
        [Header("Tabs")]
        [Tooltip("Boutons de catégorie (ALL, COMBAT, EXPLORATION, COLLECTION, SOCIAL).")]
        [SerializeField] private KButton[] _categoryButtons;

        [Header("Liste")]
        [Tooltip("Conteneur des achievements.")]
        [SerializeField] private RectTransform _listContainer;
        [Tooltip("Prefab d'une ligne achievement.")]
        [SerializeField] private GameObject _achievementRowPrefab;

        [Header("Navigation")]
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;
        [Tooltip("Texte compteur unlocked/total.")]
        [SerializeField] private TMP_Text _unlockedCountText;

        private int _currentCategory;
        private readonly List<GameObject> _rows = new(64);

        /// <summary>Achievement entry.</summary>
        [System.Serializable]
        public struct Achievement
        {
            public string Id;
            public string DisplayName;
            [TextArea] public string Description;
            public Rarity Tier;
            public int Current;
            public int Target;
            public bool Unlocked;
        }

        [Header("Données")]
        [Tooltip("Achievements (data-driven via JSON en production).")]
        [SerializeField] private Achievement[] _achievements;

        protected override void Awake()
        {
            _screenType = ScreenType.Achievements;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            for (int i = 0; i < _categoryButtons.Length; i++)
            {
                int idx = i;
                var label = idx switch { 0 => "ALL", 1 => "COMBAT", 2 => "EXPLORATION", 3 => "COLLECTION", 4 => "SOCIAL", _ => "ALL" };
                _categoryButtons[i].SetText(label);
                _categoryButtons[i].OnKClick += _ => SetCategory(idx);
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }
            if (_achievements == null || _achievements.Length == 0) _achievements = BuildDefaultAchievements();
        }

        protected override void OnShow(object payload)
        {
            BuildList();
            TrackClick("achievements_show");
        }

        protected override void OnHide()
        {
            foreach (var r in _rows) { if (r != null) Destroy(r); }
            _rows.Clear();
        }

        private void SetCategory(int index)
        {
            _currentCategory = index;
            for (int i = 0; i < _categoryButtons.Length; i++)
            {
                if (_categoryButtons[i] != null) _categoryButtons[i].SetSelected(i == index);
            }
            BuildList();
            TrackClick($"achievements_category_{index}");
        }

        private void BuildList()
        {
            foreach (var r in _rows) { if (r != null) Destroy(r); }
            _rows.Clear();
            if (_listContainer == null || _achievementRowPrefab == null) return;
            int unlocked = 0;
            foreach (var a in _achievements)
            {
                var row = Instantiate(_achievementRowPrefab, _listContainer);
                _rows.Add(row);
                var texts = row.GetComponentsInChildren<TMP_Text>();
                if (texts.Length >= 3)
                {
                    texts[0].text = a.DisplayName;
                    texts[0].font = ThemeManager.Instance.GetFont(FontRole.Display);
                    texts[0].color = a.Unlocked ? ThemeManager.SubYellow : ThemeManager.White;
                    texts[1].text = a.Description;
                    texts[1].font = ThemeManager.Instance.GetFont(FontRole.Body);
                    texts[1].color = ThemeManager.TextMuted;
                    texts[2].text = a.Unlocked ? "DONE" : $"{a.Current}/{a.Target}";
                    texts[2].font = ThemeManager.Instance.GetFont(FontRole.Mono);
                    texts[2].color = a.Unlocked ? ThemeManager.SubGreen : ThemeManager.Main;
                }
                var img = row.GetComponentInChildren<Image>();
                if (img != null) img.color = ThemeManager.ColorForRarity(a.Tier);
                if (a.Unlocked) unlocked++;
            }
            if (_unlockedCountText != null)
            {
                _unlockedCountText.text = $"{unlocked}/{_achievements.Length} UNLOCKED";
                _unlockedCountText.color = ThemeManager.SubYellow;
            }
        }

        private void OnBack()
        {
            TrackClick("achievements_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        public override bool HandleBack() { OnBack(); return true; }

        private Achievement[] BuildDefaultAchievements()
        {
            return new Achievement[]
            {
                new() { Id = "first_blood", DisplayName = "FIRST BLOOD", Description = "Eliminate your first enemy.", Tier = Rarity.Common, Current = 1, Target = 1, Unlocked = true },
                new() { Id = "marksman", DisplayName = "MARKSMAN", Description = "Get 100 headshots.", Tier = Rarity.Rare, Current = 47, Target = 100, Unlocked = false },
                new() { Id = "mission_master", DisplayName = "MISSION MASTER", Description = "Complete all 7 missions on HARD.", Tier = Rarity.Epic, Current = 3, Target = 7, Unlocked = false },
                new() { Id = "armory", DisplayName = "ARSENAL", Description = "Unlock all weapons.", Tier = Rarity.Legendary, Current = 8, Target = 14, Unlocked = false },
                new() { Id = "no_damage", DisplayName = "FLAWLESS", Description = "Complete a mission without taking damage.", Tier = Rarity.Epic, Current = 0, Target = 1, Unlocked = false },
            };
        }
    }
}
