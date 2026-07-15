using System.Collections.Generic;
using KINETICS5.Core;
using KINETICS5.Data;
using UnityEngine;

namespace KINETICS5.Gameplay.Missions
{
    /// <summary>
    /// Difficulté dynamique de KINETICS 5. Suit la performance du joueur (morts, temps par
    /// vague, précision) et ajuste en temps réel les multiplicateurs d'HP/dégâts/nombre des
    /// ennemis ( plage 0.8x – 1.5x ). Ajustements lissés pour éviter les à-coups.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le <see cref="DifficultyManager"/> s'abonne aux événements <c>EnemyKilledEvent</c> et
    /// <c>PlayerDamagedEvent</c> pour estimer la difficulté ressentie. À chaque vague complétée,
    /// il réévalue les multiplicateurs et notifie le <see cref="EnemySpawner"/>.
    /// </para>
    /// <para>
    /// <b>Plage d'ajustement :</b> 0.8x (joueur en difficulté) à 1.5x (joueur performant).
    /// L'ajustement est lissé sur 3 vagues pour éviter les sauts brusques.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class DifficultyManager : MonoBehaviour
    {
        [Header("Plage d'ajustement")]
        [Tooltip("Multiplicateur HP minimum (joueur en difficulté).")]
        [Range(0.5f, 1f)][SerializeField] private float _minHpMult = 0.8f;
        [Tooltip("Multiplicateur HP maximum (joueur performant).")]
        [Range(1f, 2f)][SerializeField] private float _maxHpMult = 1.5f;
        [Tooltip("Multiplicateur dégâts minimum.")]
        [Range(0.5f, 1f)][SerializeField] private float _minDamageMult = 0.8f;
        [Tooltip("Multiplicateur dégâts maximum.")]
        [Range(1f, 2f)][SerializeField] private float _maxDamageMult = 1.5f;
        [Tooltip("Multiplicateur count d'ennemis minimum.")]
        [Range(0.5f, 1f)][SerializeField] private float _minCountMult = 0.8f;
        [Tooltip("Multiplicateur count d'ennemis maximum.")]
        [Range(1f, 2f)][SerializeField] private float _maxCountMult = 1.3f;

        [Header("Lissage")]
        [Tooltip("Vitesse de transition (0..1 ; 0.1 = très lisse, 1 = instantané).")]
        [Range(0.05f, 1f)][SerializeField] private float _lerpSpeed = 0.15f;
        [Tooltip("Seuil de performance en dessous duquel on réduit la difficulté (0..1).")]
        [Range(0f, 1f)][SerializeField] private float _performanceLowerThreshold = 0.3f;
        [Tooltip("Seuil de performance au-dessus duquel on augmente la difficulté (0..1).")]
        [Range(0f, 1f)][SerializeField] private float _performanceUpperThreshold = 0.7f;

        [Header("Performance initiale")]
        [Tooltip("Performance initiale estimée (0..1 ; 0.5 = neutre).")]
        [Range(0f, 1f)][SerializeField] private float _initialPerformance = 0.5f;

        /// <summary>Multiplicateur d'HP courant (appliqué au spawner).</summary>
        public float CurrentHpMult { get; private set; } = 1f;

        /// <summary>Multiplicateur de dégâts courant.</summary>
        public float CurrentDamageMult { get; private set; } = 1f;

        /// <summary>Multiplicateur de count courant (arrondi à l'entier le plus proche à l'usage).</summary>
        public float CurrentCountMult { get; private set; } = 1f;

        /// <summary>Performance estimée courante (0..1).</summary>
        public float CurrentPerformance { get; private set; } = 0.5f;

        // Métriques accumulées depuis le début de la mission.
        private int _enemiesKilledThisWave;
        private int _enemiesSpawnedThisWave;
        private float _waveStartTime;
        private int _playerDeaths;
        private int _playerDamageTakenThisWave;
        private int _wavesCompleted;
        private float _smoothPerformance;
        private EnemySpawner _spawner;

        // Souscriptions au bus.
        private IDisposable _enemyKilledSub;
        private IDisposable _playerDamagedSub;

        private void Awake()
        {
            CurrentPerformance = _initialPerformance;
            _smoothPerformance = _initialPerformance;
            RecomputeMultipliers(instant: true);
        }

        private void OnEnable()
        {
            _enemyKilledSub = GameEventBus.Instance.Subscribe<EnemyKilledEvent>(HandleEnemyKilled);
            _playerDamagedSub = GameEventBus.Instance.Subscribe<PlayerDamagedEvent>(HandlePlayerDamaged);
        }

        private void OnDisable()
        {
            _enemyKilledSub?.Dispose();
            _playerDamagedSub?.Dispose();
        }

        private void Update()
        {
            // Lissage des multiplicateurs vers la cible.
            float targetHp = EvaluateHpMult(_smoothPerformance);
            float targetDmg = EvaluateDamageMult(_smoothPerformance);
            float targetCount = EvaluateCountMult(_smoothPerformance);

            CurrentHpMult = Mathf.Lerp(CurrentHpMult, targetHp, _lerpSpeed);
            CurrentDamageMult = Mathf.Lerp(CurrentDamageMult, targetDmg, _lerpSpeed);
            CurrentCountMult = Mathf.Lerp(CurrentCountMult, targetCount, _lerpSpeed);

            // Push vers le spawner.
            if (_spawner != null)
            {
                _spawner.SetDifficultyMultipliers(CurrentHpMult, CurrentDamageMult);
            }
        }

        // =================================================================================
        //  HOOKS MISSION
        // =================================================================================

        /// <summary>Appelé par <see cref="MissionDirector"/> au démarrage d'une mission.</summary>
        public void OnMissionStart(MissionDirector director)
        {
            _spawner = director != null ? director.GetComponent<EnemySpawner>() : null;
            if (_spawner == null) _spawner = FindFirstObjectByType<EnemySpawner>();
            _enemiesKilledThisWave = 0;
            _enemiesSpawnedThisWave = 0;
            _playerDeaths = 0;
            _playerDamageTakenThisWave = 0;
            _wavesCompleted = 0;
            _waveStartTime = Time.time;
            RecomputeMultipliers(instant: true);
        }

        /// <summary>Appelé quand une vague est complétée (par hook du spawner).</summary>
        public void OnWaveCompleted(int waveIndex)
        {
            _wavesCompleted++;
            float waveDuration = Time.time - _waveStartTime;
            _waveStartTime = Time.time;

            // Calcul de la performance pour cette vague.
            float perf = EvaluateWavePerformance(waveDuration);
            UpdatePerformance(perf);

            // Reset métriques.
            _enemiesKilledThisWave = 0;
            _enemiesSpawnedThisWave = 0;
            _playerDamageTakenThisWave = 0;
        }

        /// <summary>Appelé par <see cref="MissionDirector"/> quand un ennemi est tué.</summary>
        public void OnEnemyKilled(EnemyKilledEvent evt)
        {
            _enemiesKilledThisWave++;
        }

        // =================================================================================
        //  EVENT HANDLERS
        // =================================================================================

        private void HandleEnemyKilled(EnemyKilledEvent evt)
        {
            _enemiesKilledThisWave++;
        }

        private void HandlePlayerDamaged(PlayerDamagedEvent evt)
        {
            _playerDamageTakenThisWave += Mathf.CeilToInt(evt.Amount);
            if (evt.IsFatal)
            {
                _playerDeaths++;
                // Pénalité forte : -0.2 performance par mort.
                _smoothPerformance = Mathf.Max(0f, _smoothPerformance - 0.2f);
            }
        }

        // =================================================================================
        //  ÉVALUATION
        // =================================================================================

        /// <summary>Évalue la performance du joueur sur la dernière vague (0..1).</summary>
        private float EvaluateWavePerformance(float waveDuration)
        {
            // Métriques normalisées :
            // - Taux de kill : si 100% des ennemis spawnés ont été tués, perf = 1.
            // - Damage taken : 0 = parfait, > 100 = mauvais.
            // - Temps par ennemi : < 2s = excellent, > 5s = médiocre.

            float killRate = _enemiesSpawnedThisWave > 0
                ? (float)_enemiesKilledThisWave / _enemiesSpawnedThisWave
                : 1f;

            float damageScore = _playerDamageTakenThisWave == 0
                ? 1f
                : Mathf.Clamp01(1f - _playerDamageTakenThisWave / 200f);

            float timePerEnemy = _enemiesKilledThisWave > 0
                ? waveDuration / _enemiesKilledThisWave
                : waveDuration;
            float timeScore = Mathf.Clamp01(1f - (timePerEnemy - 1f) / 4f);

            // Moyenne pondérée.
            float perf = (killRate * 0.5f) + (damageScore * 0.3f) + (timeScore * 0.2f);
            return Mathf.Clamp01(perf);
        }

        /// <summary>Lisse la performance accumulée.</summary>
        private void UpdatePerformance(float newPerf)
        {
            // Lissage : 70% ancienne + 30% nouvelle (évite les sauts brusques).
            _smoothPerformance = (_smoothPerformance * 0.7f) + (newPerf * 0.3f);
            CurrentPerformance = _smoothPerformance;
        }

        private void RecomputeMultipliers(bool instant)
        {
            float targetHp = EvaluateHpMult(_smoothPerformance);
            float targetDmg = EvaluateDamageMult(_smoothPerformance);
            float targetCount = EvaluateCountMult(_smoothPerformance);
            if (instant)
            {
                CurrentHpMult = targetHp;
                CurrentDamageMult = targetDmg;
                CurrentCountMult = targetCount;
            }
        }

        // =================================================================================
        //  COURBES D'ÉVALUATION
        // =================================================================================

        /// <summary>Performance [0..1] → multiplicateur HP.</summary>
        private float EvaluateHpMult(float perf)
        {
            // perf = 0 → _minHpMult ; perf = 1 → _maxHpMult.
            return Mathf.Lerp(_minHpMult, _maxHpMult, perf);
        }

        /// <summary>Performance [0..1] → multiplicateur dégâts.</summary>
        private float EvaluateDamageMult(float perf)
        {
            return Mathf.Lerp(_minDamageMult, _maxDamageMult, perf);
        }

        /// <summary>Performance [0..1] → multiplicateur count.</summary>
        private float EvaluateCountMult(float perf)
        {
            return Mathf.Lerp(_minCountMult, _maxCountMult, perf);
        }

        // =================================================================================
        //  DEBUG
        // =================================================================================

        /// <summary>Snapshot debug (pour UI diagnostic).</summary>
        public string GetDebugSnapshot()
        {
            return $"Perf={CurrentPerformance:F2} | HP={CurrentHpMult:F2}x | " +
                   $"DMG={CurrentDamageMult:F2}x | Count={CurrentCountMult:F2}x | " +
                   $"Waves={_wavesCompleted} | Kills={_enemiesKilledThisWave} | " +
                   $"Deaths={_playerDeaths} | DmgTaken={_playerDamageTakenThisWave}";
        }
    }
}
