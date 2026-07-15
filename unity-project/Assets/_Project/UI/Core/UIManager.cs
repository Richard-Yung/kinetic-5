using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Gestionnaire central des écrans UI KINETICS 5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Responsabilités</b> :
    /// <list type="bullet">
    /// <item>Registre des écrans par <see cref="ScreenType"/> (Dictionary).</item>
    /// <item>Transitions Show/Hide orchestrées via DOTween (délégué aux écrans).</item>
    /// <item>Pile modale pour les dialogues superposés (Settings, Pause...).</item>
    /// <item>Gestion du bouton BACK (escape / mobile back) : dépile les modales.</item>
    /// <item>Souscription à <see cref="GameManager.StateChanged"/> pour auto-show
    /// de l'écran correspondant à l'état global.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Usage typique</b> :
    /// <code>
    /// UIManager.Instance.RegisterScreen(ScreenType.Start, _startScreenPrefab);
    /// await UIManager.Instance.ShowAsync(ScreenType.Start);
    /// </code>
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour
    {
        // =================================================================================
        //  SINGLETON
        // =================================================================================

        private static UIManager _instance;
        /// <summary>Instance globale (peut être null avant le boot).</summary>
        public static UIManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[UIManager]");
                _instance = go.AddComponent<UIManager>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        // =================================================================================
        //  CONFIGURATION INSPECTOR
        // =================================================================================

        [Header("Racine UI")]
        [Tooltip("Canvas racine contenant tous les écrans. Si null, un Canvas auto est créé.")]
        [SerializeField] private Canvas _rootCanvas;
        [Tooltip("Parent des écrans plein-éran (non modaux).")]
        [SerializeField] private RectTransform _screenLayer;
        [Tooltip("Parent des écrans modaux (au-dessus de tout).")]
        [SerializeField] private RectTransform _modalLayer;
        [Tooltip("Backdrop semi-transparent affiché derrière les modales.")]
        [SerializeField] private Image _modalBackdrop;

        [Header("Comportement")]
        [Tooltip("Si vrai, cache automatiquement tous les écrans sauf celui visible.")]
        [SerializeField] private bool _hideOthersOnShow = true;
        [Tooltip("Si vrai, souscrit automatiquement aux changements d'état du GameManager.")]
        [SerializeField] private bool _autoBindGameManager = true;
        [Tooltip("Durée du fondu du backdrop modal (secondes).")]
        [Range(0.05f, 1f)][SerializeField] private float _backdropFadeDuration = 0.20f;

        [Header("Mapping d'état")]
        [Tooltip("Mapping état GameManager -> écran à afficher automatiquement.")]
        [SerializeField] private GameStateScreenBinding[] _stateBindings =
        {
            new(GameState.Boot, ScreenType.Boot),
            new(GameState.MainMenu, ScreenType.Start),
            new(GameState.Loading, ScreenType.Loading),
            new(GameState.InMission, ScreenType.HUD),
            new(GameState.Paused, ScreenType.Pause),
            new(GameState.Results, ScreenType.OperationSummary),
        };

        /// <summary>Liaison état -> écran (sérialisable pour l'inspecteur).</summary>
        [Serializable]
        public struct GameStateScreenBinding
        {
            public GameState State;
            public ScreenType Screen;
            public GameStateScreenBinding(GameState s, ScreenType sc) { State = s; Screen = sc; }
        }

        // =================================================================================
        //  ÉTAT INTERNE
        // =================================================================================

        private readonly Dictionary<ScreenType, UIScreen> _registry = new(32);
        private readonly Stack<UIScreen> _modalStack = new(8);
        private UIScreen _currentScreen;
        private bool _isTransitioning;

        /// <summary>Écran courant (haut de la pile non-modale).</summary>
        public UIScreen Current => _currentScreen;
        /// <summary>Vrai si une transition globale est en cours.</summary>
        public bool IsTransitioning => _isTransitioning;
        /// <summary>Écran racine.</summary>
        public Canvas RootCanvas => _rootCanvas;

        // =================================================================================
        //  CYCLE DE VIE
        // =================================================================================

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureInfrastructure();
        }

        private void OnEnable()
        {
            if (_autoBindGameManager && GameManager.Instance != null)
                GameManager.Instance.StateChanged += OnGameStateChanged;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.StateChanged -= OnGameStateChanged;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            // Bouton BACK : escape clavier ou bouton back mobile.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleBackButton();
            }
        }

        // =================================================================================
        //  API PUBLIQUE — REGISTRE
        // =================================================================================

        /// <summary>
        /// Enregistre un écran déjà instancié dans le registre.
        /// </summary>
        public void RegisterScreen(ScreenType type, UIScreen screen)
        {
            if (screen == null) { Debug.LogWarning($"[UIManager] Écran null pour {type}."); return; }
            screen.Owner = this;
            _registry[type] = screen;
            // Assure parentage correct.
            if (screen.transform.parent != _screenLayer && screen.transform.parent != _modalLayer)
            {
                screen.transform.SetParent(_screenLayer, false);
            }
            screen.HideImmediate();
        }

        /// <summary>
        /// Instancie un prefab d'écran et l'enregistre.
        /// </summary>
        public UIScreen RegisterPrefab(ScreenType type, UIScreen prefab)
        {
            if (prefab == null) { Debug.LogWarning($"[UIManager] Prefab null pour {type}."); return null; }
            var screen = Instantiate(prefab, _screenLayer);
            RegisterScreen(type, screen);
            return screen;
        }

        /// <summary>
        /// Tente de récupérer un écran enregistré (sans l'afficher).
        /// </summary>
        public bool TryGetScreen(ScreenType type, out UIScreen screen) => _registry.TryGetValue(type, out screen);

        /// <summary>Désenregistre un écran (le détruit si <paramref name="destroy"/> vrai).</summary>
        public void Unregister(ScreenType type, bool destroy = true)
        {
            if (!_registry.TryGetValue(type, out var screen)) return;
            if (destroy && screen != null) Destroy(screen.gameObject);
            _registry.Remove(type);
        }

        // =================================================================================
        //  API PUBLIQUE — AFFICHAGE
        // =================================================================================

        /// <summary>
        /// Affiche un écran (non-modal). Masque l'écran courant.
        /// </summary>
        public async UniTask ShowAsync(ScreenType type, object payload = null)
        {
            if (!_registry.TryGetValue(type, out var screen))
            {
                Debug.LogWarning($"[UIManager] Écran {type} non enregistré.");
                return;
            }
            if (_isTransitioning) return;
            _isTransitioning = true;

            try
            {
                var previous = _currentScreen;
                _currentScreen = screen;

                // Masquer le précédent (sauf si c'est le même).
                if (previous != null && previous != screen && _hideOthersOnShow)
                {
                    await previous.Hide();
                }

                // Reparenter vers la couche non-modale si nécessaire.
                if (screen.transform.parent != _screenLayer)
                    screen.transform.SetParent(_screenLayer, false);
                screen.transform.SetAsLastSibling();

                await screen.Show(payload);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// Affiche un écran en tant que modal (superposé, ne masque pas l'écran courant).
        /// </summary>
        public async UniTask ShowModalAsync(ScreenType type, object payload = null)
        {
            if (!_registry.TryGetValue(type, out var screen))
            {
                Debug.LogWarning($"[UIManager] Modal {type} non enregistré.");
                return;
            }
            if (_isTransitioning) return;
            _isTransitioning = true;
            try
            {
                // Reparenter vers la couche modale.
                if (screen.transform.parent != _modalLayer)
                    screen.transform.SetParent(_modalLayer, false);
                screen.transform.SetAsLastSibling();
                _modalStack.Push(screen);
                await ShowBackdropAsync();
                await screen.Show(payload);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// Ferme le modal au sommet de la pile.
        /// </summary>
        public async UniTask CloseTopModalAsync()
        {
            if (_modalStack.Count == 0) return;
            var screen = _modalStack.Pop();
            await screen.Hide();
            if (_modalStack.Count == 0)
                await HideBackdropAsync();
            else
                await ShowBackdropAsync();
        }

        /// <summary>
        /// Ferme tous les modals ouverts.
        /// </summary>
        public async UniTask CloseAllModalsAsync()
        {
            while (_modalStack.Count > 0)
            {
                var screen = _modalStack.Pop();
                await screen.Hide();
            }
            await HideBackdropAsync();
        }

        /// <summary>
        /// Cache l'écran courant (retour à un état "aucun écran").
        /// </summary>
        public async UniTask HideCurrentAsync()
        {
            if (_currentScreen == null) return;
            var s = _currentScreen;
            _currentScreen = null;
            await s.Hide();
        }

        // =================================================================================
        //  API PUBLIQUE — BACK
        // =================================================================================

        /// <summary>
        /// Traite la pression du bouton BACK. Retourne vrai si un écran a consommé l'événement.
        /// </summary>
        public bool HandleBackButton()
        {
            // 1) Modale au sommet ?
            if (_modalStack.Count > 0)
            {
                var top = _modalStack.Peek();
                if (top != null && top.HandleBack()) return true;
                _ = CloseTopModalAsync();
                return true;
            }
            // 2) Écran courant ?
            if (_currentScreen != null && _currentScreen.HandleBack()) return true;
            // 3) Si on est en Pause, demander le resume.
            var gm = GameManager.Instance;
            if (gm != null && gm.CurrentState == GameState.Paused)
            {
                gm.TogglePause();
                return true;
            }
            // 4) Si on est en MainMenu sur un sous-écran, retour au Start.
            if (gm != null && gm.CurrentState == GameState.MainMenu
                && _currentScreen != null
                && _currentScreen.ScreenType != ScreenType.Start
                && _currentScreen.ScreenType != ScreenType.Boot)
            {
                _ = ShowAsync(ScreenType.Start);
                return true;
            }
            return false;
        }

        // =================================================================================
        //  IMPLÉMENTATION INTERNE
        // =================================================================================

        private void EnsureInfrastructure()
        {
            if (_rootCanvas == null)
            {
                var go = new GameObject("[UI_RootCanvas]");
                go.transform.SetParent(transform, false);
                _rootCanvas = go.AddComponent<Canvas>();
                _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _rootCanvas.sortingOrder = 100;
                go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
                go.AddComponent<GraphicRaycaster>();
                ApplySafeArea(go);
            }
            if (_screenLayer == null)
            {
                var layer = new GameObject("ScreenLayer", typeof(RectTransform));
                layer.transform.SetParent(_rootCanvas.transform, false);
                _screenLayer = layer.GetComponent<RectTransform>();
                _screenLayer.anchorMin = Vector2.zero;
                _screenLayer.anchorMax = Vector2.one;
                _screenLayer.offsetMin = _screenLayer.offsetMax = Vector2.zero;
            }
            if (_modalLayer == null)
            {
                var layer = new GameObject("ModalLayer", typeof(RectTransform));
                layer.transform.SetParent(_rootCanvas.transform, false);
                _modalLayer = layer.GetComponent<RectTransform>();
                _modalLayer.anchorMin = Vector2.zero;
                _modalLayer.anchorMax = Vector2.one;
                _modalLayer.offsetMin = _modalLayer.offsetMax = Vector2.zero;
            }
            if (_modalBackdrop == null)
            {
                var bd = new GameObject("ModalBackdrop", typeof(Image));
                bd.transform.SetParent(_modalLayer, false);
                var rt = bd.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var img = bd.GetComponent<Image>();
                img.color = ThemeManager.Backdrop;
                img.raycastTarget = true;
                _modalBackdrop = img;
                _modalBackdrop.gameObject.SetActive(false);
            }
        }

        private void ApplySafeArea(GameObject go)
        {
            // Composant safe area (ajouté si absent) : applique le SafeArea du device.
            if (go.GetComponent<SafeAreaFitter>() == null) go.AddComponent<SafeAreaFitter>();
        }

        private async UniTask ShowBackdropAsync()
        {
            if (_modalBackdrop == null) return;
            _modalBackdrop.gameObject.SetActive(true);
            _modalBackdrop.color = new Color(0f, 0f, 0f, 0f);
            await _modalBackdrop.DOFade(ThemeManager.Backdrop.a, _backdropFadeDuration)
                .SetUpdate(true).ToUniTask();
        }

        private async UniTask HideBackdropAsync()
        {
            if (_modalBackdrop == null) return;
            await _modalBackdrop.DOFade(0f, _backdropFadeDuration).SetUpdate(true).ToUniTask();
            _modalBackdrop.gameObject.SetActive(false);
        }

        private void OnGameStateChanged(GameStateChangedEventArgs args)
        {
            // Mappe l'état GameManager -> ScreenType (auto-show).
            foreach (var b in _stateBindings)
            {
                if (b.State == args.Current)
                {
                    _ = ShowAsync(b.Screen, payload: null);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Composant utilitaire appliquant le SafeArea du device (encoches, bords arrondis).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rt;
        private Rect _lastSafeArea;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            Refresh();
        }

        private void OnRectTransformDimensionsChange()
        {
            Refresh();
        }

        private void Refresh()
        {
            var safe = Screen.safeArea;
            if (safe == _lastSafeArea) return;
            _lastSafeArea = safe;

            var parentSize = Vector2.zero;
            var parent = _rt.parent as RectTransform;
            if (parent != null) parentSize = parent.rect.size;
            if (parentSize.x <= 0f || parentSize.y <= 0f) return;

            // Convertit le safe-area (pixels) en coordonnées anchor 0..1.
            var xMin = safe.xMin / Screen.width;
            var xMax = safe.xMax / Screen.width;
            var yMin = safe.yMin / Screen.height;
            var yMax = safe.yMax / Screen.height;
            _rt.anchorMin = new Vector2(xMin, yMin);
            _rt.anchorMax = new Vector2(xMax, yMax);
            _rt.offsetMin = _rt.offsetMax = Vector2.zero;
        }
    }
}
