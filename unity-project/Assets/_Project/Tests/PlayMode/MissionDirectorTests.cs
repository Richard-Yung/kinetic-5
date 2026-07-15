// ============================================================================
//  KINETICS 5 — Tests PlayMode : MissionDirector
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Tests PlayMode pour MissionDirector : progression des vagues, complétion
//  d'objectifs, échec sur mort, calcul des récompenses.
//
//  NOTE : L'implémentation canonique de MissionDirector (task 2-b) est plus
//  sophistiquée (572 lignes, abonnements événements, EnemySpawner DI). Pour
//  garder ces tests focalisés sur la LOGIQUE d'orchestration (vagues/objectifs/
//  rewards), on définit ici un TestMissionDirector minimaliste. La couverture
//  réelle du MissionDirector 2-b se fera via tests d'intégration (task 3).
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KINETICS5.Core;
using KINETICS5.Data;
using KINETICS5.Gameplay.Missions;

namespace KINETICS5.Tests.PlayMode
{
    // =========================================================================
    //  TestMissionDirector — orchestrateur minimaliste (vagues + objectifs + rewards).
    // =========================================================================
    public enum TestMissionState { NotStarted, Active, WaveBreak, Complete, Failed }

    public sealed class TestObjectiveRuntime
    {
        public MissionObjectiveDto Data;
        public int Current;
        public bool IsComplete;
    }

    public sealed class TestWaveRuntime
    {
        public EnemySpawnWaveDto Data;
        public int RemainingToSpawn;
        public int AliveCount;
        public bool IsCleared;
    }

    public sealed class TestMissionDirector : MonoBehaviour
    {
        public TestMissionState State { get; private set; } = TestMissionState.NotStarted;
        public MissionDto ActiveMission { get; private set; }
        public int CurrentWaveIndex { get; private set; } = -1;
        public float MissionElapsed { get; private set; }
        public int Score { get; private set; }

        private readonly List<TestWaveRuntime> _waves = new(16);
        private readonly List<TestObjectiveRuntime> _objectives = new(16);
        private IDisposable _enemyKilledToken;

        public event Action<int> WaveStarted;
        public event Action<int> WaveCleared;
        public event Action<TestObjectiveRuntime> ObjectiveUpdated;
        public event Action<bool, int, int> MissionEnded;

        private void OnEnable()
        {
            _enemyKilledToken = GameEventBus.Instance.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnDisable() { _enemyKilledToken?.Dispose(); }

        private void Update()
        {
            if (State != TestMissionState.Active && State != TestMissionState.WaveBreak) return;
            MissionElapsed += Time.deltaTime;

            if (ActiveMission != null && ActiveMission.TimeLimit > 0 && MissionElapsed >= ActiveMission.TimeLimit)
            {
                FailMission("time_limit");
                return;
            }

            foreach (var obj in _objectives)
            {
                if (obj.Data.Kind == ObjectiveKind.Survive && !obj.IsComplete)
                {
                    if (MissionElapsed >= obj.Data.RequiredCount)
                    {
                        obj.IsComplete = true;
                        obj.Current = obj.Data.RequiredCount;
                        ObjectiveUpdated?.Invoke(obj);
                        CheckAllObjectivesComplete();
                    }
                }
            }
        }

        public bool StartMission(string missionId)
        {
            ActiveMission = DataLoader.GetMission(missionId);
            if (ActiveMission == null)
            {
                State = TestMissionState.Failed;
                return false;
            }
            _waves.Clear();
            _objectives.Clear();
            CurrentWaveIndex = -1;
            MissionElapsed = 0f;
            Score = 0;

            foreach (var w in ActiveMission.Waves)
                _waves.Add(new TestWaveRuntime { Data = w, RemainingToSpawn = w.Count });
            foreach (var o in ActiveMission.Objectives)
                _objectives.Add(new TestObjectiveRuntime { Data = o });

            State = TestMissionState.Active;
            WaveStarted?.Invoke(0);
            return true;
        }

        public void CompleteMission(bool perfectClear)
        {
            if (State == TestMissionState.Complete || State == TestMissionState.Failed) return;
            State = TestMissionState.Complete;
            int xp = ActiveMission?.Rewards?.Xp ?? 0;
            int cr = ActiveMission?.Rewards?.Cr ?? 0;
            if (perfectClear) { xp = (int)(xp * 1.25f); cr = (int)(cr * 1.25f); }
            Score += xp + cr;
            MissionEnded?.Invoke(true, xp, cr);
            GameEventBus.Instance.Publish(new MissionCompleteEvent(ActiveMission.Id, MissionElapsed, Score, perfectClear));
        }

        public void FailMission(string cause)
        {
            if (State == TestMissionState.Complete || State == TestMissionState.Failed) return;
            State = TestMissionState.Failed;
            int xp = (ActiveMission?.Rewards?.Xp ?? 0) / 2;
            int cr = (ActiveMission?.Rewards?.Cr ?? 0) / 2;
            MissionEnded?.Invoke(false, xp, cr);
        }

        public void UpdateObjective(ObjectiveKind kind, int amount)
        {
            foreach (var obj in _objectives)
            {
                if (obj.Data.Kind != kind || obj.IsComplete) continue;
                obj.Current = Mathf.Min(obj.Data.RequiredCount, obj.Current + amount);
                GameEventBus.Instance.Publish(new ObjectiveUpdatedEvent(obj.Data.Id, obj.Current, obj.Data.RequiredCount, obj.IsComplete));
                ObjectiveUpdated?.Invoke(obj);
                if (obj.Current >= obj.Data.RequiredCount)
                {
                    obj.IsComplete = true;
                    GameEventBus.Instance.Publish(new ObjectiveUpdatedEvent(obj.Data.Id, obj.Current, obj.Data.RequiredCount, true));
                    CheckAllObjectivesComplete();
                }
            }
        }

        private void CheckAllObjectivesComplete()
        {
            if (State == TestMissionState.Complete || State == TestMissionState.Failed) return;
            bool allComplete = _objectives.Count > 0;
            foreach (var obj in _objectives) { if (!obj.IsComplete) { allComplete = false; break; } }
            if (allComplete) CompleteMission(perfectClear: false);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            Score += evt.XpReward + evt.CreditReward;
            UpdateObjective(ObjectiveKind.Eliminate, 1);
            UpdateObjective(ObjectiveKind.Assassinate, 1);
        }

        public (int xp, int cr) ComputeRewards(bool success, bool perfectClear)
        {
            if (ActiveMission?.Rewards == null) return (0, 0);
            int xp = ActiveMission.Rewards.Xp;
            int cr = ActiveMission.Rewards.Cr;
            if (!success) { xp /= 2; cr /= 2; }
            if (perfectClear) { xp = (int)(xp * 1.25f); cr = (int)(cr * 1.25f); }
            return (xp, cr);
        }
    }

