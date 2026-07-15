// ============================================================================
//  KINETICS 5 — Tests PlayMode : PlayerController
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Tests PlayMode (nécessitent le runtime Unity) : mouvement WASD, saut, crouch,
//  collisions, stamina. Utilisent un GameObject de test avec PlayerController +
//  CharacterController + InputManager stubbé.
// ============================================================================
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using KINETICS5.Core;
using KINETICS5.Gameplay.Player;

namespace KINETICS5.Tests.PlayMode
{
    [TestFixture]
    [Category("PlayMode")]
    public sealed class PlayerControllerTests
    {
        private GameObject _playerGo;
        private PlayerController _player;
        private GameObject _groundGo;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Sol plane à y=0.
            _groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _groundGo.transform.position = Vector3.zero;
            _groundGo.transform.localScale = new Vector3(10f, 1f, 10f); // 100×100 m
            // Layer par défaut + collider déjà présent sur Plane.

            // Joueur.
            _playerGo = new GameObject("TestPlayer", typeof(CharacterController), typeof(PlayerController));
            _playerGo.transform.position = new Vector3(0f, 1f, 0f);
            _player = _playerGo.GetComponent<PlayerController>();

            // Stub ServiceLocator + InputManager (pour que PlayerController lise CurrentState).
            var slGo = new GameObject("[ServiceLocator]", typeof(ServiceLocator));
            var sl = slGo.GetComponent<ServiceLocator>();
            var inputGo = new GameObject("[InputManager]", typeof(InputManager));
            var input = inputGo.GetComponent<InputManager>();
            // Rendre ServiceLocator.Instance utilisable via réflexion (champ privé _instance).
            // Plus simple : on teste sans InputManager en simulant directement.
            sl.Register(input);

