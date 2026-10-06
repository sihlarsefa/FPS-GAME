using NUnit.Framework;
using Project.Infrastructure.World;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class RiverFlowRulesTests
    {
        [Test]
        public void Speed_IncreasesWithSlopeAndNarrowing_AndIsBounded()
        {
            Assert.Greater(RiverFlowRules.Speed01(0.05f, 10f, 10f), RiverFlowRules.Speed01(0.005f, 10f, 10f));
            Assert.Greater(RiverFlowRules.Speed01(0.02f, 5f, 10f), RiverFlowRules.Speed01(0.02f, 20f, 10f));
            Assert.AreEqual(1f, RiverFlowRules.Speed01(5f, 1f, 50f));
            Assert.GreaterOrEqual(RiverFlowRules.Speed01(0f, 100f, 1f), 0f);
        }

        [Test]
        public void Foam_DenseOnlyWhenFast()
        {
            Assert.AreEqual(0f, RiverFlowRules.FoamDensity(0.2f));
            Assert.AreEqual(1f, RiverFlowRules.FoamDensity(1f), 1e-4f);
            Assert.AreEqual(0, RiverFlowRules.StreakCount(0.3f));
            Assert.AreEqual(3, RiverFlowRules.StreakCount(1f));
            Assert.Greater(RiverFlowRules.StreakLength(1f), RiverFlowRules.StreakLength(0f));
        }

        [Test]
        public void RockCount_WithinSixToTen()
        {
            foreach (var len in new[] { 0f, 100f, 500f, 5000f })
                Assert.That(RiverFlowRules.RockCount(len), Is.InRange(6, 10));
        }

        [Test]
        public void RockPlacement_DeterministicAndBounded()
        {
            for (var i = 0; i < 10; i++)
            {
                Assert.AreEqual(RiverFlowRules.RockT(i, 10, 7), RiverFlowRules.RockT(i, 10, 7));
                Assert.That(RiverFlowRules.RockT(i, 10, 7), Is.InRange(0.04f, 0.96f));
                Assert.That(RiverFlowRules.RockLateral(i, 7), Is.InRange(-0.6f, 0.6f));
                Assert.That(RiverFlowRules.RockSize(i, 7), Is.InRange(0.9f, 2.1f));
            }
        }

        [Test]
        public void RingGravelWake_Monotonic()
        {
            Assert.Greater(RiverFlowRules.RockRingRadius(1.5f, 1f), RiverFlowRules.RockRingRadius(1.5f, 0f));
            Assert.Greater(RiverFlowRules.GravelWidth(0.1f), RiverFlowRules.GravelWidth(0.9f));
            Assert.Greater(RiverFlowRules.PierWakeLength(1f), RiverFlowRules.PierWakeLength(0f));
        }

        [Test]
        public void WaterSound_ScalesWithSpeedAndFadesWithDistance()
        {
            Assert.Greater(RiverFlowRules.WaterSoundIntensity(1f, 5f), RiverFlowRules.WaterSoundIntensity(0.2f, 5f));
            Assert.Greater(RiverFlowRules.WaterSoundIntensity(0.8f, 5f), RiverFlowRules.WaterSoundIntensity(0.8f, 30f));
            Assert.AreEqual(0f, RiverFlowRules.WaterSoundIntensity(1f, 100f));
        }
    }
}
