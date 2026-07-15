using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Pause en mission — Resume, Settings, Abandon Mission, Save & Quit.
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/PauseScreen")]
    [DisallowMultipleComponent]
    public sealed class PauseScreen : UIScreen
    {
        [Header("Header")]
        [Tooltip("Texte 'PAUSED'.")]
        [SerializeField] private TMP_Text _titleText;
        [Tooltip("Texte nom mission.")]
        [SerializeField] private TMP_Text _missionNameText;

        [Header("Boutons")]
        [SerializeField] private KButton _resumeButton;
        [SerializeField] private KButton _settingsButton;
        [SerializeField] private KButton _abandonButton;
        [SerializeField] private KButton _saveQuitButton;

        protected override void Awake()
        {
            _screenType = ScreenType.Pause;
            _enterTransition = ScreenTransition.ScaleFade;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_titleText != null)
            {
                _titleText.text = "PAUSED";
                _titleText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _titleText.color = ThemeManager.Main;
            }
            if (_resumeButton != null)
            {
                _resumeButton.SetLocalizationKey("pause.resume", "RESUME");
                _resumeButton.OnKClick += _ => OnResume();
            }
            if (_settingsButton != null)
            {
                _settingsButton.SetLocalizationKey("pause.settings", "SETTINGS");
                _settingsButton.OnKClick += _ => OnSettings();
            }
            if (_abandonButton != null)
            {
                _abandonButton.SetLocalizationKey("pause.abandon", "ABANDON MISSION");
                _abandonButton.OnKClick += _ => OnAbandon();
            }
            if (_saveQuitButton != null)
            {
                _saveQuitButton.SetLocalizationKey("pause.save_quit", "SAVE & QUIT");
                _saveQuitButton.OnKClick += _ => OnSaveQuit();
            }
        }

        protected override void OnShow(object payload)
        {
            TrackClick("pause_show");
        }

        // =================================================================================
        //  ACTIONS
        // =================================================================================

        private void OnResume()
        {
            TrackClick("pause_resume");
            GameManager.Instance?.TogglePause();
        }

        private void OnSettings()
        {
            TrackClick("pause_settings");
            _ = UIManager.Instance?.ShowModalAsync(ScreenType.Settings);
        }

        private void OnAbandon()
        {
            TrackClick("pause_abandon");
            // Abandonne la mission et retour au lobby.
            _ = GameManager.Instance?.ReturnToMainMenu();
        }

        private void OnSaveQuit()
        {
            TrackClick("pause_save_quit");
            var save = ServiceLocator.Instance?.Get<SaveSystem>();
            save?.MarkDirty();
            _ = GameManager.Instance?.ReturnToMainMenu();
        }

        public override bool HandleBack()
        {
            OnResume();
            return true;
        }
    }
}
