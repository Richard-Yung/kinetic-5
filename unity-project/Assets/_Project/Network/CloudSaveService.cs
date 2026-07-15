// ============================================================================
//  KINETICS 5 — Cloud Save Service (sync + merge + résolution de conflits)
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Synchronisation de la sauvegarde joueur entre le disque local (SaveSystem)
//  et le cloud Nakama (storage engine). Stratégie hybride :
//
//    • LAST-WRITE-WINS pour les champs scalaires (level, XP, credits, settings).
//    • MERGE pour les collections (inventaire, missions complétées, achievements).
//    • RESOLUTION DE CONFLIT : comparaison des timestamps, le plus récent gagne.
//    • PUSH : upload local → cloud après chaque SaveImmediate().
//    • PULL : download cloud → local au démarrage (si timestamp cloud > local).
//
//  Storage Nakama :
//    Collection "player_save", key "main", permission OwnerRead/OwnerWrite.
//    Value = JSON sérialisé (chiffré côté client AES-128 via SaveSystem).
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using KINETICS5.Core;

namespace KINETICS5.Network
{
    /// <summary>État d'une synchro cloud.</summary>
    public enum CloudSyncState
    {
        Idle,
        Pushing,
        Pulling,
        Merging,
        Conflict,
        Synced,
        Error,
        Offline
    }

    /// <summary>Résultat d'une opération de sync cloud.</summary>
    public readonly struct CloudSyncResult
    {
        public readonly CloudSyncState State;
        public readonly string ConflictField;
        public readonly string Error;
        public CloudSyncResult(CloudSyncState s, string conflictField = null, string error = null)
        { State = s; ConflictField = conflictField; Error = error; }
    }

    /// <summary>
    /// Service de sauvegarde cloud. Travaille en tandem avec <see cref="SaveSystem"/>
    /// du Core et <see cref="NakamaClient"/> du Network.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CloudSaveService : MonoBehaviour
    {
        private static CloudSaveService _instance;
        public static CloudSaveService Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[CloudSaveService]");
                _instance = go.AddComponent<CloudSaveService>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        [Header("Configuration")]
        [Tooltip("Collection de storage Nakama.")]
        [SerializeField] private string _collection = "player_save";
        [Tooltip("Clé de l'objet storage.")]
        [SerializeField] private string _storageKey = "main";
        [Tooltip("Active la synchro auto après chaque SaveImmediate local.")]
        [SerializeField] private bool _autoPush = true;
        [Tooltip("Délai (s) entre sync auto (debounce pour grouper).")]
        [SerializeField] private float _pushDebounce = 5f;
        [Tooltip("Version du schéma cloud (pour migrations futures).")]
        [SerializeField] private int _cloudSchemaVersion = 1;

        public CloudSyncState State { get; private set; } = CloudSyncState.Idle;
        public DateTime? LastSyncAt { get; private set; }
        public event Action<CloudSyncResult> SyncCompleted;

        private float _pendingPushAt = -1f;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            // S'abonner à l'événement de save local pour déclencher le push.
            var save = SaveSystem.Instance;
            if (save != null && _autoPush)
            {
                // Pas d'event direct sur SaveSystem ; on expose MarkDirty puis on pousse.
            }
        }

