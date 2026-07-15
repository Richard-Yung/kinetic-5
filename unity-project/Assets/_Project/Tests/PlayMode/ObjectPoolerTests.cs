// ============================================================================
//  KINETICS 5 — Tests PlayMode : ObjectPooler
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Tests PlayMode pour ObjectPooler : Get/Release, pré-chauffe, statistiques,
//  assertion d'absence d'allocations dans la hot path (via GC sample).
// ============================================================================
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Profiling;
using KINETICS5.Core;

namespace KINETICS5.Tests.PlayMode
{
    // Composant de test (poolé).
    public sealed class PoolableBullet : MonoBehaviour, IPooledItem
    {
        public bool Spawned;
        public bool Returned;
        public void OnSpawnFromPool() { Spawned = true; Returned = false; }
        public void OnReturnToPool() { Spawned = false; Returned = true; }
    }

    [TestFixture]
    [Category("PlayMode")]
    public sealed class ObjectPoolerTests
    {
        private GameObject _poolerGo;
        private ObjectPooler _pooler;
        private GameObject _bulletPrefab;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _poolerGo = new GameObject("[TestPooler]", typeof(ObjectPooler));
            _pooler = _poolerGo.GetComponent<ObjectPooler>();
            _bulletPrefab = new GameObject("BulletPrefab", typeof(PoolableBullet));
            _bulletPrefab.SetActive(false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_poolerGo != null) Object.Destroy(_poolerGo);
            if (_bulletPrefab != null) Object.Destroy(_bulletPrefab);
            yield return null;
        }