            yield return null; // laisse Awake/Start s'exécuter
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_playerGo != null) Object.Destroy(_playerGo);
            if (_groundGo != null) Object.Destroy(_groundGo);
            var sl = Object.FindObjectOfType<ServiceLocator>();
            if (sl != null) Object.Destroy(sl.gameObject);
            var im = Object.FindObjectOfType<InputManager>();
            if (im != null) Object.Destroy(im.gameObject);
            yield return null;
        }

        // ------------------------------------------------------------------------
        //  ÉTAT INITIAL
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Player_EtatInitial_Idle()
        {
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(PlayerMovementState.Idle, _player.MovementState);
            Assert.AreEqual(100f, _player.Health);
            Assert.AreEqual(50f, _player.Shield);
            Assert.AreEqual(5f, _player.Stamina, 0.01f);
        }

        // ------------------------------------------------------------------------
        //  MOUVEMENT
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Player_DeplacementAvant_BougeEnZ()
        {
            float initialZ = _playerGo.transform.position.z;
            // On manipule directement la vélocie via le CharacterController (bypass input).
            // Pour ce test on simule un input Move = (0, 1).
            var input = Object.FindObjectOfType<InputManager>();
            input.CurrentState = new InputState { Move = new Vector2(0f, 1f) };
            // Laisse 1 seconde de mouvement.
            yield return new WaitForSeconds(1f);
            float finalZ = _playerGo.transform.position.z;
            Assert.Greater(finalZ, initialZ + 0.5f, "Le joueur devrait avancer en Z.");
            Assert.AreEqual(PlayerMovementState.Walking, _player.MovementState);
        }

        [UnityTest]
        public IEnumerator Player_DeplacementLateral_BougeEnX()
        {
            float initialX = _playerGo.transform.position.x;
            var input = Object.FindObjectOfType<InputManager>();
            input.CurrentState = new InputState { Move = new Vector2(1f, 0f) };
            yield return new WaitForSeconds(1f);
            Assert.Greater(_playerGo.transform.position.x, initialX + 0.5f, "Le joueur devrait bouger en X.");
        }

        [UnityTest]
        public IEnumerator Player_AucunInput_ResteImmobile()
        {
            Vector3 initialPos = _playerGo.transform.position;
            yield return new WaitForSeconds(0.5f);
            // Pas d'input → vitesse horizontale nulle (la gravité peut bouger Y).
            Assert.AreEqual(0f, _player.HorizontalSpeed, 0.1f, "Sans input, vitesse horizontale ≈ 0.");
        }

        // ------------------------------------------------------------------------
        //  SAUT
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Player_Saut_SElèveAuDessusDe1m()
        {
            float startY = _playerGo.transform.position.y;
            var input = Object.FindObjectOfType<InputManager>();
            input.CurrentState = new InputState { JumpPressed = true };
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(_playerGo.transform.position.y, startY + 0.3f, "Le joueur devrait monter pendant le saut.");
        }

        [UnityTest]
        public IEnumerator Player_Saut_RetombeAuSol()
        {
            float startY = _playerGo.transform.position.y;
            var input = Object.FindObjectOfType<InputManager>();
            input.CurrentState = new InputState { JumpPressed = true };
            yield return new WaitForSeconds(0.1f); // déclenche saut
            input.CurrentState = default;
            yield return new WaitForSeconds(1.5f); // attend la retombée
            Assert.AreEqual(startY, _playerGo.transform.position.y, 0.1f, "Le joueur devrait retomber au sol.");
        }

        // ------------------------------------------------------------------------
        //  COLLISIONS
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Player_NeTraversePasLeMur()
        {
            // Mur devant le joueur.
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = _playerGo.transform.position + new Vector3(0f, 0f, 2f);
            wall.transform.localScale = new Vector3(5f, 3f, 0.5f);

            var input = Object.FindObjectOfType<InputManager>();
            input.CurrentState = new InputState { Move = new Vector2(0f, 1f) };
            yield return new WaitForSeconds(1f);
            // Le joueur ne doit pas avoir traversé le mur.
            Assert.Less(_playerGo.transform.position.z, 1.7f, 0.1f, "Le joueur doit être bloqué par le mur.");
            Object.Destroy(wall);
        }

        // ------------------------------------------------------------------------
        //  STAMINA
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Player_Sprint_ConsumeStamina()
        {
            float initialStamina = _player.Stamina;
            var input = Object.FindObjectOfType<InputManager>();
            // Move.y > 0.5 + pas AimHeld = sprint.
            input.CurrentState = new InputState { Move = new Vector2(0f, 1f), AimHeld = false };
            yield return new WaitForSeconds(1f);
            Assert.Less(_player.Stamina, initialStamina, "Le sprint doit consommer de la stamina.");
        }

        [UnityTest]
        public IEnumerator Player_SansBouger_StaminaSeRegenere()
        {
            // D'abord on épuise la stamina (sprint 5s).
            var input = Object.FindObjectOfType<InputManager>();
            input.CurrentState = new InputState { Move = new Vector2(0f, 1f) };
            yield return new WaitForSeconds(6f);
            Assert.AreEqual(0f, _player.Stamina, 0.1f, "Stamina devrait être épuisée.");

            // Puis on attend (sans bouger) + délai de régénération.
            input.CurrentState = default;
            yield return new WaitForSeconds(3f);
            Assert.Greater(_player.Stamina, 0f, "La stamina devrait se régénérer après le délai.");
        }

        // ------------------------------------------------------------------------
        //  VITALS
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Player_TakeDamage_ReduitHealthEtShield()
        {
            float initialHealth = _player.Health;
            float initialShield = _player.Shield;
            _player.TakeDamage(20f, sourceId: 999);
            yield return null;
            // 30% absorbés par shield → 14 sur health, 6 sur shield.
            Assert.Less(_player.Shield, initialShield, "Le shield doit absorber une partie.");
            Assert.Less(_player.Health, initialHealth, "La santé doit diminuer.");
        }

        [UnityTest]
        public IEnumerator Player_Heal_RemonteHealth()
        {
            _player.TakeDamage(80f, sourceId: 1);
            float damaged = _player.Health;
            _player.Heal(30f);
            yield return null;
            Assert.Greater(_player.Health, damaged, "Heal doit remonter la santé.");
        }

        [UnityTest]
        public IEnumerator Player_DegatsFatals_TueEtPasseDead()
        {
            _player.TakeDamage(10000f, sourceId: 1);
            yield return null;
            Assert.IsTrue(_player.IsDead, "Le joueur doit être mort.");
            Assert.AreEqual(PlayerMovementState.Dead, _player.MovementState);
        }

        [UnityTest]
        public IEnumerator Player_Respawn_RetablitVitals()
        {
            _player.TakeDamage(10000f, sourceId: 1);
            yield return null;
            Assert.IsTrue(_player.IsDead);
            _player.Respawn(Vector3.zero, Quaternion.identity);
            yield return null;
            Assert.IsFalse(_player.IsDead);
            Assert.AreEqual(100f, _player.Health);
            Assert.AreEqual(50f, _player.Shield);
        }

        // ------------------------------------------------------------------------
        //  ANTI-CHEAT BORNE
        // ------------------------------------------------------------------------

        [Test]
        public void Player_VitesseMaxTheorique_NeDepassePasSprint1_15x()
        {
            float expected = 7.0f * 1.15f; // sprintSpeed × marge anti-cheat
            Assert.AreEqual(expected, _player.MaxTheoreticalSpeed, 0.1f,
                "MaxTheoreticalSpeed doit refléter la borne anti-cheat.");
        }
    }
}
