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
    /// Variantes visuelles d'un <see cref="KCard"/> (PDF page 9).
    /// </summary>
    public enum KCardVariant
    {
        /// <summary>Carte par défaut (non sélectionnée, accessible).</summary>
        Default,
        /// <summary>Carte sélectionnée (bordure cyan animée).</summary>
        Selected,
        /// <summary>Carte verrouillée (overlay + grayscale).</summary>
        Locked
    }

    /// <summary>
    /// Carte KINETICS 5 — composant générique pour missions, agents, armes (PDF page 9).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spécifications PDF page 9</b> :
    /// <list type="bullet">
    /// <item>Variants Default / Selected / Locked.</item>
    /// <item>Bordure de sélection animée (cyan, pulse lent).</item>
    /// <item>Image de preview (cover), titre (Audiowide), sous-titre, badges rareté.</item>
    /// <item>Clic sélectionne la carte (toggle exclusif dans un KCardGroup).</item>
    /// </list>
    /// </para>
    /// <para>
    /// La carte est conçue pour être utilisée dans une liste verticale ou une
    /// grille horizontale (carousel d'armes, agents, missions...).
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/KCard")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class KCard : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Références UI")]
        [Tooltip("Image de fond (panneau).")]
        [SerializeField] private Image _background;
        [Tooltip("Image de bordure (outline sélection).")]
        [SerializeField] private Image _selectionBorder;
        [Tooltip("Image de preview (cover).")]
        [SerializeField] private Image _cover;
        [Tooltip("Texte TMP du titre (Audiowide automatique).")]
        [SerializeField] private TMP_Text _title;
        [Tooltip("Texte TMP du sous-titre (corps).")]
        [SerializeField] private TMP_Text _subtitle;
        [Tooltip("Image du badge de rareté.")]
        [SerializeField] private Image _rarityBadge;
        [Tooltip("Texte de la rareté (mono).")]
        [SerializeField] private TMP_Text _rarityText;
        [Tooltip("Image overlay verrouillé (icône cadenas).")]
        [SerializeField] private Image _lockOverlay;

        [Header("Couleurs thématiques")]
        [SerializeField] private ThemeColor _bgColor = ThemeColor.Surface;
        [SerializeField] private ThemeColor _bgSelectedColor = ThemeColor.DarkBlue;
        [SerializeField] private ThemeColor _titleColor = ThemeColor.White;
        [SerializeField] private ThemeColor _subtitleColor = ThemeColor.TextMuted;

        [Header("Animation")]
        [Range(0.05f, 1f)][SerializeField] private float _hoverScale = 1.04f;
        [Range(0.05f, 1f)][SerializeField] private float _selectedScale = 1.06f;
        [Range(0.02f, 0.5f)][SerializeField] private float _animDuration = 0.18f;
        [Range(0.1f, 5f)][SerializeField] private float _borderPulsePeriod = 1.4f;
        [Range(0f, 1f)][SerializeField] private float _borderPulseAmplitude = 0.35f;

        [Header("Données")]
        [Tooltip("Identifiant métier de la carte (agent id, mission id, weapon id...).")]
        [SerializeField] private string _itemId = string.Empty;
        [Tooltip("Rareté affichée (impacte la couleur du badge).")]
        [SerializeField] private Rarity _rarity = Rarity.Common;
        [Tooltip("Variant initial.")]
        [SerializeField] private KCardVariant _variant = KCardVariant.Default;

        /// <summary>Variant courant.</summary>
        public KCardVariant Variant
        {
            get => _variant;
            set => ApplyVariant(value);
        }

        /// <summary>Identifiant métier de la carte.</summary>
        public string ItemId
        {
            get => _itemId;
            set => _itemId = value;
        }

        /// <summary>Vrai si la carte est sélectionnée.</summary>
        public bool IsSelected => _variant == KCardVariant.Selected;

        /// <summary>Vrai si la carte est verrouillée.</summary>
        public bool IsLocked => _variant == KCardVariant.Locked;

        /// <summary>Événement déclenché au clic sur la carte.</summary>
        public event Action<KCard> OnCardClicked;

        private bool _isHovered;
        private Tween _pulseTween;
        private bool _isPulsing;

        // =================================================================================
        //  CYCLE DE VIE
        // =================================================================================

        private void Awake()
        {
            if (_background == null) _background = GetComponent<Image>();
            ApplyFonts();
            ApplyRarityColor();
            ApplyVariant(_variant);
        }

        private void OnEnable()
        {
            UpdatePulseAnimation();
        }

        private void OnDisable()
        {
            StopPulseAnimation();
        }

        private void OnDestroy()
        {
            _pulseTween?.Kill();
        }

        // =================================================================================
        //  API PUBLIQUE
        // =================================================================================

        /// <summary>Configure la carte avec les données d'un agent.</summary>
        public void Bind(KINETICS5.Data.AgentDto agent, bool locked = false)
        {
            _itemId = agent?.Id ?? string.Empty;
            if (_title != null) _title.text = agent?.DisplayName ?? string.Empty;
            if (_subtitle != null) _subtitle.text = agent?.Motto ?? string.Empty;
            ApplyVariant(locked ? KCardVariant.Locked : KCardVariant.Default);
        }

        /// <summary>Configure la carte avec les données d'une arme.</summary>
        public void Bind(KINETICS5.Data.WeaponDto weapon, bool locked = false)
        {
            _itemId = weapon?.Id ?? string.Empty;
            _rarity = weapon?.Rarity ?? Rarity.Common;
            if (_title != null) _title.text = weapon?.DisplayName ?? string.Empty;
            if (_subtitle != null) _subtitle.text = $"PWR {weapon?.Power ?? 0}";
            ApplyRarityColor();
            ApplyVariant(locked ? KCardVariant.Locked : KCardVariant.Default);
        }

        /// <summary>Configure la carte avec les données d'une mission.</summary>
        public void Bind(KINETICS5.Data.MissionDto mission, bool locked = false)
        {
            _itemId = mission?.Id ?? string.Empty;
            if (_title != null) _title.text = mission?.DisplayName ?? string.Empty;
            if (_subtitle != null) _subtitle.text = mission?.Type.ToString() ?? string.Empty;
            ApplyVariant(locked ? KCardVariant.Locked : KCardVariant.Default);
        }

        /// <summary>Sélectionne la carte (toggle exclusif géré par KCardGroup).</summary>
        public void Select() => ApplyVariant(KCardVariant.Selected);

        /// <summary>Désélectionne la carte.</summary>
        public void Deselect() => ApplyVariant(KCardVariant.Default);

        /// <summary>Verrouille/déverrouille la carte.</summary>
        public void SetLocked(bool locked) => ApplyVariant(locked ? KCardVariant.Locked : KCardVariant.Default);

        // =================================================================================
        //  HANDLERS UI
        // =================================================================================

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsLocked) return;
            // Toggle sélection : si déjà selected -> default, sinon selected.
            ApplyVariant(_variant == KCardVariant.Selected ? KCardVariant.Default : KCardVariant.Selected);
            PlayClickFeedback();
            FireTelemetry();
            OnCardClicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            UpdateScale();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            UpdateScale();
        }

        // =================================================================================
        //  IMPLÉMENTATION INTERNE
        // =================================================================================

        private void ApplyVariant(KCardVariant variant)
        {
            _variant = variant;
            // Background.
            if (_background != null)
            {
                var bg = variant == KCardVariant.Selected
                    ? ThemeManager.GetColor(_bgSelectedColor)
                    : ThemeManager.GetColor(_bgColor);
                _background.DOColor(bg, _animDuration).SetUpdate(true);
            }
            // Bordure sélection.
            if (_selectionBorder != null)
            {
                bool active = variant == KCardVariant.Selected;
                _selectionBorder.gameObject.SetActive(active);
                if (active) _selectionBorder.color = ThemeManager.Main;
            }
            // Overlay verrouillage.
            if (_lockOverlay != null)
            {
                _lockOverlay.gameObject.SetActive(variant == KCardVariant.Locked);
            }
            // Cover grayscale si verrouillé.
            if (_cover != null)
            {
                _cover.color = variant == KCardVariant.Locked
                    ? new Color(0.4f, 0.4f, 0.4f, 1f)
                    : Color.white;
            }
            UpdateScale();
            UpdatePulseAnimation();
        }

        private void UpdateScale()
        {
            float scale = 1f;
            if (_variant == KCardVariant.Selected) scale = _selectedScale;
            else if (_isHovered) scale = _hoverScale;
            transform.DOScale(scale, _animDuration).SetUpdate(true);
        }

        private void UpdatePulseAnimation()
        {
            if (!isActiveAndEnabled) { StopPulseAnimation(); return; }
            if (_variant != KCardVariant.Selected || _selectionBorder == null)
            {
                StopPulseAnimation();
                return;
            }
            if (_isPulsing) return;
            _isPulsing = true;
            var baseColor = ThemeManager.Main;
            _selectionBorder.color = baseColor;
            _pulseTween?.Kill();
            _pulseTween = DOTween.To(
                () => _selectionBorder.color.a,
                a => _selectionBorder.color = new Color(baseColor.r, baseColor.g, baseColor.b, a),
                baseColor.a - _borderPulseAmplitude,
                _borderPulsePeriod * 0.5f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }

        private void StopPulseAnimation()
        {
            _isPulsing = false;
            _pulseTween?.Kill();
            _pulseTween = null;
        }

        private void ApplyRarityColor()
        {
            var color = ThemeManager.ColorForRarity(_rarity);
            if (_rarityBadge != null) _rarityBadge.color = color;
            if (_rarityText != null)
            {
                _rarityText.text = _rarity.ToString().ToUpperInvariant();
                _rarityText.color = color;
            }
        }

        private void ApplyFonts()
        {
            var tm = ThemeManager.Instance;
            if (tm == null) return;
            if (_title != null)
            {
                _title.font = tm.GetFont(FontRole.Display);
                _title.color = ThemeManager.GetColor(_titleColor);
            }
            if (_subtitle != null)
            {
                _subtitle.font = tm.GetFont(FontRole.Body);
                _subtitle.color = ThemeManager.GetColor(_subtitleColor);
            }
            if (_rarityText != null) _rarityText.font = tm.GetFont(FontRole.Mono);
        }

        private void PlayClickFeedback()
        {
            var am = AudioManager.Instance;
            if (am != null) am.PlaySfx(null);
        }

        private void FireTelemetry()
        {
            var t = TelemetryLogger.Instance;
            if (t != null) t.TrackUiClick("KCard", _itemId);
        }
    }

    /// <summary>
    /// Gestionnaire de groupe de cartes : assure la sélection exclusive.
    /// </summary>
    [AddComponentMenu("KINETICS 5/KCardGroup")]
    [DisallowMultipleComponent]
    public sealed class KCardGroup : MonoBehaviour
    {
        [Tooltip("Si vrai, une seule carte est sélectionnée à la fois (radio).")]
        [SerializeField] private bool _singleSelection = true;
        [Tooltip("Autorise la désélection en cliquant une carte déjà sélectionnée.")]
        [SerializeField] private bool _allowDeselect = false;

        /// <summary>Événement déclenché quand la sélection change (carte cliquée).</summary>
        public event Action<KCard> OnSelectionChanged;

        /// <summary>Carte actuellement sélectionnée (null si aucune).</summary>
        public KCard Selected { get; private set; }

        /// <summary>Ajoute une carte au groupe (s'abonne à son clic).</summary>
        public void Register(KCard card)
        {
            if (card == null) return;
            card.OnCardClicked += OnCardClicked;
        }

        /// <summary>Retire une carte du groupe.</summary>
        public void Unregister(KCard card)
        {
            if (card == null) return;
            card.OnCardClicked -= OnCardClicked;
            if (Selected == card) Selected = null;
        }

        private void OnCardClicked(KCard card)
        {
            if (_singleSelection)
            {
                if (Selected == card && _allowDeselect)
                {
                    card.Deselect();
                    Selected = null;
                }
                else
                {
                    Selected?.Deselect();
                    card.Select();
                    Selected = card;
                }
            }
            else
            {
                if (card.IsSelected) card.Deselect();
                else card.Select();
                Selected = card;
            }
            OnSelectionChanged?.Invoke(card);
        }
    }
}
