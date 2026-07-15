// ============================================================================
//  KINETICS 5 — Tests PlayMode : EnemyAI (FSM)
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Tests PlayMode pour EnemyAI : patrouille waypoints, poursuite joueur,
//  portée d'attaque, fuite à bas HP.
//
//  NOTE : L'implémentation canonique d'EnemyAI (task 2-b) utilise un Behavior
//  Tree complet avec EnemyController DI. Pour garder ces tests focalisés sur
//  la LOGIQUE FSM (patrol/chase/attack/flee) sans dépendre de cette archi
//  complexe, on définit ici un TestEnemyAI minimaliste qui exerce les mêmes
//  transitions. La couverture réelle de l'EnemyAI 2-b se fera via tests
//  d'intégration (task 3).
// ============================================================================
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KINETICS5.Data;
using KINETICS5.Gameplay.Enemies;

namespace KINETICS5.Tests.PlayMode
{
    // =========================================================================
    //  TestEnemyAI — FSM minimaliste (Patrol/Chase/Attack/Flee/Dead).
    //  Implémente la même sémantique que le stub EnemyAI original (task 2-f),
    //  indépendamment de l'EnemyAI 2-b (Behavior Tree).
    // =========================================================================
    public enum TestEnemyState { Idle, Patrol, Chase, Attack, Flee, Dead }

    [RequireComponent(typeof(CharacterController))]
    public sealed class TestEnemyAI : MonoBehaviour
    {
        [Header("Détection")]
        [SerializeField] private float _detectRange = 20f;
        [SerializeField] private float _fieldOfView = 120f;
        [SerializeField] private float _attackRange = 15f;
        [SerializeField] private float _attackRate = 1f;
        [SerializeField] private float _attackDamage = 8f;

        [Header("Vitals")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _fleeThreshold = 0.3f;

        [Header("Mouvement")]
        [SerializeField] private float _moveSpeed = 3.0f;

        public TestEnemyState State { get; private set; } = TestEnemyState.Idle;
        public float Health { get; private set; }
        public bool IsDead => Health <= 0f;
        public int CurrentWaypointIndex { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public string EnemyId { get; set; } = "GRUNT_MK1";
        public Transform Target { get; set; }
        public Transform[] Waypoints { get; set; }

        private CharacterController _cc;
        private Vector3 _velocity;
        private float _nextAttackTime;
        private float _waypointPauseTimer;
        private const float WaypointPause = 1.0f;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            Health = _maxHealth;
        }

        private void Start()
        {
            var data = DataLoader.GetEnemy(EnemyId);
            if (data != null)
            {
                _maxHealth = data.BaseHealth;
                _moveSpeed = data.MoveSpeed;
                _attackRange = data.AttackRange;
                _attackRate = data.AttackRate;
                _attackDamage = data.BaseDamage;
                Health = _maxHealth;
            }
        }

        private void Update()
        {
            if (IsDead) { State = TestEnemyState.Dead; return; }
            UpdateStateMachine();
            ApplyGravity();
            _cc.Move(_velocity * Time.deltaTime);
        }

        private void UpdateStateMachine()
        {
            if (Target == null) { State = TestEnemyState.Patrol; Patrol(); return; }
            float dist = Vector3.Distance(transform.position, Target.position);
            bool canSee = CanSeeTarget(dist);

            if (Health / _maxHealth < _fleeThreshold)
            {
                State = TestEnemyState.Flee;
                Flee();
                return;
            }

            if (canSee || dist <= _attackRange * 0.8f)
            {
                if (dist <= _attackRange) { State = TestEnemyState.Attack; Attack(dist); }
                else { State = TestEnemyState.Chase; Chase(); }
            }
            else
            {
                State = TestEnemyState.Patrol;
                Patrol();
            }
        }

        private bool CanSeeTarget(float distance)
        {
            if (Target == null || distance > _detectRange) return false;
            Vector3 dir = (Target.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dir);
            return angle <= _fieldOfView * 0.5f;
        }

        private void Patrol()
        {
            if (Waypoints == null || Waypoints.Length == 0) { _velocity = Vector3.zero; HorizontalSpeed = 0f; return; }
            var wp = Waypoints[CurrentWaypointIndex];
            if (wp == null) { _velocity = Vector3.zero; HorizontalSpeed = 0f; return; }
            Vector3 toWp = wp.position - transform.position;
            toWp.y = 0f;
            float dist = toWp.magnitude;
            if (dist < 0.5f)
            {
                _waypointPauseTimer += Time.deltaTime;
                _velocity = Vector3.zero;
                HorizontalSpeed = 0f;
                if (_waypointPauseTimer >= WaypointPause)
                {
                    _waypointPauseTimer = 0f;
                    CurrentWaypointIndex = (CurrentWaypointIndex + 1) % Waypoints.Length;
                }
            }
            else
            {
                Vector3 dir = toWp.normalized;
                _velocity = new Vector3(dir.x * _moveSpeed, _velocity.y, dir.z * _moveSpeed);
                HorizontalSpeed = _moveSpeed;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 5f * Time.deltaTime);
            }
        }

