using System;
using System.Collections.Generic;
using KINETICS5.Core;
using KINETICS5.Data;
using KINETICS5.Gameplay.Enemies;
using UnityEngine;

namespace KINETICS5.Gameplay.Missions
{
    /// <summary>
    /// Réalise l'orchestration runtime d'une mission KINETICS 5. Chargé dans la scène de mission,
    /// il initialise les objectifs, démarre les vagues via <see cref="EnemySpawner"/>, suit la
    /// progression, gère le compte à rebours, et publie <see cref="MissionCompleteEvent"/> /
    /// <see cref="ObjectiveUpdatedEvent"/> sur le bus global.
    /// </summary>
    /// <remarks>
    /// <para><b>Cycle de vie :</b></para>
    /// <list type="1">
    ///   <item><see cref="StartMission"/> : charge la mission via <see cref="DataLoader"/>, crée
    ///   les <see cref="ObjectiveTracker"/>, démarre le spawner, abonne les events.</item>
    ///   <item><see cref="Update"/> : décrémente le timer, met à jour les objectifs SurviveTime.</item>
    ///   <item><see cref="CompleteMission"/> ou <see cref="FailMission"/> : publie l'événement
    ///   final, désabonne, déclenche le calcul des récompenses via <see cref="MissionRewards"/>.</item>
    /// </list>
    /// </remarks>
    [DisallowMultipleComponent]
    public class MissionDirector : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Id de la mission tel que défini dans missions.json. Si vide, _missionOverride est utilisé.")]
        [SerializeField] private string _missionId = string.Empty;
        [Tooltip("MissionSO direct (override DataLoader ; utile pour tests éditeur).")]
        [SerializeField] private MissionSO _missionSoOverride;

        [Header("Composants")]
        [Tooltip("EnemySpawner à piloter. Auto-résolu si sur le même GameObject.")]
        [SerializeField] private EnemySpawner _spawner;
        [Tooltip("DifficultyManager optionnel (auto-résolu si dans la scène).")]
        [SerializeField] private DifficultyManager _difficultyManager;

        [Header("Debug")]
        [Tooltip("Vrai si la mission doit démarrer automatiquement sur Start().")]
        [SerializeField] private bool _autoStart = true;

        /// <summary>Événement local de démarrage de mission.</summary>
        public event Action<MissionDto> OnMissionStarted;

        /// <summary>Événement local de complétion (succès).</summary>
        public event Action<MissionDto, int> OnMissionSucceeded;

        /// <summary>Événement local d'échec.</summary>
        public event Action<MissionDto, string> OnMissionFailed;

        /// <summary>Données de la mission courante.</summary>
        public MissionDto Mission { get; private set; }

        /// <summary>Liste des trackers d'objectifs.</summary>
        public IReadOnlyList<ObjectiveTracker> Objectives => _objectives;

        /// <summary>Vrai si la mission est en cours.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Temps écoulé depuis le début de la mission (secondes).</summary>
        public float ElapsedTime { get; private set; }

        /// <summary>Temps restant avant échec (0 = pas de limite).</summary>
        public float TimeRemaining { get; private set; }

        /// <summary>Score courant (cumul des kills/objectifs).</summary>
        public int Score { get; private set; }

        /// <summary>Vrai si le joueur est mort durant la mission (malus récompenses).</summary>
        public bool PlayerDiedDuringMission { get; set; }

        /// <summary>Vrai si aucune alerte n'a été déclenchée (bonus furtif).</summary>
        public bool StealthMaintained { get; private set; } = true;

        private readonly List<ObjectiveTracker> _objectives = new(8);
        private IDisposable _enemyKilledSub;
        private IDisposable _lootPickupSub;
        private bool _missionEnded;

        private void Reset()
        {
            _spawner = GetComponent<EnemySpawner>();
            _difficultyManager = FindFirstObjectByType<DifficultyManager>();
        }

