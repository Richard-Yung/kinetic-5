using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KINETICS5.Core
{
    /// <summary>
    /// Point d'entrée de KINETICS 5. Exécuté automatiquement au chargement du runtime Unity
    /// via <see cref="RuntimeInitializeOnLoadMethod"/>. Ordonnance l'initialisation des
    /// sous-systèmes du <see cref="ServiceLocator"/>, affiche le splash de boot, charge la
    /// dernière sauvegarde (ou crée une nouvelle partie) puis entre en état MainMenu.
    ///
    /// Graphe de dépendances (ordre topologique) :
    /// 1. ServiceLocator           (racine DI)
    /// 2. GameEventBus             (aucune dépendance)
    /// 3. SaveSystem               (aucune dépendance, IO)
    /// 4. ObjectPooler             (pré-chauffe prefabs)
    /// 5. LocalizationManager      (dépend SaveSystem pour langue)
    /// 6. AudioManager             (dépend SaveSystem pour volumes)
    /// 7. InputManager             (dépend SaveSystem pour rebinds)
    /// 8. CameraManager            (dépend InputManager)
    /// 9. TimeManager              (aucune dépendance)
    /// 10. TelemetryLogger         (dépend SaveSystem pour consent)
    /// 11. SceneLoader             (dépend GameManager)
    /// 12. GameManager             (dépend tous les précédents pour transitions)
    /// </summary>
    public static class Bootstrapper
    {
        private const string BootSceneName = "Boot";
        private const string BaseSceneName = "Base";

        /// <summary>
        /// Hook Unity exécuté avant la première scène (RuntimeInitializeLoadType.BeforeSceneLoad).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Debug.Log("[Bootstrapper] Démarrage KINETICS 5 ...");
            Application.runInBackground = false;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            BootstrapAsync().Forget();
        }

        private static async UniTaskVoid BootstrapAsync()
        {
            try
            {
                // 1) ServiceLocator (DI racine)
                var locator = ServiceLocator.Instance;
                locator.MarkInitialized();

                // 2) EventBus
                var eventBus = GameEventBus.Instance;
                locator.Register<GameEventBus>(eventBus);

                // 3) SaveSystem (IO, charge la dernière save ou crée un profil neuf)
                var save = SaveSystem.Instance;
                locator.Register<SaveSystem>(save);
                LoadDefaultSlot(save);

                // 4) ObjectPooler (pré-chauffe pools configurés)
                var pooler = ObjectPooler.Instance;
                pooler.PreWarmAll();
                locator.Register<ObjectPooler>(pooler);

                // 5) Localization (charge fallback EN + langue save)
                var loc = LocalizationManager.Instance;
                var lang = LocalizationManager.CodeToLanguage(save.ActiveData?.Settings.Language ?? "en");
                await loc.InitializeAsync(lang);
                locator.Register<LocalizationManager>(loc);

                // 6) AudioManager (applique volumes save)
                var audio = AudioManager.Instance;
                if (save.ActiveData != null)
                {
                    audio.SetBusVolume(AudioBus.Master, save.ActiveData.Settings.VolumeMaster);
                    audio.SetBusVolume(AudioBus.Music, save.ActiveData.Settings.VolumeMusic);
                    audio.SetBusVolume(AudioBus.Sfx, save.ActiveData.Settings.VolumeSfx);
                    audio.SetBusVolume(AudioBus.Voice, save.ActiveData.Settings.VolumeVoice);
                }
                locator.Register<AudioManager>(audio);

                // 7) InputManager (charge rebinds save)
                var input = InputManager.Instance;
                if (save.ActiveData != null) input.SetHaptics(save.ActiveData.Settings.HapticsEnabled);
                locator.Register<InputManager>(input);

                // 8) CameraManager
                var cam = CameraManager.Instance;
                locator.Register<CameraManager>(cam);

                // 9) TimeManager
                var time = TimeManager.Instance;
                locator.Register<TimeManager>(time);

                // 10) TelemetryLogger (consent + session_start)
                var telemetry = TelemetryLogger.Instance;
                locator.Register<TelemetryLogger>(telemetry);
                telemetry.TrackSessionStart(Guid.NewGuid().ToString("N"), Application.version);

                // 11) SceneLoader
                var sceneLoader = SceneLoader.Instance;
                locator.Register<SceneLoader>(sceneLoader);

                // 12) GameManager (toujours en dernier: orchestre les transitions)
                var game = GameManager.EnsureInstance();
                locator.Register<GameManager>(game);

                // Vérification graphe de dépendances.
                var required = new System.Collections.Generic.List<Type>(12)
                {
                    typeof(GameEventBus), typeof(SaveSystem), typeof(ObjectPooler),
                    typeof(LocalizationManager), typeof(AudioManager), typeof(InputManager),
                    typeof(CameraManager), typeof(TimeManager), typeof(TelemetryLogger),
                    typeof(SceneLoader), typeof(GameManager)
                };
                if (!locator.ValidateDependencies(required, out var report))
                {
                    Debug.LogError($"[Bootstrapper] Dépendances manquantes: {report}");
                }

                // Charge la scène de base persistante si non présente.
                await EnsureBaseSceneLoadedAsync();

                // Lance la séquence de boot du GameManager (splash -> MainMenu).
                await game.BootAsync();

                Debug.Log("[Bootstrapper] Initialisation terminée. État: " + game.CurrentState);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Bootstrapper] ÉCHEC CRITIQUE: {ex}");
                // En cas d'erreur, on bascule en mode dégradé: tentative de chargement MainMenu direct.
                try { GameManager.EnsureInstance().RequestStateChange(GameState.MainMenu); }
                catch { /* nothing more we can do */ }
            }
        }

        /// <summary>
        /// Charge le slot 0 par défaut. Si vide, initialise une nouvelle partie sur le slot 0
        /// sans sauvegarder immédiatement (sauvegarde explicite via SaveSystem.SaveImmediate).
        /// </summary>
        private static void LoadDefaultSlot(SaveSystem save)
        {
            try
            {
                const int defaultSlot = 0;
                if (save.SlotExists(defaultSlot))
                {
                    save.LoadSlot(defaultSlot);
                }
                else
                {
                    save.LoadSlot(defaultSlot); // crée un ActiveData neuf
                    save.MarkDirty();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Bootstrapper] Échec chargement save: {ex}");
            }
        }

        /// <summary>
        /// Charge additivement la scène de base persistante (UI globale, managers de scène)
        /// si elle n'est pas déjà chargée. Évite les dépendances circulaires avec SceneLoader.
        /// </summary>
        private static async UniTask EnsureBaseSceneLoadedAsync()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == BaseSceneName) return;
            }
            if (!Application.CanStreamedLevelBeLoaded(BaseSceneName))
            {
                Debug.LogWarning($"[Bootstrapper] Scène '{BaseSceneName}' absente du Build Settings. Ignoré.");
                return;
            }
            var op = SceneManager.LoadSceneAsync(BaseSceneName, LoadSceneMode.Additive);
            while (op != null && !op.isDone) await UniTask.Yield();
        }
    }
}