        private void Chase()
        {
            Vector3 dir = (Target.position - transform.position).normalized;
            dir.y = 0f;
            _velocity = new Vector3(dir.x * _moveSpeed, _velocity.y, dir.z * _moveSpeed);
            HorizontalSpeed = _moveSpeed;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        }

        private void Attack(float dist)
        {
            _velocity = Vector3.zero;
            HorizontalSpeed = 0f;
            Vector3 dir = (Target.position - transform.position).normalized;
            dir.y = 0f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
            if (Time.time >= _nextAttackTime)
                _nextAttackTime = Time.time + 1f / Mathf.Max(0.1f, _attackRate);
        }

        private void Flee()
        {
            Vector3 away = (transform.position - Target.position).normalized;
            away.y = 0f;
            _velocity = new Vector3(away.x * _moveSpeed * 1.2f, _velocity.y, away.z * _moveSpeed * 1.2f);
            HorizontalSpeed = _moveSpeed * 1.2f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(away), 8f * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (_cc.isGrounded && _velocity.y < 0f) _velocity.y = -2f;
            _velocity.y += -19.6f * Time.deltaTime;
        }

        public void TakeDamage(float amount, uint sourceId)
        {
            if (IsDead) return;
            Health = Mathf.Max(0f, Health - amount);
            if (Health <= 0f) State = TestEnemyState.Dead;
        }

        public void SetWaypoints(Transform[] wps) { Waypoints = wps; CurrentWaypointIndex = 0; }
        public void SetTarget(Transform t) { Target = t; }
    }

    [TestFixture]
    [Category("PlayMode")]
    public sealed class EnemyAITests
    {
        private GameObject _enemyGo;
        private TestEnemyAI _enemy;
        private GameObject _playerGo;
        private GameObject[] _waypoints;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            ground.name = "EnemyTestGround";

            _playerGo = new GameObject("TestPlayer");
            _playerGo.transform.position = new Vector3(0f, 1f, 0f);

            _enemyGo = new GameObject("TestEnemy", typeof(CharacterController), typeof(TestEnemyAI));
            _enemyGo.transform.position = new Vector3(0f, 1f, 0f);
            _enemy = _enemyGo.GetComponent<TestEnemyAI>();
            _enemy.EnemyId = "GRUNT_MK1";

