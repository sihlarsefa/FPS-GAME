#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.World;

namespace Project.Tests.EditMode
{
    public sealed class MountainRulesTests
    {
        [Test]
        public void CliffRock_FlatNone_SteepStrong()
        {
            Assert.AreEqual(0f, TerrainPaintRules.CliffRock(20f, 50f), 1e-4f);
            Assert.Greater(TerrainPaintRules.CliffRock(50f, 50f), 0.85f);
            Assert.Greater(TerrainPaintRules.CliffRock(40f, 50f), TerrainPaintRules.CliffRock(33f, 50f));
        }

        [Test]
        public void CliffDarkening_SteeperIsDarker()
        {
            Assert.AreEqual(1f, TerrainPaintRules.CliffDarkening(20f), 1e-4f);
            Assert.Less(TerrainPaintRules.CliffDarkening(60f), 0.6f);
        }

        [Test]
        public void SlopeSnow_HeightBandAndSteepExposed()
        {
            Assert.AreEqual(0f, TerrainPaintRules.SlopeSnow(40f, 72f, 10f, 0f), 1e-4f);
            Assert.AreEqual(1f, TerrainPaintRules.SlopeSnow(100f, 72f, 10f, 0f), 1e-4f);
            Assert.AreEqual(0f, TerrainPaintRules.SlopeSnow(100f, 72f, 55f, 0f), 1e-4f);
            Assert.Greater(TerrainPaintRules.SlopeSnow(100f, 72f, 25f, 0f), TerrainPaintRules.SlopeSnow(100f, 72f, 40f, 0f));
        }

        [Test]
        public void SnowExposedRock_OnlyAboveLineAndSteep()
        {
            Assert.AreEqual(0f, TerrainPaintRules.SnowExposedRock(40f, 72f, 50f), 1e-4f);
            Assert.AreEqual(0f, TerrainPaintRules.SnowExposedRock(100f, 72f, 15f), 1e-4f);
            Assert.Greater(TerrainPaintRules.SnowExposedRock(100f, 72f, 50f), 0.8f);
        }

        [Test]
        public void RidgeSharpen_OnlyNearEdgeMountains()
        {
            Assert.AreEqual(0f, TerrainPaintRules.RidgeSharpen(0f, 1f), 1e-4f);
            Assert.Greater(TerrainPaintRules.RidgeSharpen(1f, 1f), 7f);
            Assert.Less(TerrainPaintRules.RidgeSharpen(1f, 0f), 0f);
        }

        [Test]
        public void Outcrop_Rules()
        {
            Assert.IsTrue(RockScatter.OutcropAllowed(45f, 80f, 0f));
            Assert.IsFalse(RockScatter.OutcropAllowed(10f, 80f, 0f));
            Assert.IsFalse(RockScatter.OutcropAllowed(45f, 80f, 0.5f));
            Assert.AreEqual(26, RockScatter.ClusterSeedCount(260));
            Assert.AreEqual(0, RockScatter.ClusterSeedCount(0));
            Assert.Greater(RockScatter.OutcropRockSize(0, 0f), RockScatter.OutcropRockSize(3, 0.5f));
        }
    }
}
#endif
