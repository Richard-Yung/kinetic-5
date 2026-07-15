using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KINETICS5.Core;
using KINETICS5.Data;

namespace KINETICS5.UI
{
    /// <summary>
    /// États visuels d'un <see cref="KButton"/> (PDF page 9).
    /// </summary>
    public enum KButtonState
    {
        /// <summary>État normal.</summary>
        Default,
        /// <summary>Pressed / actif.</summary>
        Pressed,
        /// <summary>Sélectionné (toggle radio, tab).</summary>
        Selected,
        /// <summary>Verrouillé (verrou niveau / paywall).</summary>
        Locked,
        /// <summary>Désactivé (interactable false).</summary>
        Disabled
    }

    /// <summary>
    /// Bouton KINETICS 5 — composant custom dérivé de <see cref="Button"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spécifications PDF page 9</b> :
    /// <list type="bullet">
    /// <item>Police Audiowide automatique (titre + texte bouton).</item>
    /// <item>Lueur cyan au survol (hover) et à la sélection.</item>
    /// <item>Haptic feedback mobile + son via AudioManager.</item>
    /// <item>Télémétrie du clic (TrackUiClick).</item>
    /// <item>États : Default, Pressed, Selected, Locked, Disabled.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Tap target mobile</b> : garantit une taille minimale de 44 dp via
    /// <see cref="LayoutElement.minWidth"/>/<see cref="minHeight"/> au démarrage.
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/KButton")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class KButton : Button, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Références UI")]
        [Tooltip("Texte TMP du bouton (Audiowide automatique).")]
        [SerializeField] private TMP_Text _label;
        [Tooltip("Image de fond (bouton lui-même).")]
        [SerializeField] private Image _background;
        [Tooltip("Image optionnelle de l'icône (à gauche du label).")]
        [SerializeField] private Image _icon;
        [Tooltip("Image optionnelle de la lueur (outline glow).")]
        [SerializeField] private Image _glow;

        [Header("Clé de localisation")]
        [Tooltip("Clé de localisation du label (vide = texte statique).")]
        [SerializeField] private string _localizationKey = string.Empty;
        [Tooltip("Texte par défaut si la clé est introuvable.")]
        [SerializeField] private string _fallbackText = "BUTTON";

        [Header("Couleurs thématiques")]
        [Tooltip("Couleur de fond par défaut (rôle).")]
        [SerializeField] private ThemeColor _defaultColor = ThemeColor.DarkBlue;
        [Tooltip("Couleur de fond pressé (rôle).")]
        [SerializeField] private ThemeColor _pressedColor = ThemeColor.Main;
        [Tooltip("Couleur de fond sélectionné (rôle).")]
        [SerializeField] private ThemeColor _selectedColor = ThemeColor.Main;
        [Tooltip("Couleur de fond verrouillé (rôle).")]
        [SerializeField] private ThemeColor _lockedColor = ThemeColor.Surface;
        [Tooltip("Couleur de fond désactivé (rôle).")]
        [SerializeField] private ThemeColor _disabledColor = ThemeColor.Surface;
        [Tooltip("Couleur du texte par défaut (rôle).")]
        [SerializeField] private ThemeColor _textColor = ThemeColor.White;

        [Header("Animation")]
        [Tooltip("Scale au survol (1 = pas de scale).")]
        [Range(0.9f, 1.2f)][SerializeField] private float _hoverScale = 1.05f;
        [Tooltip("Scale au press (1 = pas de scale).")]
        [Range(0.8f, 1.1f)][SerializeField] private float _pressScale = 0.95f;
        [Tooltip("Durée des transitions (secondes).")]
        [Range(0.02f, 0.5f)][SerializeField] private float _animDuration = 0.12f;
        [Tooltip("Intensité de la lueur cyan (alpha).")]
        [Range(0f, 1f)][SerializeField] private float _glowAlpha = 0.45f;

        [Header("Audio & Haptics")]
        [Tooltip("Son joué au clic (null = son par défaut via Settings).")]
        [SerializeField] private AudioClip _clickSound;
        [Tooltip("Intensité haptic au clic (0 = désactivé).")]
        [Range(0f, 1f)][SerializeField] private float _hapticIntensity = 0.6f;

        [Header("Télémétrie")]
        [Tooltip("Si vrai, log le clic via TelemetryLogger.")]
        [SerializeField] private bool _trackClick = true;
        [Tooltip("Nom logique de l'élément pour la télémétrie.")]
        [SerializeField] private string _elementName = string.Empty;

        [Header("État initial")]
        [Tooltip("État initial du bouton.")]
        [SerializeField] private KButtonState _state = KButtonState.Default;

        /// <summary>État courant.</summary>
        public KButtonState State
        {
            get => _state;
            set => ApplyState(value);
        }

        /// <summary>Vrai si le bouton est verrouillé.</summary>
        public bool IsLocked => _state == KButtonState.Locked || _state == KButtonState.Disabled;

        private bool _isHovered;
        private bool _isPressed;
        private bool _isSelected;

        /// <summary>Événement déclenché AU-DESSUS du onClick standard, avec telemetry et haptic.</summary>
        public event Action<KButton> OnKClick;

        // =================================================================================
        //  CYCLE DE VIE
        // =================================================================================

        protected override void Awake()
        {
            base.Awake();
            if (_background == null) _background = GetComponent<Image>();
            onClick.AddListener(HandleClick);
            EnsureMinTapTarget();
            ApplyLabelFont();
            RefreshLabel();
            ApplyState(_state);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
        }

        // =================================================================================
        //  API PUBLIQUE
        // =================================================================================

        /// <summary>Définit le texte du label (et désactive la localisation auto).</summary>
        public void SetText(string text)
        {
            _localizationKey = string.Empty;
            _fallbackText = text ?? string.Empty;
            if (_label != null) _label.text = _fallbackText;
        }

        /// <summary>Définit la clé de localisation.</summary>
        public void SetLocalizationKey(string key, string fallback = "")
        {
            _localizationKey = key;
            _fallbackText = fallback;
            RefreshLabel();
        }

        /// <summary>Définit l'icône.</summary>
        public void SetIcon(Sprite sprite)
        {
            if (_icon == null) return;
            _icon.sprite = sprite;
            _icon.enabled = sprite != null;
        }

        /// <summary>Sélectionne/désélectionne le bouton (état Selected).</summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            ApplyState(_isSelected ? KButtonState.Selected : KButtonState.Default);
        }

        /// <summary>Verrouille/déverrouille le bouton.</summary>
        public void SetLocked(bool locked)
        {
            ApplyState(locked ? KButtonState.Locked : KButtonState.Default);
        }

        /// <summary>Active/désactive le bouton (interactable).</summary>
        public void SetDisabled(bool disabled)
        {
            interactable = !disabled;
            ApplyState(disabled ? KButtonState.Disabled : KButtonState.Default);
        }

        // =================================================================================
        //  HANDLERS UI
        // =================================================================================

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            _isHovered = true;
            UpdateVisualState();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            _isHovered = false;
            UpdateVisualState();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            _isPressed = true;
            UpdateVisualState();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            _isPressed = false;
            UpdateVisualState();
        }

        // =================================================================================
        //  IMPLÉMENTATION INTERNE
        // =================================================================================

        private void HandleClick()
        {
            if (IsLocked) return;
            PlayClickFeedback();
            FireTelemetry();
            OnKClick?.Invoke(this);
        }

        private void PlayClickFeedback()
        {
            // Son.
            var am = AudioManager.Instance;
            if (am != null) am.PlaySfx(_clickSound);
            // Haptic mobile.
            if (_hapticIntensity > 0f)
            {
#if UNITY_ANDROID || UNITY_IOS
                Handheld.Vibrate();
#endif
            }
        }

        private void FireTelemetry()
        {
            if (!_trackClick) return;
            var t = TelemetryLogger.Instance;
            if (t == null) return;
            var element = !string.IsNullOrEmpty(_elementName) ? _elementName : (_fallbackText ?? name);
            t.TrackUiClick("KButton", element);
        }

        private void ApplyState(KButtonState newState)
        {
            _state = newState;
            interactable = newState != KButtonState.Disabled && newState != KButtonState.Locked;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (_background == null) return;
            var targetColor = ResolveCurrentColor();
            var targetScale = ResolveCurrentScale();
            _background.DOColor(targetColor, _animDuration).SetUpdate(true);
            transform.DOScale(targetScale, _animDuration).SetUpdate(true);

            // Glow cyan : activé au hover, sélection, ou pressed.
            bool showGlow = _isHovered || _isSelected || _isPressed;
            if (_glow != null)
            {
                var glowColor = ThemeManager.Main;
                glowColor.a = showGlow ? _glowAlpha : 0f;
                _glow.DOColor(glowColor, _animDuration).SetUpdate(true);
            }

            // Texte : gris si verrouillé, blanc sinon.
            if (_label != null)
            {
                var txt = (_state == KButtonState.Locked || _state == KButtonState.Disabled)
                    ? ThemeManager.TextMuted
                    : ThemeManager.GetColor(_textColor);
                _label.DOColor(txt, _animDuration).SetUpdate(true);
            }
        }

        private Color ResolveCurrentColor()
        {
            if (_state == KButtonState.Disabled) return ThemeManager.GetColor(_disabledColor);
            if (_state == KButtonState.Locked) return ThemeManager.GetColor(_lockedColor);
            if (_state == KButtonState.Selected || _isSelected) return ThemeManager.GetColor(_selectedColor);
            if (_isPressed) return ThemeManager.GetColor(_pressedColor);
            return ThemeManager.GetColor(_defaultColor);
        }

        private Vector3 ResolveCurrentScale()
        {
            if (_isPressed) return Vector3.one * _pressScale;
            if (_isHovered) return Vector3.one * _hoverScale;
            return Vector3.one;
        }

        private void ApplyLabelFont()
        {
            if (_label == null) return;
            var tm = ThemeManager.Instance;
            if (tm != null) _label.font = tm.GetFont(FontRole.Display);
            _label.color = ThemeManager.GetColor(_textColor);
        }

        private void RefreshLabel()
        {
            if (_label == null) return;
            if (!string.IsNullOrEmpty(_localizationKey) && LocalizationManager.Instance != null)
            {
                _label.text = LocalizationManager.Instance.Get(_localizationKey);
            }
            else if (!string.IsNullOrEmpty(_fallbackText))
            {
                _label.text = _fallbackText;
            }
        }

        private void OnLanguageChanged(Language lang) => RefreshLabel();

        private void EnsureMinTapTarget()
        {
            var le = GetComponent<LayoutElement>();
            if (le == null) le = gameObject.AddComponent<LayoutElement>();
            // 44 dp minimum (Apple HIG / Material Design mobile).
            if (le.minWidth < 44f) le.minWidth = 44f;
            if (le.minHeight < 44f) le.minHeight = 44f;
        }
    }
}