        private void Awake()
        {
            if (_spawner == null) _spawner = GetComponent<EnemySpawner>();
            if (_difficultyManager == null) _difficultyManager = FindFirstObjectByType<DifficultyManager>();
        }

        private void Start()
        {
            if (_autoStart && !IsRunning)
            {
                StartMission(_missionId);
            }
        }

        private void Update()
        {
            if (!IsRunning || _missionEnded) return;

            ElapsedTime += Time.deltaTime;

            // Compte à rebours.
            if (TimeRemaining > 0f)
            {
                TimeRemaining -= Time.deltaTime;
                if (TimeRemaining <= 0f)
                {
                    TimeRemaining = 0f;
                    FailMission("time_out");
                    return;
                }
            }

            // Tick des objectifs SurviveTime.
            for (int i = 0; i < _objectives.Count; i++)
            {
                _objectives[i].Tick(Time.deltaTime);
            }
        }

        private void OnEnable()
        {
            // Souscription au bus global pour les events de kill / loot.
            _enemyKilledSub = GameEventBus.Instance.Subscribe<EnemyKilledEvent>(HandleEnemyKilled);
            _lootPickupSub = GameEventBus.Instance.Subscribe<LootPickupEvent>(HandleLootPickup);
        }

        private void OnDisable()
        {
            _enemyKilledSub?.Dispose();
            _lootPickupSub?.Dispose();
            _enemyKilledSub = null;
            _lootPickupSub = null;
        }

        // =================================================================================
        //  DÉMARRAGE / ARRÊT
        // =================================================================================

        /// <summary>Démarre la mission par Id (charge via DataLoader).</summary>
        public void StartMission(string missionId)
        {
            if (IsRunning)
            {
                Debug.LogWarning("[MissionDirector] Mission déjà en cours.");
                return;
            }

            // Résolution data.
            if (_missionSoOverride != null)
            {
                Mission = MissionDtoFromSO(_missionSoOverride);
            }
            else
            {
                Mission = DataLoader.GetMission(missionId);
            }

            if (Mission == null)
            {
                Debug.LogError($"[MissionDirector] Mission '{missionId}' introuvable.");
                return;
            }

            // Initialisation objectifs.
            _objectives.Clear();
            if (Mission.Objectives != null)
            {
                foreach (var obj in Mission.Objectives)
                {
                    var tracker = new ObjectiveTracker(obj);
                    tracker.OnCompleted += HandleObjectiveCompleted;
                    _objectives.Add(tracker);
                }
            }

            // Temps limite.
            ElapsedTime = 0f;
            TimeRemaining = Mission.TimeLimit > 0 ? Mission.TimeLimit : 0f;
            Score = 0;
            PlayerDiedDuringMission = false;
            StealthMaintained = true;
            _missionEnded = false;
            IsRunning = true;

            // Notification au DifficultyManager.
            if (_difficultyManager != null)
            {
                _difficultyManager.OnMissionStart(this);
            }

            // Démarrage des vagues (si spawner présent).
            if (_spawner != null && Mission.Waves != null && Mission.Waves.Count > 0)
            {
                _spawner.OnAllWavesCompleted += HandleAllWavesCompleted;
                _spawner.StartMission(Mission);
            }

            // Si la mission a des phases de boss (Assassination/BossRush), gère séparément.
            // Le BossController est placé dans la scène, et son InitializeBoss est appelé
            // par un sous-système de spawn dédié (ou par le MissionDirector si hook configuré).
            if (Mission.BossPhases != null && Mission.BossPhases.Count > 0)
            {
                InitializeBosses();
            }

            OnMissionStarted?.Invoke(Mission);
            Debug.Log($"[MissionDirector] Mission '{Mission.Id}' démarrée. " +
                      $"{_objectives.Count} objectifs, {Mission.Waves?.Count ?? 0} vagues, " +
                      $"{Mission.BossPhases?.Count ?? 0} phases de boss.");
        }

