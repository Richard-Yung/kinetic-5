using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Onboarding (première session) — landing page après premier boot.
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/OnboardingScreen")]
    [DisallowMultipleComponent]
    public sealed class OnboardingScreen : UIScreen
    {
        [Header("Identité")]
        [Tooltip("Texte de bienvenue.")]
        [SerializeField] private TMP_Text _welcomeText;
        [Tooltip("Champ de saisie du nom d'opérateur.")]
        [SerializeField] private TMP_InputField _nameInput;
        [Tooltip("Image avatar sélectionné.")]
        [SerializeField] private Image _avatar;
        [Tooltip("Boutons de sélection d'avatar.")]
        [SerializeField] private KButton[] _avatarButtons;

        [Header("GDPR Consent")]
        [Tooltip("Toggle consentement analytics.")]
        [SerializeField] private Toggle _analyticsToggle;
        [Tooltip("Texte descriptif GDPR.")]
        [SerializeField] private TMP_Text _gdprText;

        [Header("Actions")]
        [Tooltip("Bouton CONFIRM.")]
        [SerializeField] private KButton _confirmButton;

        protected override void Awake()
        {
            _screenType = ScreenType.Onboarding;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_welcomeText != null)
            {
                _welcomeText.text = "WELCOME, OPERATIVE";
                _welcomeText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _welcomeText.color = ThemeManager.Main;
            }
            if (_nameInput != null) _nameInput.characterLimit = 16;
            if (_gdprText != null)
            {
                _gdprText.text = "Allow anonymous analytics to help us improve the game. You can change this later in Settings.";
                _gdprText.color = ThemeManager.TextMuted;
            }
            for (int i = 0; i < _avatarButtons.Length; i++)
            {
                int idx = i;
                _avatarButtons[i].OnKClick += _ => SelectAvatar(idx);
            }
            if (_confirmButton != null)
            {
                _confirmButton.SetLocalizationKey("onboarding.confirm", "CONFIRM");
                _confirmButton.OnKClick += _ => OnConfirm();
            }
        }

        protected override void OnShow(object payload)
        {
            TrackClick("onboarding_show");
        }

        private void SelectAvatar(int index)
        {
            for (int i = 0; i < _avatarButtons.Length; i++)
            {
                if (_avatarButtons[i] != null) _avatarButtons[i].SetSelected(i == index);
            }
            TrackClick($"onboarding_avatar_{index}");
        }

        private void OnConfirm()
        {
            TrackClick("onboarding_confirm");
            // Consentement GDPR.
            bool consent = _analyticsToggle != null && _analyticsToggle.isOn;
            TelemetryLogger.Instance?.SetConsent(consent);
            // Sauvegarde le nom.
            var name = _nameInput != null ? _nameInput.text : "OPERATIVE-001";
            PlayerPrefs.SetString("K5_PlayerName", string.IsNullOrEmpty(name) ? "OPERATIVE-001" : name);
            PlayerPrefs.SetInt("K5_OnboardingDone", 1);
            PlayerPrefs.Save();
            // Passe au menu Start.
            _ = UIManager.Instance?.ShowAsync(ScreenType.Start);
        }
    }
}
