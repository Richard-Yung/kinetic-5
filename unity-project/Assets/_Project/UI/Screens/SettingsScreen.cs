using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Paramètres — PDF page 7-8.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spécifications PDF page 7-8</b> :
    /// <list type="bullet">
    /// <item>Langue (ENGLISH, FRANÇAIS).</item>
    /// <item>Sliders volume Music / SFX.</item>
    /// <item>Difficulté (EASY/NORMAL/HARD) en mode segmenté.</item>
    /// <item>Qualité graphique.</item>
    /// <item>Contrôles (sensibilité, layout gauche/droite, taille des boutons).</item>
    /// <item>Onglets par catégorie.</item>
    /// <item>Bouton SAVE.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/Screens/SettingsScreen")]
    [DisallowMultipleComponent]
    public sealed class SettingsScreen : UIScreen
    {
        [Header("Tabs")]
        [Tooltip("Boutons de tab (AUDIO, VIDEO, GAMEPLAY, CONTROLS, LANGUAGE, PRIVACY).")]
        [SerializeField] private KButton[] _tabButtons = Array.Empty<KButton>();
        [Tooltip("Panneaux de tab.")]
        [SerializeField] private RectTransform[] _tabPanels = Array.Empty<RectTransform>();

        [Header("Audio")]
        [Tooltip("Slider Music.")]
        [SerializeField] private Slider _musicSlider;
        [Tooltip("Slider SFX.")]
        [SerializeField] private Slider _sfxSlider;
        [Tooltip("Texte valeur Music (%).")]
        [SerializeField] private TMP_Text _musicValueText;
        [Tooltip("Texte valeur SFX (%).")]
        [SerializeField] private TMP_Text _sfxValueText;

        [Header("Video")]
        [Tooltip("Dropdown qualité graphique.")]
        [SerializeField] private TMP_Dropdown _qualityDropdown;
        [Tooltip("Toggle plein écran.")]
        [SerializeField] private Toggle _fullscreenToggle;
        [Tooltip("Slider fréquence cible (FPS).")]
        [SerializeField] private Slider _fpsSlider;
        [Tooltip("Texte valeur FPS.")]
        [SerializeField] private TMP_Text _fpsValueText;

        [Header("Gameplay")]
        [Tooltip("Boutons de difficulté (EASY, NORMAL, HARD).")]
        [SerializeField] private KButton[] _difficultyButtons = Array.Empty<KButton>();
        [Tooltip("Slider sensibilité générale.")]
        [SerializeField] private Slider _sensitivitySlider;
        [Tooltip("Texte valeur sensibilité.")]
        [SerializeField] private TMP_Text _sensitivityValueText;

        [Header("Controls")]
        [Tooltip("Toggle layout main gauche.")]
        [SerializeField] private Toggle _leftHandedToggle;
        [Tooltip("Slider taille des boutons.")]
        [SerializeField] private Slider _buttonSizeSlider;
        [Tooltip("Texte valeur taille boutons.")]
        [SerializeField] private TMP_Text _buttonSizeValueText;

        [Header("Language")]
        [Tooltip("Boutons de langue (EN, FR).")]
        [SerializeField] private KButton[] _languageButtons = Array.Empty<KButton>();

        [Header("Privacy")]
        [Tooltip("Toggle consentement GDPR analytics.")]
        [SerializeField] private Toggle _analyticsConsentToggle;

        [Header("Actions")]
        [Tooltip("Bouton SAVE.")]
        [SerializeField] private KButton _saveButton;
        [Tooltip("Bouton CLOSE (modal).")]
        [SerializeField] private KButton _closeButton;

        private int _currentDifficulty = 1; // 0 = Easy, 1 = Normal, 2 = Hard.
        private int _currentLanguage = 0; // 0 = English, 1 = French.

        protected override void Awake()
        {
            _screenType = ScreenType.Settings;
            _enterTransition = ScreenTransition.ScaleFade;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();

            // Tabs.
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int idx = i;
                _tabButtons[i].OnKClick += _ => SelectTab(idx);
            }

            // Sliders audio.
            if (_musicSlider != null)
            {
                _musicSlider.onValueChanged.AddListener(OnMusicChanged);
                _musicSlider.value = AudioManager.Instance?.GetBusVolume(AudioBus.Music) ?? 0.8f;
            }
            if (_sfxSlider != null)
            {
                _sfxSlider.onValueChanged.AddListener(OnSfxChanged);
                _sfxSlider.value = AudioManager.Instance?.GetBusVolume(AudioBus.Sfx) ?? 1.0f;
            }

            // Difficulté.
            for (int i = 0; i < _difficultyButtons.Length; i++)
            {
                int idx = i;
                var label = idx switch { 0 => "EASY", 1 => "NORMAL", 2 => "HARD", _ => "NORMAL" };
                _difficultyButtons[i].SetText(label);
                _difficultyButtons[i].OnKClick += _ => SetDifficulty(idx);
            }
            SetDifficulty(1);

            // Sliders.
            if (_sensitivitySlider != null)
            {
                _sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
                _sensitivitySlider.value = 0.5f;
            }
            if (_buttonSizeSlider != null)
            {
                _buttonSizeSlider.onValueChanged.AddListener(OnButtonSizeChanged);
                _buttonSizeSlider.value = 1.0f;
            }
            if (_fpsSlider != null)
            {
                _fpsSlider.onValueChanged.AddListener(OnFpsChanged);
                _fpsSlider.value = 60f;
                _fpsSlider.minValue = 30f;
                _fpsSlider.maxValue = 120f;
            }

            // Langues.
            for (int i = 0; i < _languageButtons.Length; i++)
            {
                int idx = i;
                var label = idx switch { 0 => "ENGLISH", 1 => "FRANÇAIS", _ => "ENGLISH" };
                _languageButtons[i].SetText(label);
                _languageButtons[i].OnKClick += _ => SetLanguage(idx);
            }

            // Qualité graphique.
            if (_qualityDropdown != null)
            {
                _qualityDropdown.ClearOptions();
                _qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
                _qualityDropdown.value = QualitySettings.GetQualityLevel();
                _qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            }

            // Boutons actions.
            if (_saveButton != null)
            {
                _saveButton.SetLocalizationKey("common.save", "SAVE");
                _saveButton.OnKClick += _ => OnSave();
            }
            if (_closeButton != null)
            {
                _closeButton.SetLocalizationKey("common.close", "CLOSE");
                _closeButton.OnKClick += _ => OnClose();
            }
        }

        protected override void OnShow(object payload)
        {
            SelectTab(0);
            TrackClick("settings_show");
        }

        public override bool HandleBack()
        {
            OnClose();
            return true;
        }

        // =================================================================================
        //  TABS
        // =================================================================================

        private void SelectTab(int index)
        {
            if (index < 0 || index >= _tabPanels.Length) return;
            for (int i = 0; i < _tabPanels.Length; i++)
            {
                if (_tabPanels[i] == null) continue;
                _tabPanels[i].gameObject.SetActive(i == index);
            }
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == null) continue;
                _tabButtons[i].SetSelected(i == index);
            }
            TrackClick($"settings_tab_{index}");
        }

        // =================================================================================
        //  HANDLERS
        // =================================================================================

        private void OnMusicChanged(float value)
        {
            if (_musicValueText != null) _musicValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
            AudioManager.Instance?.SetBusVolume(AudioBus.Music, value);
        }

        private void OnSfxChanged(float value)
        {
            if (_sfxValueText != null) _sfxValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
            AudioManager.Instance?.SetBusVolume(AudioBus.Sfx, value);
        }

        private void OnSensitivityChanged(float value)
        {
            if (_sensitivityValueText != null) _sensitivityValueText.text = $"{value:F2}";
            var im = ServiceLocator.Instance?.Get<InputManager>();
            if (im != null)
            {
                // InputManager expose SetSensitivity ou similaire ; sinon via PlayerPrefs.
                PlayerPrefs.SetFloat("K5_Sensitivity", value);
            }
        }

        private void OnButtonSizeChanged(float value)
        {
            if (_buttonSizeValueText != null) _buttonSizeValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
            PlayerPrefs.SetFloat("K5_ButtonSize", value);
        }

        private void OnFpsChanged(float value)
        {
            if (_fpsValueText != null) _fpsValueText.text = $"{Mathf.RoundToInt(value)} FPS";
            Application.targetFrameRate = Mathf.RoundToInt(value);
        }

        private void OnQualityChanged(int index)
        {
            QualitySettings.SetQualityLevel(index, true);
        }

        private void SetDifficulty(int index)
        {
            _currentDifficulty = index;
            for (int i = 0; i < _difficultyButtons.Length; i++)
            {
                if (_difficultyButtons[i] == null) continue;
                _difficultyButtons[i].SetSelected(i == index);
            }
            PlayerPrefs.SetInt("K5_Difficulty", index);
            TrackClick($"settings_difficulty_{index}");
        }

        private void SetLanguage(int index)
        {
            _currentLanguage = index;
            for (int i = 0; i < _languageButtons.Length; i++)
            {
                if (_languageButtons[i] == null) continue;
                _languageButtons[i].SetSelected(i == index);
            }
            var lang = index == 0 ? Language.English : Language.French;
            _ = LocalizationManager.Instance?.SetLanguageAsync(lang);
            TrackClick($"settings_language_{lang}");
        }

        private void OnSave()
        {
            TrackClick("settings_save");
            PlayerPrefs.Save();
            var save = ServiceLocator.Instance?.Get<SaveSystem>();
            save?.MarkDirty();
        }

        private void OnClose()
        {
            TrackClick("settings_close");
            _ = UIManager.Instance?.CloseTopModalAsync();
        }
    }
}
