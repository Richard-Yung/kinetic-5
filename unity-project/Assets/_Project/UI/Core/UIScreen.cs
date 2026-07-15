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
    /// Mode de transition d'entrée/sortie d'un écran.
    /// </summary>
    public enum ScreenTransition
    {
        /// <summary>Fondu CanvasGroup (défaut).</summary>
        Fade,
        /// <summary>Glissement depuis la droite.</summary>
        SlideRight,
        /// <summary>Glissement depuis la gauche.</summary>
        SlideLeft,
        /// <summary>Glissement vers le haut.</summary>
        SlideUp,
        /// <summary>Glissement vers le bas.</summary>
        SlideDown,
        /// <summary>Scale + fade (centré).</summary>
        ScaleFade,
        /// <summary>Pas d'animation (instantané).</summary>
        None
    }

    /// <summary>
    /// Classe de base abstraite de tous les écrans KINETICS 5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chaque écran dérive de <see cref="UIScreen"/> et implémente
    /// <see cref="OnShow"/> / <see cref="OnHide"/> pour brancher sa logique
    /// métier. La visibilité est contrôlée par un <see cref="CanvasGroup"/>
    /// (fade + interactible) et un <see cref="RectTransform"/> animé via DOTween.
    /// </para>
    /// <para>
    /// <b>Chaîne de vie</b> :
    /// <code>
    /// Awake() -> InitBindings() -> Show() -> OnShow() -> (visible)
    ///         -> Hide() -> OnHide() -> (caché)
    /// </code>
    /// </para>
    /// <para>
    /// <b>Localisation</b> : surcharger <see cref="RefreshLocalization"/> pour
    /// rafraîchir les chaînes traduites. La méthode est appelée automatiquement
    /// après <see cref="OnShow"/> et à chaque changement de langue.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [Header("Identité écran")]
        [Tooltip("Type d'écran (doit correspondre au prefab).")]
        [SerializeField] protected ScreenType _screenType = ScreenType.Start;
        /// <summary>Type d'écran.</summary>
        public ScreenType ScreenType => _screenType;

        [Header("Transition")]
        [Tooltip("Mode de transition à l'entrée.")]
        [SerializeField] protected ScreenTransition _enterTransition = ScreenTransition.Fade;
        [Tooltip("Mode de transition à la sortie.")]
        [SerializeField] protected ScreenTransition _exitTransition = ScreenTransition.Fade;
        [Tooltip("Durée d'animation d'entrée (secondes).")]
        [Range(0.05f, 2f)][SerializeField] protected float _enterDuration = 0.30f;
        [Tooltip("Durée d'animation de sortie (secondes).")]
        [Range(0.05f, 2f)][SerializeField] protected float _exitDuration = 0.20f;
        [Tooltip("Déplacement de slide (en pixels) pour les transitions Slide*.")]
        [SerializeField] protected float _slideDistance = 120f;
        [Tooltip("Si vrai, l'écran reste actif en arrière-plan (mémoire conservée).")]
        [SerializeField] protected bool _keepAliveWhenHidden = false;

        [Header("Audio")]
        [Tooltip("Son joué à l'entrée de l'écran (peut être null).")]
        [SerializeField] protected AudioClip _enterSound;
        [Tooltip("Son joué à la sortie de l'écran (peut être null).")]
        [SerializeField] protected AudioClip _exitSound;

        [Header("Titre")]
        [Tooltip("Texte TMP du titre de l'écran (police Audiowide automatique).")]
        [SerializeField] protected TMP_Text _titleText;
        [Tooltip("Clé de localisation du titre.")]
        [SerializeField] protected string _titleKey = string.Empty;

        /// <summary>CanvasGroup racine (auto-créé via RequireComponent).</summary>
        protected CanvasGroup _canvasGroup;
        /// <summary>RectTransform racine pour animations de slide/scale.</summary>
        protected RectTransform _rectTransform;
        /// <summary>Vrai si l'écran est actuellement visible.</summary>
        public bool IsVisible { get; private set; }
        /// <summary>Vrai si l'écran est en cours de transition.</summary>
        public bool IsTransitioning { get; private set; }

        /// <summary>Jeton d'annulation pour transitions async en cours.</summary>
        protected CancellationTokenSource _transitionCts;

        /// <summary>Référence faible au UIManager propriétaire.</summary>
        public UIManager Owner { get; internal set; }

        // =================================================================================
        //  CYCLE DE VIE UNITY
        // =================================================================================

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _rectTransform = transform as RectTransform;
            InitBindings();
            // Par défaut caché tant que Show() n'est pas appelé.
            SetVisibleImmediate(false);
        }

        protected virtual void OnEnable()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        }

        protected virtual void OnDisable()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
        }

        protected virtual void OnDestroy()
        {
            _transitionCts?.Cancel();
            _transitionCts?.Dispose();
            _transitionCts = null;
        }

        // =================================================================================
        //  API PUBLIQUE
        // =================================================================================

        /// <summary>
        /// Affiche l'écran avec animation. Retourne une UniTask complétée à la fin
        /// de la transition.
        /// </summary>
        public virtual async UniTask Show(object payload = null)
        {
            if (IsVisible) return;
            CancelTransition();
            _transitionCts = new CancellationTokenSource();
            var token = _transitionCts.Token;
            IsTransitioning = true;

            gameObject.SetActive(true);
            SetVisibleImmediate(true);

            // Préparation de l'état initial selon le mode de transition.
            PrepareEnterState();

            try
            {
                await AnimateEnterAsync(token);
                OnShow(payload);
                await RefreshLocalizationAsync();
                PlayEnterSound();
            }
            catch (OperationCanceledException) { /* OK */ }
            finally
            {
                IsTransitioning = false;
                IsVisible = true;
            }
        }

        /// <summary>
        /// Masque l'écran avec animation. Retourne une UniTask complétée à la fin.
        /// </summary>
        public virtual async UniTask Hide()
        {
            if (!IsVisible) return;
            CancelTransition();
            _transitionCts = new CancellationTokenSource();
            var token = _transitionCts.Token;
            IsTransitioning = true;

            OnHide();

            try
            {
                await AnimateExitAsync(token);
                PlayExitSound();
            }
            catch (OperationCanceledException) { /* OK */ }
            finally
            {
                IsTransitioning = false;
                IsVisible = false;
                if (_keepAliveWhenHidden)
                {
                    SetVisibleImmediate(false);
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Affiche immédiatement l'écran sans animation (boot, restauration d'état).
        /// </summary>
        public void ShowImmediate(object payload = null)
        {
            CancelTransition();
            gameObject.SetActive(true);
            SetVisibleImmediate(true);
            ResetTransformState();
            IsVisible = true;
            OnShow(payload);
            _ = RefreshLocalizationAsync();
        }

        /// <summary>
        /// Masque immédiatement (sans animation).
        /// </summary>
        public void HideImmediate()
        {
            CancelTransition();
            IsVisible = false;
            OnHide();
            if (_keepAliveWhenHidden)
            {
                SetVisibleImmediate(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Hook d'initialisation des références UI (override dans les sous-classes).
        /// Appelé depuis Awake, avant la première activation.
        /// </summary>
        protected virtual void InitBindings() { }

        /// <summary>
        /// Hook appelé après l'animation d'entrée. Brancher ici la logique métier
        /// (souscription aux events, refresh des données, etc.).
        /// </summary>
        /// <param name="payload">Données optionnelles passées par l'appelant.</param>
        protected virtual void OnShow(object payload) { }

        /// <summary>
        /// Hook appelé au début de l'animation de sortie. Désabonner les events ici.
        /// </summary>
        protected virtual void OnHide() { }

        /// <summary>
        /// Rafraîchit les chaînes traduites de l'écran. Appelé automatiquement après
        /// OnShow et à chaque changement de langue.
        /// </summary>
        protected virtual void RefreshLocalization()
        {
            // Titre automatique.
            if (_titleText != null && !string.IsNullOrEmpty(_titleKey))
            {
                _titleText.text = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get(_titleKey)
                    : _titleKey;
            }
        }

        /// <summary>Variante async (par défaut synchrone, override si besoin).</summary>
        protected virtual UniTask RefreshLocalizationAsync()
        {
            RefreshLocalization();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Appelé quand le bouton BACK (escape/touch back) est pressé pendant que
        /// cet écran est au sommet de la pile. Override pour personnaliser.
        /// </summary>
        /// <returns>Vrai si l'écran a consommé le back (sinon UIManager remonte).</returns>
        public virtual bool HandleBack() => false;

        // =================================================================================
        //  ANIMATIONS DOTWEEN
        // =================================================================================

        private void PrepareEnterState()
        {
            if (_canvasGroup == null) return;
            switch (_enterTransition)
            {
                case ScreenTransition.Fade:
                    _canvasGroup.alpha = 0f;
                    break;
                case ScreenTransition.SlideRight:
                    _canvasGroup.alpha = 1f;
                    if (_rectTransform != null) _rectTransform.anchoredPosition = new Vector2(_slideDistance, 0f);
                    break;
                case ScreenTransition.SlideLeft:
                    _canvasGroup.alpha = 1f;
                    if (_rectTransform != null) _rectTransform.anchoredPosition = new Vector2(-_slideDistance, 0f);
                    break;
                case ScreenTransition.SlideUp:
                    _canvasGroup.alpha = 1f;
                    if (_rectTransform != null) _rectTransform.anchoredPosition = new Vector2(0f, -_slideDistance);
                    break;
                case ScreenTransition.SlideDown:
                    _canvasGroup.alpha = 1f;
                    if (_rectTransform != null) _rectTransform.anchoredPosition = new Vector2(0f, _slideDistance);
                    break;
                case ScreenTransition.ScaleFade:
                    _canvasGroup.alpha = 0f;
                    if (_rectTransform != null) _rectTransform.localScale = Vector3.one * 0.92f;
                    break;
                case ScreenTransition.None:
                default:
                    _canvasGroup.alpha = 1f;
                    break;
            }
        }

        private async UniTask AnimateEnterAsync(CancellationToken ct)
        {
            if (_canvasGroup == null) return;
            var dur = Mathf.Max(0.01f, _enterDuration);
            switch (_enterTransition)
            {
                case ScreenTransition.Fade:
                    await _canvasGroup.DOFade(1f, dur).SetUpdate(true).ToUniTask(cancellationToken: ct);
                    break;
                case ScreenTransition.SlideRight:
                case ScreenTransition.SlideLeft:
                case ScreenTransition.SlideUp:
                case ScreenTransition.SlideDown:
                    if (_rectTransform != null)
                    {
                        await _rectTransform.DOAnchorPos(Vector2.zero, dur)
                            .SetEase(Ease.OutCubic).SetUpdate(true).ToUniTask(cancellationToken: ct);
                    }
                    break;
                case ScreenTransition.ScaleFade:
                    if (_rectTransform != null)
                    {
                        var t1 = _rectTransform.DOScale(Vector3.one, dur).SetEase(Ease.OutBack).SetUpdate(true);
                        var t2 = _canvasGroup.DOFade(1f, dur).SetUpdate(true);
                        await UniTask.WhenAll(t1.ToUniTask(cancellationToken: ct), t2.ToUniTask(cancellationToken: ct));
                    }
                    break;
                case ScreenTransition.None:
                default:
                    _canvasGroup.alpha = 1f;
                    break;
            }
        }

        private async UniTask AnimateExitAsync(CancellationToken ct)
        {
            if (_canvasGroup == null) return;
            var dur = Mathf.Max(0.01f, _exitDuration);
            switch (_exitTransition)
            {
                case ScreenTransition.Fade:
                    await _canvasGroup.DOFade(0f, dur).SetUpdate(true).ToUniTask(cancellationToken: ct);
                    break;
                case ScreenTransition.SlideRight:
                    if (_rectTransform != null)
                        await _rectTransform.DOAnchorPos(new Vector2(_slideDistance, 0f), dur)
                            .SetEase(Ease.InCubic).SetUpdate(true).ToUniTask(cancellationToken: ct);
                    break;
                case ScreenTransition.SlideLeft:
                    if (_rectTransform != null)
                        await _rectTransform.DOAnchorPos(new Vector2(-_slideDistance, 0f), dur)
                            .SetEase(Ease.InCubic).SetUpdate(true).ToUniTask(cancellationToken: ct);
                    break;
                case ScreenTransition.SlideUp:
                    if (_rectTransform != null)
                        await _rectTransform.DOAnchorPos(new Vector2(0f, _slideDistance), dur)
                            .SetEase(Ease.InCubic).SetUpdate(true).ToUniTask(cancellationToken: ct);
                    break;
                case ScreenTransition.SlideDown:
                    if (_rectTransform != null)
                        await _rectTransform.DOAnchorPos(new Vector2(0f, -_slideDistance), dur)
                            .SetEase(Ease.InCubic).SetUpdate(true).ToUniTask(cancellationToken: ct);
                    break;
                case ScreenTransition.ScaleFade:
                    if (_rectTransform != null)
                    {
                        var t1 = _rectTransform.DOScale(Vector3.one * 0.92f, dur).SetEase(Ease.InCubic).SetUpdate(true);
                        var t2 = _canvasGroup.DOFade(0f, dur).SetUpdate(true);
                        await UniTask.WhenAll(t1.ToUniTask(cancellationToken: ct), t2.ToUniTask(cancellationToken: ct));
                    }
                    break;
                case ScreenTransition.None:
                default:
                    _canvasGroup.alpha = 0f;
                    break;
            }
        }

        private void ResetTransformState()
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = Vector2.zero;
                _rectTransform.localScale = Vector3.one;
            }
        }

        private void SetVisibleImmediate(bool visible)
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private void CancelTransition()
        {
            if (_transitionCts == null) return;
            _transitionCts.Cancel();
            _transitionCts.Dispose();
            _transitionCts = null;
            // Kill tweens en cours sur ce CanvasGroup + RectTransform.
            if (_canvasGroup != null) _canvasGroup.DOKill();
            if (_rectTransform != null) _rectTransform.DOKill();
        }

        private void PlayEnterSound()
        {
            if (_enterSound == null) return;
            var am = AudioManager.Instance;
            if (am != null) am.PlaySfx(_enterSound);
        }

        private void PlayExitSound()
        {
            if (_exitSound == null) return;
            var am = AudioManager.Instance;
            if (am != null) am.PlaySfx(_exitSound);
        }

        private void OnLanguageChanged(KINETICS5.Core.Language lang)
        {
            _ = RefreshLocalizationAsync();
        }

        // =================================================================================
        //  HELPERS PROTÉGÉS
        // =================================================================================

        /// <summary>Localise une clé via LocalizationManager (null-safe).</summary>
        protected string L(string key)
        {
            return LocalizationManager.Instance != null
                ? LocalizationManager.Instance.Get(key)
                : key;
        }

        /// <summary>Localise avec formatage (string.Format).</summary>
        protected string L(string key, params object[] args)
        {
            return LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetFormat(key, args)
                : key;
        }

        /// <summary>
        /// Applique une police Display (Audiowide) à un TextMeshProUGUI.
        /// </summary>
        protected void ApplyDisplayFont(TMP_Text text)
        {
            if (text == null) return;
            text.font = ThemeManager.Instance.GetFont(FontRole.Display);
        }

        /// <summary>
        /// Applique une police Body (Rajdhani) à un TextMeshProUGUI.
        /// </summary>
        protected void ApplyBodyFont(TMP_Text text)
        {
            if (text == null) return;
            text.font = ThemeManager.Instance.GetFont(FontRole.Body);
        }

        /// <summary>
        /// Applique une police Mono (JetBrains Mono) à un TextMeshProUGUI.
        /// </summary>
        protected void ApplyMonoFont(TMP_Text text)
        {
            if (text == null) return;
            text.font = ThemeManager.Instance.GetFont(FontRole.Mono);
        }

        /// <summary>
        /// Enregistre un clic télémétrique sur l'écran courant (helper central).
        /// </summary>
        protected void TrackClick(string element)
        {
            var t = TelemetryLogger.Instance;
            if (t != null) t.TrackUiClick(_screenType.ToString(), element);
        }
    }
}