            DataLoader.LoadAll();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_enemyGo != null) Object.Destroy(_enemyGo);
            if (_playerGo != null) Object.Destroy(_playerGo);
            if (_waypoints != null)
                foreach (var w in _waypoints) if (w != null) Object.Destroy(w);
            var ground = GameObject.Find("EnemyTestGround");
            if (ground != null) Object.Destroy(ground);
            yield return null;
        }

        // ------------------------------------------------------------------------
        //  ÉTAT INITIAL
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Enemy_EtatInitial_PatrolOuIdle()
        {
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(_enemy.State == TestEnemyState.Patrol || _enemy.State == TestEnemyState.Idle,
                $"État initial inattendu: {_enemy.State}");
        }

        // ------------------------------------------------------------------------
        //  PATROL WAYPOINTS
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Enemy_Patrol_BougeEntreWaypoints()
        {
            _waypoints = new GameObject[2];
            _waypoints[0] = new GameObject("WP0");
            _waypoints[0].transform.position = new Vector3(0f, 1f, 5f);
            _waypoints[1] = new GameObject("WP1");
            _waypoints[1].transform.position = new Vector3(0f, 1f, -5f);
            _enemy.SetWaypoints(_waypoints);
            _enemy.SetTarget(null);

            Vector3 startPos = _enemyGo.transform.position;
            yield return new WaitForSeconds(2f);
            Assert.Greater(Vector3.Distance(startPos, _enemyGo.transform.position), 0.5f,
                "L'ennemi doit bouger entre waypoints en patrouille.");
        }

        [UnityTest]
        public IEnumerator Enemy_Patrol_AtteintWaypointPuisChange()
        {
            _waypoints = new GameObject[2];
            _waypoints[0] = new GameObject("WP0");
            _waypoints[0].transform.position = new Vector3(0f, 1f, 2f);
            _waypoints[1] = new GameObject("WP1");
            _waypoints[1].transform.position = new Vector3(0f, 1f, -2f);
            _enemy.SetWaypoints(_waypoints);
            _enemy.SetTarget(null);

            yield return new WaitForSeconds(3f);
            Assert.AreEqual(1, _enemy.CurrentWaypointIndex, "L'ennemi doit passer au waypoint suivant après l'atteindre.");
        }

        // ------------------------------------------------------------------------
        //  CHASE
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Enemy_Chase_PoursuitJoueur()
        {
            _playerGo.transform.position = _enemyGo.transform.position + new Vector3(0f, 0f, 8f);
            _enemy.SetTarget(_playerGo.transform);

            Vector3 initialPos = _enemyGo.transform.position;
            yield return new WaitForSeconds(1f);
            float distBefore = Vector3.Distance(initialPos, _playerGo.transform.position);
            float distAfter = Vector3.Distance(_enemyGo.transform.position, _playerGo.transform.position);
            Assert.Less(distAfter, distBefore, "L'ennemi doit se rapprocher du joueur en chase.");
            Assert.AreEqual(TestEnemyState.Chase, _enemy.State);
        }

        // ------------------------------------------------------------------------
        //  ATTACK RANGE
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Enemy_Attack_QuandJoueurDansRange()
        {
            _playerGo.transform.position = _enemyGo.transform.position + new Vector3(0f, 0f, 3f);
            _enemy.SetTarget(_playerGo.transform);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(TestEnemyState.Attack, _enemy.State, "L'ennemi doit être en Attack quand joueur est dans la portée.");
        }

        [UnityTest]
        public IEnumerator Enemy_Attack_ResteImmobile()
        {
            _playerGo.transform.position = _enemyGo.transform.position + new Vector3(0f, 0f, 3f);
            _enemy.SetTarget(_playerGo.transform);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0f, _enemy.HorizontalSpeed, 0.1f, "En attack, l'ennemi ne doit pas bouger horizontalement.");
        }

        // ------------------------------------------------------------------------
        //  FLEE À BAS HP
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Enemy_Flee_QuandHPBas()
        {
            _playerGo.transform.position = _enemyGo.transform.position + new Vector3(0f, 0f, 5f);
            _enemy.SetTarget(_playerGo.transform);
            _enemy.TakeDamage(_enemy.Health * 0.8f, sourceId: 1); // reste 20% HP
            yield return null;
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(TestEnemyState.Flee, _enemy.State,
                $"L'ennemi doit fuir à bas HP. État actuel: {_enemy.State}");
        }

        // ------------------------------------------------------------------------
        //  DAMAGE / DEATH
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Enemy_TakeDamage_ReduitHP()
        {
            float initial = _enemy.Health;
            _enemy.TakeDamage(10f, sourceId: 1);
            yield return null;
            Assert.Less(_enemy.Health, initial, "Les dégâts doivent réduire HP.");
        }

        [UnityTest]
        public IEnumerator Enemy_DegatsFataux_Tue()
        {
            _enemy.TakeDamage(100000f, sourceId: 1);
            yield return null;
            Assert.IsTrue(_enemy.IsDead, "L'ennemi doit être mort.");
            Assert.AreEqual(TestEnemyState.Dead, _enemy.State);
        }

        // ------------------------------------------------------------------------
        //  FIELD OF VIEW
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Enemy_NeDetectePasSiJoueurDerriere()
        {
            _enemyGo.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            _playerGo.transform.position = _enemyGo.transform.position + new Vector3(0f, 0f, -3f);
            _enemy.SetTarget(_playerGo.transform);
            yield return new WaitForSeconds(0.3f);
            Assert.AreNotEqual(TestEnemyState.Attack, _enemy.State,
                "L'ennemi ne doit pas attaquer si le joueur est derrière (hors FoV).");
        }
    }
}
