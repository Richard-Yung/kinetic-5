// ============================================================================
//  KINETICS 5 — Tests EditMode : DataLoader
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Vérifie que tous les fichiers JSON de Resources/Data/ se chargent correctement,
//  que les références (mission→ennemi, mission→région) sont valides, et qu'aucun
//  ennemi n'a 0 HP.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KINETICS5.Data;

namespace KINETICS5.Tests.EditMode
{
    [TestFixture]
    [Category("Data")]
    public sealed class DataLoaderTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            // Force le (re)chargement des JSON avant chaque suite.
            DataLoader.LoadAll();
        }

        // ------------------------------------------------------------------------
        //  CHARGEMENT GLOBAL
        // ------------------------------------------------------------------------

        [Test]
        public void DataLoader_EstCharge_ApresLoadAll()
        {
            Assert.IsTrue(DataLoader.IsLoaded, "DataLoader.IsLoaded devrait être vrai après LoadAll().");
        }

        [Test]
        public void DataLoader_TousLesFichiersJsonSontCharges()
        {
            Assert.IsTrue(DataLoader.GetAllAgents().Count    >= 4,  "Au moins 4 agents attendus.");
            Assert.IsTrue(DataLoader.GetAllWeapons().Count   >= 14, "Au moins 14 armes attendues.");
            Assert.IsTrue(DataLoader.GetAllMissions().Count  >= 7,  "Au moins 7 missions attendues.");
            Assert.IsTrue(DataLoader.GetAllEnemies().Count   >= 11, "Au moins 11 ennemis attendus.");
            Assert.IsTrue(DataLoader.GetAllRegions().Count   >= 7,  "Au moins 7 régions attendues.");
            Assert.IsTrue(DataLoader.GetAllTacticals().Count >= 4,  "Au moins 4 objets tactiques attendus.");
        }

        [Test]
        public void DataLoader_CourbeProgressionA60Paliers()
        {
            var curve = DataLoader.GetProgressionCurve();
            Assert.IsNotNull(curve);
            Assert.AreEqual(60, curve.MaxLevel, "MaxLevel devrait être 60.");
            Assert.AreEqual(60, curve.Levels.Count, "60 paliers attendus.");
            Assert.AreEqual(0, curve.Levels[0].TotalXp, "Le premier palier devrait avoir totalXp = 0.");
        }

        // ------------------------------------------------------------------------
        //  AGENTS
        // ------------------------------------------------------------------------

        [Test]
        public void Agents_VulcanExisteEtEstUnTank()
        {
            var vulcan = DataLoader.GetAgent("VULCAN");
            Assert.IsNotNull(vulcan, "VULCAN devrait exister.");
            Assert.AreEqual(AgentClass.Tank, vulcan.Class);
            Assert.AreEqual(47, vulcan.Level);
            Assert.AreEqual(2500, vulcan.BasePower);
            Assert.GreaterOrEqual(vulcan.Abilities.Count, 1, "VULCAN devrait avoir au moins 1 compétence.");
        }

        [Test]
        public void Agents_TousOntBaseHealthPositive()
        {
            foreach (var a in DataLoader.GetAllAgents())
            {
                Assert.Greater(a.BaseHealth, 0, $"Agent {a.Id} devrait avoir BaseHealth > 0.");
                Assert.Greater(a.BasePower, 0, $"Agent {a.Id} devrait avoir BasePower > 0.");
            }
        }

        [Test]
        public void Agents_GetUnlockedAgentsFiltreParNiveau()
        {
            var unlocked55 = DataLoader.GetUnlockedAgents(55);
            var unlocked10 = DataLoader.GetUnlockedAgents(10);
            Assert.GreaterOrEqual(unlocked55.Count, unlocked10.Count,
                "Plus d'agents devraient être débloqués à L55 qu'à L10.");
        }

        // ------------------------------------------------------------------------
        //  ARMES
        // ------------------------------------------------------------------------

        [Test]
        public void Armes_HeavyRx14ExisteAvecStatsPDF()
        {
            var w = DataLoader.GetWeapon("HEAVY_RX_14");
            Assert.IsNotNull(w);
            Assert.AreEqual(WeaponCategory.Primary, w.Category);
            Assert.AreEqual(1500, w.Power);
            Assert.AreEqual(Rarity.Rare, w.Rarity);
        }

        [Test]
        public void Armes_CategoriesCoherentes()
        {
            var primaries   = DataLoader.GetWeaponsByCategory(WeaponCategory.Primary);
            var secondaries = DataLoader.GetWeaponsByCategory(WeaponCategory.Secondary);
            var tacticals   = DataLoader.GetWeaponsByCategory(WeaponCategory.Tactical);
            Assert.GreaterOrEqual(primaries.Count, 5);
            Assert.GreaterOrEqual(secondaries.Count, 4);
            Assert.GreaterOrEqual(tacticals.Count, 4);
        }

        [Test]
        public void Armes_ToutesOntPowerPositif()
        {
            foreach (var w in DataLoader.GetAllWeapons())
            {
                Assert.Greater(w.Power, 0, $"Arme {w.Id} : Power devrait être > 0.");
            }
        }

        // ------------------------------------------------------------------------
        //  MISSIONS — INTÉGRITÉ RÉFÉRENTIELLE
        // ------------------------------------------------------------------------

        [Test]
        public void Missions_ToutesLesVaguesReferencentUnEnnemiExistant()
        {
            foreach (var m in DataLoader.GetAllMissions())
            {
                foreach (var wave in m.Waves)
                {
                    var enemy = DataLoader.GetEnemy(wave.EnemyId);
                    Assert.IsNotNull(enemy,
                        $"Mission {m.Id} vague {wave.Id} : ennemi '{wave.EnemyId}' introuvable dans enemies.json.");
                }
                foreach (var bp in m.BossPhases)
                {
                    var boss = DataLoader.GetEnemy(bp.EnemyId);
                    Assert.IsNotNull(boss,
                        $"Mission {m.Id} phase {bp.Id} : boss '{bp.EnemyId}' introuvable.");
                }
            }
        }

        [Test]
        public void Missions_ToutesReferencentUneRegionExistante()
        {
            foreach (var m in DataLoader.GetAllMissions())
            {
                Assert.IsFalse(string.IsNullOrEmpty(m.Region), $"Mission {m.Id} : region vide.");
                var r = DataLoader.GetRegion(m.Region);
                Assert.IsNotNull(r, $"Mission {m.Id} : région '{m.Region}' introuvable.");
            }
        }

        [Test]
        public void Missions_ToutesOntSceneNameEtRewards()
        {
            foreach (var m in DataLoader.GetAllMissions())
            {
                Assert.IsFalse(string.IsNullOrEmpty(m.SceneName), $"Mission {m.Id} : sceneName vide.");
                Assert.IsNotNull(m.Rewards, $"Mission {m.Id} : rewards null.");
                Assert.GreaterOrEqual(m.Rewards.Xp, 0);
                Assert.GreaterOrEqual(m.Rewards.Cr, 0);
            }
        }

        [Test]
        public void Missions_Les7TypesSontRepresentes()
        {
            var types = System.Enum.GetValues(typeof(MissionType)).Cast<MissionType>();
            foreach (var t in types)
            {
                var list = DataLoader.GetMissionsByType(t);
                Assert.GreaterOrEqual(list.Count, 1, $"Au moins 1 mission de type {t} attendue.");
            }
        }

        // ------------------------------------------------------------------------
        //  ENNEMIS
        // ------------------------------------------------------------------------

        [Test]
        public void Ennemis_AucunZeroHP()
        {
            foreach (var e in DataLoader.GetAllEnemies())
            {
                Assert.Greater(e.BaseHealth, 0, $"Ennemi {e.Id} ne devrait pas avoir 0 HP.");
            }
        }

        [Test]
        public void Ennemis_BossesOntPlusDe10000HP()
        {
            foreach (var e in DataLoader.GetAllEnemies())
            {
                if (e.IsBoss) Assert.Greater(e.BaseHealth, 10000, $"Boss {e.Id} devrait avoir > 10000 HP.");
            }
        }

        [Test]
        public void Ennemis_TousOntLootTableNonVide()
        {
            foreach (var e in DataLoader.GetAllEnemies())
            {
                Assert.IsNotNull(e.LootTable, $"Ennemi {e.Id} : lootTable null.");
                // Bosses et elites doivent avoir au moins 1 drop.
                if (e.IsBoss || e.Class == EnemyClass.Elite)
                    Assert.GreaterOrEqual(e.LootTable.Count, 1, $"Ennemi {e.Id} : au moins 1 drop attendu.");
            }
        }

        // ------------------------------------------------------------------------
        //  RÉGIONS
        // ------------------------------------------------------------------------

        [Test]
        public void Regions_ToutesReferencentDesMissionsExistantes()
        {
            foreach (var r in DataLoader.GetAllRegions())
            {
                foreach (var mid in r.Missions)
                {
                    var m = DataLoader.GetMission(mid);
                    Assert.IsNotNull(m, $"Région {r.Id} : mission '{mid}' introuvable.");
                }
            }
        }

        // ------------------------------------------------------------------------
        //  PROGRESSION
        // ------------------------------------------------------------------------

        [Test]
        public void Progression_XpCroissanteParPalier()
        {
            var curve = DataLoader.GetProgressionCurve();
            for (int i = 1; i < curve.Levels.Count; i++)
            {
                Assert.Greater(curve.Levels[i].XpRequired, curve.Levels[i - 1].XpRequired,
                    $"Palier {curve.Levels[i].Level} : XP devrait être croissante.");
                Assert.Greater(curve.Levels[i].TotalXp, curve.Levels[i - 1].TotalXp,
                    $"Palier {curve.Levels[i].Level} : totalXp devrait être croissant.");
            }
        }

        [Test]
        public void Progression_GetLevelForXp_RetourneBonneValeur()
        {
            var curve = DataLoader.GetProgressionCurve();
            int xp1 = curve.Levels[0].TotalXp; // 0
            int xp10 = curve.Levels[10].TotalXp;
            Assert.AreEqual(1, DataLoader.GetLevelForXp(xp1));
            Assert.AreEqual(10, DataLoader.GetLevelForXp(xp10));
        }

        [Test]
        public void Progression_GetXpToNextLevel_Correct()
        {
            var curve = DataLoader.GetProgressionCurve();
            int xpCurrent = curve.Levels[4].TotalXp; // palier 4 (niveau 5)
            int expected = curve.Levels[5].TotalXp - xpCurrent;
            int actual = DataLoader.GetXpToNextLevel(5, xpCurrent);
            Assert.AreEqual(expected, actual);
        }
    }
}
