using System;
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
    /// Écran d'accueil (Start Screen) — PDF page 2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Spécifications PDF page 2</b> :
    /// <list type="bullet">
    /// <item>Boutons : NEW GAME, CONTINUE, LOAD GAME, OPTIONS, QUIT.</item>
    /// <item>Fond : scène spatiale (planète, débris, vaisseaux) + personnage blindé.</item>
    /// <item>Logo KINETICS 5 (Audiowide).</item>
    /// <item>Arrière-plan animé (parallax étoiles).</item>
    /// </list>
    /// </para>
    /// </remarks>
    [AddComponentMenu("KINETICS 5/Screens/StartScreen")]
    [DisallowMultipleComponent]
    public sealed class StartScreen : UIScreen
    {
        [Header("Identité écran")]
        [SerializeField] private ScreenType _overrideType = ScreenType.Start;

        [Header("Logo")]
        [Tooltip("Logo KINETICS 5 (Image).")]
        [SerializeField] private Image _logoImage;
        [Tooltip("Texte KINETICS 5 si pas d'image logo.")]
        [SerializeField] private TMP_Text _logoText;

        [Header("Boutons menu")]
        [SerializeField] private KButton _newGameButton;
        [SerializeField] private KButton _continueButton;
        [SerializeField] private KButton _loadGameButton;
        [SerializeField] private KButton _optionsButton;
        [SerializeField] private KButton _quitButton;

        [Header("Arrière-plan parallax")]
        [Tooltip("Layers de parallax (loin -> proche).")]
        [SerializeField] private RectTransform[] _parallaxLayers;
        [Tooltip("Vitesse de parallax (par layer, 0..1).")]
        [SerializeField] private float[] _parallaxSpeeds;
        [Tooltip("Image du personnage blindé au premier plan.")]
        [SerializeField] private RectTransform _characterRender;

        [Header("Audio")]
        [Tooltip("Musique BGM du menu.")]
        [SerializeField] private AudioClip _menuMusic;

        [Header("Version")]
        [Tooltip("Texte de version (mono, en bas).")]
        [SerializeField] private TMP_Text _versionText;

        protected override void Awake()
        {
            _screenType = _overrideType;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            BindButton(_newGameButton, "menu.new_game", "NEW GAME", OnNewGame);
            BindButton(_continueButton, "menu.continue", "CONTINUE", OnContinue);
            BindButton(_loadGameButton, "menu.load_game", "LOAD GAME", OnLoadGame);
            BindButton(_optionsButton, "menu.options", "OPTIONS", OnOptions);
            BindButton(_quitButton, "menu.quit", "QUIT", OnQuit);

            if (_logoText != null)
            {
                _logoText.text = "KINETICS 5";
                _logoText.font = ThemeManager.Instance.GetFont(FontRole.Display);
                _logoText.color = ThemeManager.Main;
            }
            if (_versionText != null)
            {
                _versionText.text = $"v{Application.version}";
                _versionText.font = ThemeManager.Instance.GetFont(FontRole.Mono);
                _versionText.color = ThemeManager.TextMuted;
            }
        }

        protected override void OnShow(object payload)
        {
            // Lance le BGM du menu.
            if (_menuMusic != null)
            {
                _ = AudioManager.Instance?.PlayMusicAsync(_menuMusic);
            }
            // Anime le logo (pulse léger cyan).
            if (_logoImage != null)
            {
                _logoImage.transform.DOScale(1.02f, 1.8f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true);
            }
            TrackClick("start_show");
        }

        protected override void OnHide()
        {
            if (_logoImage != null) _logoImage.transform.DOKill();
        }

        private void Update()
        {
            if (!IsVisible) return;
            UpdateParallax();
        }

        private void UpdateParallax()
        {
            if (_parallaxLayers == null || _parallaxSpeeds == null) return;
            // Utilise la position souris (écran) ou un drift automatique.
            Vector2 input;
            if (Input.mousePresent)
            {
                input = new Vector2(
                    (Input.mousePosition.x / Screen.width - 0.5f) * 2f,
                    (Input.mousePosition.y / Screen.height - 0.5f) * 2f);
            }
            else
            {
                input = new Vector2(Mathf.Sin(Time.unscaledTime * 0.1f), 0f);
            }
            int count = _parallaxLayers.Length;
            for (int i = 0; i < count; i++)
            {
                if (_parallaxLayers[i] == null) continue;
                float speed = (i < _parallaxSpeeds.Length) ? _parallaxSpeeds[i] : 0.2f;
                var pos = _parallaxLayers[i].anchoredPosition;
                var target = new Vector2(-input.x * 30f * speed, -input.y * 20f * speed);
                _parallaxLayers[i].anchoredPosition = Vector2.Lerp(pos, target, Time.deltaTime * 2f);
            }
            if (_characterRender != null)
            {
                var pos = _characterRender.anchoredPosition;
                var target = new Vector2(-input.x * 8f, -input.y * 5f);
                _characterRender.anchoredPosition = Vector2.Lerp(pos, target, Time.deltaTime * 2f);
            }
        }

        // =================================================================================
        //  HANDLERS BOUTONS
        // =================================================================================

        private void OnNewGame(KButton _)
        {
            TrackClick("start_new_game");
            _ = GameManager.Instance?.RequestStateChangeAsync(GameState.MainMenu);
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        private void OnContinue(KButton _)
        {
            TrackClick("start_continue");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        private void OnLoadGame(KButton _)
        {
            TrackClick("start_load_game");
            // Ouvre une modal de sélection de slot.
            _ = UIManager.Instance?.ShowModalAsync(ScreenType.Mail); // placeholder : utiliser un SaveSlotScreen dédié si disponible
        }

        private void OnOptions(KButton _)
        {
            TrackClick("start_options");
            _ = UIManager.Instance?.ShowModalAsync(ScreenType.Settings);
        }

        private void OnQuit(KButton _)
        {
            TrackClick("start_quit");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // =================================================================================
        //  HELPERS
        // =================================================================================

        private void BindButton(KButton button, string key, string fallback, Action<KButton> handler)
        {
            if (button == null) return;
            button.SetLocalizationKey(key, fallback);
            button.OnKClick += handler;
        }
    }
}
