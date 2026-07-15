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
    /// Battle Pass — 50 paliers, free + premium tracks.
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/BattlePassScreen")]
    [DisallowMultipleComponent]
    public sealed class BattlePassScreen : UIScreen
    {
        [Header("Header")]
        [Tooltip("Texte titre saison (SEASON 1).")]
        [SerializeField] private TMP_Text _seasonTitle;
        [Tooltip("Texte timer saison.")]
        [SerializeField] private TMP_Text _seasonTimer;

        [Header("Tracks")]
        [Tooltip("Conteneur de la track FREE.")]
        [SerializeField] private RectTransform _freeTrackContainer;
        [Tooltip("Conteneur de la track PREMIUM.")]
        [SerializeField] private RectTransform _premiumTrackContainer;
        [Tooltip("Prefab d'une cellule de tier (reward).")]
        [SerializeField] private GameObject _tierCellPrefab;
        [Tooltip("Nombre total de paliers (50).")]
        [SerializeField] private int _totalTiers = 50;
        [Tooltip("Tier courant atteint.")]
        [SerializeField] private int _currentTier = 12;

        [Header("Progression")]
        [Tooltip("Barre de progression de la saison (XP).")]
        [SerializeField] private KProgressBar _seasonProgressBar;
        [Tooltip("Texte tier courant.")]
        [SerializeField] private TMP_Text _tierText;

        [Header("Challenges")]
        [Tooltip("Conteneur des challenges quotidiens.")]
        [SerializeField] private RectTransform _dailyChallengesContainer;
        [Tooltip("Conteneur des challenges hebdomadaires.")]
        [SerializeField] private RectTransform _weeklyChallengesContainer;
        [Tooltip("Prefab d'une ligne challenge.")]
        [SerializeField] private GameObject _challengeRowPrefab;

        [Header("Actions")]
        [Tooltip("Bouton UPGRADE TO PREMIUM.")]
        [SerializeField] private KButton _upgradeButton;
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;

        private readonly List<GameObject> _freeCells = new(64);
        private readonly List<GameObject> _premiumCells = new(64);
        private readonly List<GameObject> _challenges = new(16);

        protected override void Awake()
        {
            _screenType = ScreenType.BattlePass;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_seasonTitle != null)
            {
                _seasonTitle.text = "SEASON 1 — KINETIC DAWN";
                _seasonTitle.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _seasonTitle.color = ThemeManager.Main;
            }
            if (_seasonTimer != null)
            {
                _seasonTimer.text = "14d 06h left";
                _seasonTimer.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _seasonTimer.color = ThemeManager.SubYellow;
            }
            if (_upgradeButton != null)
            {
                _upgradeButton.SetLocalizationKey("battlepass.upgrade", "UPGRADE TO PREMIUM");
                _upgradeButton.OnKClick += _ => OnUpgrade();
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }
        }

        protected override void OnShow(object payload)
        {
            BuildTracks();
            BuildChallenges();
            RefreshProgression();
            TrackClick("battlepass_show");
        }

        protected override void OnHide()
        {
            ClearAll();
        }

        // =================================================================================
        //  TRACKS
        // =================================================================================

        private void BuildTracks()
        {
            ClearAll();
            for (int i = 1; i <= _totalTiers; i++)
            {
                var free = Instantiate(_tierCellPrefab, _freeTrackContainer);
                ConfigureTierCell(free, i, false);
                _freeCells.Add(free);

                var prem = Instantiate(_tierCellPrefab, _premiumTrackContainer);
                ConfigureTierCell(prem, i, true);
                _premiumCells.Add(prem);
            }
        }

        private void ConfigureTierCell(GameObject cell, int tier, bool premium)
        {
            var texts = cell.GetComponentsInChildren<TMP_Text>();
            if (texts.Length > 0)
            {
                texts[0].text = tier.ToString();
                texts[0].font = ThemeManager.Instance.GetFont(FontRole.Display);
                texts[0].color = tier <= _currentTier ? ThemeManager.SubGreen : ThemeManager.TextMuted;
            }
            var img = cell.GetComponentInChildren<Image>();
            if (img != null)
            {
                img.color = premium ? ThemeManager.DarkBlue : ThemeManager.Surface;
                if (tier > _currentTier) img.color *= 0.5f;
            }
            var btn = cell.GetComponentInChildren<KButton>();
            if (btn != null)
            {
                int t = tier; bool p = premium;
                btn.OnKClick += _ => OnTierClicked(t, p);
                btn.SetLocked(tier > _currentTier);
            }
        }

        private void OnTierClicked(int tier, bool premium)
        {
            TrackClick($"battlepass_tier_{tier}_{(premium ? "premium" : "free")}");
            // Extension future : claim reward via backend (Nakama).
        }

        private void BuildChallenges()
        {
            BuildChallenge(_dailyChallengesContainer, "Daily", "Eliminate 20 enemies", 12, 20);
            BuildChallenge(_dailyChallengesContainer, "Daily", "Complete 2 missions", 1, 2);
            BuildChallenge(_weeklyChallengesContainer, "Weekly", "Earn 10,000 XP", 4500, 10000);
            BuildChallenge(_weeklyChallengesContainer, "Weekly", "Win 5 matches", 3, 5);
        }

        private void BuildChallenge(RectTransform container, string type, string label, int current, int target)
        {
            if (container == null || _challengeRowPrefab == null) return;
            var row = Instantiate(_challengeRowPrefab, container);
            _challenges.Add(row);
            var texts = row.GetComponentsInChildren<TMP_Text>();
            if (texts.Length >= 2)
            {
                texts[0].text = label;
                texts[0].font = ThemeManager.Instance.GetFont(FontRole.Body);
                texts[0].color = ThemeManager.White;
                texts[1].text = $"{current}/{target}";
                texts[1].font = ThemeManager.Instance.GetFont(FontRole.Mono);
                texts[1].color = ThemeManager.SubYellow;
            }
            var bar = row.GetComponentInChildren<KProgressBar>();
            if (bar != null)
            {
                bar.SetType(StatBarType.Xp);
                bar.SetRange(0f, target);
                bar.Value = current;
            }
        }

        private void RefreshProgression()
        {
            if (_seasonProgressBar != null)
            {
                _seasonProgressBar.SetType(StatBarType.Xp);
                _seasonProgressBar.SetRange(0f, _totalTiers);
                _seasonProgressBar.Value = _currentTier;
            }
            if (_tierText != null)
            {
                _tierText.text = $"TIER {_currentTier}/{_totalTiers}";
                _tierText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _tierText.color = ThemeManager.Main;
            }
        }

        private void OnUpgrade()
        {
            TrackClick("battlepass_upgrade");
            TelemetryLogger.Instance?.TrackPurchase("battlepass_premium", 999, "GEMS");
        }

        private void OnBack()
        {
            TrackClick("battlepass_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        public override bool HandleBack() { OnBack(); return true; }

        private void ClearAll()
        {
            foreach (var c in _freeCells) { if (c != null) Destroy(c); }
            foreach (var c in _premiumCells) { if (c != null) Destroy(c); }
            foreach (var c in _challenges) { if (c != null) Destroy(c); }
            _freeCells.Clear(); _premiumCells.Clear(); _challenges.Clear();
        }
    }
}