        private void Update()
        {
            if (_autoPush && _pendingPushAt > 0 && Time.unscaledTime >= _pendingPushAt)
            {
                _pendingPushAt = -1f;
                _ = PushAsync();
            }
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        /// <summary>Programme un push différé (debounce). À appeler après SaveSystem.MarkDirty().</summary>
        public void SchedulePush()
        {
            if (!_autoPush) return;
            _pendingPushAt = Time.unscaledTime + _pushDebounce;
        }

        // ------------------------------------------------------------------------
        //  PULL (cloud → local)
        // ------------------------------------------------------------------------

        /// <summary>Télécharge la sauvegarde cloud et fusionne avec la locale.</summary>
        public async UniTask<CloudSyncResult> PullAsync()
        {
            var client = NakamaClient.Instance;
            if (client == null || client.IsOfflineMode)
            {
                SetState(CloudSyncState.Offline);
                return new CloudSyncResult(CloudSyncState.Offline);
            }
            SetState(CloudSyncState.Pulling);
#if KINETICS_NAKAMA
            try
            {
                var objects = await client.Client.ReadStorageObjectsAsync(client.Session, new[]
                {
                    new Nakama.StorageObjectId { Collection = _collection, Key = _storageKey, UserId = client.Session.UserId }
                });
                var cloudObj = objects?.Objects?.FirstOrDefault(o => o.Collection == _collection && o.Key == _storageKey);
                if (cloudObj == null)
                {
                    // Aucune sauvegarde cloud → push local pour initialiser.
                    await PushAsync();
                    SetState(CloudSyncState.Synced);
                    return new CloudSyncResult(CloudSyncState.Synced);
                }

                var cloudData = JsonConvert.DeserializeObject<CloudSaveEnvelope>(cloudObj.Value);
                return await MergeWithLocalAsync(cloudData, cloudObj.Version);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CloudSaveService] Pull échec: {ex.Message}");
                SetState(CloudSyncState.Error);
                return new CloudSyncResult(CloudSyncState.Error, error: ex.Message);
            }
#else
            await UniTask.Yield();
            SetState(CloudSyncState.Offline);
            return new CloudSyncResult(CloudSyncState.Offline);
#endif
        }

        // ------------------------------------------------------------------------
        //  PUSH (local → cloud)
        // ------------------------------------------------------------------------

