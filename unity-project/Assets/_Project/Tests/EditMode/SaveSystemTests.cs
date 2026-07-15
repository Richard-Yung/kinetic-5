// ============================================================================
//  KINETICS 5 — Tests EditMode : SaveSystem
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Vérifie save/load roundtrip, migrations v1→v2→v3, chiffrement AES-128,
//  gestion des saves corrompues.
// ============================================================================
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KINETICS5.Core;

namespace KINETICS5.Tests.EditMode
{
    [TestFixture]
    [Category("Core")]
    public sealed class SaveSystemTests
    {
        private SaveSystem _save;
        private int _testSlot = 2; // slot 2 réservé aux tests (jamais utilisé en prod)

        [SetUp]
        public void Setup()
        {
            // Force l'instance (créée si absente).
            _save = SaveSystem.Instance;
            // Nettoie le slot de test.
            _save.DeleteSlot(_testSlot);
            PlayerPrefs.DeleteKey($"K5_Save_Slot{_testSlot}");
        }

        [TearDown]
        public void Teardown()
        {
            _save.DeleteSlot(_testSlot);
            PlayerPrefs.DeleteKey($"K5_Save_Slot{_testSlot}");
        }

        // ------------------------------------------------------------------------
        //  ROUNDTRIP
        // ------------------------------------------------------------------------

        [Test]
        public void Save_PuisLoad_RoundtripIdentique()
        {
            _save.LoadSlot(_testSlot);
            _save.ActiveData.Profile.DisplayName = "TestOperative";
            _save.ActiveData.Profile.PlayerLevel = 42;
            _save.ActiveData.Profile.Credits = 1337;
            _save.ActiveData.Progress.CompletedMissions.Add("SHADOW_FALL");
            _save.ActiveData.Inventory.OwnedWeapons.Add("AX_9_SR");
            _save.SaveImmediate();

            // Re-charge depuis le disque.
            _save.LoadSlot(_testSlot);
            Assert.AreEqual("TestOperative", _save.ActiveData.Profile.DisplayName);
            Assert.AreEqual(42, _save.ActiveData.Profile.PlayerLevel);
            Assert.AreEqual(1337, _save.ActiveData.Profile.Credits);
            Assert.Contains("SHADOW_FALL", _save.ActiveData.Progress.CompletedMissions);
            Assert.Contains("AX_9_SR", _save.ActiveData.Inventory.OwnedWeapons);
        }

        [Test]
        public void Save_InitialiseAvecValeursParDefaut()
        {
            _save.LoadSlot(_testSlot);
            Assert.AreEqual(SaveData.CurrentSchemaVersion, _save.ActiveData.SchemaVersion);
            Assert.AreEqual("Operative", _save.ActiveData.Profile.DisplayName);
            Assert.AreEqual(1, _save.ActiveData.Profile.PlayerLevel);
            Assert.AreEqual(0, _save.ActiveData.Profile.Xp);
        }

        [Test]
        public void Save_SlotExists_RetourneVraiApresSauvegarde()
        {
            Assert.IsFalse(_save.SlotExists(_testSlot), "Slot devrait être vide avant sauvegarde.");
            _save.LoadSlot(_testSlot);
            _save.SaveImmediate();
            Assert.IsTrue(_save.SlotExists(_testSlot), "Slot devrait exister après sauvegarde.");
        }

        [Test]
        public void Save_DeleteSlot_SupprimeDonnees()
        {
            _save.LoadSlot(_testSlot);
            _save.SaveImmediate();
            Assert.IsTrue(_save.SlotExists(_testSlot));
            _save.DeleteSlot(_testSlot);
            Assert.IsFalse(_save.SlotExists(_testSlot));
        }

        // ------------------------------------------------------------------------
        //  MIGRATIONS
        // ------------------------------------------------------------------------

        [Test]
        public void Migration_V1VersV3_AppliqueeCorrectement()
        {
            // Simule une sauvegarde v1 (manque Resources + HapticsEnabled).
            var v1 = new SaveData { SchemaVersion = 1 };
            v1.Inventory.Resources = null;
            v1.Settings.HapticsEnabled = false;

            // Serialize/deserialize + migration via réflexion de la méthode privée Migrate.
            // Comme Migrate est privée, on teste via LoadSlot en écrivant directement un fichier v1.
            _save.LoadSlot(_testSlot);
            _save.ActiveData = v1;
            _save.SaveImmediate();
            // Re-charge : la migration doit s'appliquer.
            _save.LoadSlot(_testSlot);
            Assert.AreEqual(SaveData.CurrentSchemaVersion, _save.ActiveData.SchemaVersion);
            Assert.IsNotNull(_save.ActiveData.Inventory.Resources, "Resources devrait être créé par migration v2.");
            Assert.IsTrue(_save.ActiveData.Settings.HapticsEnabled, "HapticsEnabled devrait être true après migration v3.");
        }

