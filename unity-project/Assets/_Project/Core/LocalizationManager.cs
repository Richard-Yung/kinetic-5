using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace KINETICS5.Core
{
    /// <summary>
    /// Code de langue ISO 639-1 (étendant la liste initiale FR/EN pour JP/CN/KR/ES/DE).
    /// </summary>
    public enum Language
    {
        French,
        English,
        Japanese,
        ChineseSimplified,
        Korean,
        Spanish,
        German
    }

    /// <summary>
    /// Gestionnaire de localisation de KINETICS 5.
    /// - Charge les fichiers JSON de langue depuis StreamingAssets/Localization/.
    /// - Fallback automatique vers l'anglais si une clé est absente.
    /// - Changement de langue à l'exécution sans recharger la scène.
    /// - Support RTL (arabe, hébreu) stubbé pour extension future.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalizationManager : MonoBehaviour
    {
        private static LocalizationManager _instance;
        /// <summary>Instance globale.</summary>
        public static LocalizationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[LocalizationManager]");
                    _instance = go.AddComponent<LocalizationManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Configuration")]
        [Tooltip("Langue affichée au démarrage.")]
        [SerializeField] private Language _currentLanguage = Language.English;
        [Tooltip("Langue de fallback (en cas de clé manquante).")]
        [SerializeField] private Language _fallbackLanguage = Language.English;
        [Tooltip("Dossier racine des fichiers de langue (sous StreamingAssets).")]
        [SerializeField] private string _localizationFolder = "Localization";

        /// <summary>Langue courante.</summary>
        public Language CurrentLanguage => _currentLanguage;
        /// <summary>Vrai si la langue courante est RTL (stub pour futur support arabe/hébreu).</summary>
        public bool IsRightToLeft => false;

        /// <summary>Événement publié quand la langue change (les vues doivent rafraîchir leurs chaînes).</summary>
        public event Action<Language> LanguageChanged;

        // Dictionnaires de traduction: clé -> texte.
        private readonly Dictionary<string, string> _current = new(2048);
        private readonly Dictionary<string, string> _fallback = new(2048);

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
        }

        /// <summary>Initialise et charge les fichiers de langue. À appeler au boot.</summary>
        public async UniTask InitializeAsync(Language? overrideLang = null)
        {
            // Charge toujours le fallback (anglais) en premier.
            await LoadLanguageAsync(_fallbackLanguage, _fallback);
            var lang = overrideLang ?? _currentLanguage;
            await LoadLanguageAsync(lang, _current);
            _currentLanguage = lang;
            LanguageChanged?.Invoke(lang);
        }

        /// <summary>Change la langue à l'exécution et notifie les vues.</summary>
        public async UniTask SetLanguageAsync(Language lang)
        {
            if (lang == _currentLanguage) return;
            _current.Clear();
            await LoadLanguageAsync(lang, _current);
            _currentLanguage = lang;
            LanguageChanged?.Invoke(lang);
            // Sauvegarde du choix utilisateur.
            var save = ServiceLocator.Instance?.Get<SaveSystem>();
            if (save?.ActiveData != null)
            {
                save.ActiveData.Settings.Language = LanguageToCode(lang);
                save.MarkDirty();
            }
        }

        /// <summary>
        /// Récupère la chaîne traduite pour la clé donnée.
        /// Retourne la clé elle-même si introuvable (debug-friendly).
        /// </summary>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (_current.TryGetValue(key, out var value)) return value;
            if (_fallback.TryGetValue(key, out value)) return value;
            return key;
        }

        /// <summary>Variante avec formatage (style string.Format, sans allocation si possible).</summary>
        public string GetFormat(string key, params object[] args)
        {
            var raw = Get(key);
            try { return args.Length == 0 ? raw : string.Format(raw, args); }
            catch (FormatException) { return raw; }
        }

        /// <summary>Indique si une clé existe dans la langue courante.</summary>
        public bool HasKey(string key) => _current.ContainsKey(key) || _fallback.ContainsKey(key);

        /// <summary>Convertit l'enum Language en code ISO 639-1.</summary>
        public static string LanguageToCode(Language lang) => lang switch
        {
            Language.French => "fr",
            Language.English => "en",
            Language.Japanese => "ja",
            Language.ChineseSimplified => "zh-CN",
            Language.Korean => "ko",
            Language.Spanish => "es",
            Language.German => "de",
            _ => "en"
        };

        /// <summary>Convertit un code ISO 639-1 vers l'enum Language.</summary>
        public static Language CodeToLanguage(string code)
        {
            if (string.IsNullOrEmpty(code)) return Language.English;
            code = code.ToLowerInvariant();
            return code switch
            {
                "fr" => Language.French,
                "en" => Language.English,
                "ja" => Language.Japanese,
                "zh" or "zh-cn" or "zh-sg" => Language.ChineseSimplified,
                "ko" => Language.Korean,
                "es" => Language.Spanish,
                "de" => Language.German,
                _ => Language.English
            };
        }

        // --- Chargement des fichiers JSON ---

        private async UniTask LoadLanguageAsync(Language lang, Dictionary<string, string> target)
        {
            var code = LanguageToCode(lang);
            var path = Path.Combine(Application.streamingAssetsPath, _localizationFolder, $"{code}.json");
            try
            {
                string json;
#if UNITY_ANDROID && !UNITY_EDITOR
                // StreamingAssets sur Android est dans une archive, doit passer par UnityWebRequest.
                json = await LoadViaWebRequestAsync(path);
#else
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[Localization] Fichier manquant: {path}. Fallback {LanguageToCode(_fallbackLanguage)}.");
                    if (lang != _fallbackLanguage) return;
                    json = "{}";
                }
                else json = File.ReadAllText(path);
#endif
                var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (dict == null) return;
                target.Clear();
                foreach (var kvp in dict) target[kvp.Key] = kvp.Value;
                Debug.Log($"[Localization] Langue {code} chargée: {target.Count} entrées.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Localization] Échec chargement {code}: {ex}");
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private async UniTask<string> LoadViaWebRequestAsync(string path)
        {
            using var req = UnityEngine.Networking.UnityWebRequest.Get(path);
            await req.SendWebRequest().ToUniTask();
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                throw new IOException($"UnityWebRequest échec: {req.error}");
            return req.downloadHandler.text;
        }
#endif
    }
}
