// ============================================================================
//  KINETICS 5 — Tests EditMode : DamageCalculator
//  Task 2-f — Shaders / Network / Tests / Docs
// ----------------------------------------------------------------------------
//  Vérifie la formule de dégâts (DamageCalculator), multiplicateurs élémentaires,
//  headshot/crit, edge cases (0 HP, overkill).
// ============================================================================
using NUnit.Framework;
using KINETICS5.Data;
using KINETICS5.Gameplay.Combat;

namespace KINETICS5.Tests.EditMode
{
    [TestFixture]
    [Category("Combat")]
    public sealed class DamageCalculatorTests
    {
        // IDs de référence chargés via DataLoader.
        private const string WeaponId = "HEAVY_RX_14";     // DamagePct = 72 (selon PDF)
        private const string EnemyId = "GRUNT_MK1";        // faiblesse/résistance dans enemies.json

        [OneTimeSetUp]
        public void Setup()
        {
            KINETICS5.Data.DataLoader.LoadAll();
        }

        // ------------------------------------------------------------------------
        //  FORMULE DE BASE
        // ------------------------------------------------------------------------

        [Test]
        public void Damage_BaseNonNul()
        {
            var input = new DamageInput(WeaponId, EnemyId, Element.Kinetic, false, false, 5f);
            var r = DamageCalculator.Calculate(input, 100f, 0f);
            Assert.Greater(r.BaseDamage, 0f, "BaseDamage devrait être > 0.");
            Assert.Greater(r.FinalDamage, 0f, "FinalDamage devrait être > 0.");
        }

        [Test]
        public void Damage_NeDepassePasLeCap()
        {
            // Même avec un overkill massif, on est plafonné à DamageCap.
            var input = new DamageInput(WeaponId, EnemyId, Element.Kinetic, true, true, 0f);
            var r = DamageCalculator.Calculate(input, 1f, 0f);
            Assert.LessOrEqual(r.FinalDamage, DamageCalculator.DamageCap, "Damage devrait être plafonné.");
        }

        [Test]
        public void Damage_NeverNegative()
        {
            // Arme inconnue → baseDamage = 0, finalDamage = 0.
            var input = new DamageInput("WEAPON_DOES_NOT_EXIST", EnemyId, Element.Kinetic, false, false, 5f);
            var r = DamageCalculator.Calculate(input, 100f, 0f);
            Assert.GreaterOrEqual(r.FinalDamage, 0f, "Damage ne devrait jamais être négatif.");
        }

        // ------------------------------------------------------------------------
        //  MULTIPLICATEUR ÉLÉMENTAIRE
        // ------------------------------------------------------------------------

        [Test]
        public void Element_WeaknessBonusApplique()
        {
            var enemy = KINETICS5.Data.DataLoader.GetEnemy(EnemyId);
            Assume.That(enemy, Is.Not.Null);
            Assume.That(enemy.Weakness, Is.Not.EqualTo(Element.Kinetic), "Ennemi test doit avoir une weakness non-Kinetic.");

            float neutral = DamageCalculator.CalculateFast(WeaponId, EnemyId, Element.Kinetic, false, false, 5f, 0f);
            float weak    = DamageCalculator.CalculateFast(WeaponId, EnemyId, enemy.Weakness,  false, false, 5f, 0f);
            Assert.Greater(weak, neutral, "Dégâts weakness devraient être supérieurs à neutres.");
            Assert.AreEqual(1.5f, DamageCalculator.WeaknessMultiplier, 0.001);
        }

        [Test]
        public void Element_ResistancePenaltyApplique()
        {
            var enemy = KINETICS5.Data.DataLoader.GetEnemy(EnemyId);
            Assume.That(enemy, Is.Not.Null);
            Assume.That(enemy.Resistance, Is.Not.EqualTo(Element.Kinetic), "Ennemi test doit avoir une resistance non-Kinetic.");

            float neutral = DamageCalculator.CalculateFast(WeaponId, EnemyId, Element.Kinetic, false, false, 5f, 0f);
            float resist  = DamageCalculator.CalculateFast(WeaponId, EnemyId, enemy.Resistance, false, false, 5f, 0f);
            Assert.Less(resist, neutral, "Dégâts resistance devraient être inférieurs à neutres.");
            Assert.AreEqual(0.5f, DamageCalculator.ResistanceMultiplier, 0.001);
        }

        [Test]
        public void Element_GetElementMultiplier_TableComplete()
        {
            Assert.AreEqual(1.5f, DamageCalculator.GetElementMultiplier(Element.Cryo, Element.Cryo, Element.Volt));
            Assert.AreEqual(0.5f, DamageCalculator.GetElementMultiplier(Element.Volt, Element.Cryo, Element.Volt));
            Assert.AreEqual(1.0f, DamageCalculator.GetElementMultiplier(Element.Kinetic, Element.Cryo, Element.Volt));
        }

        // ------------------------------------------------------------------------
        //  HEADSHOT / CRIT
        // ------------------------------------------------------------------------

        [Test]
        public void Headshot_DoubleLesDegats()
        {
            float normal   = DamageCalculator.CalculateFast(WeaponId, EnemyId, Element.Kinetic, false, false, 5f, 0f);
            float headshot = DamageCalculator.CalculateFast(WeaponId, EnemyId, Element.Kinetic, true,  false, 5f, 0f);
            Assert.AreEqual(normal * 2f, headshot, 0.01f, "Headshot devrait doubler les dégâts.");
        }