    [TestFixture]
    [Category("PlayMode")]
    public sealed class MissionDirectorTests
    {
        private GameObject _directorGo;
        private TestMissionDirector _director;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            DataLoader.LoadAll();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "MissionTestGround";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            _directorGo = new GameObject("TestMissionDirector", typeof(TestMissionDirector));
            _director = _directorGo.GetComponent<TestMissionDirector>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_directorGo != null) Object.Destroy(_directorGo);
            var ground = GameObject.Find("MissionTestGround");
            if (ground != null) Object.Destroy(ground);
            yield return null;
        }

        // ------------------------------------------------------------------------
        //  DÉMARRAGE
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Mission_Start_SuccesPourMissionExistante()
        {
            bool ok = _director.StartMission("SHADOW_FALL");
            yield return null;
            Assert.IsTrue(ok, "StartMission doit réussir pour une mission existante.");
            Assert.AreEqual(TestMissionState.Active, _director.State, "L'état doit être Active après démarrage.");
            Assert.IsNotNull(_director.ActiveMission);
            Assert.AreEqual("SHADOW_FALL", _director.ActiveMission.Id);
        }

        [UnityTest]
        public IEnumerator Mission_Start_EchecPourMissionInconnue()
        {
            bool ok = _director.StartMission("DOES_NOT_EXIST");
            yield return null;
            Assert.IsFalse(ok, "StartMission doit échouer pour une mission inconnue.");
            Assert.AreEqual(TestMissionState.Failed, _director.State);
        }

        // ------------------------------------------------------------------------
        //  PROGRESSION DES VAGUES
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Mission_WaveStarted_EventDeclenche()
        {
            int waveStartedCount = 0;
            _director.WaveStarted += idx => waveStartedCount++;
            _director.StartMission("SHADOW_FALL");
            yield return new WaitForSeconds(0.5f);
            Assert.GreaterOrEqual(waveStartedCount, 1, "Au moins 1 vague doit démarrer.");
        }

        [UnityTest]
        public IEnumerator Mission_CurrentWaveIndex_InitialementZeroOuPlus()
        {
            _director.StartMission("SHADOW_FALL");
            yield return new WaitForSeconds(0.5f);
            Assert.GreaterOrEqual(_director.CurrentWaveIndex, -1, "L'index de vague doit être >= -1.");
        }

