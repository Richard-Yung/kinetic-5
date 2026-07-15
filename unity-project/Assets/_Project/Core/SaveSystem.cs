using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace KINETICS5.Core
{
    /// <summary>
    /// Schéma de sauvegarde versionné. Toute évolution du schéma nécessite un bump de
    /// <see cref="SchemaVersion"/> et une migration dans <see cref="Migrate"/>.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentSchemaVersion = 3;

        /// <summary>Version du schéma à l'écriture.</summary>
        public int SchemaVersion = CurrentSchemaVersion;
        /// <summary>Profil joueur (nom, agent sélectionné, niveau, etc.).</summary>
        public PlayerProfile Profile = new();
        /// <summary>Progression (missions complétées, succès, stats).</summary>
        public PlayerProgress Progress = new();
        /// <summary>Inventaire (armes, gadgets, ressources).</summary>
        public PlayerInventory Inventory = new();
        /// <summary>Paramètres (audio, graphismes, langue, contrôles).</summary>
        public GameSettings Settings = new();
        /// <summary>Horodatage Unix de la dernière sauvegarde (secondes).</summary>
        public long LastSaveUnix;
    }

    [Serializable] public sealed class PlayerProfile { public string DisplayName = "Operative"; public string SelectedAgent = "VULCAN"; public int PlayerLevel = 1; public long Xp; public long Credits; }
    [Serializable] public sealed class PlayerProgress { public List<string> CompletedMissions = new(); public List<string> UnlockedAchievements = new(); public int TotalKills; public float TotalPlaytime; }
    [Serializable] public sealed class PlayerInventory { public List<string> OwnedWeapons = new() { "RIFLE_CX_24", "GUARD_V_9", "FRAG_X" }; public List<string> Equipped = new() { "RIFLE_CX_24", "GUARD_V_9", "FRAG_X" }; public Dictionary<string, int> Resources = new(); }
    [Serializable] public sealed class GameSettings { public string Language = "fr"; public float VolumeMaster = 1f; public float VolumeMusic = 0.8f; public float VolumeSfx = 1f; public float VolumeVoice = 1f; public int Difficulty = 1; public int QualityLevel = 2; public bool HapticsEnabled = true; }

    /// <summary>
    /// Système de sauvegarde de KINETICS 5.
    /// - 3 slots (Slot0, Slot1, Slot2)
    /// - JSON sérialisé (Newtonsoft.Json) sur disque + fallback PlayerPrefs
    /// - Chiffrement AES-128 des données sensibles (profil, progression)
    /// - Auto-save toutes les 60s en arrière-plan
    /// - Format versionné avec migrations (v1 -> v2 -> v3)
    /// - Synchronisation cloud (stub configurable)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaveSystem : MonoBehaviour
    {
        private static SaveSystem _instance;
        /// <summary>Instance globale.</summary>
        public static SaveSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[SaveSystem]");
                    _instance = go.AddComponent<SaveSystem>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Configuration")]
        [Tooltip("Clé AES-128 (16 octets hex). En prod: stockée via Android Keystore / iOS Keychain.")]
        [SerializeField] private string _aesKeyHex = "0123456789ABCDEF0123456789ABCDEF";
        [Tooltip("Salt d'IV AES (16 octets hex).")]
        [SerializeField] private string _aesIvHex = "FEDCBA9876543210FEDCBA9876543210";
        [Tooltip("Intervalle d'auto-save en secondes.")]
        [Min(10f)][SerializeField] private float _autoSaveInterval = 60f;
        [Tooltip("Active la synchro cloud (stub).")]
        [SerializeField] private bool _enableCloudSync = false;

        [Header("Slots")]
        [Tooltip("Slot courant (0..2). -1 = aucun slot actif (nouvelle partie).")]
        [Range(-1, 2)][SerializeField] private int _currentSlot = -1;
        /// <summary>Slot actuellement chargé.</summary>
        public int CurrentSlot => _currentSlot;

        /// <summary>Données en mémoire (null si aucun slot chargé).</summary>
        public SaveData ActiveData { get; private set; }

        private float _lastSaveTime;
        private bool _dirty;
        private bool _isSaving;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            if (_dirty) SaveImmediate();
            _instance = null;
        }

        private void Update()
        {
            // Auto-save périodique (mobile-safe: pas d'IO par frame, juste un timer).
            if (_dirty && Time.unscaledTime - _lastSaveTime >= _autoSaveInterval && !_isSaving)
            {
                SaveImmediate();
            }
        }

        /// <summary>Marque les données comme modifiées (déclenche l'auto-save différé).</summary>
        public void MarkDirty() => _dirty = true;

        /// <summary>Charge un slot. Retourne false si le slot est vide ou corrompu.</summary>
        public bool LoadSlot(int slot)
        {
            if (slot < 0 || slot > 2) { Debug.LogError("[SaveSystem] Slot invalide."); return false; }
            try
            {
                var path = GetSlotPath(slot);
                if (!File.Exists(path))
                {
                    // Fallback PlayerPrefs
                    var ppKey = $"K5_Save_Slot{slot}";
                    if (PlayerPrefs.HasKey(ppKey))
                    {
                        ActiveData = DecryptAndDeserialize(PlayerPrefs.GetString(ppKey));
                        ActiveData = Migrate(ActiveData);
                    }
                    else
                    {
                        ActiveData = new SaveData(); // Nouvelle partie (version courante, pas de migration)
                    }
                }
                else
                {
                    var encrypted = File.ReadAllText(path);
                    ActiveData = DecryptAndDeserialize(encrypted);
                    ActiveData = Migrate(ActiveData);
                }
                _currentSlot = slot;
                _dirty = false;
                _lastSaveTime = Time.unscaledTime;
                Debug.Log($"[SaveSystem] Slot {slot} chargé (v{ActiveData.SchemaVersion}).");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Échec chargement slot {slot}: {ex}");
                ActiveData = new SaveData();
                _currentSlot = slot;
                return false;
            }
        }

        /// <summary>Sauvegarde immédiate sur disque + PlayerPrefs (backup).</summary>
        public void SaveImmediate()
        {
            if (ActiveData == null || _currentSlot < 0 || _isSaving) return;
            _isSaving = true;
            try
            {
                ActiveData.LastSaveUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var json = JsonConvert.SerializeObject(ActiveData, Formatting.None);
                var encrypted = Encrypt(json);
                var path = GetSlotPath(_currentSlot);
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, encrypted);
                PlayerPrefs.SetString($"K5_Save_Slot{_currentSlot}", encrypted);
                PlayerPrefs.Save();
                if (_enableCloudSync) _ = CloudSyncStubAsync(encrypted, _currentSlot);
                _dirty = false;
                _lastSaveTime = Time.unscaledTime;
                Debug.Log($"[SaveSystem] Slot {_currentSlot} sauvegardé.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Échec sauvegarde slot {_currentSlot}: {ex}");
            }
            finally { _isSaving = false; }
        }

        /// <summary>Supprime un slot (disque + PlayerPrefs).</summary>
        public void DeleteSlot(int slot)
        {
            try
            {
                var path = GetSlotPath(slot);
                if (File.Exists(path)) File.Delete(path);
                PlayerPrefs.DeleteKey($"K5_Save_Slot{slot}");
                PlayerPrefs.Save();
                if (_currentSlot == slot) { ActiveData = null; _currentSlot = -1; }
                Debug.Log($"[SaveSystem] Slot {slot} supprimé.");
            }
            catch (Exception ex) { Debug.LogError($"[SaveSystem] Échec suppression slot {slot}: {ex}"); }
        }

        /// <summary>Indique si un slot contient une sauvegarde.</summary>
        public bool SlotExists(int slot)
        {
            return File.Exists(GetSlotPath(slot)) || PlayerPrefs.HasKey($"K5_Save_Slot{slot}");
        }

        /// <summary>Résume un slot pour l'écran Load Game (sans charger entièrement).</summary>
        public SaveSlotMetadata GetSlotMetadata(int slot)
        {
            try
            {
                if (!SlotExists(slot)) return default;
                var encrypted = File.Exists(GetSlotPath(slot)) ? File.ReadAllText(GetSlotPath(slot)) : PlayerPrefs.GetString($"K5_Save_Slot{slot}");
                var data = DecryptAndDeserialize(encrypted);
                return new SaveSlotMetadata(
                    exists: true,
                    displayName: data.Profile.DisplayName,
                    selectedAgent: data.Profile.SelectedAgent,
                    playerLevel: data.Profile.PlayerLevel,
                    lastSaveUnix: data.LastSaveUnix,
                    schemaVersion: data.SchemaVersion);
            }
            catch { return default; }
        }

        public readonly struct SaveSlotMetadata
        {
            public readonly bool Exists;
            public readonly string DisplayName;
            public readonly string SelectedAgent;
            public readonly int PlayerLevel;
            public readonly long LastSaveUnix;
            public readonly int SchemaVersion;

            public SaveSlotMetadata(bool exists, string displayName, string selectedAgent, int playerLevel, long lastSaveUnix, int schemaVersion)
            {
                Exists = exists; DisplayName = displayName; SelectedAgent = selectedAgent;
                PlayerLevel = playerLevel; LastSaveUnix = lastSaveUnix; SchemaVersion = schemaVersion;
            }
        }

        // --- Sérialisation & chiffrement ---

        private string GetSlotPath(int slot) => Path.Combine(Application.persistentDataPath, $"save_slot{slot}.dat");

        private SaveData DecryptAndDeserialize(string encrypted)
        {
            var json = Decrypt(encrypted);
            return JsonConvert.DeserializeObject<SaveData>(json) ?? new SaveData();
        }

        private string Encrypt(string plain)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = ConvertHexStringToBytes(_aesKeyHex);
                aes.IV = ConvertHexStringToBytes(_aesIvHex);
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using var enc = aes.CreateEncryptor();
                var bytes = Encoding.UTF8.GetBytes(plain);
                var cipher = enc.TransformFinalBlock(bytes, 0, bytes.Length);
                return Convert.ToBase64String(cipher);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Encrypt échec: {ex}");
                return plain; // Fallback non chiffré (jamais en prod)
            }
        }

        private string Decrypt(string cipherBase64)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = ConvertHexStringToBytes(_aesKeyHex);
                aes.IV = ConvertHexStringToBytes(_aesIvHex);
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using var dec = aes.CreateDecryptor();
                var cipher = Convert.FromBase64String(cipherBase64);
                var plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Decrypt échec: {ex}");
                throw;
            }
        }

        private static byte[] ConvertHexStringToBytes(string hex)
        {
            if (hex.Length % 2 != 0) throw new ArgumentException("Hex de longueur impaire.");
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        // --- Migrations versionnées ---
        private static SaveData Migrate(SaveData data)
        {
            if (data.SchemaVersion >= SaveData.CurrentSchemaVersion) return data;
            try
            {
                // v1 -> v2 : ajout de Resources dans Inventory (vide si absent).
                if (data.SchemaVersion < 2) { data.Inventory.Resources ??= new Dictionary<string, int>(); data.SchemaVersion = 2; }
                // v2 -> v3 : ajout de HapticsEnabled dans Settings (true par défaut).
                if (data.SchemaVersion < 3) { data.Settings.HapticsEnabled = true; data.SchemaVersion = 3; }
                // Futures migrations ici...
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Migration échec: {ex}");
                return data;
            }
        }

        // --- Stub sync cloud (PostHog / Nakama / Firebase dans une future tâche) ---
        private async UniTaskVoid CloudSyncStubAsync(string encrypted, int slot)
        {
            await UniTask.Delay(100);
            Debug.Log($"[SaveSystem] Cloud sync stub pour slot {slot} ({encrypted.Length} octets).");
        }
    }
}
