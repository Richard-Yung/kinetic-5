using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Analytics;

namespace KINETICS5.Core
{
    /// <summary>
    /// Événement télémétrique structuré (sérialisable Newtonsoft).
    /// </summary>
    [Serializable]
    public struct TelemetryEvent
    {
        public string Name;
        public long UnixTimestamp;
        public Dictionary<string, object> Properties;
        public TelemetryEvent(string name) { Name = name; UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); Properties = new(); }
    }

    /// <summary>
    /// Wrapper analytics pour KINETICS 5.
    /// - Côté Unity Analytics (intégré) + stub PostHog (HTTP batch).
    /// - Envoi par lot toutes les 30s pour économiser la batterie mobile.
    /// - File d'attente offline (persistance PlayerPrefs) si pas de réseau.
    /// - Portail de consentement GDPR: aucun envoi avant consentement explicite.
    /// - Cappe la file à 1000 événements (LRU drop) pour éviter la mémoire.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TelemetryLogger : MonoBehaviour
    {
        private static TelemetryLogger _instance;
        public static TelemetryLogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[TelemetryLogger]");
                    _instance = go.AddComponent<TelemetryLogger>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Configuration")]
        [Tooltip("Active l'envoi analytics (post-consentement GDPR).")]
        [SerializeField] private bool _enabled = false;
        [Tooltip("Active le consentement GDPR (true = attendre l'accord).")]
        [SerializeField] private bool _gdprGateActive = true;
        [Tooltip("Intervalle d'envoi par lot en secondes.")]
        [Range(10f, 300f)][SerializeField] private float _batchInterval = 30f;
        [Tooltip("Taille max de la file offline (LRU drop au-delà).")]
        [Range(100, 5000)][SerializeField] private int _maxQueueSize = 1000;
        [Tooltip("Endpoint PostHog (stub).")]
        [SerializeField] private string _posthogEndpoint = "https://posthog.kinetics5.local/batch";
        [Tooltip("Clé projet PostHog (stub).")]
        [SerializeField] private string _posthogProjectKey = "phk_stub_k5";

        [Header("Performance")]
        [Tooltip("Active la collecte auto des métriques perf (FPS/mémoire).")]
        [SerializeField] private bool _collectPerfMetrics = true;
        [Tooltip("Intervalle entre deux samples perf (secondes).")]
        [Range(1f, 60f)][SerializeField] private float _perfSampleInterval = 10f;

        /// <summary>Statut du consentement GDPR (persisté en PlayerPrefs).</summary>
        public bool HasConsent { get; private set; }

        private readonly Queue<TelemetryEvent> _queue = new(256);
        private float _lastBatchTime;
        private float _lastPerfSample;
        private int _frameCount;
        private float _fpsAccumTime;
        private CancellationTokenSource _cts;

        // Clé PlayerPrefs pour la file offline.
        private const string OfflineQueueKey = "K5_Telemetry_OfflineQueue";
        private const string ConsentKey = "K5_Telemetry_Consent";

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadConsent();
            LoadOfflineQueue();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            if (_enabled) SaveOfflineQueue();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            _frameCount++;
            _fpsAccumTime += Time.unscaledDeltaTime;
            if (_collectPerfMetrics && Time.unscaledTime - _lastPerfSample >= _perfSampleInterval)
            {
                float fps = _frameCount / Mathf.Max(_fpsAccumTime, 0.0001f);
                _frameCount = 0; _fpsAccumTime = 0f;
                _lastPerfSample = Time.unscaledTime;
                var mem = System.Gc.GetTotalMemory(false) / (1024f * 1024f);
                Track("performance_metric", new()
                {
                    { "fps", (int)fps },
                    { "gc_mb", (float)System.Math.Round(mem, 2) },
                    { "device", SystemInfo.deviceModel }
                });
            }

            if (!_enabled || !HasConsent) return;
            if (Time.unscaledTime - _lastBatchTime >= _batchInterval)
            {
                _lastBatchTime = Time.unscaledTime;
                _ = SendBatchAsync();
            }
        }

        // --- API publique ---

        /// <summary>Définit le consentement GDPR (appelé par le portail opt-in).</summary>
        public void SetConsent(bool granted)
        {
            HasConsent = granted;
            PlayerPrefs.SetInt(ConsentKey, granted ? 1 : 0);
            PlayerPrefs.Save();
            _enabled = granted;
            Debug.Log($"[Telemetry] Consentement: {granted}");
        }

        /// <summary>Active/désactive la collecte à chaud (ex: opt-out depuis Settings).</summary>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled && HasConsent;
        }

        /// <summary>Log un événement avec propriétés optionnelles (pas d'alloc si props nulle).</summary>
        public void Track(string name, Dictionary<string, object> properties = null)
        {
            if (!_enabled || !HasConsent) return;
            if (string.IsNullOrEmpty(name)) return;
            var evt = new TelemetryEvent(name) { Properties = properties ?? new() };
            Enqueue(evt);
        }

        // --- Helpers d'événements standards (API typée) ---

        public void TrackSessionStart(string sessionId, string version)
        {
            Track("session_start", new() { { "session_id", sessionId }, { "app_version", version }, { "device", SystemInfo.deviceModel }, { "platform", Application.platform.ToString() } });
        }
        public void TrackMissionStart(string missionId, string agentId, int difficulty)
        {
            Track("mission_start", new() { { "mission_id", missionId }, { "agent", agentId }, { "difficulty", difficulty } });
        }
        public void TrackMissionComplete(string missionId, float duration, int score, bool perfect)
        {
            Track("mission_complete", new() { { "mission_id", missionId }, { "duration_s", (float)System.Math.Round(duration, 2) }, { "score", score }, { "perfect", perfect } });
        }
        public void TrackMissionFail(string missionId, string cause)
        {
            Track("mission_fail", new() { { "mission_id", missionId }, { "cause", cause } });
        }
        public void TrackEnemyKilled(string enemyType, string weaponId, bool critical)
        {
            Track("enemy_killed", new() { { "enemy_type", enemyType }, { "weapon", weaponId }, { "critical", critical } });
        }
        public void TrackWeaponUsed(string weaponId, int shotsFired)
        {
            Track("weapon_used", new() { { "weapon_id", weaponId }, { "shots", shotsFired } });
        }
        public void TrackPurchase(string itemId, int credits, string currency = "CR")
        {
            Track("purchase", new() { { "item_id", itemId }, { "price", credits }, { "currency", currency } });
        }
        public void TrackTutorialStep(int stepIndex, string stepName)
        {
            Track("tutorial_step", new() { { "step_index", stepIndex }, { "step_name", stepName } });
        }
        public void TrackUiClick(string screen, string element)
        {
            Track("ui_click", new() { { "screen", screen }, { "element", element } });
        }

        // --- Implémentation interne ---

        private void Enqueue(TelemetryEvent evt)
        {
            // LRU drop si file saturée (mobile-safe).
            while (_queue.Count >= _maxQueueSize) _queue.Dequeue();
            _queue.Enqueue(evt);
        }

        private async UniTask SendBatchAsync()
        {
            if (_queue.Count == 0) return;
            _cts ??= new CancellationTokenSource();
            var token = _cts.Token;

            // Snapshot de la file courante pour libérer le thread principal.
            var batch = new List<TelemetryEvent>(_queue.Count);
            while (_queue.Count > 0) batch.Add(_queue.Dequeue());

            try
            {
                // 1) Unity Analytics (natif, batching interne).
                foreach (var e in batch)
                {
                    try { Analytics.CustomEvent(e.Name, e.Properties); } catch { /* ignore */ }
                }

                // 2) PostHog stub (HTTP batch) - désactivé en mock-up.
                await UniTask.Delay(10, cancellationToken: token);
                Debug.Log($"[Telemetry] Batch envoyé: {batch.Count} événements.");
            }
            catch (OperationCanceledException) { /* OK */ }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Telemetry] Envoi échoué, remise en file: {ex.Message}");
                // Remet en file (offline).
                for (int i = batch.Count - 1; i >= 0; i--) Enqueue(batch[i]);
                SaveOfflineQueue();
            }
        }

        private void LoadConsent()
        {
            HasConsent = PlayerPrefs.GetInt(ConsentKey, 0) == 1;
            _enabled = HasConsent;
        }

        private void LoadOfflineQueue()
        {
            // La file offline est volontairement limitée en taille pour rester mobile-safe.
            if (!PlayerPrefs.HasKey(OfflineQueueKey)) return;
            try
            {
                var json = PlayerPrefs.GetString(OfflineQueueKey);
                var arr = Newtonsoft.Json.JsonConvert.DeserializeObject<List<TelemetryEvent>>(json);
                if (arr != null)
                {
                    for (int i = 0; i < arr.Count && i < _maxQueueSize; i++) _queue.Enqueue(arr[i]);
                }
                PlayerPrefs.DeleteKey(OfflineQueueKey);
            }
            catch (Exception ex) { Debug.LogWarning($"[Telemetry] Load offline queue échoué: {ex}"); }
        }

        private void SaveOfflineQueue()
        {
            if (_queue.Count == 0) return;
            try
            {
                var arr = new List<TelemetryEvent>(_queue.Count);
                while (_queue.Count > 0) arr.Add(_queue.Dequeue());
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(arr);
                PlayerPrefs.SetString(OfflineQueueKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex) { Debug.LogWarning($"[Telemetry] Save offline queue échoué: {ex}"); }
        }
    }
}