        [Test]
        public void Crit_MultipliePar1_5()
        {
            float normal = DamageCalculator.CalculateFast(WeaponId, EnemyId, Element.Kinetic, false, false, 5f, 0f);
            float crit   = DamageCalculator.CalculateFast(WeaponId, EnemyId, Element.Kinetic, false, true,  5f, 0f);
            Assert.AreEqual(normal * 1.5f, crit, 0.01f, "Crit devrait multiplier par 1.5.");
        }

        [Test]
        public void HeadshotEtCrit_NonCumulables_MaxDesDeux()
        {
            float headshot = DamageCalculator.CalculateFast(WeaponId, EnemyId, Element.Kinetic, true,  false, 5f, 0f);
            float both     = DamageCalculator.CalculateFast(WeaponId, EnemyId, Element.Kinetic, true,  true,  5f, 0f);
            // Les deux devraient donner la même valeur (max(2.0, 1.5) = 2.0).
            Assert.AreEqual(headshot, both, 0.01f, "Headshot + crit = max(2.0, 1.5), pas cumul.");
        }

        // ------------------------------------------------------------------------
        //  DISTANCE / FALLOFF
        // ------------------------------------------------------------------------

        [Test]
        public void Distance_FalloffApplique()
        {
            float close = DamageCalculator.CalculateFast(WeaponId, EnemyId, Element.Kinetic, false, false, 1f,  0f);
            float far   = DamageCalculator.CalculateFast(WeaponId, EnemyId, Element.Kinetic, false, false, 99f, 0f);
            Assert.GreaterOrEqual(close, far, "Dégâts proches >= dégâts lointains.");
        }

        [Test]
        public void Distance_GetMultiplier_Lineaire()
        {
            // À 0 distance → 1.0, à range max → 0.3, à mi-distance → lerp(1, 0.3, 0.5) = 0.65.
            Assert.AreEqual(1.0f, DamageCalculator.GetDistanceMultiplier(0f, 100f), 0.001);
            Assert.AreEqual(0.3f, DamageCalculator.GetDistanceMultiplier(100f, 100f), 0.001);
            Assert.AreEqual(0.65f, DamageCalculator.GetDistanceMultiplier(50f, 100f), 0.001);
        }

        // ------------------------------------------------------------------------
        //  ARMURE
        // ------------------------------------------------------------------------

        [Test]
        public void Armure_ReduitLesDegats()
        {
            float noArmor  = DamageCalculator.CalculateFast(WeaponId, EnemyId, Element.Kinetic, false, false, 5f, 0f);
            float halfArm  = DamageCalculator.CalculateFast(WeaponId, EnemyId, Element.Kinetic, false, false, 5f, 50f);
            Assert.Less(halfArm, noArmor, "Armure devrait réduire les dégâts.");
            // 50% armor → ×0.5.
            Assert.AreEqual(noArmor * 0.5f, halfArm, 0.5f);
        }

        // ------------------------------------------------------------------------
        //  EDGE CASES
        // ------------------------------------------------------------------------

        [Test]
        public void Edge_0HP_Ennemi_DegatsLetaux()
        {
            var input = new DamageInput(WeaponId, EnemyId, Element.Kinetic, false, false, 5f);
            var r = DamageCalculator.Calculate(input, 0f, 0f);
            // Si l'ennemi a 0 HP, tout dégât est létal.
            Assert.IsTrue(r.IsLethal, "Tir sur ennemi à 0 HP devrait être létal.");
        }

        [Test]
        public void Edge_Overkill_Detecte()
        {
            var input = new DamageInput(WeaponId, EnemyId, Element.Kinetic, true, true, 1f);
            // 10 HP + gros dégât → overkill si dégâts > 1.5 × HP.
            var r = DamageCalculator.Calculate(input, 10f, 0f);
            Assert.IsTrue(r.IsOverkill, "Devrait être overkill (dégâts >> HP).");
        }

        [Test]
        public void Edge_DegatsLetaux_ExactementEgauxHP()
        {
            // On ajuste pour avoir dégâts ~= HP : il faut qu'IsLethal = true si finalDamage >= HP.
            var input = new DamageInput(WeaponId, EnemyId, Element.Kinetic, false, false, 5f);
            var probe = DamageCalculator.Calculate(input, 100000f, 0f);
            var r = DamageCalculator.Calculate(input, probe.FinalDamage, 0f);
            Assert.IsTrue(r.IsLethal, "Dégâts == HP devrait être létal.");
        }

        [Test]
        public void DamageFast_CorrespondACalculate()
        {
            var input = new DamageInput(WeaponId, EnemyId, Element.Cryo, true, false, 30f);
            var r = DamageCalculator.Calculate(input, 100f, 25f);
            var fast = DamageCalculator.CalculateFast(WeaponId, EnemyId, Element.Cryo, true, false, 30f, 25f);
            Assert.AreEqual(r.FinalDamage, fast, 0.01f, "Calculate et CalculateFast doivent donner le même résultat.");
        }
    }
}
