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
    /// Écran de Victoire — PDF page 7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spécifications PDF page 7</b> :
    /// <list type="bullet">
    /// <item>Titre "VICTORY" en Audiowide avec lueur cyan.</item>
    /// <item>Récompenses : +5000 CR, +2500 XP.</item>
    /// <item>Boutons : CONTINUE, REMATCH, SETTINGS, SAVE.</item>
    /// <item>Effets VFX confettis / particules.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/Screens/VictoryScreen")]
    [DisallowMultipleComponent]
    public sealed class VictoryScreen : UIScreen
    {
        [Header("Titre")]
        [Tooltip("Texte 'VICTORY' (Audiowide, lueur cyan).")]
        [SerializeField] private TMP_Text _titleText;
        [Tooltip("Image de lueur derrière le titre.")]
        [SerializeField] private Image _titleGlow;

        [Header("Récompenses")]
        [Tooltip("Texte +5000 CR.")]
        [SerializeField] private TMP_Text _crRewardText;
        [Tooltip("Texte +2500 XP.")]
        [SerializeField] private TMP_Text _xpRewardText;
        [Tooltip("Image icône CR.")]
        [SerializeField] private Image _crIcon;
        [Tooltip("Image icône XP.")]
        [SerializeField] private Image _xpIcon;

        [Header("Boutons")]
        [SerializeField] private KButton _continueButton;
        [SerializeField] private KButton _rematchButton;
        [SerializeField] private KButton _settingsButton;
        [SerializeField] private KButton _saveButton;

        [Header("VFX")]
        [Tooltip("ParticleSystem de confettis.")]
        [SerializeField] private ParticleSystem _confettiVfx;

        /// <summary>Récompenses de la mission (payload à Show).</summary>
        public struct VictoryPayload
        {
            public int Cr;
            public int Xp;
            public string MissionId;
        }

        private VictoryPayload _payload;

        protected override void Awake()
        {
            _screenType = ScreenType.Victory;
            _enterTransition = ScreenTransition.ScaleFade;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_titleText != null)
            {
                _titleText.text = "VICTORY";
                _titleText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _titleText.color = ThemeManager.Main;
            }
            if (_titleGlow != null)
            {
                _titleGlow.color = new Color(ThemeManager.Main.r, ThemeManager.Main.g, ThemeManager.Main.b, 0f);
            }
            BindButton(_continueButton, "victory.continue", "CONTINUE", OnContinue);
            BindButton(_rematchButton, "victory.rematch", "REMATCH", OnRematch);
            BindButton(_settingsButton, "victory.settings", "SETTINGS", OnSettings);
            BindButton(_saveButton, "victory.save", "SAVE", OnSave);
        }

        protected override void OnShow(object payload)
        {
            if (payload is VictoryPayload vp) _payload = vp;
            ApplyRewards();
            PlayVfx();
            StartTitleAnimation();
            TrackClick("victory_show");
        }

        protected override void OnHide()
        {
            if (_titleText != null) _titleText.transform.DOKill();
            if (_titleGlow != null) _titleGlow.DOKill();
            if (_confettiVfx != null) _confettiVfx.Stop();
        }

        // =================================================================================
        //  AFFICHAGE
        // =================================================================================

        private void ApplyRewards()
        {
            int cr = _payload.Cr > 0 ? _payload.Cr : 5000;
            int xp = _payload.Xp > 0 ? _payload.Xp : 2500;
            if (_crRewardText != null)
            {
                _crRewardText.text = $"+{cr} CR";
                _crRewardText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _crRewardText.color = ThemeManager.SubYellow;
                _crRewardText.transform.localScale = Vector3.zero;
                _crRewardText.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetUpdate(true).SetDelay(0.3f);
            }
            if (_xpRewardText != null)
            {
                _xpRewardText.text = $"+{xp} XP";
                _xpRewardText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _xpRewardText.color = ThemeManager.XpPurple;
                _xpRewardText.transform.localScale = Vector3.zero;
                _xpRewardText.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetUpdate(true).SetDelay(0.5f);
            }
        }

        private void PlayVfx()
        {
            if (_confettiVfx != null) _confettiVfx.Play();
        }

        private void StartTitleAnimation()
        {
            // Pulse glow cyan en boucle.
            if (_titleGlow != null)
            {
                _titleGlow.gameObject.SetActive(true);
                _titleGlow.DOFade(0.6f, 1.2f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
            }
            // Scale pulse sur le titre.
            if (_titleText != null)
            {
                _titleText.transform.localScale = Vector3.one * 0.9f;
                _titleText.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetUpdate(true);
                _titleText.transform.DOScale(1.05f, 1.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetDelay(0.5f);
            }
        }

        // =================================================================================
        //  HANDLERS BOUTONS
        // =================================================================================

        private void OnContinue(KButton _)
        {
            TrackClick("victory_continue");
            // Ouvre l'Operation Summary.
            _ = UIManager.Instance?.ShowAsync(ScreenType.OperationSummary);
        }

        private void OnRematch(KButton _)
        {
            TrackClick("victory_rematch");
            // Relance la mission (missionId depuis payload).
            if (!string.IsNullOrEmpty(_payload.MissionId))
            {
                _ = GameManager.Instance?.StartMissionAsync(_payload.MissionId);
            }
        }

        private void OnSettings(KButton _)
        {
            TrackClick("victory_settings");
            _ = UIManager.Instance?.ShowModalAsync(ScreenType.Settings);
        }

        private void OnSave(KButton _)
        {
            TrackClick("victory_save");
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
