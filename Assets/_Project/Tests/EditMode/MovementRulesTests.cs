#if UNITY_EDITOR
using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class MovementRulesTests
    {
        [Test]
        public void Stamina_SprintDrains_ThenExhausts_AndBlocksSprint()
        {
            var s = new StaminaModel();
            var exhausted = false;
            for (var i = 0; i < 1200 && !exhausted; i++)
                exhausted = s.Tick(0.01f, true, 1f, 1f);

            Assert.IsTrue(exhausted);
            Assert.IsTrue(s.Exhausted);
            Assert.IsFalse(s.CanSprint(true));
            Assert.IsFalse(s.CanAfford(MovementRules.JumpStaminaCost));
        }

        [Test]
        public void Stamina_RegenWaitsForDelay_ThenRecoversAndLeavesExhaustion()
        {
            var s = new StaminaModel();
            s.Spend(100f);
            Assert.IsTrue(s.Exhausted);
            s.Tick(0.5f, false, 1f, 1f);
            Assert.AreEqual(0f, s.Current, 1e-4f, "bekleme süresi dolmadan toparlanma yok");
            for (var i = 0; i < 100; i++)
                s.Tick(0.1f, false, 1f, 1f);
            Assert.Greater(s.Current, 25f);
            Assert.IsFalse(s.Exhausted);
            Assert.IsTrue(s.CanSprint(false));
        }

        [Test]
        public void Stamina_HeavyLoadDrainsFaster()
        {
            var a = new StaminaModel();
            var b = new StaminaModel();
            a.Tick(1f, true, 1f, MovementRules.LoadDrainMultiplier(0f));
            b.Tick(1f, true, 1f, MovementRules.LoadDrainMultiplier(1f));
            Assert.Less(b.Current, a.Current);
        }

        [Test]
        public void Load_BurdenMonotonic_AndFactorsBounded()
        {
            Assert.AreEqual(0f, MovementRules.LoadBurden(0.1f, 0f), 1e-4f);
            Assert.Less(MovementRules.LoadBurden(0.6f, 0f), MovementRules.LoadBurden(1f, 0f));
            Assert.Less(MovementRules.LoadBurden(0.5f, 0f), MovementRules.LoadBurden(0.5f, 4f));
            Assert.LessOrEqual(MovementRules.LoadBurden(5f, 20f), 1f);
            Assert.AreEqual(1f, MovementRules.LoadSpeedFactor(0f), 1e-4f);
            Assert.GreaterOrEqual(MovementRules.LoadSpeedFactor(1f), 0.8f);
            Assert.Less(MovementRules.LoadAccelFactor(1f), MovementRules.LoadAccelFactor(0f));
        }

        [Test]
        public void Slope_UphillSlows_DownhillSlightlyFaster_FlatNeutral()
        {
            Assert.AreEqual(1f, MovementRules.SlopeSpeedFactor(2f, -1f), 1e-4f);
            Assert.Less(MovementRules.SlopeSpeedFactor(30f, -1f), 0.8f);
            Assert.GreaterOrEqual(MovementRules.SlopeSpeedFactor(60f, -1f), 0.6f);
            Assert.Greater(MovementRules.SlopeSpeedFactor(30f, 1f), 1f);
            Assert.LessOrEqual(MovementRules.SlopeSpeedFactor(60f, 1f), 1.06f);
            Assert.AreEqual(1f, MovementRules.SlopeSpeedFactor(30f, 0f), 1e-4f);
        }

        [Test]
        public void Landing_HardImpactCostsStaminaAndSpeed()
        {
            Assert.AreEqual(0f, MovementRules.LandingStaminaCost(3f), 1e-4f);
            Assert.Greater(MovementRules.LandingStaminaCost(9f), 0f);
            Assert.AreEqual(1f, MovementRules.LandingVelocityKeep(2f), 1e-4f);
            Assert.AreEqual(0.45f, MovementRules.LandingVelocityKeep(30f), 1e-4f);
        }

        [Test]
        public void StanceCurve_EaseInOut_AndDurationScales()
        {
            Assert.AreEqual(0f, MovementRules.StanceEase(0f), 1e-4f);
            Assert.AreEqual(1f, MovementRules.StanceEase(1f), 1e-4f);
            Assert.AreEqual(0.5f, MovementRules.StanceEase(0.5f), 1e-4f);
            Assert.Less(MovementRules.StanceEase(0.1f), 0.1f, "yavaş başlar");
            Assert.Greater(MovementRules.StanceDuration(1.8f, 0.6f, 10f), MovementRules.StanceDuration(1.8f, 1.2f, 10f));
            Assert.Less(MovementRules.StanceDuration(1.8f, 1.2f, 20f), MovementRules.StanceDuration(1.8f, 1.2f, 10f));
            Assert.AreEqual(1f, MovementRules.StanceTransitionSpeedFactor(1.8f, 1.8f), 1e-4f);
            Assert.AreEqual(0.65f, MovementRules.StanceTransitionSpeedFactor(1.8f, 0.6f), 1e-4f);
        }

        [Test]
        public void Footstep_CadenceTiedToSpeed()
        {
            var walk = MovementRules.StepsPerSecond(4.6f, MovementRules.StrideLength(false, false, false));
            var sprint = MovementRules.StepsPerSecond(7.2f, MovementRules.StrideLength(false, false, true));
            Assert.Greater(sprint, walk);
            Assert.AreEqual(0f, MovementRules.StepsPerSecond(0f, 2.2f), 1e-4f);
            Assert.Less(MovementRules.StrideLength(false, true, false), MovementRules.StrideLength(true, false, false));
        }

        [Test]
        public void Slide_RequiresStaminaAndCooldown()
        {
            Assert.IsTrue(MovementRules.CanSlide(50f, 0f));
            Assert.IsFalse(MovementRules.CanSlide(5f, 0f));
            Assert.IsFalse(MovementRules.CanSlide(50f, 1f));
        }

        [Test]
        public void Breathing_RisesAsStaminaFalls()
        {
            Assert.AreEqual(0f, MovementRules.BreathingIntensity(1f, false), 1e-4f);
            Assert.Greater(MovementRules.BreathingIntensity(0.1f, false), MovementRules.BreathingIntensity(0.4f, false));
            Assert.GreaterOrEqual(MovementRules.BreathingIntensity(0.9f, true), 0.75f);
        }

        [Test]
        public void Lean_ClearanceScalesAllowance()
        {
            Assert.AreEqual(1f, MovementRules.LeanAllowed(1f, 0.35f), 1e-4f);
            Assert.AreEqual(0.5f, MovementRules.LeanAllowed(0.175f, 0.35f), 1e-4f);
            Assert.AreEqual(0f, MovementRules.LeanAllowed(0f, 0.35f), 1e-4f);
        }
    }
}
#endif
