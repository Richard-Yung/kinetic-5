using System;
using System.Collections;
using System.Collections.Generic;
using KINETICS5.Core;
using KINETICS5.Data;
using KINETICS5.Gameplay.Missions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KINETICS5.Gameplay.Enemies
{
    /// <summary>
    /// Gestionnaire de vagues d'apparition d'ennemis pour une mission.
    /// Lit <see cref="MissionDto.Waves"/> (ou <see cref="MissionSO.Waves"/>), spawn les ennemis
    /// via pooling, suit le compte d'ennemis actifs, déclenche la vague suivante quand tous
    /// sont morts, et notifie le <see cref="Missions.MissionDirector"/> à la fin.
    /// </summary>
    /// <remarks>
    /// <b>Cap mobile :</b> max 12 ennemis actifs simultanément (constante
    /// <see cref="MaxConcurrentEnemies"/>). Si la vague demande plus, les spawns excédentaires
    /// sont mis en file d'attente jusqu'à libération d'un slot.
    /// </remarks>
    [DisallowMultipleComponent]
    public class EnemySpawner : MonoBehaviour
    {
        /// <summary>Cap d'ennemis actifs simultanés (mobile low-end).</summary>
        public const int MaxConcurrentEnemies = 12;

        [Header("Configuration")]
        [Tooltip("Points d'apparition disponibles. Si vide, utilise les EnemySpawnPoint de la scène.")]
        [SerializeField] private Transform[] _spawnPoints = Array.Empty<Transform>();
        [Tooltip("Prefab d'ennemi par défaut (si non résolu via DataLoader.ModelPrefab).")]
        [SerializeField] private GameObject _fallbackEnemyPrefab;
        [Tooltip("Délai maximum entre deux spawns d'une même vague (secondes).")]
        [SerializeField] private float _spawnInterval = 0.5f;
        [Tooltip("Vrai si le spawner doit réutiliser les EnemySpawnPoint de la scène automatiquement.")]
        [SerializeField] private bool _autoDiscoverSpawnPoints = true;

        [Header("Difficulté")]
        [Tooltip("Multiplicateur de PV de base (ajusté par DifficultyManager en runtime).")]
        [Range(0.5f, 3f)][SerializeField] private float _baseHpMult = 1f;
        [Tooltip("Multiplicateur de dégâts de base.")]
        [Range(0.5f, 3f)][SerializeField] private float _baseDamageMult = 1f;
        [Tooltip("Niveau joueur (pour scaling).")]
        [SerializeField] private int _playerLevel = 1;

        [Header("Pooling")]
        [Tooltip("Taille initiale des pools d'ennemis (par type).")]
        [Min(1)][SerializeField] private int _poolPrewarm = 4;
        [Tooltip("Taille maximale d'un pool d'ennemis (par type).")]
        [Min(1)][SerializeField] private int _poolMaxSize = 24;

        /// <summary>Événement déclenché au démarrage d'une vague.</summary>
        public event Action<int, string> OnWaveStarted;

        /// <summary>Événement déclenché à la fin d'une vague (tous ennemis morts).</summary>
        public event Action<int> OnWaveCompleted;

        /// <summary>Événement déclenché quand toutes les vagues sont terminées.</summary>
        public event Action OnAllWavesCompleted;

        /// <summary>Nombre d'ennemis actuellement actifs (en vie).</summary>
        public int ActiveEnemyCount { get; private set; }

        /// <summary>Index de la vague en cours (-1 si pas commencé).</summary>
        public int CurrentWaveIndex { get; private set; } = -1;

        /// <summary>Vrai si le spawner a démarré une mission.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Vrai si toutes les vagues ont été terminées.</summary>
        public bool AllWavesCompleted { get; private set; }

        private readonly List<EnemyController> _activeEnemies = new(MaxConcurrentEnemies);
        private readonly Queue<PendingSpawn> _pendingSpawns = new(32);
        private MissionDto _mission;
        private EnemySpawnPoint[] _discoveredPoints;
        private Coroutine _waveCoroutine;
        private bool _waveInProgress;
        private float _currentHpMult = 1f;
        private float _currentDamageMult = 1f;

        private struct PendingSpawn
        {
            public string EnemyId;
            public Vector3 Position;
            public int WaveIndex;
        }

        private void Awake()
        {
            _currentHpMult = _baseHpMult;
            _currentDamageMult = _baseDamageMult;
        }

        private void Start()
        {
            if (_autoDiscoverSpawnPoints)
            {
                _discoveredPoints = FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
                if (_discoveredPoints.Length > 0 && _spawnPoints.Length == 0)
                {
                    _spawnPoints = new Transform[_discoveredPoints.Length];
                    for (int i = 0; i < _discoveredPoints.Length; i++)
                    {
                        _spawnPoints[i] = _discoveredPoints[i].transform;
                    }
                }
            }
        }

        /// <summary>Démarre la séquence de vagues d'une mission.</summary>
        /// <param name="mission">Mission dont les vagues sont à spawner.</param>
        public void StartMission(MissionDto mission)
        {
            if (IsRunning)
            {
                Debug.LogWarning("[EnemySpawner] Mission déjà en cours, arrêt puis redémarrage.");
                StopMission();
            }
            _mission = mission;
            IsRunning = true;
            AllWavesCompleted = false;
            CurrentWaveIndex = -1;
            _activeEnemies.Clear();
            ActiveEnemyCount = 0;
            _pendingSpawns.Clear();

            if (mission?.Waves == null || mission.Waves.Count == 0)
            {
                Debug.Log("[EnemySpawner] Mission sans vague : complétion immédiate.");
                AllWavesCompleted = true;
                OnAllWavesCompleted?.Invoke();
                return;
            }
            _waveCoroutine = StartCoroutine(RunWavesCoroutine());
        }

        /// <summary>Arrête la séquence de vagues et nettoie les ennemis actifs.</summary>
        public void StopMission()
        {
            if (_waveCoroutine != null)
            {
                StopCoroutine(_waveCoroutine);
                _waveCoroutine = null;
            }
            IsRunning = false;
            _waveInProgress = false;
            _pendingSpawns.Clear();

            // Nettoyage des ennemis encore actifs.
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var e = _activeEnemies[i];
                if (e != null && e.gameObject != null)
                {
                    if (ObjectPooler.Instance != null)
                    {
                        ObjectPooler.Instance.Release(e);
                    }
                    else if (e.gameObject != null)
                    {
                        Destroy(e.gameObject);
                    }
                }
            }
            _activeEnemies.Clear();
            ActiveEnemyCount = 0;
        }

        /// <summary>Met à jour les multiplicateurs de difficulté (appelé par DifficultyManager).</summary>
        public void SetDifficultyMultipliers(float hpMult, float damageMult)
        {
            _currentHpMult = Mathf.Clamp(hpMult, 0.5f, 3f) * _baseHpMult;
            _currentDamageMult = Mathf.Clamp(damageMult, 0.5f, 3f) * _baseDamageMult;
        }

        /// <summary>Notifié par EnemyController à la mort d'un ennemi.</summary>
        public void NotifyEnemyDeath(EnemyController enemy)
        {
            if (enemy == null) return;
            bool removed = _activeEnemies.Remove(enemy);
            if (removed)
            {
                ActiveEnemyCount = _activeEnemies.Count;
            }

            // Si une vague est en cours et qu'il ne reste plus d'ennemis actifs ET plus de spawns en attente,
            // la vague est terminée.
            if (_waveInProgress && _pendingSpawns.Count == 0 && _activeEnemies.Count == 0)
            {
                _waveInProgress = false;
                int completedIndex = CurrentWaveIndex;
                OnWaveCompleted?.Invoke(completedIndex);

                // Si c'était la dernière vague, notifie la complétion.
                if (_mission != null && CurrentWaveIndex >= _mission.Waves.Count - 1)
                {
                    AllWavesCompleted = true;
                    OnAllWavesCompleted?.Invoke();
                }
            }
        }

        // =================================================================================
        //  COROUTINE DE VAGUES
        // =================================================================================

        private IEnumerator RunWavesCoroutine()
        {
            for (int i = 0; i < _mission.Waves.Count; i++)
            {
                CurrentWaveIndex = i;
                var wave = _mission.Waves[i];

                // Délai avant la vague.
                if (wave.Delay > 0f)
                {
                    yield return new WaitForSeconds(wave.Delay);
                }

                OnWaveStarted?.Invoke(i, wave.EnemyId);
                _waveInProgress = true;

                // Programmation des spawns.
                Vector3 spawnPos = wave.SpawnPoint != null ? wave.SpawnPoint.ToVector3() : Vector3.zero;
                for (int k = 0; k < wave.Count; k++)
                {
                    Vector3 pos = ResolveSpawnPosition(spawnPos, k);
                    _pendingSpawns.Enqueue(new PendingSpawn
                    {
                        EnemyId = wave.EnemyId,
                        Position = pos,
                        WaveIndex = i
                    });
                }

                // Démarre le dépilement des spawns en attente (avec cap mobile).
                yield return StartCoroutine(DrainPendingSpawnsCoroutine());

                // Attend que tous les ennemis de la vague soient morts avant de passer à la suivante.
                yield return new WaitWhile(() => _waveInProgress);
            }
        }

        private IEnumerator DrainPendingSpawnsCoroutine()
        {
            while (_pendingSpawns.Count > 0)
            {
                // Cap mobile : attend si on a déjà MaxConcurrentEnemies ennemis actifs.
                if (_activeEnemies.Count >= MaxConcurrentEnemies)
                {
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }
                var pending = _pendingSpawns.Dequeue();
                SpawnEnemy(pending.EnemyId, pending.Position);
                if (_spawnInterval > 0f)
                {
                    yield return new WaitForSeconds(_spawnInterval);
                }
            }
        }

        // =================================================================================
        //  SPAWN UNITAIRE
        // =================================================================================

        /// <summary>Spawn un ennemi par EnemyId à la position donnée (avec pooling).</summary>
        public EnemyController SpawnEnemy(string enemyId, Vector3 position)
        {
            if (string.IsNullOrEmpty(enemyId))
            {
                Debug.LogWarning("[EnemySpawner] SpawnEnemy: enemyId vide.");
                return null;
            }
            var data = DataLoader.GetEnemy(enemyId);
            if (data == null)
            {
                Debug.LogError($"[EnemySpawner] EnemyId '{enemyId}' introuvable dans DataLoader.");
                return null;
            }

            GameObject prefab = ResolvePrefab(data);
            if (prefab == null)
            {
                Debug.LogError($"[EnemySpawner] Aucun prefab résolu pour '{enemyId}'.");
                return null;
            }

            // Pooling : enregistre le pool à la volée s'il n'existe pas encore.
            string poolId = "enemy_" + enemyId;
            EnsurePoolRegistered(poolId, prefab);

            EnemyController enemy = ObjectPooler.Instance.Get<EnemyController>(poolId, position, Quaternion.identity);
            if (enemy == null)
            {
                Debug.LogError($"[EnemySpawner] Pool Get a retourné null pour '{poolId}'.");
                return null;
            }

            enemy.transform.position = position;
            enemy.Initialize(this, _currentHpMult, _currentDamageMult);

            _activeEnemies.Add(enemy);
            ActiveEnemyCount = _activeEnemies.Count;
            return enemy;
        }

        private GameObject ResolvePrefab(EnemyDto data)
        {
            // En production : Addressables.LoadAsset<GameObject>(data.ModelPrefab).
            // Ici : on ne charge pas d'Addressables (sandbox), on retourne le fallback.
            if (!string.IsNullOrEmpty(data.ModelPrefab) && _fallbackEnemyPrefab == null)
            {
                // Laisse un avertissement : le prefab doit être assigné via Inspector
                // ou résolu par un système Addressables futur.
                Debug.LogWarning($"[EnemySpawner] ModelPrefab '{data.ModelPrefab}' non résolu (Addressables non configuré). " +
                                 $"Utilisez _fallbackEnemyPrefab ou assignez les prefabs par EnemyId.");
            }
            return _fallbackEnemyPrefab;
        }

        private void EnsurePoolRegistered(string poolId, GameObject prefab)
        {
            if (ObjectPooler.Instance == null) return;
            // Tentative Get : si ça réussit, le pool existe déjà (on relâche immédiatement).
            // Plus simple : maintenir un HashSet des pools enregistrés.
            if (_registeredPools.Contains(poolId)) return;
            ObjectPooler.Instance.RegisterPool(poolId, prefab.GetComponent<EnemyController>() ?? prefab.GetComponent<Component>(),
                                               _poolPrewarm, _poolMaxSize);
            _registeredPools.Add(poolId);
        }

        private readonly HashSet<string> _registeredPools = new();

        private Vector3 ResolveSpawnPosition(Vector3 basePosition, int spawnIndex)
        {
            // Si on a des spawn points, on cycle entre eux ; sinon, on offset autour du point de base.
            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                int idx = spawnIndex % _spawnPoints.Length;
                return _spawnPoints[idx].position;
            }
            // Offset en cercle autour du point de base.
            float angle = spawnIndex * 60f * Mathf.Deg2Rad;
            const float radius = 2.5f;
            return basePosition + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        // =================================================================================
        //  ACCÈS DEBUG
        // =================================================================================

        /// <summary>Liste des ennemis actifs (lecture seule ; pour UI minimap / debug).</summary>
        public IReadOnlyList<EnemyController> ActiveEnemies => _activeEnemies;
    }
}