        /// <summary>Termine la mission avec succès.</summary>
        public void CompleteMission()
        {
            if (_missionEnded) return;
            _missionEnded = true;
            IsRunning = false;

            // Score final : somme des récompenses + bonus.
            int baseScore = Mission?.Rewards?.Xp ?? 0;
            int bonusScore = ComputeBonusScore();
            Score = baseScore + bonusScore;

            bool perfectClear = !PlayerDiedDuringMission && bonusScore > 0;

            // Publication MissionCompleteEvent (zero-alloc).
            if (GameEventBus.Instance != null && Mission != null)
            {
                GameEventBus.Instance.Publish(new MissionCompleteEvent(
                    Mission.Id, ElapsedTime, Score, perfectClear));
            }

            // Calcul des récompenses finales.
            MissionRewards.ComputeAndPublish(Mission, this);

            OnMissionSucceeded?.Invoke(Mission, Score);

            // Notification au spawner (pour cleanup).
            _spawner?.StopMission();

            Debug.Log($"[MissionDirector] Mission '{Mission?.Id}' COMPLÉTÉE. Score={Score}, " +
                      $"PerfectClear={perfectClear}, Durée={ElapsedTime:F1}s.");
        }

        /// <summary>Termine la mission avec échec.</summary>
        /// <param name="cause">Cause d'échec (ex: "time_out", "player_died", "objective_failed").</param>
        public void FailMission(string cause)
        {
            if (_missionEnded) return;
            _missionEnded = true;
            IsRunning = false;

            if (GameEventBus.Instance != null && Mission != null)
            {
                // Pas d'événement dédié pour fail ; on log via telemetry futur.
                Debug.Log($"[MissionDirector] Mission '{Mission.Id}' ÉCHOUÉE. Cause={cause}.");
            }

            OnMissionFailed?.Invoke(Mission, cause);

            _spawner?.StopMission();

            // Telemetry.
            if (TelemetryLogger.Instance != null && Mission != null)
            {
                TelemetryLogger.Instance.TrackMissionFail(Mission.Id, cause);
            }
        }

        /// <summary>Arrête prématurément la mission (sans succès ni échec formel).</summary>
        public void AbortMission()
        {
            IsRunning = false;
            _missionEnded = true;
            _spawner?.StopMission();
        }

        // =================================================================================
        //  BOSS INITIALIZATION
        // =================================================================================

        private void InitializeBosses()
        {
            if (Mission?.BossPhases == null || Mission.BossPhases.Count == 0) return;
            var bosses = FindObjectsByType<BossController>(FindObjectsSortMode.None);
            if (bosses.Length == 0)
            {
                Debug.LogWarning("[MissionDirector] Mission a des BossPhases mais aucun BossController dans la scène.");
                return;
            }
            // Convertit les BossPhaseDto en BossPhaseData (compat éditeur/runtime).
            var phasesData = new List<BossPhaseData>(Mission.BossPhases.Count);
            foreach (var dto in Mission.BossPhases)
            {
                phasesData.Add(new BossPhaseData
                {
                    Id = dto.Id,
                    Phase = dto.Phase,
                    EnemyId = dto.EnemyId,
                    HealthThresholdPct = dto.HealthThresholdPct,
                    EnrageTimer = dto.EnrageTimer,
                    Description = dto.Description
                });
            }
            float hpMult = _difficultyManager?.CurrentHpMult ?? 1f;
            float dmgMult = _difficultyManager?.CurrentDamageMult ?? 1f;
            foreach (var boss in bosses)
            {
                boss.InitializeBoss(_spawner, phasesData, hpMult, dmgMult);
            }
        }

        // =================================================================================
        //  EVENT HANDLERS
        // =================================================================================

