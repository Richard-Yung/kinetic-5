// ============================================================================
//  KINETICS 5 — Tests EditMode : GameEventBus
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Vérifie subscribe/publish/unsubscribe, comptage des handlers, pas de fuite
//  mémoire (tokens), sécurité thread (verrou).
// ============================================================================
using System;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KINETICS5.Core;

namespace KINETICS5.Tests.EditMode
{
    // Événement test local (struct = zero-alloc).
    public readonly struct TestEvent
    {
        public readonly int Value;
        public TestEvent(int v) { Value = v; }
    }

    public readonly struct AnotherTestEvent
    {
        public readonly string Msg;
        public AnotherTestEvent(string m) { Msg = m; }
    }

    [TestFixture]
    [Category("Core")]
    public sealed class EventBusTests
    {
        private GameEventBus _bus;

        [SetUp]
        public void Setup()
        {
            // Utilise l'instance globale mais vide les handlers avant chaque test.
            _bus = GameEventBus.Instance;
            _bus.ClearAll();
        }

        [TearDown]
        public void Teardown()
        {
            _bus.ClearAll();
        }

        // ------------------------------------------------------------------------
        //  SOUSCRIPTION / PUBLICATION
        // ------------------------------------------------------------------------

        [Test]
        public void Subscribe_PuisPublish_HandlerAppele()
        {
            int received = 0;
            _bus.Subscribe<TestEvent>(e => received = e.Value);
            _bus.Publish(new TestEvent(42));
            Assert.AreEqual(42, received, "Le handler devrait recevoir la valeur publiée.");
        }

        [Test]
        public void Subscribe_PlusieursHandlers_TousAppeles()
        {
            int count = 0;
            _bus.Subscribe<TestEvent>(e => count++);
            _bus.Subscribe<TestEvent>(e => count++);
            _bus.Subscribe<TestEvent>(e => count++);
            _bus.Publish(new TestEvent(1));
            Assert.AreEqual(3, count, "Les 3 handlers doivent être appelés.");
        }

        [Test]
        public void Publish_SansHandler_NeLevePas()
        {
            Assert.DoesNotThrow(() => _bus.Publish(new TestEvent(0)));
        }

        [Test]
        public void Subscribe_HandlerNull_NeLevePas()
        {
            Assert.DoesNotThrow(() => _bus.Subscribe<TestEvent>(null));
            Assert.AreEqual(0, _bus.CountHandlers<TestEvent>(), "Handler null ne devrait pas être enregistré.");
        }

        // ------------------------------------------------------------------------
        //  UNSUBSCRIBE
        // ------------------------------------------------------------------------

        [Test]
        public void Unsubscribe_HandlerPlusAppele()
        {
            int count = 0;
            Action<TestEvent> handler = e => count++;
            _bus.Subscribe(handler);
            _bus.Publish(new TestEvent(1));
            Assert.AreEqual(1, count);
            _bus.Unsubscribe(handler);
            _bus.Publish(new TestEvent(1));
            Assert.AreEqual(1, count, "Handler ne doit plus être appelé après unsubscribe.");
        }

        [Test]
        public void TokenDispose_DesinscritAutomatiquement()
        {
            int count = 0;
            var token = _bus.Subscribe<TestEvent>(e => count++);
            _bus.Publish(new TestEvent(1));
            Assert.AreEqual(1, count);
            token.Dispose();
            _bus.Publish(new TestEvent(1));
            Assert.AreEqual(1, count, "Token.Dispose() doit désinscrire.");
            Assert.AreEqual(0, _bus.CountHandlers<TestEvent>());
        }

        [Test]
        public void CountHandlers_RetourneBonNombre()
        {
            Assert.AreEqual(0, _bus.CountHandlers<TestEvent>());
            var t1 = _bus.Subscribe<TestEvent>(e => {});
            Assert.AreEqual(1, _bus.CountHandlers<TestEvent>());
            var t2 = _bus.Subscribe<TestEvent>(e => {});
            Assert.AreEqual(2, _bus.CountHandlers<TestEvent>());
            t1.Dispose();
            Assert.AreEqual(1, _bus.CountHandlers<TestEvent>());
            t2.Dispose();
            Assert.AreEqual(0, _bus.CountHandlers<TestEvent>());
        }

        // ------------------------------------------------------------------------
        //  ISOLATION DES TYPES
        // ------------------------------------------------------------------------

