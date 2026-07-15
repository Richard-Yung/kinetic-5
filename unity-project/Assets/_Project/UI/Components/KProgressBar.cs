using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Barre de progression segmentée KINETICS 5 (PDF pages 4-5-6).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spécifications PDF</b> :
    /// <list type="bullet">
    /// <item>Segments (10 à 20 divisions) qui se remplissent gauche -> droite.</item>
    /// <item>Couleur par type de stat : Health vert, Shield cyan, XP violet,
    /// Speed orange, Damage vert, Power jaune.</item>
    /// <item>Lueur sur le segment rempli (bord glow).</item>
    /// <item>Animation fluide (DOTween).</item>
    /// </list>
    /// </para>
    /// <para>
    /// Usage : barres de stats lobby (POWER/HEALTH/SHIELD/SPEED), barre de
    /// santé HUD (segmentée vert), barre d'armure (segmentée cyan), barre
    /// de progression de chargement (segmentée cyan), barre d'XP de fin de mission.
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/KProgressBar")]
    [DisallowMultipleComponent]
    public sealed class KProgressBar : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Type de stat (détermine la couleur).")]
        [SerializeField] private StatBarType _type = StatBarType.Health;
        [Tooltip("Nombre de segments (10-20 selon PDF).")]
        [Range(1, 40)][SerializeField] private int _segmentCount = 12;
        [Tooltip("Valeur minimale (souvent 0).")]
        [SerializeField] private float _minValue = 0f;
        [Tooltip("Valeur maximale (ex: 5000 HP, 100 %).")]
        [SerializeField] private float _maxValue = 100f;
        [Tooltip("Valeur courante affichée.")]
        [SerializeField] private float _currentValue = 50f;

        [Header("Apparence")]
        [Tooltip("Prefab d'un segment (Image). Si null, créé à la volée.")]
        [SerializeField] private GameObject _segmentPrefab;
        [Tooltip("Couleur personnalisée (override de _type).")]
        [SerializeField] private Color? _customColor = null;
        [Tooltip("Couleur des segments vides (fond).")]
        [SerializeField] private Color _emptyColor = new(0.10f, 0.10f, 0.15f, 1f);
        [Tooltip("Espacement entre segments (pixels).")]
        [Range(0f, 12f)][SerializeField] private float _spacing = 2f;
        [Tooltip("Hauteur d'un segment (si layout vertical).")]
        [SerializeField] private float _segmentThickness = 16f;
        [Tooltip("Lueur (glow) sur les segments pleins.")]
        [Range(0f, 1f)][SerializeField] private float _fillGlow = 0.5f;

        [Header("Animation")]
        [Tooltip("Durée d'animation de remplissage (secondes).")]
        [Range(0.05f, 3f)][SerializeField] private float _animDuration = 0.4f;
        [Tooltip("Ease d'animation.")]
        [SerializeField] private Ease _animEase = Ease.OutCubic;

        [Header("Références UI")]
        [Tooltip("Conteneur des segments (HorizontalLayoutGroup auto si null).")]
        [SerializeField] private RectTransform _segmentsContainer;
        [Tooltip("Texte TMP affichant la valeur numérique (Audiowide).")]
        [SerializeField] private TMP_Text _valueText;
        [Tooltip("Format de la valeur ({0}/{1}).")]
        [SerializeField] private string _valueFormat = "{0}/{1}";

        /// <summary>Valeur courante (animée à l'affectation).</summary>
        public float Value
        {
            get => _currentValue;
            set => SetValue(value);
        }

        /// <summary>Valeur normalisée 0..1.</summary>
        public float Normalized =>
            Mathf.Approximately(_maxValue, _minValue) ? 0f :
            Mathf.Clamp01((_currentValue - _minValue) / (_maxValue - _minValue));

        private readonly List<Image> _segments = new(40);
        private float _displayedNormalized;
        private Tween _valueTween;

        // =================================================================================
        //  CYCLE DE VIE
        // =================================================================================

        private void Awake()
        {
            EnsureContainer();
            RebuildSegments();
            RefreshValueText();
        }

        private void OnValidate()
        {
            // En éditeur seulement : rebuild si segmentCount change.
            if (!Application.isPlaying) RebuildSegments();
        }

        private void OnDestroy()
        {
            _valueTween?.Kill();
        }

        // =================================================================================
        //  API PUBLIQUE
        // =================================================================================

        /// <summary>Définit le type de stat (couleur) et rebuild les segments.</summary>
        public void SetType(StatBarType type)
        {
            _type = type;
            _customColor = null;
            ApplyColorsToSegments();
        }

        /// <summary>Définit la valeur min/max et refresh.</summary>
        public void SetRange(float min, float max)
        {
            _minValue = Mathf.Min(min, max);
            _maxValue = Mathf.Max(min, max);
            _currentValue = Mathf.Clamp(_currentValue, _minValue, _maxValue);
            RefreshValueText();
            ApplySegmentsInstant(Normalized);
        }

        /// <summary>Affecte la valeur courante avec animation.</summary>
        public void SetValue(float value, bool animate = true)
        {
            var clamped = Mathf.Clamp(value, _minValue, _maxValue);
            if (Mathf.Approximately(clamped, _currentValue) && _segments.Count > 0)
            {
                ApplySegmentsInstant(Normalized);
                return;
            }
            _currentValue = clamped;
            RefreshValueText();
            if (!animate || !Application.isPlaying)
            {
                ApplySegmentsInstant(Normalized);
                return;
            }
            var targetNorm = Normalized;
            _valueTween?.Kill();
            _valueTween = DOTween.To(
                () => _displayedNormalized,
                v => { _displayedNormalized = v; ApplySegmentsInstant(v); },
                targetNorm,
                _animDuration)
                .SetEase(_animEase)
                .SetUpdate(true);
        }

        /// <summary>Définit une couleur personnalisée (override du type).</summary>
        public void SetCustomColor(Color color)
        {
            _customColor = color;
            ApplyColorsToSegments();
        }

        /// <summary>Réinitialise la palette au type par défaut.</summary>
        public void ResetCustomColor()
        {
            _customColor = null;
            ApplyColorsToSegments();
        }

        // =================================================================================
        //  IMPLÉMENTATION INTERNE
        // =================================================================================

        private void EnsureContainer()
        {
            if (_segmentsContainer != null) return;
            var go = new GameObject("Segments", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(transform, false);
            _segmentsContainer = (RectTransform)go.transform;
            _segmentsContainer.anchorMin = Vector2.zero;
            _segmentsContainer.anchorMax = Vector2.one;
            _segmentsContainer.offsetMin = _segmentsContainer.offsetMax = Vector2.zero;
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = _spacing;
            hlg.childAlignment = TextAnchor.LowerLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
        }

        private void RebuildSegments()
        {
            EnsureContainer();
            // Nettoie les anciens.
            for (int i = _segmentsContainer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(_segmentsContainer.GetChild(i).gameObject);
            }
            _segments.Clear();

            for (int i = 0; i < _segmentCount; i++)
            {
                var seg = CreateSegment();
                _segments.Add(seg);
            }
            ApplyColorsToSegments();
            ApplySegmentsInstant(Normalized);
        }

        private Image CreateSegment()
        {
            GameObject go;
            if (_segmentPrefab != null)
            {
                go = Instantiate(_segmentPrefab, _segmentsContainer);
            }
            else
            {
                go = new GameObject($"Seg_{_segments.Count}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_segmentsContainer, false);
                var img = go.GetComponent<Image>();
                img.sprite = null;
                img.type = Image.Type.Sliced;
            }
            var image = go.GetComponent<Image>();
            image.color = _emptyColor;
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = _segmentThickness;
            return image;
        }

        private void ApplyColorsToSegments()
        {
            var fill = ResolveFillColor();
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i] == null) continue;
                bool filled = i < Mathf.RoundToInt(_displayedNormalized * _segmentCount);
                _segments[i].color = filled ? fill : _emptyColor;
            }
        }

        private void ApplySegmentsInstant(float normalized)
        {
            _displayedNormalized = Mathf.Clamp01(normalized);
            var fill = ResolveFillColor();
            int filledCount = Mathf.RoundToInt(_displayedNormalized * _segmentCount);
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i] == null) continue;
                bool filled = i < filledCount;
                var c = filled ? fill : _emptyColor;
                if (filled && _fillGlow > 0f)
                {
                    // Augmente la luminosité du segment actif pour un effet glow.
                    c = Color.Lerp(c, Color.white, _fillGlow * 0.25f);
                }
                _segments[i].color = c;
            }
        }

        private Color ResolveFillColor()
        {
            if (_customColor.HasValue) return _customColor.Value;
            return ThemeManager.ColorForStatBar(_type);
        }

        private void RefreshValueText()
        {
            if (_valueText == null) return;
            // Force la police Audiowide pour les chiffres.
            var tm = ThemeManager.Instance;
            if (tm != null && _valueText.font == null) _valueText.font = tm.GetFont(FontRole.Display);
            try
            {
                _valueText.text = string.Format(_valueFormat,
                    Mathf.RoundToInt(_currentValue),
                    Mathf.RoundToInt(_maxValue));
            }
            catch (FormatException)
            {
                _valueText.text = Mathf.RoundToInt(_currentValue).ToString();
            }
        }
    }
}