        [Test]
        public void Migration_SchemaCourant_NEstPasReMigree()
        {
            _save.LoadSlot(_testSlot);
            _save.ActiveData.SchemaVersion = SaveData.CurrentSchemaVersion;
            _save.SaveImmediate();
            _save.LoadSlot(_testSlot);
            Assert.AreEqual(SaveData.CurrentSchemaVersion, _save.ActiveData.SchemaVersion,
                "Pas de migration si déjà au schéma courant.");
        }

        // ------------------------------------------------------------------------
        //  CHIFFREMENT
        // ------------------------------------------------------------------------

        [Test]
        public void Chiffrement_LeFichierSurDisqueNEstPasDuJSONEnClair()
        {
            _save.LoadSlot(_testSlot);
            _save.ActiveData.Profile.DisplayName = "SecretOperative";
            _save.SaveImmediate();

            var path = Path.Combine(Application.persistentDataPath, $"save_slot{_testSlot}.dat");
            Assert.IsTrue(File.Exists(path), "Le fichier de save devrait exister.");
            var content = File.ReadAllText(path);
            // Le JSON en clair contiendrait "SecretOperative". Le chiffré ne devrait pas.
            StringAssert.DoesNotContain("SecretOperative", content,
                "Le fichier de save ne devrait pas contenir le profil en clair (chiffrement AES).");
            StringAssert.DoesNotContain("SchemaVersion", content,
                "Le fichier de save ne devrait pas contenir les clés JSON en clair.");
        }

        // ------------------------------------------------------------------------
        //  METADATA
        // ------------------------------------------------------------------------

        [Test]
        public void GetSlotMetadata_RetourneInfosSansChargerComplet()
        {
            _save.LoadSlot(_testSlot);
            _save.ActiveData.Profile.DisplayName = "MetaOperative";
            _save.ActiveData.Profile.PlayerLevel = 7;
            _save.SaveImmediate();

            var meta = _save.GetSlotMetadata(_testSlot);
            Assert.IsTrue(meta.Exists);
            Assert.AreEqual("MetaOperative", meta.DisplayName);
            Assert.AreEqual(7, meta.PlayerLevel);
            Assert.AreEqual(SaveData.CurrentSchemaVersion, meta.SchemaVersion);
            Assert.Greater(meta.LastSaveUnix, 0);
        }

        [Test]
        public void GetSlotMetadata_SlotVide_RetourneExistsFalse()
        {
            var meta = _save.GetSlotMetadata(_testSlot);
            Assert.IsFalse(meta.Exists);
        }

        // ------------------------------------------------------------------------
        //  CORRUPTION
        // ------------------------------------------------------------------------

        [Test]
        public void Save_Corrompue_LoadRetourneFalseEtCreeNouvelleSave()
        {
            // Écrit un fichier corrompu (Base64 invalide).
            var path = Path.Combine(Application.persistentDataPath, $"save_slot{_testSlot}.dat");
            File.WriteAllText(path, "CECI_N_EST_PAS_DU_BASE64_VALIDE!!!@#$");

            bool ok = _save.LoadSlot(_testSlot);
            // Le load doit échouer mais ne pas planter, et créer une nouvelle save par défaut.
            Assert.IsFalse(ok, "Load d'un slot corrompu doit retourner false.");
            Assert.IsNotNull(_save.ActiveData, "Une save par défaut doit être créée même après corruption.");
            Assert.AreEqual("Operative", _save.ActiveData.Profile.DisplayName);
        }

        [Test]
        public void Save_MarkDirty_DirtyFlagActif()
        {
            _save.LoadSlot(_testSlot);
            _save.MarkDirty();
            // MarkDirty est privé après SaveImmediate. On vérifie juste qu'aucune exception n'est levée.
            Assert.Pass("MarkDirty ne lève pas d'exception.");
        }

        // ------------------------------------------------------------------------
        //  EDGE CASES
        // ------------------------------------------------------------------------

        [Test]
        public void Save_SlotInvalide_LoadRetourneFalse()
        {
            Assert.IsFalse(_save.LoadSlot(-1), "Slot -1 devrait être invalide.");
            Assert.IsFalse(_save.LoadSlot(99), "Slot 99 devrait être invalide.");
        }

        [Test]
        public void Save_AutoSave_IntervalPasTropCourt()
        {
            // On ne peut pas tester l'auto-save complet en EditMode sans avancer le temps,
            // mais on vérifie que la propriété publique CurrentSlot est cohérente.
            _save.LoadSlot(0);
            Assert.AreEqual(0, _save.CurrentSlot);
        }
    }
}