        [Test]
        public void Subscribe_IsolationParType()
        {
            int testCount = 0, anotherCount = 0;
            _bus.Subscribe<TestEvent>(e => testCount++);
            _bus.Subscribe<AnotherTestEvent>(e => anotherCount++);
            _bus.Publish(new TestEvent(1));
            Assert.AreEqual(1, testCount);
            Assert.AreEqual(0, anotherCount, "Handler d'un autre type ne doit pas être appelé.");
            _bus.Publish(new AnotherTestEvent("hi"));
            Assert.AreEqual(1, anotherCount);
        }

        // ------------------------------------------------------------------------
        //  CAS LIMITES
        // ------------------------------------------------------------------------

        [Test]
        public void Publish_DurantHandler_SouscriptionNouvelleNonAppelee()
        {
            int count = 0;
            _bus.Subscribe<TestEvent>(e =>
            {
                count++;
                // On souscrit un nouveau handler pendant la publication.
                _bus.Subscribe<TestEvent>(_ => count++);
            });
            _bus.Publish(new TestEvent(1));
            // Le handler initial est appelé (count=1), le nouveau ne doit PAS l'être cette frame.
            Assert.AreEqual(1, count, "Le nouveau handler ne doit pas être appelé pendant la publication courante.");
            _bus.Publish(new TestEvent(1));
            // Maintenant les 2 handlers doivent être appelés.
            Assert.AreEqual(3, count, "Les 2 handlers doivent être appelés à la 2e publication.");
        }

        [Test]
        public void Publish_HandlerLeveException_LesAutresToujoursAppeles()
        {
            int count = 0;
            _bus.Subscribe<TestEvent>(e => throw new InvalidOperationException("fail"));
            _bus.Subscribe<TestEvent>(e => count++);
            LogAssert.NoUnexpectedReceived(); // le bus log l'exception mais ne crash pas
            _bus.Publish(new TestEvent(1));
            Assert.AreEqual(1, count, "Le 2e handler doit être appelé même si le 1er a levé.");
        }

        // ------------------------------------------------------------------------
        //  THREAD SAFETY (basique — pas de deadlock)
        // ------------------------------------------------------------------------

        [Test]
        public void Publish_FromAnotherThread_NeDeadlockPas()
        {
            int received = 0;
            _bus.Subscribe<TestEvent>(e => Interlocked.Exchange(ref received, e.Value));

            var t = new Thread(() => _bus.Publish(new TestEvent(99)));
            t.Start();
            t.Join(1000);

            Assert.AreEqual(99, received, "Le handler doit recevoir la valeur publiée depuis un autre thread.");
        }

        [Test]
        public void Subscribe_Unsubscribe_FromMultipleThreads_NeCorromptPas()
        {
            int counter = 0;
            Action<TestEvent> handler = e => Interlocked.Increment(ref counter);

            var threads = new Thread[4];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        _bus.Subscribe(handler);
                        _bus.Unsubscribe(handler);
                    }
                });
            }
            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join(2000);

            Assert.AreEqual(0, _bus.CountHandlers<TestEvent>(), "Toutes les souscriptions doivent être désinscrites.");
        }

        // ------------------------------------------------------------------------
        //  TYPES D'ÉVÉNEMENTS KINETICS 5
        // ------------------------------------------------------------------------

        [Test]
        public void EventBus_DamageDealtEvent_BienRoute()
        {
            DamageDealtEvent? received = null;
            _bus.Subscribe<DamageDealtEvent>(e => received = e);
            var sent = new DamageDealtEvent(1, 2, 50f, false, (int)KINETICS5.Data.Element.Kinetic, Vector3.zero);
            _bus.Publish(sent);
            Assert.IsTrue(received.HasValue);
            Assert.AreEqual(50f, received.Value.Amount);
        }

        [Test]
        public void EventBus_MissionCompleteEvent_BienRoute()
        {
            string id = null;
            _bus.Subscribe<MissionCompleteEvent>(e => id = e.MissionId);
            _bus.Publish(new MissionCompleteEvent("TEST_MISSION", 60f, 1000, true));
            Assert.AreEqual("TEST_MISSION", id);
        }

        [Test]
        public void ClearAll_VideTousLesHandlers()
        {
            _bus.Subscribe<TestEvent>(e => {});
            _bus.Subscribe<AnotherTestEvent>(e => {});
            Assert.AreEqual(1, _bus.CountHandlers<TestEvent>());
            Assert.AreEqual(1, _bus.CountHandlers<AnotherTestEvent>());
            _bus.ClearAll();
            Assert.AreEqual(0, _bus.CountHandlers<TestEvent>());
            Assert.AreEqual(0, _bus.CountHandlers<AnotherTestEvent>());
        }
    }
}
