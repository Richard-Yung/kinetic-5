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
    /// Sélection de mission — carte/liste 7 missions avec lock states.
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/MissionSelectScreen")]
    [DisallowMultipleComponent]
    public sealed class MissionSelectScreen : UIScreen
    {
        [Header("Header")]
        [Tooltip("Texte titre.")]
        [SerializeField] private TMP_Text _titleText;
        [Tooltip("Texte power score joueur.")]
        [SerializeField] private TMP_Text _playerPowerText;

        [Header("Filtres")]
        [Tooltip("Boutons de filtre par région.")]
        [SerializeField] private KButton[] _regionFilterButtons;
        [Tooltip("Texte difficulté sélectionnée.")]
        [SerializeField] private TMP_Text _difficultyText;
        [Tooltip("Boutons difficulté (EASY/NORMAL/HARD).")]
        [SerializeField] private KButton[] _difficultyButtons;

        [Header("Liste")]
        [Tooltip("Conteneur des cartes de mission.")]
        [SerializeField] private RectTransform _missionsContainer;
        [Tooltip("Prefab de carte mission (KCard).")]
        [SerializeField] private KCard _missionCardPrefab;
        [Tooltip("CardGroup pour sélection exclusive.")]
        [SerializeField] private KCardGroup _cardGroup;

        [Header("Détails mission")]
        [Tooltip("Panneau de détails mission sélectionnée.")]
        [SerializeField] private RectTransform _detailsPanel;
        [Tooltip("Texte nom mission.")]
        [SerializeField] private TMP_Text _missionNameText;
        [Tooltip("Texte description.")]
        [SerializeField] private TMP_Text _missionDescriptionText;
        [Tooltip("Texte power recommandé.")]
        [SerializeField] private TMP_Text _recommendedPowerText;
        [Tooltip("Texte objectifs.")]
        [SerializeField] private TMP_Text _objectivesText;
        [Tooltip("Texte récompenses (XP/CR).")]
        [SerializeField] private TMP_Text _rewardsText;

        [Header("Actions")]
        [Tooltip("Bouton DEPLOY (lance la mission).")]
        [SerializeField] private KButton _deployButton;
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;

        private int _playerPower = 2500;
        private int _currentDifficulty = 1;
        private string _selectedMissionId;
        private readonly List<KCard> _cards = new(16);

        protected override void Awake()
        {
            _screenType = ScreenType.MissionSelect;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_titleText != null)
            {
                _titleText.text = "SELECT MISSION";
                _titleText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _titleText.color = ThemeManager.Main;
            }
            if (_playerPowerText != null)
            {
                _playerPowerText.text = $"POWER {_playerPower}";
                _playerPowerText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _playerPowerText.color = ThemeManager.SubYellow;
            }
            for (int i = 0; i < _difficultyButtons.Length; i++)
            {
                int idx = i;
                var label = idx switch { 0 => "EASY", 1 => "NORMAL", 2 => "HARD", _ => "NORMAL" };
                _difficultyButtons[i].SetText(label);
                _difficultyButtons[i].OnKClick += _ => SetDifficulty(idx);
            }
            SetDifficulty(1);
            if (_deployButton != null)
            {
                _deployButton.SetLocalizationKey("mission.deploy", "DEPLOY");
                _deployButton.OnKClick += _ => OnDeploy();
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }
        }

        protected override void OnShow(object payload)
        {
            BuildMissionCards();
            if (_detailsPanel != null) _detailsPanel.gameObject.SetActive(false);
            TrackClick("missionselect_show");
        }

        protected override void OnHide()
        {
            ClearCards();
        }

        // =================================================================================
        //  LISTE MISSIONS
        // =================================================================================

        private void BuildMissionCards()
        {
            ClearCards();
            if (_missionsContainer == null || _missionCardPrefab == null) return;
            var missions = DataLoader.GetAllMissions();
            foreach (var m in missions)
            {
                var card = Instantiate(_missionCardPrefab, _missionsContainer);
                bool locked = m.RecommendedPower > _playerPower;
                card.Bind(m, locked);
                card.OnCardClicked += OnMissionCardClicked;
                _cards.Add(card);
                _cardGroup?.Register(card);
            }
        }

        private void OnMissionCardClicked(KCard card)
        {
            if (card == null || card.IsLocked) return;
            _selectedMissionId = card.ItemId;
            ShowDetails(card.ItemId);
        }

        private void ShowDetails(string missionId)
        {
            var mission = DataLoader.GetMission(missionId);
            if (mission == null) return;
            if (_detailsPanel != null) _detailsPanel.gameObject.SetActive(true);
            if (_missionNameText != null) { _missionNameText.text = mission.DisplayName; _missionNameText.color = ThemeManager.Main; }
            if (_missionDescriptionText != null) { _missionDescriptionText.text = mission.Description; _missionDescriptionText.color = ThemeManager.White; }
            if (_recommendedPowerText != null) { _recommendedPowerText.text = $"RECOMMENDED POWER: {mission.RecommendedPower}"; _recommendedPowerText.color = mission.RecommendedPower > _playerPower ? ThemeManager.SubRed : ThemeManager.SubGreen; }
            if (_objectivesText != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var o in mission.Objectives) sb.AppendLine($"• {o.Description}");
                _objectivesText.text = sb.ToString();
                _objectivesText.color = ThemeManager.White;
            }
            if (_rewardsText != null)
            {
                _rewardsText.text = $"+{mission.Rewards?.Xp ?? 0} XP\n+{mission.Rewards?.Cr ?? 0} CR";
                _rewardsText.color = ThemeManager.SubYellow;
            }
        }

        private void SetDifficulty(int index)
        {
            _currentDifficulty = index;
            for (int i = 0; i < _difficultyButtons.Length; i++)
            {
                if (_difficultyButtons[i] != null) _difficultyButtons[i].SetSelected(i == index);
            }
            if (_difficultyText != null)
            {
                _difficultyText.text = index switch { 0 => "EASY", 1 => "NORMAL", 2 => "HARD", _ => "NORMAL" };
                _difficultyText.color = index == 2 ? ThemeManager.SubRed : ThemeManager.Main;
            }
            TrackClick($"missionselect_difficulty_{index}");
        }

        // =================================================================================
        //  ACTIONS
        // =================================================================================

        private void OnDeploy()
        {
            if (string.IsNullOrEmpty(_selectedMissionId)) return;
            TrackClick($"missionselect_deploy_{_selectedMissionId}");
            TelemetryLogger.Instance?.TrackMissionStart(_selectedMissionId, "vulcan", _currentDifficulty);
            _ = GameManager.Instance?.StartMissionAsync(_selectedMissionId);
        }

        private void OnBack()
        {
            TrackClick("missionselect_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        public override bool HandleBack() { OnBack(); return true; }

        private void ClearCards()
        {
            foreach (var c in _cards)
            {
                if (c == null) continue;
                _cardGroup?.Unregister(c);
                c.OnCardClicked -= OnMissionCardClicked;
                Destroy(c.gameObject);
            }
            _cards.Clear();
        }
    }
}
