using System;
using System.Collections.Generic;
using UnityEngine;

namespace KINETICS5.Core
{
    /// <summary>
    /// Localisateur de services léger (conteneur DI minimaliste) optimisé pour mobile.
    /// Alternative à Zenject/VContainer pour éviter la surcharge de réflexion au runtime.
    /// Enregistre et résout des singletons par type, sur le thread principal uniquement.
    /// </summary>
    public sealed class ServiceLocator : MonoBehaviour
    {
        // --- Singleton ---
        private static ServiceLocator _instance;
        /// <summary>Instance globale du localisateur (créée par Bootstrapper).</summary>
        public static ServiceLocator Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[ServiceLocator]");
                    _instance = go.AddComponent<ServiceLocator>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // --- Stockage ---
        private readonly Dictionary<Type, object> _services = new(64);
        private readonly Dictionary<Type, Action<object>> _onRegisterCallbacks = new(32);

        /// <summary>Indique si le localisateur a été initialisé par le Bootstrapper.</summary>
        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _services.Clear();
                _onRegisterCallbacks.Clear();
                _instance = null;
            }
        }

        /// <summary>Marque le localisateur comme initialisé (appelé par Bootstrapper après wiring).</summary>
        public void MarkInitialized() => IsInitialized = true;

        /// <summary>
        /// Enregistre une instance de service. Échoue si un service du même type existe déjà
        /// (sauf si <paramref name="overwrite"/> est vrai).
        /// </summary>
        /// <typeparam name="T">Type du contrat (interface ou classe).</typeparam>
        /// <param name="instance">Instance concrète à enregistrer.</param>
        /// <param name="overwrite">Remplace un service existant si vrai.</param>
        public void Register<T>(T instance, bool overwrite = false) where T : class
        {
            if (instance == null)
            {
                Debug.LogError($"[ServiceLocator] Tentative d'enregistrement d'un service null pour {typeof(T).Name}.");
                return;
            }
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                if (!overwrite)
                {
                    Debug.LogWarning($"[ServiceLocator] Service {type.Name} déjà enregistré. Remplacement ignoré.");
                    return;
                }
                _services[type] = instance;
            }
            else
            {
                _services.Add(type, instance);
            }

            if (_onRegisterCallbacks.TryGetValue(type, out var cb))
            {
                cb.Invoke(instance);
            }
        }

        /// <summary>
        /// Enregistre un callback appelé dès qu'un service du type donné est enregistré.
        /// Utile pour les sous-systèmes qui dépendent d'un service non encore prêt.
        /// </summary>
        public void RegisterCallback<T>(Action<T> callback) where T : class
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var existing))
            {
                callback.Invoke((T)existing);
            }
            else
            {
                _onRegisterCallbacks[type] = obj => callback.Invoke((T)obj);
            }
        }

        /// <summary>
        /// Récupère une instance de service. Retourne null si non enregistré (pas d'exception, mobile-safe).
        /// </summary>
        public T Get<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var svc) ? (T)svc : null;
        }

        /// <summary>
        /// Variante TryGet pour éviter les allocations et gérer proprement l'absence de service.
        /// </summary>
        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var svc))
            {
                service = (T)svc;
                return true;
            }
            service = null;
            return false;
        }

        /// <summary>Désenregistre un service (ne le détruit pas, juste le retire du localisateur).</summary>
        public void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        /// <summary>Vérifie l'enregistrement d'un service.</summary>
        public bool IsRegistered<T>() where T : class => _services.ContainsKey(typeof(T));

        /// <summary>
        /// Valide que tous les types de services fournis sont enregistrés. Utilisé par le Bootstrapper
        /// pour vérifier le graphe de dépendances après l'init.
        /// </summary>
        public bool ValidateDependencies(IList<Type> required, out string missingReport)
        {
            var missing = new List<string>(4);
            for (int i = 0; i < required.Count; i++)
            {
                if (!_services.ContainsKey(required[i]))
                    missing.Add(required[i].Name);
            }
            missingReport = missing.Count > 0 ? "Services manquants: " + string.Join(", ", missing) : string.Empty;
            return missing.Count == 0;
        }

        /// <summary>Vide tous les services (appelé sur shutdown propre).</summary>
        public void ClearAll()
        {
            _services.Clear();
            _onRegisterCallbacks.Clear();
            IsInitialized = false;
        }
    }
}