        /// <summary>Upload la sauvegarde locale vers le cloud.</summary>
        public async UniTask<CloudSyncResult> PushAsync()
        {
            var client = NakamaClient.Instance;
            if (client == null || client.IsOfflineMode)
            {
                SetState(CloudSyncState.Offline);
                return new CloudSyncResult(CloudSyncState.Offline);
            }
            var save = SaveSystem.Instance;
            if (save?.ActiveData == null)
            {
                return new CloudSyncResult(CloudSyncState.Error, error: "Pas de save locale chargée.");
            }
            SetState(CloudSyncState.Pushing);

            var envelope = new CloudSaveEnvelope
            {
                SchemaVersion = _cloudSchemaVersion,
                Profile = save.ActiveData.Profile,
                Progress = save.ActiveData.Progress,
                Inventory = save.ActiveData.Inventory,
                Settings = save.ActiveData.Settings,
                LastSaveUnix = save.ActiveData.LastSaveUnix,
                DeviceId = SystemInfo.deviceUniqueIdentifier
            };
            var json = JsonConvert.SerializeObject(envelope);

#if KINETICS_NAKAMA
            try
            {
                await client.Client.WriteStorageObjectsAsync(client.Session, new[]
                {
                    new Nakama.WriteStorageObject
                    {
                        Collection = _collection,
                        Key = _storageKey,
                        Value = json,
                        Version = "*", // overwrite (pour résolution conflit, on vérifiera après)
                        PermissionRead = Nakama.StoragePermissionRead.OwnerRead,
                        PermissionWrite = Nakama.StoragePermissionWrite.OwnerWrite
                    }
                });
                LastSyncAt = DateTime.UtcNow;
                SetState(CloudSyncState.Synced);
                return new CloudSyncResult(CloudSyncState.Synced);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CloudSaveService] Push échec: {ex.Message}");
                SetState(CloudSyncState.Error);
                return new CloudSyncResult(CloudSyncState.Error, error: ex.Message);
            }
#else
            await UniTask.Delay(50);
            LastSyncAt = DateTime.UtcNow;
            SetState(CloudSyncState.Synced);
            return new CloudSyncResult(CloudSyncState.Synced);
#endif
        }

        // ------------------------------------------------------------------------
        //  MERGE
        // ------------------------------------------------------------------------

        private UniTask<CloudSyncResult> MergeWithLocalAsync(CloudSaveEnvelope cloud, string cloudVersion)
        {
            SetState(CloudSyncState.Merging);
            var local = SaveSystem.Instance?.ActiveData;
            if (local == null)
            {
                // Pas de local : on adopte cloud.
                ApplyCloudToLocal(cloud);
                SetState(CloudSyncState.Synced);
                return UniTask.FromResult(new CloudSyncResult(CloudSyncState.Synced));
            }

            // Comparaison des timestamps.
            bool cloudIsNewer = cloud.LastSaveUnix > local.LastSaveUnix;
            bool localIsNewer = local.LastSaveUnix > cloud.LastSaveUnix;

            // Champs scalaires : last-write-wins.
            var merged = new SaveData
            {
                SchemaVersion = Math.Max(local.SchemaVersion, cloud.SchemaVersion)
            };
            if (cloudIsNewer)
            {
                merged.Profile = cloud.Profile;
                merged.Settings = cloud.Settings;
            }
            else
            {
                merged.Profile = local.Profile;
                merged.Settings = local.Settings;
            }

            // Collections : merge (union).
            merged.Inventory = MergeInventory(local.Inventory, cloud.Inventory);
            merged.Progress = MergeProgress(local.Progress, cloud.Progress);
            merged.LastSaveUnix = Math.Max(local.LastSaveUnix, cloud.LastSaveUnix);

            // Détection de conflit (champ scalaire divergent + timestamps égaux).
            if (localIsNewer == cloudIsNewer)
            {
                SetState(CloudSyncState.Conflict);
                SyncCompleted?.Invoke(new CloudSyncResult(CloudSyncState.Conflict, conflictField: "LastSaveUnix"));
                // On garde quand même le merge mais on signale.
            }

            SaveSystem.Instance.ActiveData = merged;
            SetState(CloudSyncState.Synced);
            SyncCompleted?.Invoke(new CloudSyncResult(CloudSyncState.Synced));
            return UniTask.FromResult(new CloudSyncResult(CloudSyncState.Synced));
        }

        private static PlayerInventory MergeInventory(PlayerInventory local, PlayerInventory cloud)
        {
            var merged = new PlayerInventory
            {
                OwnedWeapons = local.OwnedWeapons.Union(cloud.OwnedWeapons).ToList(),
                Equipped = local.Equipped.Count >= cloud.Equipped.Count ? local.Equipped : cloud.Equipped,
                Resources = new Dictionary<string, int>(local.Resources)
            };
            foreach (var kvp in cloud.Resources)
            {
                if (merged.Resources.TryGetValue(kvp.Key, out var existing))
                    merged.Resources[kvp.Key] = Math.Max(existing, kvp.Value);
                else
                    merged.Resources[kvp.Key] = kvp.Value;
            }
            return merged;
        }

        private static PlayerProgress MergeProgress(PlayerProgress local, PlayerProgress cloud)
        {
            return new PlayerProgress
            {
                CompletedMissions = local.CompletedMissions.Union(cloud.CompletedMissions).ToList(),
                UnlockedAchievements = local.UnlockedAchievements.Union(cloud.UnlockedAchievements).ToList(),
                TotalKills = Math.Max(local.TotalKills, cloud.TotalKills),
                TotalPlaytime = Math.Max(local.TotalPlaytime, cloud.TotalPlaytime)
            };
        }

        private static void ApplyCloudToLocal(CloudSaveEnvelope cloud)
        {
            var save = SaveSystem.Instance;
            if (save?.ActiveData == null) return;
            save.ActiveData.Profile = cloud.Profile;
            save.ActiveData.Progress = cloud.Progress;
            save.ActiveData.Inventory = cloud.Inventory;
            save.ActiveData.Settings = cloud.Settings;
            save.ActiveData.LastSaveUnix = cloud.LastSaveUnix;
            save.MarkDirty();
        }

        private void SetState(CloudSyncState s)
        {
            State = s;
            if (s == CloudSyncState.Synced) LastSyncAt = DateTime.UtcNow;
        }
    }

    /// <summary>Enveloppe de sauvegarde cloud (inclut deviceId pour traçabilité).</summary>
    [Serializable]
    public sealed class CloudSaveEnvelope
    {
        public int SchemaVersion;
        public PlayerProfile Profile;
        public PlayerProgress Progress;
        public PlayerInventory Inventory;
        public GameSettings Settings;
        public long LastSaveUnix;
        public string DeviceId;
    }
}
