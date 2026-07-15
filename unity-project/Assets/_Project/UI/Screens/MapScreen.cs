using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Carte tactique en mission (overlay pleine page).
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/MapScreen")]
    [DisallowMultipleComponent]
    public sealed class MapScreen : UIScreen
    {
        [Header("Carte")]
        [Tooltip("Image brute de la carte (render texture ou sprite).")]
        [SerializeField] private RawImage _mapImage;
        [Tooltip("Conteneur des marqueurs d'intérêt (POI).")]
        [SerializeField] private RectTransform _markersContainer;
        [Tooltip("Prefab d'un marqueur POI.")]
        [SerializeField] private GameObject _markerPrefab;
        [Tooltip("Bouton ZOOM IN.")]
        [SerializeField] private KButton _zoomInButton;
        [Tooltip("Bouton ZOOM OUT.")]
        [SerializeField] private KButton _zoomOutButton;

        [Header("Légende")]
        [Tooltip("Texte de légende (objectifs, ennemis, alliés).")]
        [SerializeField] private TMP_Text _legendText;

        [Header("Navigation")]
        [Tooltip("Bouton CLOSE.")]
        [SerializeField] private KButton _closeButton;

        private float _zoom = 1f;

        protected override void Awake()
        {
            _screenType = ScreenType.Map;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_zoomInButton != null)
            {
                _zoomInButton.SetText("+");
                _zoomInButton.OnKClick += _ => Zoom(0.2f);
            }
            if (_zoomOutButton != null)
            {
                _zoomOutButton.SetText("-");
                _zoomOutButton.OnKClick += _ => Zoom(-0.2f);
            }
            if (_closeButton != null)
            {
                _closeButton.SetLocalizationKey("map.close", "CLOSE");
                _closeButton.OnKClick += _ => OnClose();
            }
            if (_legendText != null)
            {
                _legendText.text = "OBJECTIVE\nENEMY\nALLY\nLOOT";
                _legendText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _legendText.color = ThemeManager.White;
            }
        }

        protected override void OnShow(object payload)
        {
            BuildMarkers();
            TrackClick("map_show");
        }

        private void BuildMarkers()
        {
            if (_markersContainer == null || _markerPrefab == null) return;
            // Place 4 marqueurs factices.
            var positions = new Vector2[] { new(0f, 80f), new(-60f, -40f), new(80f, -60f), new(0f, 0f) };
            var colors = new Color[] { ThemeManager.SubYellow, ThemeManager.SubRed, ThemeManager.Main, ThemeManager.SubGreen };
            for (int i = 0; i < positions.Length; i++)
            {
                var go = Instantiate(_markerPrefab, _markersContainer);
                var rt = go.transform as RectTransform;
                if (rt != null) rt.anchoredPosition = positions[i];
                var img = go.GetComponent<Image>();
                if (img != null) img.color = colors[i];
            }
        }

        private void Zoom(float delta)
        {
            _zoom = Mathf.Clamp(_zoom + delta, 0.5f, 3f);
            if (_mapImage != null) _mapImage.rectTransform.localScale = Vector3.one * _zoom;
            if (_markersContainer != null) _markersContainer.localScale = Vector3.one * _zoom;
            TrackClick($"map_zoom_{_zoom:F1}");
        }

        private void OnClose()
        {
            TrackClick("map_close");
            _ = UIManager.Instance?.CloseTopModalAsync();
        }

        public override bool HandleBack() { OnClose(); return true; }
    }
}
