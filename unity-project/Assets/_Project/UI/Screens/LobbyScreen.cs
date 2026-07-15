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
    /// Lobby / hub central — PDF page 4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spécifications PDF page 4</b> :
    /// <list type="bullet">
    /// <item>Render du personnage centré (VULCAN).</item>
    /// <item>Stats à droite (CLASS, VULCAN, LEVEL 47, POWER SCORE 2500, barres segmentées).</item>
    /// <item>Carte de mission courante en haut-gauche (MISSION TYPE, OPERATION, XP 1.5K, CR 2.7K).</item>
    /// <item>Sidebar gauche : MISSIONS, LOADOUT, SHOP (empilés).</item>
    /// <item>Bouton PLAY bas-droite (cyan, large).</item>
    /// <item>Icône Settings en haut-droite.</item>
    /// <item>Devises XP/CR en haut-droite.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/Screens/LobbyScreen")]
    [DisallowMultipleComponent]
    public sealed class LobbyScreen : UIScreen
    {
        [Header("Render personnage")]
        [Tooltip("Conteneur du render du personnage (Image ou RawImage).")]
        [SerializeField] private RectTransform _characterRender;
        [Tooltip("Image du portrait de l'agent courant.")]
        [SerializeField] private Image _characterPortrait;

        [Header("Stats agent")]
        [Tooltip("Texte CLASS (ex: TANK).")]
        [SerializeField] private TMP_Text _classText;
        [Tooltip("Texte nom agent (ex: VULCAN).")]
        [SerializeField] private TMP_Text _agentNameText;
        [Tooltip("Texte LEVEL (ex: LEVEL 47).")]
        [SerializeField] private TMP_Text _levelText;
        [Tooltip("Texte POWER SCORE (ex: 2500).")]
        [SerializeField] private TMP_Text _powerScoreText;
        [Tooltip("Barre POWER segmentée.")]
        [SerializeField] private KProgressBar _powerBar;
        [Tooltip("Barre HEALTH segmentée.")]
        [SerializeField] private KProgressBar _healthBar;
        [Tooltip("Barre SHIELD segmentée.")]
        [SerializeField] private KProgressBar _shieldBar;
        [Tooltip("Barre SPEED segmentée.")]
        [SerializeField] private KProgressBar _speedBar;

        [Header("Carte mission courante")]
        [Tooltip("Texte MISSION TYPE (ex: EXTRACTION).")]
        [SerializeField] private TMP_Text _missionTypeText;
        [Tooltip("Texte OPERATION (ex: SHADOW FALL).")]
        [SerializeField] private TMP_Text _missionNameText;
        [Tooltip("Texte XP (ex: 1.5K).")]
        [SerializeField] private TMP_Text _missionXpText;
        [Tooltip("Texte CR (ex: 2.7K).")]
        [SerializeField] private TMP_Text _missionCrText;
        [Tooltip("Carte mission courante (KCard).")]
        [SerializeField] private KCard _currentMissionCard;

        [Header("Sidebar")]
        [SerializeField] private KButton _missionsButton;
        [SerializeField] private KButton _loadoutButton;
        [SerializeField] private KButton _shopButton;

        [Header("Action")]
        [Tooltip("Bouton PLAY (cyan, large, bas-droite).")]
        [SerializeField] private KButton _playButton;
        [Tooltip("Bouton Settings (icône, haut-droite).")]
        [SerializeField] private KButton _settingsButton;
        [Tooltip("Bouton Inventory (icône, haut-droite).")]
        [SerializeField] private KButton _inventoryButton;

        [Header("Devises")]
        [Tooltip("Texte XP (haut-droite).")]
        [SerializeField] private TMP_Text _xpText;
        [Tooltip("Texte CR (haut-droite).")]
        [SerializeField] private TMP_Text _crText;

        [Header("Agent courant")]
        [Tooltip("Id de l'agent sélectionné (default: vulcan).")]
        [SerializeField] private string _currentAgentId = "vulcan";
        [Tooltip("Id de la mission sélectionnée (default: shadow_fall).")]
        [SerializeField] private string _currentMissionId = "shadow_fall";

        protected override void Awake()
        {
            _screenType = ScreenType.Lobby;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();

            // Sidebar.
            BindSidebarButton(_missionsButton, "lobby.missions", "MISSIONS", _ => OnMissions());
            BindSidebarButton(_loadoutButton, "lobby.loadout", "LOADOUT", _ => OnLoadout());
            BindSidebarButton(_shopButton, "lobby.shop", "SHOP", _ => OnShop());

            // Bouton PLAY (cyan, large).
            if (_playButton != null)
            {
                _playButton.SetLocalizationKey("lobby.play", "PLAY");
                _playButton.OnKClick += _ => OnPlay();
            }
            // Settings / Inventory.
            if (_settingsButton != null)
            {
                _settingsButton.SetLocalizationKey("lobby.settings", "SETTINGS");
                _settingsButton.OnKClick += _ => OnSettings();
            }
            if (_inventoryButton != null)
            {
                _inventoryButton.SetLocalizationKey("lobby.inventory", "INVENTORY");
                _inventoryButton.OnKClick += _ => OnInventory();
            }
        }

        protected override void OnShow(object payload)
        {
            RefreshAgentDisplay();
            RefreshMissionDisplay();
            RefreshCurrencyDisplay();
            TrackClick("lobby_show");
        }

        protected override void RefreshLocalization()
        {
            base.RefreshLocalization();
            RefreshAgentDisplay();
            RefreshMissionDisplay();
            RefreshCurrencyDisplay();
        }

        // =================================================================================
        //  RAFRAÎCHISSEMENT UI
        // =================================================================================

        private void RefreshAgentDisplay()
        {
            var agent = DataLoader.GetAgent(_currentAgentId);
            if (agent == null) return;

            if (_classText != null)
            {
                _classText.text = agent.Class.ToString().ToUpperInvariant();
                _classText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _classText.color = ThemeManager.SubYellow;
            }
            if (_agentNameText != null)
            {
                _agentNameText.text = agent.DisplayName;
                _agentNameText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _agentNameText.color = ThemeManager.Main;
            }
            if (_levelText != null)
            {
                _levelText.text = $"LEVEL {agent.Level}";
                _levelText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _levelText.color = ThemeManager.White;
            }
            if (_powerScoreText != null)
            {
                _powerScoreText.text = agent.BasePower.ToString();
                _powerScoreText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _powerScoreText.color = ThemeManager.White;
            }
            ConfigureStatBar(_powerBar, StatBarType.Power, agent.BasePower, 5000);
            ConfigureStatBar(_healthBar, StatBarType.Health, agent.BaseHealth, 10000);
            ConfigureStatBar(_shieldBar, StatBarType.Shield, agent.BaseShield, 5000);
            ConfigureStatBar(_speedBar, StatBarType.Speed, agent.BaseSpeed * 100f, 300f);
        }

        private void RefreshMissionDisplay()
        {
            var mission = DataLoader.GetMission(_currentMissionId);
            if (mission == null) return;

            if (_missionTypeText != null)
            {
                _missionTypeText.text = $"MISSION TYPE: {mission.Type.ToString().ToUpperInvariant()}";
                _missionTypeText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _missionTypeText.color = ThemeManager.Main;
            }
            if (_missionNameText != null)
            {
                _missionNameText.text = $"OPERATION: {mission.DisplayName}";
                _missionNameText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _missionNameText.color = ThemeManager.White;
            }
            if (_missionXpText != null)
            {
                var xp = mission.Rewards?.Xp ?? 0;
                _missionXpText.text = $"XP {FormatK(xp)}";
                _missionXpText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _missionXpText.color = ThemeManager.XpPurple;
            }
            if (_missionCrText != null)
            {
                var cr = mission.Rewards?.Cr ?? 0;
                _missionCrText.text = $"CR {FormatK(cr)}";
                _missionCrText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _missionCrText.color = ThemeManager.SubYellow;
            }
            if (_currentMissionCard != null) _currentMissionCard.Bind(mission);
        }

        private void RefreshCurrencyDisplay()
        {
            // Récupère depuis SaveSystem (si disponible).
            var save = ServiceLocator.Instance?.Get<SaveSystem>();
            int xp = 0, cr = 0;
            if (save?.ActiveData != null)
            {
                // SaveData doit exposer XP/CR ; si non disponible, on garde à 0.
                var json = JsonUtility.ToJson(save.ActiveData);
                // Best-effort : on extrait les champs par parsing simple.
                xp = ExtractInt(json, "xp");
                cr = ExtractInt(json, "cr");
            }
            if (_xpText != null)
            {
                _xpText.text = $"XP {FormatK(xp)}";
                _xpText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _xpText.color = ThemeManager.XpPurple;
            }
            if (_crText != null)
            {
                _crText.text = $"CR {FormatK(cr)}";
                _crText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _crText.color = ThemeManager.SubYellow;
            }
        }

        // =================================================================================
        //  HANDLERS BOUTONS
        // =================================================================================

        private void OnMissions()
        {
            TrackClick("lobby_missions");
            _ = UIManager.Instance?.ShowAsync(ScreenType.MissionSelect);
        }

        private void OnLoadout()
        {
            TrackClick("lobby_loadout");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Loadout);
        }

        private void OnShop()
        {
            TrackClick("lobby_shop");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Shop);
        }

        private void OnPlay()
        {
            TrackClick("lobby_play");
            // Lance la mission via GameManager.
            _ = GameManager.Instance?.StartMissionAsync(_currentMissionId);
        }

        private void OnSettings()
        {
            TrackClick("lobby_settings");
            _ = UIManager.Instance?.ShowModalAsync(ScreenType.Settings);
        }

        private void OnInventory()
        {
            TrackClick("lobby_inventory");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Inventory);
        }

        // =================================================================================
        //  HELPERS
        // =================================================================================

        private void BindSidebarButton(KButton button, string key, string fallback, System.Action<KButton> handler)
        {
            if (button == null) return;
            button.SetLocalizationKey(key, fallback);
            button.OnKClick += handler;
        }

        private void ConfigureStatBar(KProgressBar bar, StatBarType type, float value, float max)
        {
            if (bar == null) return;
            bar.SetType(type);
            bar.SetRange(0f, max);
            bar.Value = value;
        }

        private static string FormatK(int value)
        {
            if (value >= 1000) return $"{value / 1000f:F1}K";
            return value.ToString();
        }

        private static int ExtractInt(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json)) return 0;
            // Recherche simple : "fieldName":1234
            var key = "\"" + fieldName + "\":";
            var idx = json.IndexOf(key, System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return 0;
            idx += key.Length;
            int end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            if (int.TryParse(json.Substring(idx, end - idx), out var v)) return v;
            return 0;
        }
    }
}