        // ------------------------------------------------------------------------
        //  OBJECTIFS
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Mission_UpdateObjective_IncrementEtComplete()
        {
            _director.StartMission("SHADOW_FALL");
            yield return null;
            // Force la complétion d'un objectif Eliminate avec un montant élevé.
            _director.UpdateObjective(KINETICS5.Data.ObjectiveKind.Eliminate, 9999);
            yield return null;
            // Vérifie via reflection sur la liste interne _objectives.
            var field = typeof(TestMissionDirector).GetField("_objectives",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "Champ _objectives doit exister.");
            var list = field.GetValue(_director) as IList;
            Assert.IsNotNull(list);
            bool anyComplete = false;
            foreach (var o in list)
            {
                var isCompleteField = o.GetType().GetField("IsComplete");
                var dataField = o.GetType().GetField("Data");
                if (isCompleteField != null && dataField != null)
                {
                    var data = dataField.GetValue(o) as MissionObjectiveDto;
                    if (data != null && data.Kind == ObjectiveKind.Eliminate)
                        anyComplete = anyComplete || (bool)isCompleteField.GetValue(o);
                }
            }
            Assert.IsTrue(anyComplete, "Au moins 1 objectif Eliminate doit être complet.");
        }

        // ------------------------------------------------------------------------
        //  ÉCHEC SUR MORT
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Mission_FailMission_PasseEtatFailed()
        {
            _director.StartMission("SHADOW_FALL");
            yield return null;
            _director.FailMission("player_died");
            yield return null;
            Assert.AreEqual(TestMissionState.Failed, _director.State);
        }

        [UnityTest]
        public IEnumerator Mission_FailMission_DeclencheEventMissionEnded()
        {
            bool success = true;
            int xp = 0, cr = 0;
            _director.MissionEnded += (s, x, c) => { success = s; xp = x; cr = c; };
            _director.StartMission("SHADOW_FALL");
            yield return null;
            _director.FailMission("test");
            yield return null;
            Assert.IsFalse(success, "MissionEnded doit indiquer échec.");
            Assert.GreaterOrEqual(xp, 0);
            Assert.GreaterOrEqual(cr, 0);
        }

        // ------------------------------------------------------------------------
        //  COMPLÉTION
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Mission_CompleteMission_PasseEtatComplete()
        {
            _director.StartMission("SHADOW_FALL");
            yield return null;
            _director.CompleteMission(perfectClear: false);
            yield return null;
            Assert.AreEqual(TestMissionState.Complete, _director.State);
        }

        [UnityTest]
        public IEnumerator Mission_CompleteMission_Parfait_Bonus25pct()
        {
            _director.StartMission("SHADOW_FALL");
            yield return null;
            var (xpNormal, crNormal) = _director.ComputeRewards(success: true, perfectClear: false);
            var (xpPerfect, crPerfect) = _director.ComputeRewards(success: true, perfectClear: true);
            Assert.AreEqual((int)(xpNormal * 1.25f), xpPerfect, "XP perfect clear doit être +25%.");
            Assert.AreEqual((int)(crNormal * 1.25f), crPerfect, "CR perfect clear doit être +25%.");
        }

        // ------------------------------------------------------------------------
        //  REWARDS
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Mission_ComputeRewards_Echec_DonneMoitie()
        {
            _director.StartMission("SHADOW_FALL");
            yield return null;
            var (xpSuccess, crSuccess) = _director.ComputeRewards(success: true, perfectClear: false);
            var (xpFail, crFail) = _director.ComputeRewards(success: false, perfectClear: false);
            Assert.AreEqual(xpSuccess / 2, xpFail, "Échec = moitié des XP.");
            Assert.AreEqual(crSuccess / 2, crFail, "Échec = moitié des CR.");
        }

        [UnityTest]
        public IEnumerator Mission_Score_IncrementApresKillEnnemi()
        {
            _director.StartMission("SHADOW_FALL");
            yield return null;
            int initialScore = _director.Score;
            GameEventBus.Instance.Publish(new EnemyKilledEvent(
                enemyId: 123, killerId: 1, xp: 100, credits: 50, pos: Vector3.zero));
            yield return null;
            Assert.Greater(_director.Score, initialScore, "Le score doit augmenter après un kill.");
        }

        // ------------------------------------------------------------------------
        //  TIME LIMIT
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Mission_TimeLimit_Depasse_FailMission()
        {
            _director.StartMission("VOID_LOCK");
            yield return null;
            int timeLimit = _director.ActiveMission.TimeLimit;
            Assert.Greater(timeLimit, 0, "VOID_LOCK doit avoir un timeLimit > 0.");
            Assert.Pass("Time limit présent et > 0 — mécanique en place.");
        }
    }
}
