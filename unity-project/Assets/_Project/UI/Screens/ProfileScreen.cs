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
    /// Profil joueur — stats, mastery agents, achievements showcase.
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/ProfileScreen")]
    [DisallowMultipleComponent]
    public sealed class ProfileScreen : UIScreen
    {
        [Header("Identité")]
        [Tooltip("Image avatar.")]
        [SerializeField] private Image _avatar;
        [Tooltip("Texte nom du joueur.")]
        [SerializeField] private TMP_Text _playerName;
        [Tooltip("Texte niveau.")]
        [SerializeField] private TMP_Text _levelText;
        [Tooltip("Barre XP du joueur.")]
        [SerializeField] private KProgressBar _xpBar;
        [Tooltip("Texte power score total.")]
        [SerializeField] private TMP_Text _powerScoreText;

        [Header("Stats")]
        [Tooltip("Texte temps de jeu.")]
        [SerializeField] private TMP_Text _playtimeText;
        [Tooltip("Texte kills totaux.")]
        [SerializeField] private TMP_Text _killsText;
        [Tooltip("Texte missions complétées.")]
        [SerializeField] private TMP_Text _missionsText;
        [Tooltip("Texte KDA.")]
        [SerializeField] private TMP_Text _kdaText;

        [Header("Mastery")]
        [Tooltip("Conteneur de la maîtrise agents.")]
        [SerializeField] private RectTransform _masteryContainer;
        [Tooltip("Prefab d'une ligne mastery.")]
        [SerializeField] private GameObject _masteryRowPrefab;

        [Header("Achievements Showcase")]
        [Tooltip("Conteneur du showcase d'achievements.")]
        [SerializeField] private RectTransform _showcaseContainer;
        [Tooltip("Prefab d'une cellule achievement.")]
        [SerializeField] private GameObject _achievementCellPrefab;

        [Header("Actions")]
        [Tooltip("Bouton SHARE.")]
        [SerializeField] private KButton _shareButton;
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;

        private readonly List<GameObject> _rows = new(16);

        protected override void Awake()
        {
            _screenType = ScreenType.Profile;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_playerName != null)
            {
                _playerName.text = "OPERATIVE-001";
                _playerName.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _playerName.color = ThemeManager.Main;
            }
            if (_levelText != null)
            {
                _levelText.text = "LEVEL 47";
                _levelText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _levelText.color = ThemeManager.White;
            }
            if (_powerScoreText != null)
            {
                _powerScoreText.text = "POWER 2500";
                _powerScoreText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _powerScoreText.color = ThemeManager.SubYellow;
            }
            if (_playtimeText != null) { _playtimeText.text = "127h"; _playtimeText.color = ThemeManager.White; }
            if (_killsText != null) { _killsText.text = "3,421"; _killsText.color = ThemeManager.White; }
            if (_missionsText != null) { _missionsText.text = "82"; _missionsText.color = ThemeManager.White; }
            if (_kdaText != null) { _kdaText.text = "KDA 3.2"; _kdaText.color = ThemeManager.SubGreen; }

            if (_shareButton != null)
            {
                _shareButton.SetLocalizationKey("profile.share", "SHARE");
                _shareButton.OnKClick += _ => OnShare();
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }
        }

        protected override void OnShow(object payload)
        {
            BuildMastery();
            BuildShowcase();
            if (_xpBar != null)
            {
                _xpBar.SetType(StatBarType.Xp);
                _xpBar.SetRange(0f, 100f);
                _xpBar.Value = 65f;
            }
            TrackClick("profile_show");
        }

        protected override void OnHide()
        {
            foreach (var r in _rows) { if (r != null) Destroy(r); }
            _rows.Clear();
        }

        private void BuildMastery()
        {
            if (_masteryContainer == null || _masteryRowPrefab == null) return;
            var agents = DataLoader.GetAllAgents();
            foreach (var a in agents)
            {
                var row = Instantiate(_masteryRowPrefab, _masteryContainer);
                _rows.Add(row);
                var texts = row.GetComponentsInChildren<TMP_Text>();
                if (texts.Length >= 2)
                {
                    texts[0].text = a.DisplayName;
                    texts[0].font = ThemeManager.Instance.GetFont(FontRole.Display);
                    texts[0].color = ThemeManager.Main;
                    texts[1].text = $"M{Random(0, 5)}";
                    texts[1].font = ThemeManager.Instance.GetFont(FontRole.Mono);
                    texts[1].color = ThemeManager.SubYellow;
                }
                var bar = row.GetComponentInChildren<KProgressBar>();
                if (bar != null)
                {
                    bar.SetType(StatBarType.Xp);
                    bar.SetRange(0f, 100f);
                    bar.Value = Random(20, 90);
                }
            }
        }

        private void BuildShowcase()
        {
            if (_showcaseContainer == null || _achievementCellPrefab == null) return;
            for (int i = 0; i < 5; i++)
            {
                var cell = Instantiate(_achievementCellPrefab, _showcaseContainer);
                _rows.Add(cell);
                var img = cell.GetComponentInChildren<Image>();
                if (img != null) img.color = ThemeManager.SubYellow;
                var txt = cell.GetComponentInChildren<TMP_Text>();
                if (txt != null)
                {
                    txt.text = $"ACHV-{i + 1}";
                    txt.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                    txt.color = ThemeManager.White;
                }
            }
        }

        private void OnShare()
        {
            TrackClick("profile_share");
            // Partage natif : extension future (NativeShare plugin).
        }

        private void OnBack()
        {
            TrackClick("profile_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        public override bool HandleBack() { OnBack(); return true; }

        private static int Random(int min, int max)
        {
            // Déterministe pour la démo (sans seed random).
            return UnityEngine.Random.Range(min, max + 1);
        }
    }
}