        // ------------------------------------------------------------------------
        //  REGISTER / PRE-WARM
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Pool_Register_CreePoolAvecPreWarm()
        {
            _pooler.RegisterPool("bullets", _bulletPrefab.GetComponent<PoolableBullet>(), preWarm: 5, maxSize: 20);
            var (inactive, alive, max) = _pooler.GetStats("bullets");
            Assert.AreEqual(5, inactive, "5 objets pré-chauffés doivent être inactifs.");
            Assert.AreEqual(0, alive, "Aucun objet ne doit être vivant à l'initialisation.");
            Assert.AreEqual(20, max, "MaxSize doit être 20.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pool_Register_Doublon_Identique()
        {
            _pooler.RegisterPool("bullets", _bulletPrefab.GetComponent<PoolableBullet>(), 5, 20);
            // Re-register le même id : doit être ignoré (warning).
            LogAssert.Expect(LogType.Warning, "[ObjectPooler] Pool bullets déjà enregistré.");
            _pooler.RegisterPool("bullets", _bulletPrefab.GetComponent<PoolableBullet>(), 5, 20);
            yield return null;
            var (inactive, _, _) = _pooler.GetStats("bullets");
            Assert.AreEqual(5, inactive, "Le pool ne doit pas avoir été dupliqué.");
        }

        // ------------------------------------------------------------------------
        //  GET / RELEASE
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Pool_Get_RetourneObjetActif()
        {
            _pooler.RegisterPool("bullets", _bulletPrefab.GetComponent<PoolableBullet>(), 5, 20);
            var bullet = _pooler.Get<PoolableBullet>("bullets", Vector3.zero, Quaternion.identity);
            yield return null;
            Assert.IsNotNull(bullet, "Get doit retourner un objet non null.");
            Assert.IsTrue(bullet.gameObject.activeSelf, "L'objet retourné doit être actif.");
            Assert.IsTrue(bullet.Spawned, "OnSpawnFromPool doit être appelée.");
            var (inactive, alive, _) = _pooler.GetStats("bullets");
            Assert.AreEqual(4, inactive, "1 objet retiré du pool inactif.");
            Assert.AreEqual(1, alive, "1 objet vivant.");
        }

        [UnityTest]
        public IEnumerator Pool_Release_RetourneObjetInactif()
        {
            _pooler.RegisterPool("bullets", _bulletPrefab.GetComponent<PoolableBullet>(), 5, 20);
            var bullet = _pooler.Get<PoolableBullet>("bullets");
            yield return null;
            _pooler.Release(bullet);
            yield return null;
            Assert.IsFalse(bullet.gameObject.activeSelf, "L'objet retourné doit être inactif.");
            Assert.IsTrue(bullet.Returned, "OnReturnToPool doit être appelée.");
            var (inactive, alive, _) = _pooler.GetStats("bullets");
            Assert.AreEqual(5, inactive, "L'objet doit être de retour dans le pool inactif.");
            Assert.AreEqual(0, alive, "Plus aucun objet vivant.");
        }

        [UnityTest]
        public IEnumerator Pool_GetSansPosition_ActifAZero()
        {
            _pooler.RegisterPool("bullets", _bulletPrefab.GetComponent<PoolableBullet>(), 5, 20);
            var bullet = _pooler.Get<PoolableBullet>("bullets");
            yield return null;
            Assert.AreEqual(Vector3.zero, bullet.transform.position);
        }

        [UnityTest]
        public IEnumerator Pool_GetSurPoolInconnu_RetourneNull()
        {
            LogAssert.Expect(LogType.Error, "[ObjectPooler] Pool inconnu: ghost");
            var bullet = _pooler.Get<PoolableBullet>("ghost");
            yield return null;
            Assert.IsNull(bullet);
        }

        // ------------------------------------------------------------------------
        //  SATURATION
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Pool_Sature_DepasseMaxSizeAvecWarning()
        {
            _pooler.RegisterPool("bullets", _bulletPrefab.GetComponent<PoolableBullet>(), 2, 3);
            // Tire 5 objets (au-delà du max).
            var a = _pooler.Get<PoolableBullet>("bullets");
            var b = _pooler.Get<PoolableBullet>("bullets");
            var c = _pooler.Get<PoolableBullet>("bullets");
            // 3 = max atteint. La 4e va logger un warning.
            LogAssert.Expect(LogType.Warning, "[ObjectPooler] Pool bullets saturé, instanciation forcée.");
            var d = _pooler.Get<PoolableBullet>("bullets");
            yield return null;
            Assert.IsNotNull(d, "Même saturé, Get doit retourner un objet (instanciation forcée).");
        }

        // ------------------------------------------------------------------------
        //  CLEAR
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Pool_Clear_DetruitObjetsInactifs()
        {
            _pooler.RegisterPool("bullets", _bulletPrefab.GetComponent<PoolableBullet>(), 5, 20);
            var (inactive, _, _) = _pooler.GetStats("bullets");
            Assert.AreEqual(5, inactive);
            _pooler.ClearPool("bullets");
            yield return null;
            var (inactive2, _, _) = _pooler.GetStats("bullets");
            Assert.AreEqual(0, inactive2, "ClearPool doit vider les inactifs.");
        }

        // ------------------------------------------------------------------------
        //  HOT PATH — NO ALLOCATIONS
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Pool_HotPath_GetRelease_PasDAllocationsGC()
        {
            _pooler.RegisterPool("bullets", _bulletPrefab.GetComponent<PoolableBullet>(), 50, 200);
            yield return null;

            // Pré-boucle de chauffe (JIT des caches internes).
            for (int i = 0; i < 10; i++)
            {
                var b = _pooler.Get<PoolableBullet>("bullets");
                _pooler.Release(b);
            }

            // Sample GC avant.
            long gcBefore = System.GC.GetTotalMemory(forceFullCollection: true);

            // Hot path : 1000 cycles Get/Release.
            for (int i = 0; i < 1000; i++)
            {
                var b = _pooler.Get<PoolableBullet>("bullets");
                _pooler.Release(b);
            }

            long gcAfter = System.GC.GetTotalMemory(forceFullCollection: false);
            long delta = gcAfter - gcBefore;

            // Tolérance : le GC peut allouer un peu (Unity interne, sample GC lui-même).
            // On accepte jusqu'à 4 KB pour 1000 cycles (≈ 4 octets/cycle = négligeable).
            Assert.Less(delta, 4096,
                $"Hot path ObjectPooler doit être zero-alloc (delta = {delta} octets pour 1000 cycles).");
            yield return null;
        }

        // ------------------------------------------------------------------------
        //  IPooledItem CALLBACKS
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Pool_IPooledItem_CallbacksAppeles()
        {
            _pooler.RegisterPool("bullets", _bulletPrefab.GetComponent<PoolableBullet>(), 5, 20);
            var b = _pooler.Get<PoolableBullet>("bullets");
            yield return null;
            Assert.IsTrue(b.Spawned, "OnSpawnFromPool doit être appelée.");
            Assert.IsFalse(b.Returned);
            _pooler.Release(b);
            yield return null;
            Assert.IsTrue(b.Returned, "OnReturnToPool doit être appelée.");
            Assert.IsFalse(b.Spawned);
        }

        // ------------------------------------------------------------------------
        //  RELEASE D'UN OBJET NON POOLÉ
        // ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Pool_ReleaseObjetExterne_DetruitSansPlanter()
        {
            var go = new GameObject("ExternalObj", typeof(PoolableBullet));
            var external = go.GetComponent<PoolableBullet>();
            _pooler.Release(external);
            yield return null;
            Assert.IsTrue(go == null, "L'objet externe doit être détruit par Release.");
        }
    }
}
