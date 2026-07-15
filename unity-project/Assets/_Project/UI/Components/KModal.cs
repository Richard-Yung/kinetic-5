using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Modal dialog KINETICS 5 — backdrop blur + animation centrée.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Utilisé pour : confirmations, dialogues système, achats, claim de récompense.
    /// La modal est affichée via <see cref="UIManager.ShowModalAsync"/> (couche modale).
    /// </para>
    /// <para>
    /// <b>Spécifications PDF</b> :
    /// <list type="bullet">
    /// <item>Backdrop blur (si URP disponible, sinon overlay sombre).</item>
    /// <item>Animation d'entrée scale + fade (centrée).</item>
    /// <item>Boutons d'action : OK / Cancel / Custom.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/KModal")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class KModal : MonoBehaviour
    {
        [Header("Références UI")]
        [Tooltip("Image de fond de la modal (panneau).")]
        [SerializeField] private Image _background;
        [Tooltip("Image de backdrop (overlay sombre pleine page).")]
        [SerializeField] private Image _backdrop;
        [Tooltip("Texte TMP du titre (Audiowide).")]
        [SerializeField] private TMP_Text _titleText;
        [Tooltip("Texte TMP du corps (Rajdhani).")]
        [SerializeField] private TMP_Text _messageText;
        [Tooltip("Bouton primaire (OK / Confirm).")]
        [SerializeField] private KButton _primaryButton;
        [Tooltip("Bouton secondaire (Cancel / Close).")]
        [SerializeField] private KButton _secondaryButton;
        [Tooltip("Bouton tertiaire optionnel (Custom).")]
        [SerializeField] private KButton _tertiaryButton;

        [Header("Animation")]
        [Range(0.05f, 1f)][SerializeField] private float _enterDuration = 0.25f;
        [Range(0.05f, 1f)][SerializeField] private float _exitDuration = 0.18f;
        [Tooltip("Scale initial (centré).")]
        [SerializeField] private float _enterScale = 0.85f;
        [Tooltip("Ease d'entrée (overshoot pour effet pop).")]
        [SerializeField] private Ease _enterEase = Ease.OutBack;

        [Header("Audio")]
        [Tooltip("Son joué à l'ouverture.")]
        [SerializeField] private AudioClip _openSound;
        [Tooltip("Son joué à la fermeture.")]
        [SerializeField] private AudioClip _closeSound;

        /// <summary>Événement déclenché à la fermeture de la modal (bool = confirmé).</summary>
        public event Action<bool> OnClosed;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private CancellationTokenSource _cts;
        private bool _isOpen;

        // =================================================================================
        //  CYCLE DE VIE
        // =================================================================================

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            ApplyFonts();
            ApplyColors();
            // Caché au démarrage.
            SetVisibleImmediate(false);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        // =================================================================================
        //  API PUBLIQUE
        // =================================================================================

        /// <summary>
        /// Ouvre la modal avec un titre, un message et des libellés de boutons.
        /// </summary>
        /// <param name="title">Titre (clé localisée ou texte brut).</param>
        /// <param name="message">Corps du message.</param>
        /// <param name="primaryLabel">Libellé du bouton primaire (OK si vide).</param>
        /// <param name="secondaryLabel">Libellé du bouton secondaire (vide = caché).</param>
        /// <param name="tertiaryLabel">Libellé du bouton tertiaire (vide = caché).</param>
        public async UniTask OpenAsync(string title, string message,
            string primaryLabel = "OK",
            string secondaryLabel = "",
            string tertiaryLabel = "")
        {
            SetTexts(title, message);
            ConfigureButton(_primaryButton, primaryLabel, () => CloseAsync(true));
            ConfigureButton(_secondaryButton, secondaryLabel, () => CloseAsync(false), string.IsNullOrEmpty(secondaryLabel));
            ConfigureButton(_tertiaryButton, tertiaryLabel, () => CloseAsync(false), string.IsNullOrEmpty(tertiaryLabel));

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _isOpen = true;
            SetVisibleImmediate(true);

            // Backdrop fade in.
            if (_backdrop != null)
            {
                _backdrop.color = new Color(0f, 0f, 0f, 0f);
                _ = _backdrop.DOFade(ThemeManager.Backdrop.a, _enterDuration).SetUpdate(true).ToUniTask(cancellationToken: token);
            }

            // Modal scale + fade.
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            if (_rectTransform != null) _rectTransform.localScale = Vector3.one * _enterScale;
            PlaySound(_openSound);

            try
            {
                if (_canvasGroup != null)
                    await _canvasGroup.DOFade(1f, _enterDuration).SetUpdate(true).ToUniTask(cancellationToken: token);
                if (_rectTransform != null)
                    await _rectTransform.DOScale(Vector3.one, _enterDuration)
                        .SetEase(_enterEase).SetUpdate(true).ToUniTask(cancellationToken: token);
            }
            catch (OperationCanceledException) { /* OK */ }
        }

        /// <summary>Ferme la modal.</summary>
        /// <param name="confirmed">Vrai si fermée via bouton primaire.</param>
        public async UniTask CloseAsync(bool confirmed)
        {
            if (!_isOpen) return;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            PlaySound(_closeSound);

            try
            {
                if (_backdrop != null)
                    await _backdrop.DOFade(0f, _exitDuration).SetUpdate(true).ToUniTask(cancellationToken: token);
                if (_canvasGroup != null)
                    await _canvasGroup.DOFade(0f, _exitDuration).SetUpdate(true).ToUniTask(cancellationToken: token);
                if (_rectTransform != null)
                    await _rectTransform.DOScale(Vector3.one * _enterScale, _exitDuration)
                        .SetEase(Ease.InCubic).SetUpdate(true).ToUniTask(cancellationToken: token);
            }
            catch (OperationCanceledException) { /* OK */ }

            _isOpen = false;
            SetVisibleImmediate(false);
            OnClosed?.Invoke(confirmed);
        }

        /// <summary>Raccourci : ouvre une modal de confirmation OUI/NON.</summary>
        public async UniTask<bool> ConfirmAsync(string title, string message, string confirmLabel = "OK", string cancelLabel = "CANCEL")
        {
            bool result = false;
            var tcs = new UniTaskCompletionSource<bool>();
            void Closed(bool confirmed)
            {
                result = confirmed;
                tcs.TrySetResult(confirmed);
            }
            OnClosed += Closed;
            try
            {
                await OpenAsync(title, message, confirmLabel, cancelLabel);
                await tcs.Task;
            }
            finally
            {
                OnClosed -= Closed;
            }
            return result;
        }

        // =================================================================================
        //  IMPLÉMENTATION INTERNE
        // =================================================================================

        private void SetTexts(string title, string message)
        {
            if (_titleText != null)
            {
                _titleText.text = ResolveText(title);
                _titleText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _titleText.color = ThemeManager.Main;
            }
            if (_messageText != null)
            {
                _messageText.text = ResolveText(message);
                _messageText.font = ThemeManager.Instance.GetFont(FontRole.Body);
                _messageText.color = ThemeManager.White;
            }
        }

        private static string ResolveText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.HasKey(raw))
                return LocalizationManager.Instance.Get(raw);
            return raw;
        }

        private void ConfigureButton(KButton button, string label, Action onClick, bool hide = false)
        {
            if (button == null) return;
            if (hide)
            {
                button.gameObject.SetActive(false);
                return;
            }
            button.gameObject.SetActive(true);
            button.SetText(ResolveText(label));
            button.OnKClick -= _ => onClick?.Invoke();
            button.OnKClick += _ => onClick?.Invoke();
        }

        private void ApplyFonts()
        {
            var tm = ThemeManager.Instance;
            if (tm == null) return;
            if (_titleText != null) _titleText.font = tm.GetFont(FontRole.Display);
            if (_messageText != null) _messageText.font = tm.GetFont(FontRole.Body);
        }

        private void ApplyColors()
        {
            if (_background != null) _background.color = ThemeManager.DarkBlue;
            if (_backdrop != null) _backdrop.color = ThemeManager.Backdrop;
        }

        private void SetVisibleImmediate(bool visible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.interactable = visible;
                _canvasGroup.blocksRaycasts = visible;
            }
            if (_backdrop != null) _backdrop.raycastTarget = visible;
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null) return;
            var am = AudioManager.Instance;
            if (am != null) am.PlaySfx(clip);
        }
    }
}