        private void HandleEnemyKilled(EnemyKilledEvent evt)
        {
            if (!IsRunning) return;

            // Score + récompenses.
            Score += evt.XpReward;

            // Notification aux trackers (KillTarget/Assassinate).
            // Note : l'evt ne contient pas l'enemyId string (seulement l'InstanceId uint) ;
            // on ne peut pas matcher directement par TargetId. Pour permettre le matching,
            // on s'appuie sur une convention : tous les ennemis d'un même type partagent le
            // même EnemySO.Id, et le spawner peut exposer un mapping InstanceId -> EnemyId.
            if (_spawner != null)
            {
                string enemyType = ResolveEnemyTypeFromInstanceId(evt.EnemyId);
                if (!string.IsNullOrEmpty(enemyType))
                {
                    for (int i = 0; i < _objectives.Count; i++)
                    {
                        _objectives[i].NotifyKill(enemyType);
                    }
                }
            }

            // Update DifficultyManager.
            _difficultyManager?.OnEnemyKilled(evt);
        }

        private void HandleLootPickup(LootPickupEvent evt)
        {
            if (!IsRunning) return;
            for (int i = 0; i < _objectives.Count; i++)
            {
                _objectives[i].NotifyCollect(evt.ItemId);
            }
        }

        private void HandleObjectiveCompleted(ObjectiveTracker tracker)
        {
            Debug.Log($"[MissionDirector] Objectif complété: {tracker}");
            // Si tous les objectifs requis sont complétés, mission réussie.
            bool allRequiredComplete = true;
            for (int i = 0; i < _objectives.Count; i++)
            {
                if (!_objectives[i].IsOptional && !_objectives[i].IsComplete)
                {
                    allRequiredComplete = false;
                    break;
                }
            }
            if (allRequiredComplete)
            {
                CompleteMission();
            }
        }

        private void HandleAllWavesCompleted()
        {
            // Pour les missions Survival/Wave-based, complétion de toutes les vagues = mission complète
            // si aucun objectif Assassinate n'est en attente.
            bool hasAssassinatePending = false;
            for (int i = 0; i < _objectives.Count; i++)
            {
                if (!_objectives[i].IsComplete &&
                    (_objectives[i].Data.Kind == ObjectiveKind.Assassinate))
                {
                    hasAssassinatePending = true;
                    break;
                }
            }
            if (!hasAssassinatePending)
            {
                CompleteMission();
            }
        }

        // =================================================================================
        //  PUBLIC HOOKS (appelés par les triggers de scène)
        // =================================================================================

        /// <summary>Notifie qu'une zone d'atteinte a été triggerée (par ExtractionZone, TriggerEnter, etc.).</summary>
        public void NotifyReachPoint(string zoneId)
        {
            for (int i = 0; i < _objectives.Count; i++)
            {
                _objectives[i].NotifyReach(zoneId);
            }
        }

        /// <summary>Notifie qu'une cible sabotage a été détruite.</summary>
        public void NotifySabotageCore(string targetId)
        {
            for (int i = 0; i < _objectives.Count; i++)
            {
                _objectives[i].NotifySabotage(targetId);
            }
        }

        /// <summary>Notifie que l'extraction a été complétée.</summary>
        public void NotifyExtractionComplete()
        {
            for (int i = 0; i < _objectives.Count; i++)
            {
                _objectives[i].NotifyExtract();
            }
        }

        /// <summary>Notifie un tick de défense (par la zone de défense, chaque seconde).</summary>
        public void NotifyDefendTick()
        {
            for (int i = 0; i < _objectives.Count; i++)
            {
                _objectives[i].NotifyDefendTick();
            }
        }

        /// <summary>Marque la mission comme ayant perdu la furtivité (alerte déclenchée).</summary>
        public void NotifyStealthBroken()
        {
            StealthMaintained = false;
        }

        // =================================================================================
        //  HELPERS
        // =================================================================================

