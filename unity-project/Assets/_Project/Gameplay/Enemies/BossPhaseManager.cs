using System;
using System.Collections;
using System.Collections.Generic;
using KINETICS5.Core;
using KINETICS5.Data;
using UnityEngine;

namespace KINETICS5.Gameplay.Enemies
{
    /// <summary>
    /// Pilote les phases d'un combat de boss. Lit <see cref="BossPhaseData"/> depuis la mission,
    /// surveille les seuils de PV, exécute les patterns d'attaque par phase, et déclenche les
    /// VFX de transition (slow-mo via <see cref="TimeManager"/>, screen shake via
    /// <see cref="CameraManager"/>).
    /// </summary>
    /// <remarks>
    /// <para><b>Phases standard :</b></para>
    /// <list type="bullet">
    ///   <item><b>Phase 1</b> (100% → 66%) : attaques normales, patterns simples.</item>
    ///   <item><b>Phase 2</b> (66% → 33%) : enragé, nouveaux patterns (AoE, adds).</item>
    ///   <item><b>Phase 3</b> (33% → 0%) : désespéré, invocation d'adds, enrage définitif.</item>
    /// </list>
    /// <para>
    /// Les patterns d'attaque sont exécutés via <see cref="EnemyCombat.ForceAttack"/> avec un
    /// cycle aléatoire parmi les patterns disponibles pour la phase courante.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class BossPhaseManager : MonoBehaviour
    {
        [Header("Patterns par phase")]
        [Tooltip("Patterns disponibles en Phase 1 (cycle aléatoire).")]
        [SerializeField] private EnemyAttackType[] _phase1Patterns =
            { EnemyAttackType.Ranged, EnemyAttackType.Ranged, EnemyAttackType.Melee };
        [Tooltip("Patterns disponibles en Phase 2 (enragé).")]
        [SerializeField] private EnemyAttackType[] _phase2Patterns =
            { EnemyAttackType.Ranged, EnemyAttackType.AoESlam, EnemyAttackType.Charge };
        [Tooltip("Patterns disponibles en Phase 3 (désespéré).")]
        [SerializeField] private EnemyAttackType[] _phase3Patterns =
            { EnemyAttackType.AoESlam, EnemyAttackType.Charge, EnemyAttackType.GrenadeToss };

        [Header("Invocation d'adds (Phase 3)")]
        [Tooltip("Id de l'ennemi à invoquer en Phase 3 (ex: \"swarm_bot\").")]
        [SerializeField] private string _addEnemyId = "swarm_bot";
        [Tooltip("Nombre d'adds invoqués à chaque transition en Phase 3.")]
        [Range(1, 8)][SerializeField] private int _addCount = 3;
        [Tooltip("Cooldown entre deux invocations d'adds en Phase 3 (secondes).")]
        [SerializeField] private float _addSummonCooldown = 20f;

        [Header("Timing patterns")]
        [Tooltip("Intervalle minimum entre deux patterns (secondes).")]
        [SerializeField] private float _patternInterval = 3f;
        [Tooltip("Vrai si le pattern est forcé même si l'ennemi est en attaque (interrupt).")]
        [SerializeField] private bool _interruptCurrentAttack = true;

        /// <summary>Événement déclenché à chaque changement de phase.</summary>
        public event Action<int, BossPhaseData> OnPhaseTransition;

        private BossController _boss;
        private List<BossPhaseData> _phases;
        private float _damageMult;
        private int _currentPhaseIndex = -1;
        private Coroutine _patternCoroutine;
        private Coroutine _addsCoroutine;
        private float _nextPatternTime;
        private float _nextAddSummonTime;
        private EnemyCombat _combat;
        private EnemySpawner _spawner;

        /// <summary>Phase courante (1-indexed ; -1 si non démarré).</summary>
        public int CurrentPhase => _currentPhaseIndex >= 0 ? _phases[_currentPhaseIndex].Phase : 1;

        /// <summary>Initialise le manager avec les données de phases et le boss propriétaire.</summary>
        public void Initialize(BossController boss, List<BossPhaseData> phases, float damageMult = 1f)
        {
            _boss = boss;
            _phases = phases;
            _damageMult = damageMult;
            _combat = boss.Combat;
            _spawner = GetComponentInParent<EnemySpawner>();
            if (_spawner == null)
            {
                // Fallback : chercher dans la scène.
                _spawner = FindFirstObjectByType<EnemySpawner>();
            }
            _currentPhaseIndex = 0; // Phase 1 par défaut (BossController.InitializeBoss appelle SetPhase(1))
            _nextPatternTime = Time.time + 2f; // premier pattern 2s après init

            // Démarrage des patterns.
            if (_patternCoroutine != null) StopCoroutine(_patternCoroutine);
            _patternCoroutine = StartCoroutine(PatternLoopCoroutine());
        }

        private void Update()
        {
            if (_boss == null || _boss.IsDead || _phases == null || _phases.Count == 0) return;

            // Vérification du seuil de phase (transition descendante).
            CheckPhaseTransition();
        }

        private void OnDestroy()
        {
            if (_patternCoroutine != null) StopCoroutine(_patternCoroutine);
            if (_addsCoroutine != null) StopCoroutine(_addsCoroutine);
        }

        // =================================================================================
        //  TRANSITIONS DE PHASE
        // =================================================================================

        private void CheckPhaseTransition()
        {
            if (_boss.Health == null) return;
            float hpPct = _boss.Health.CurrentHealth / (_boss.Health.MaxHealth * _boss.Health.HealthScale);

            // On cherche la phase la plus avancée dont le seuil est atteint.
            // Les phases sont triées par seuil décroissant (Phase 1 à 100%, Phase 2 à 66%, Phase 3 à 33%).
            int targetIndex = 0;
            for (int i = 0; i < _phases.Count; i++)
            {
                if (hpPct <= _phases[i].HealthThresholdPct)
                {
                    targetIndex = i;
                }
                else
                {
                    break;
                }
            }

            if (targetIndex > _currentPhaseIndex)
            {
                TransitionToPhase(targetIndex);
            }
        }

        private void TransitionToPhase(int newIndex)
        {
            int oldIndex = _currentPhaseIndex;
            _currentPhaseIndex = newIndex;
            var phase = _phases[newIndex];

            // Notification au BossController (invulnérabilité + VFX).
            _boss.SetPhase(phase.Phase);

            // Pattern loop redémarre avec les patterns de la nouvelle phase.
            // (le coroutine PatternLoopCoroutine lit CurrentPhase à chaque itération.)

            // Enrage définitif si Phase 3 avec enrageTimer.
            if (phase.EnrageTimer > 0f && newIndex == _phases.Count - 1)
            {
                _boss.SetInvulnerable(true, 1.5f);
                // Le timer d'enrage est déjà géré par BossController._enrageTimer.
            }

            // Invocation d'adds en Phase 3.
            if (newIndex == _phases.Count - 1 && _spawner != null && !string.IsNullOrEmpty(_addEnemyId))
            {
                _addsCoroutine = StartCoroutine(AddsSummonLoopCoroutine());
            }

            OnPhaseTransition?.Invoke(phase.Phase, phase);
        }

        // =================================================================================
        //  PATTERN LOOP
        // =================================================================================

        private IEnumerator PatternLoopCoroutine()
        {
            while (_boss != null && !_boss.IsDead)
            {
                if (Time.time >= _nextPatternTime && _combat != null && !_boss.IsInvulnerable)
                {
                    EnemyAttackType pattern = PickPatternForPhase(CurrentPhase);
                    _combat.ForceAttack(pattern, _patternInterval * 0.8f, _damageMult);
                    _nextPatternTime = Time.time + _patternInterval;
                }
                yield return new WaitForSeconds(0.5f); // check toutes les 0.5s
            }
        }

        private EnemyAttackType PickPatternForPhase(int phase)
        {
            EnemyAttackType[] patterns = phase switch
            {
                1 => _phase1Patterns,
                2 => _phase2Patterns,
                3 => _phase3Patterns,
                _ => _phase1Patterns
            };
            if (patterns == null || patterns.Length == 0) return EnemyAttackType.Ranged;
            return patterns[UnityEngine.Random.Range(0, patterns.Length)];
        }

        // =================================================================================
        //  INVOCATION D'ADDS
        // =================================================================================

        private IEnumerator AddsSummonLoopCoroutine()
        {
            _nextAddSummonTime = Time.time + 2f; // premier summon 2s après entrée en Phase 3
            while (_boss != null && !_boss.IsDead && _spawner != null)
            {
                if (Time.time >= _nextAddSummonTime)
                {
                    SummonAdds();
                    _nextAddSummonTime = Time.time + _addSummonCooldown;
                }
                yield return new WaitForSeconds(1f);
            }
        }

        private void SummonAdds()
        {
            if (_spawner == null || string.IsNullOrEmpty(_addEnemyId)) return;
            Vector3 center = _boss.transform.position;
            for (int i = 0; i < _addCount; i++)
            {
                float angle = (i * 360f / _addCount) * Mathf.Deg2Rad;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * 4f, 0f, Mathf.Sin(angle) * 4f);
                _spawner.SpawnEnemy(_addEnemyId, pos);
            }

            // VFX d'invocation : slow-mo bref.
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.TriggerSlowMotion(0.4f, 0.3f);
            }
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.Shake(1.5f, 0.3f, 0.5f);
            }
        }

        // =================================================================================
        //  DEBUG
        // =================================================================================

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_boss == null) return;
            // Affiche le seuil de phase courant.
            UnityEditor.Handles.Label(_boss.transform.position + Vector3.up * 3f,
                                       $"Phase {CurrentPhase} / {_phases?.Count ?? 0}");
        }
#endif
    }
}
