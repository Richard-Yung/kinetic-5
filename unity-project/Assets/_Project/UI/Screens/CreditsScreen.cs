using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Générique de fin (scrolling credits).
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/CreditsScreen")]
    [DisallowMultipleComponent]
    public sealed class CreditsScreen : UIScreen
    {
        [Header("Scroll")]
        [Tooltip("Texte TMP du contenu credits (vertical scroll).")]
        [SerializeField] private TMP_Text _creditsText;
        [Tooltip("RectTransform du contenu (pour DOAnchorPos).")]
        [SerializeField] private RectTransform _creditsRect;
        [Tooltip("Durée du scroll (secondes).")]
        [SerializeField] private float _scrollDuration = 30f;
        [Tooltip("Vitesse de scroll de secours (pixels/sec) si pas de DOAnchorPos.")]
        [SerializeField] private float _scrollSpeed = 80f;

        [Header("Boutons")]
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;
        [Tooltip("Bouton SKIP.")]
        [SerializeField] private KButton _skipButton;

        private float _scrollY;

        protected override void Awake()
        {
            _screenType = ScreenType.Credits;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_creditsText != null)
            {
                _creditsText.text = BuildCreditsContent();
                _creditsText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _creditsText.color = ThemeManager.White;
                _creditsText.alignment = TextAlignmentOptions.Center;
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }
            if (_skipButton != null)
            {
                _skipButton.SetLocalizationKey("credits.skip", "SKIP");
                _skipButton.OnKClick += _ => OnBack();
            }
        }

        protected override void OnShow(object payload)
        {
            _scrollY = 0f;
            if (_creditsRect != null)
            {
                var start = new Vector2(0f, -Screen.height * 0.5f);
                var end = new Vector2(0f, _creditsRect.rect.height + Screen.height * 0.5f);
                _creditsRect.anchoredPosition = start;
                _creditsRect.DOAnchorPos(end, _scrollDuration)
                    .SetEase(Ease.Linear)
                    .SetUpdate(true)
                    .OnComplete(() => OnBack());
            }
            TrackClick("credits_show");
        }

        protected override void OnHide()
        {
            if (_creditsRect != null) _creditsRect.DOKill();
        }

        private void Update()
        {
            if (!IsVisible) return;
            // Fallback scroll continu si pas de tween.
            if (_creditsRect == null && _creditsText != null)
            {
                _scrollY += _scrollSpeed * Time.deltaTime;
                _creditsText.transform.localPosition = new Vector3(0f, _scrollY, 0f);
            }
        }

        // =================================================================================
        //  HELPERS
        // =================================================================================

        private string BuildCreditsContent()
        {
            return @"KINETICS 5

DEVELOPED BY
KINETICS STUDIO

GAME DIRECTOR
J. ARDEN

LEAD ENGINEER
M. RIVERA

LEAD DESIGNER
A. KOBAYASHI

ART DIRECTOR
L. MERCER

TECHNOLOGIES
Unity 6000 LTS
URP
TextMeshPro
DOTween
UniTask
Newtonsoft.Json
FMOD

SPECIAL THANKS
Our playtesters
The open-source community
Every operative on the frontier

PRESS BACK TO RETURN
";
        }

        private void OnBack()
        {
            TrackClick("credits_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Start);
        }

        public override bool HandleBack() { OnBack(); return true; }
    }
}
