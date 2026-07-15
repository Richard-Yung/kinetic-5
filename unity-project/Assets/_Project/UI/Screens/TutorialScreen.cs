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
    /// Tutoriel interactif — overlay étape par étape.
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/TutorialScreen")]
    [DisallowMultipleComponent]
    public sealed class TutorialScreen : UIScreen
    {
        [Header("Overlay")]
        [Tooltip("Image backdrop semi-transparent.")]
        [SerializeField] private Image _backdrop;
        [Tooltip("RectTransform du spotlight (trou dans le backdrop).")]
        [SerializeField] private RectTransform _spotlight;
        [Tooltip("Tooltip panel (texte + flèche).")]
        [SerializeField] private RectTransform _tooltipPanel;
        [Tooltip("Texte TMP du tooltip.")]
        [SerializeField] private TMP_Text _tooltipText;
        [Tooltip("Indicateur d'étape (3/8).")]
        [SerializeField] private TMP_Text _stepIndicator;

        [Header("Boutons")]
        [Tooltip("Bouton NEXT.")]
        [SerializeField] private KButton _nextButton;
        [Tooltip("Bouton SKIP.")]
        [SerializeField] private KButton _skipButton;

        /// <summary>Étape de tutoriel.</summary>
        [System.Serializable]
        public struct TutorialStep
        {
            [TextArea] public string Message;
            [Tooltip("Position du spotlight (RectTransform cible). Peut être null (centré).")]
            public RectTransform Target;
            [Tooltip("Position du tooltip par rapport au spotlight.")]
            public TooltipAnchor Anchor;
        }

        /// <summary>Position du tooltip par rapport au spotlight.</summary>
        public enum TooltipAnchor { Top, Bottom, Left, Right, Center }

        [Header("Données")]
        [Tooltip("Étapes du tutoriel.")]
        [SerializeField] private TutorialStep[] _steps;
        [Tooltip("Index de l'étape courante.")]
        [SerializeField] private int _currentIndex;

        protected override void Awake()
        {
            _screenType = ScreenType.Tutorial;
            _enterTransition = ScreenTransition.Fade;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_nextButton != null)
            {
                _nextButton.SetLocalizationKey("tutorial.next", "NEXT");
                _nextButton.OnKClick += _ => NextStep();
            }
            if (_skipButton != null)
            {
                _skipButton.SetLocalizationKey("tutorial.skip", "SKIP");
                _skipButton.OnKClick += _ => SkipAll();
            }
            if (_backdrop != null) _backdrop.color = ThemeManager.Backdrop;
        }

        protected override void OnShow(object payload)
        {
            _currentIndex = 0;
            if (_steps == null || _steps.Length == 0)
            {
                _steps = BuildDefaultSteps();
            }
            ShowStep(0);
            TrackClick("tutorial_show");
        }

        // =================================================================================
        //  NAVIGATION
        // =================================================================================

        private void NextStep()
        {
            if (_currentIndex >= _steps.Length - 1)
            {
                FinishTutorial();
                return;
            }
            _currentIndex++;
            ShowStep(_currentIndex);
        }

        private void SkipAll()
        {
            TrackClick("tutorial_skip");
            FinishTutorial();
        }

        private void ShowStep(int index)
        {
            if (_steps == null || index < 0 || index >= _steps.Length) return;
            var step = _steps[index];

            if (_tooltipText != null)
            {
                _tooltipText.text = step.Message;
                _tooltipText.font = ThemeManager.Instance.GetFont(FontRole.Body);
                _tooltipText.color = ThemeManager.White;
            }
            if (_stepIndicator != null)
            {
                _stepIndicator.text = $"{index + 1}/{_steps.Length}";
                _stepIndicator.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _stepIndicator.color = ThemeManager.Main;
            }

            // Positionne le spotlight sur la cible (ou centré).
            if (_spotlight != null)
            {
                if (step.Target != null)
                {
                    _spotlight.position = step.Target.position;
                    _spotlight.sizeDelta = step.Target.rect.size + new Vector2(40f, 40f);
                }
                else
                {
                    _spotlight.anchoredPosition = Vector2.zero;
                    _spotlight.sizeDelta = new Vector2(200f, 200f);
                }
            }

            // Positionne le tooltip.
            PositionTooltip(step.Anchor);

            // Telemetry.
            TelemetryLogger.Instance?.TrackTutorialStep(index, step.Message);
        }

        private void PositionTooltip(TooltipAnchor anchor)
        {
            if (_tooltipPanel == null) return;
            var rt = _tooltipPanel;
            switch (anchor)
            {
                case TooltipAnchor.Top:
                    rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(0f, -80f);
                    break;
                case TooltipAnchor.Bottom:
                    rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, 80f);
                    break;
                case TooltipAnchor.Left:
                    rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(1f, 0.5f);
                    rt.anchoredPosition = new Vector2(80f, 0f);
                    break;
                case TooltipAnchor.Right:
                    rt.anchorMin = new Vector2(1f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(-80f, 0f);
                    break;
                case TooltipAnchor.Center:
                default:
                    rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, -200f);
                    break;
            }
        }

        private void FinishTutorial()
        {
            _ = UIManager.Instance?.CloseTopModalAsync();
            // Marque le tutoriel comme terminé (SaveSystem).
            PlayerPrefs.SetInt("K5_TutorialDone", 1);
            PlayerPrefs.Save();
        }

        private TutorialStep[] BuildDefaultSteps()
        {
            return new TutorialStep[]
            {
                new() { Message = "Welcome to KINETICS 5. Tap the MISSIONS button to start.", Anchor = TooltipAnchor.Center },
                new() { Message = "Choose your agent in the LOADOUT screen.", Anchor = TooltipAnchor.Center },
                new() { Message = "Press PLAY to deploy on your first mission.", Anchor = TooltipAnchor.Center },
            };
        }

        public override bool HandleBack()
        {
            SkipAll();
            return true;
        }
    }
}
