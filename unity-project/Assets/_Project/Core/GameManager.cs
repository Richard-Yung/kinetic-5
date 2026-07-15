using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KINETICS5.Core
{
    /// <summary>
    /// États globaux du jeu. L'ordre de l'enum définit aussi l'ordre logique
    /// du cycle de vie (Boot -> MainMenu -> Loading -> InMission -> ...).
    /// </summary>
    public enum GameState
    {
        /// <summary>Phase de démarrage (initialisation des sous-systèmes).</summary>
        Boot,
        /// <summary>Menu principal (Start Screen + Lobby + sous-menus).</summary>
        MainMenu,
        /// <summary>Chargement d'une mission (splash + barre de progression).</summary>
        Loading,
        /// <summary>Mission en cours (gameplay actif).</summary>
        InMission,
        /// <summary>Jeu en pause (TimeManager gelé, UI pause visible).</summary>
        Paused,
        /// <summary>Écran de résultats (Victory/Defeat + Operation Summary).</summary>
        Results
    }

    /// <summary>
    /// Arguments passés lors du changement d'état du GameManager.
    /// </summary>
    public readonly struct GameStateChangedEventArgs
    {
        public readonly GameState Previous;
        public readonly GameState Current;
        public GameStateChangedEventArgs(GameState prev, GameState current) { Previous = prev; Current = current; }
    }

    /// <summary>
    /// Singleton racine de KINETICS 5. Gère la machine à états du jeu,
    /// coordonne les sous-systèmes (EventBus, SaveSystem, etc.) et expose
    /// l'état courant via un événement thread-safe.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        // --- Singleton ---
        private static GameManager _instance;
        /// <summary>Instance globale (peut être null avant le boot).</summary>
        public static GameManager Instance => _instance;
        /// <summary>Accès sûr : crée l'instance si absente (à n'utiliser qu'au démarrage).</summary>
        public static GameManager EnsureInstance()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[GameManager]");
            _instance = go.AddComponent<GameManager>();
            DontDestroyOnLoad(go);
            return _instance;
        }

        // --- État ---
        [Header("État courant")]
        [Tooltip("État actuel du jeu (lecture seule, modifié via RequestStateChange).")]
        [SerializeField] private GameState _currentState = GameState.Boot;
        /// <summary>État courant du jeu.</summary>
        public GameState CurrentState => _currentState;

        [Header("Configuration Boot")]
        [Tooltip("Durée minimale du splash boot (ms).")]
        [SerializeField] private int _minBootDurationMs = 1200;
        [Tooltip("Active le démarrage automatique via Bootstrapper (sinon appel manuel).")]
        [SerializeField] private bool _autoBoot = true;

        /// <summary>Événement publié sur le thread principal à chaque changement d'état.</summary>
        public event Action<GameStateChangedEventArgs> StateChanged;

        private GameState _requestedState = GameState.Boot;
        private CancellationTokenSource _bootCts;
        private float _missionStartTime;
        private string _pendingMissionId;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void OnDestroy()
        {
            _bootCts?.Cancel();
            _bootCts?.Dispose();
            _bootCts = null;
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Demande un changement d'état. La transition réelle peut être asynchrone
        /// (chargement de scène, fade). Retourne une UniTask complétée quand l'état est effectif.
        /// </summary>
        public async UniTask RequestStateChangeAsync(GameState newState, object payload = null)
        {
            if (newState == _currentState) return;
            _requestedState = newState;

            try
            {
                await TransitionToAsync(_currentState, newState, payload);
                var prev = _currentState;
                _currentState = newState;
                StateChanged?.Invoke(new GameStateChangedEventArgs(prev, newState));
                Debug.Log($"[GameManager] État: {prev} -> {newState}");
            }
            catch (OperationCanceledException) { /* Annulation propre */ }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Échec transition {newState}: {ex}");
            }
        }

        /// <summary>Variante fire-and-forget (pour les UI buttons).</summary>
        public void RequestStateChange(GameState newState, object payload = null)
        {
            RequestStateChangeAsync(newState, payload).Forget();
        }

        /// <summary>Sequence de démarrage appelée par le Bootstrapper.</summary>
        public async UniTask BootAsync(CancellationToken ct = default)
        {
            _bootCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = _bootCts.Token;

            var minBootTask = UniTask.Delay(_minBootDurationMs, cancellationToken: token);
            // Les sous-systèmes s'initialisent en parallèle via Bootstrapper.
            await minBootTask;

            if (token.IsCancellationRequested) return;
            await RequestStateChangeAsync(GameState.MainMenu);
        }

        /// <summary>Entre dans une mission. Appelé par le bouton PLAY du Lobby.</summary>
        public async UniTask StartMissionAsync(string missionId)
        {
            _pendingMissionId = missionId;
            await RequestStateChangeAsync(GameState.Loading, missionId);
            // SceneLoader effectue le chargement pendant Loading, puis appelle OnMissionLoaded.
        }

        /// <summary>Appelé par SceneLoader une fois la scène de mission chargée.</summary>
        public void OnMissionLoaded()
        {
            _missionStartTime = Time.unscaledTime;
            RequestStateChange(GameState.InMission);
        }

        /// <summary>Termine la mission (succès) et passe à Results.</summary>
        public void CompleteMission(int score, bool perfect)
        {
            var duration = Time.unscaledTime - _missionStartTime;
            GameEventBus.Instance.Publish(new MissionCompleteEvent(_pendingMissionId, duration, score, perfect));
            RequestStateChange(GameState.Results);
        }

        /// <summary>Met en pause / reprend.</summary>
        public void TogglePause()
        {
            if (_currentState == GameState.InMission)
                RequestStateChange(GameState.Paused);
            else if (_currentState == GameState.Paused)
                RequestStateChange(GameState.InMission);
        }

        /// <summary>Retour au menu principal depuis Results ou Pause.</summary>
        public void ReturnToMainMenu()
        {
            RequestStateChangeAsync(GameState.MainMenu).Forget();
        }

        // --- Logique de transition (hook pour SceneLoader, TimeManager, etc.) ---
        private async UniTask TransitionToAsync(GameState from, GameState to, object payload)
        {
            // Pause: geler le temps; InMission: dégeler.
            if (to == GameState.Paused) ServiceLocator.Instance?.Get<TimeManager>()?.SetGameplayPaused(true);
            if (from == GameState.Paused && to == GameState.InMission)
                ServiceLocator.Instance?.Get<TimeManager>()?.SetGameplayPaused(false);

            // Loading -> InMission est piloté par SceneLoader.OnMissionLoaded.
            if (to == GameState.Loading && payload is string missionId)
            {
                var sceneLoader = ServiceLocator.Instance?.Get<SceneLoader>();
                if (sceneLoader != null) await sceneLoader.LoadMissionAsync(missionId);
            }

            // InMission -> Results: décharge la scène mission.
            if (from == GameState.InMission && to == GameState.Results)
            {
                var sceneLoader = ServiceLocator.Instance?.Get<SceneLoader>();
                if (sceneLoader != null) await sceneLoader.UnloadMissionAsync();
            }

            // Vers MainMenu: décharge tout sauf la base.
            if (to == GameState.MainMenu && from != GameState.Boot)
            {
                var sceneLoader = ServiceLocator.Instance?.Get<SceneLoader>();
                if (sceneLoader != null) await sceneLoader.UnloadMissionAsync();
            }

            await UniTask.Yield();
        }
    }
}
