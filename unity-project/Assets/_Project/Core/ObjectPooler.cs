using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KINETICS5.Core
{
    /// <summary>
    /// Pool générique d'objets réutilisables (projectiles, VFX, damage numbers).
    /// Pré-chauffé au boot, basé sur Stack<T> pour un LIFO sans GC après warmup.
    /// Pensé pour mobile low-end : aucune allocation dans Get/Release.
    /// </summary>
    public sealed class ObjectPooler : MonoBehaviour
    {
        private static ObjectPooler _instance;
        /// <summary>Instance globale (créée par Bootstrapper).</summary>
        public static ObjectPooler Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[ObjectPooler]");
                    _instance = go.AddComponent<ObjectPooler>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Serializable]
        public struct PoolConfig
        {
            [Tooltip("Identifiant unique du pool (ex: \"Bullet\", \"VFX_Hit\").")]
            public string Id;
            [Tooltip("Prefab source (doit implémenter IPooledItem pour callback Reset/Spawn).")]
            public Component Prefab;
            [Tooltip("Taille initiale du pool (pré-chauffée au boot).")]
            [Min(1)] public int PreWarm;
            [Tooltip("Taille maximale (au-delà, les objets sont détruits au lieu d'être retournés).")]
            [Min(1)] public int MaxSize;
        }

        [Header("Configuration des pools")]
        [Tooltip("Liste des pools à pré-chauffer au démarrage.")]
        [SerializeField] private PoolConfig[] _poolConfigs = Array.Empty<PoolConfig>();

        [Header("Parent organisation")]
        [Tooltip("Parent des objets inactifs (réduit le bruit dans la hiérarchie).")]
        [SerializeField] private Transform _inactiveParent;

        // --- Pool concret : Stack<Component> ---
        private sealed class Pool
        {
            public readonly Stack<Component> Stack;
            public readonly Component Prefab;
            public readonly Transform InactiveParent;
            public readonly int MaxSize;
            public int Alive;

            public Pool(Component prefab, int preWarm, int maxSize, Transform inactiveParent)
            {
                Prefab = prefab;
                InactiveParent = inactiveParent;
                MaxSize = maxSize;
                Stack = new Stack<Component>(preWarm);
            }
        }

        private readonly Dictionary<string, Pool> _pools = new(32);
        private readonly Dictionary<Component, string> _instanceToPoolId = new(512);

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            if (_inactiveParent == null)
            {
                var go = new GameObject("[Pooled_Inactive]");
                go.transform.SetParent(transform, false);
                _inactiveParent = go.transform;
            }
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            foreach (var kvp in _pools)
            {
                while (kvp.Value.Stack.Count > 0)
                {
                    var c = kvp.Value.Stack.Pop();
                    if (c != null) Destroy(c.gameObject);
                }
            }
            _pools.Clear();
            _instanceToPoolId.Clear();
            _instance = null;
        }

        /// <summary>Pré-chauffe tous les pools configurés. À appeler au boot par le Bootstrapper.</summary>
        public void PreWarmAll()
        {
            for (int i = 0; i < _poolConfigs.Length; i++)
            {
                var cfg = _poolConfigs[i];
                if (cfg.Prefab == null || string.IsNullOrEmpty(cfg.Id))
                {
                    Debug.LogWarning($"[ObjectPooler] Config #{i} invalide (prefab ou id manquant).");
                    continue;
                }
                RegisterPool(cfg.Id, cfg.Prefab, cfg.PreWarm, cfg.MaxSize);
            }
        }

        /// <summary>Enregistre dynamiquement un nouveau pool (utile pour les mods/arènes).</summary>
        public void RegisterPool(string id, Component prefab, int preWarm, int maxSize)
        {
            if (_pools.ContainsKey(id))
            {
                Debug.LogWarning($"[ObjectPooler] Pool {id} déjà enregistré.");
                return;
            }
            var pool = new Pool(prefab, preWarm, maxSize, _inactiveParent);
            for (int i = 0; i < preWarm; i++)
            {
                var inst = Instantiate(prefab, _inactiveParent);
                inst.gameObject.SetActive(false);
                pool.Stack.Push(inst);
            }
            _pools.Add(id, pool);
        }

        /// <summary>
        /// Récupère un objet du pool. Si vide, en instancie un nouveau (jusqu'à MaxSize).
        /// </summary>
        public T Get<T>(string poolId, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            if (!_pools.TryGetValue(poolId, out var pool))
            {
                Debug.LogError($"[ObjectPooler] Pool inconnu: {poolId}");
                return null;
            }
            Component inst = null;
            if (pool.Stack.Count > 0) inst = pool.Stack.Pop();
            else if (pool.Alive < pool.MaxSize) inst = Instantiate(pool.Prefab);
            else
            {
                // Pool saturé : réutilise le plus ancien actif (fallback sans détruire).
                Debug.LogWarning($"[ObjectPooler] Pool {poolId} saturé, instanciation forcée.");
                inst = Instantiate(pool.Prefab);
            }

            var t = inst.transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, rotation);
            inst.gameObject.SetActive(true);

            if (inst is IPooledItem pooled) pooled.OnSpawnFromPool();
            _instanceToPoolId[inst] = poolId;
            pool.Alive++;
            return inst as T;
        }

        /// <summary>Variante sans position (utile pour les UI / damage numbers).</summary>
        public T Get<T>(string poolId) where T : Component => Get<T>(poolId, Vector3.zero, Quaternion.identity);

        /// <summary>Retourne un objet au pool (le désactive).</summary>
        public void Release<T>(T obj) where T : Component
        {
            if (obj == null) return;
            if (!_instanceToPoolId.TryGetValue(obj, out var poolId) || !_pools.TryGetValue(poolId, out var pool))
            {
                // Objet non issu du pool : destruction classique.
                Destroy(obj.gameObject);
                return;
            }
            if (obj is IPooledItem pooled) pooled.OnReturnToPool();
            obj.transform.SetParent(_inactiveParent, false);
            obj.gameObject.SetActive(false);
            pool.Stack.Push(obj);
            _instanceToPoolId.Remove(obj);
            pool.Alive--;
        }

        /// <summary>Vide un pool (détruit tous les objets inactifs).</summary>
        public void ClearPool(string poolId)
        {
            if (!_pools.TryGetValue(poolId, out var pool)) return;
            while (pool.Stack.Count > 0)
            {
                var c = pool.Stack.Pop();
                if (c != null) Destroy(c.gameObject);
            }
        }

        /// <summary>Statistiques d'un pool (debug HUD).</summary>
        public (int inactive, int alive, int maxSize) GetStats(string poolId)
        {
            if (!_pools.TryGetValue(poolId, out var pool)) return (0, 0, 0);
            return (pool.Stack.Count, pool.Alive, pool.MaxSize);
        }
    }

    /// <summary>Interface optionnelle implémentée par les prefabs poolés pour callbacks de cycle de vie.</summary>
    public interface IPooledItem
    {
        /// <summary>Appelée quand l'objet sort du pool (activation).</summary>
        void OnSpawnFromPool();
        /// <summary>Appelée quand l'objet retourne au pool (désactivation).</summary>
        void OnReturnToPool();
    }
}