        private int ComputeBonusScore()
        {
            int bonus = 0;
            // Bonus "no death".
            if (!PlayerDiedDuringMission) bonus += 1000;
            // Bonus "all optional objectives".
            bool allOptionalDone = true;
            foreach (var obj in _objectives)
            {
                if (obj.IsOptional && !obj.IsComplete) { allOptionalDone = false; break; }
            }
            if (allOptionalDone) bonus += 1500;
            // Bonus furtif.
            if (StealthMaintained && (Mission?.StealthOptional ?? false)) bonus += 2000;
            // Bonus de temps (si moins de 50% du temps limite utilisé).
            if (Mission?.TimeLimit > 0 && ElapsedTime < Mission.TimeLimit * 0.5f) bonus += 1200;
            return bonus;
        }

        /// <summary>Résout l'EnemySO.Id d'un ennemi depuis son InstanceId.</summary>
        private string ResolveEnemyTypeFromInstanceId(uint instanceId)
        {
            if (_spawner == null) return string.Empty;
            // Recherche dans les ennemis actifs (potentiellement déjà released du pool
            // mais la liste _activeEnemies est mise à jour à la mort).
            var active = _spawner.ActiveEnemies;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] != null && active[i].InstanceId == instanceId)
                {
                    return active[i].Data?.Id ?? string.Empty;
                }
            }
            // Si l'ennemi est déjà retourné au pool, on ne peut plus le résoudre ici.
            // En pratique, on devrait maintenir un mapping InstanceId -> EnemyId à la mort,
            // mais l'EnemyKilledEvent étant publié AVANT le retour au pool, le spawner
            // l'a encore en référence. Le code ci-dessus couvre ce cas.
            return string.Empty;
        }

        /// <summary>Convertit un MissionSO (authoring éditeur) en MissionDto runtime.</summary>
        private static MissionDto MissionDtoFromSO(MissionSO so)
        {
            var dto = new MissionDto
            {
                Id = so.Id,
                DisplayName = so.DisplayName,
                Description = so.Description,
                Type = so.Type,
                Region = so.Region,
                RecommendedPower = so.RecommendedPower,
                TimeLimit = so.TimeLimit,
                StealthOptional = so.StealthOptional,
                SceneName = so.SceneName
            };
            foreach (var o in so.Objectives)
            {
                dto.Objectives.Add(new MissionObjectiveDto
                {
                    Id = o.Id,
                    Description = o.Description,
                    Kind = o.Kind,
                    TargetId = o.TargetId,
                    RequiredCount = o.RequiredCount,
                    RewardXp = o.RewardXp,
                    RewardCr = o.RewardCr
                });
            }
            foreach (var w in so.Waves)
            {
                dto.Waves.Add(new EnemySpawnWaveDto
                {
                    Id = w.Id,
                    Delay = w.Delay,
                    EnemyId = w.EnemyId,
                    Count = w.Count,
                    SpawnPoint = new Vector3Dto { X = w.SpawnPoint.x, Y = w.SpawnPoint.y, Z = w.SpawnPoint.z }
                });
            }
            foreach (var bp in so.BossPhases)
            {
                dto.BossPhases.Add(new BossPhaseDto
                {
                    Id = bp.Id,
                    Phase = bp.Phase,
                    EnemyId = bp.EnemyId,
                    HealthThresholdPct = bp.HealthThresholdPct,
                    EnrageTimer = bp.EnrageTimer,
                    Description = bp.Description
                });
            }
            if (so.Rewards != null)
            {
                dto.Rewards = new RewardDataDto
                {
                    Xp = so.Rewards.Xp,
                    Cr = so.Rewards.Cr
                };
                foreach (var l in so.Rewards.LootTable)
                {
                    dto.Rewards.LootTable.Add(new LootTableEntryDto
                    {
                        ItemId = l.ItemId,
                        DropChancePct = l.DropChancePct,
                        MinQty = l.MinQty,
                        MaxQty = l.MaxQty
                    });
                }
            }
            if (so.Environment != null)
            {
                dto.Environment = new EnvironmentDataDto
                {
                    ShipType = so.Environment.ShipType,
                    Lighting = so.Environment.Lighting,
                    Atmosphere = so.Environment.Atmosphere
                };
            }
            return dto;
        }
    }
}
