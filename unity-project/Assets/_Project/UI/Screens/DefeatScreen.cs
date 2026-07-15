using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Écran de Défaite — PDF page 7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spécifications PDF page 7</b> :
    /// <list type="bullet">
    /// <item>Titre "FAILED" en rouge #FE0022.</item>
    /// <item>Mêmes boutons que Victory : CONTINUE, REMATCH, SETTINGS, SAVE.</item>
    /// <item>Overlay désaturé (gris-bleu).</item>
    /// </list>
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/Screens/DefeatScreen")]
    [DisallowMultipleComponent]
    public sealed class DefeatScreen : UIScreen
    {
        [Header("Titre")]
        [Tooltip("Texte 'FAILED' (rouge #FE0022).")]
        [SerializeField] private TMP_Text _titleText;
        [Tooltip("Image overlay désaturé (panneau gris-bleu pleine page).")]
        [SerializeField] private Image _desaturatedOverlay;

        [Header("Récompenses partielles")]
        [Tooltip("Texte +CR (récompense partielle).")]
        [SerializeField] private TMP_Text _crText;
        [Tooltip("Texte +XP (récompense partielle).")]
        [SerializeField] private TMP_Text _xpText;

        [Header("Boutons")]
        [SerializeField] private KButton _continueButton;
        [SerializeField] private KButton _rematchButton;
        [SerializeField] private KButton _settingsButton;
        [SerializeField] private KButton _saveButton;

        /// <summary>Payload de l'écran de défaite.</summary>
        public struct DefeatPayload
        {
            public int Cr;
            public int Xp;
            public string MissionId;
            public string Cause;
        }

        private DefeatPayload _payload;

        protected override void Awake()
        {
            _screenType = ScreenType.Defeat;
            _enterTransition = ScreenTransition.Fade;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_titleText != null)
            {
                _titleText.text = "FAILED";
                _titleText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _titleText.color = ThemeManager.SubRed;
            }
            if (_desaturatedOverlay != null)
            {
                _desaturatedOverlay.color = new Color(0.3f, 0.3f, 0.35f, 0.6f);
            }
            BindButton(_continueButton, "defeat.continue", "CONTINUE", OnContinue);
            BindButton(_rematchButton, "defeat.rematch", "REMATCH", OnRematch);
            BindButton(_settingsButton, "defeat.settings", "SETTINGS", OnSettings);
            BindButton(_saveButton, "defeat.save", "SAVE", OnSave);
        }

        protected override void OnShow(object payload)
        {
            if (payload is DefeatPayload dp) _payload = dp;
            ApplyRewards();
            StartTitleAnimation();
            TrackClick("defeat_show");
            // Log télémétrique de l'échec.
            if (!string.IsNullOrEmpty(_payload.MissionId))
                TelemetryLogger.Instance?.TrackMissionFail(_payload.MissionId, _payload.Cause ?? "unknown");
        }

        protected override void OnHide()
        {
            if (_titleText != null) _titleText.transform.DOKill();
        }

        // =================================================================================
        //  AFFICHAGE
        // =================================================================================

        private void ApplyRewards()
        {
            if (_crText != null)
            {
                _crText.text = $"+{_payload.Cr} CR";
                _crText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _crText.color = ThemeManager.SubYellow;
            }
            if (_xpText != null)
            {
                _xpText.text = $"+{_payload.Xp} XP";
                _xpText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _xpText.color = ThemeManager.XpPurple;
            }
        }

        private void StartTitleAnimation()
        {
            if (_titleText == null) return;
            // Tremblement léger + scale-in.
            _titleText.transform.localScale = Vector3.one * 1.3f;
            _titleText.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutQuad).SetUpdate(true);
            _titleText.transform.DOShakePosition(0.5f, 8f, 20).SetUpdate(true);
        }

        // =================================================================================
        //  HANDLERS BOUTONS
        // =================================================================================

        private void OnContinue(KButton _)
        {
            TrackClick("defeat_continue");
            _ = UIManager.Instance?.ShowAsync(ScreenType.OperationSummary);
        }

        private void OnRematch(KButton _)
        {
            TrackClick("defeat_rematch");
            if (!string.IsNullOrEmpty(_payload.MissionId))
                _ = GameManager.Instance?.StartMissionAsync(_payload.MissionId);
        }

        private void OnSettings(KButton _)
        {
            TrackClick("defeat_settings");
            _ = UIManager.Instance?.ShowModalAsync(ScreenType.Settings);
        }

        private void OnSave(KButton _)
        {
            TrackClick("defeat_save");
            var save = ServiceLocator.Instance?.Get<SaveSystem>();
            save?.MarkDirty();
        }

        // =================================================================================
        //  HELPERS
        // =================================================================================

        private void BindButton(KButton button, string key, string fallback, System.Action<KButton> handler)
        {
            if (button == null) return;
            button.SetLocalizationKey(key, fallback);
            button.OnKClick += handler;
        }
    }
}
