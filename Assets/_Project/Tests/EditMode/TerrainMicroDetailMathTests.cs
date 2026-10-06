using NUnit.Framework;
using Project.Infrastructure.World;

namespace Project.Tests.EditMode
{
    public sealed class TerrainMicroDetailMathTests
    {
        [Test]
        public void RoadWear_PeaksAtEdge_ZeroFarAway()
        {
            Assert.Greater(TerrainMicroDetailMath.RoadWear(0.01f, 2.5f), 0.9f);
            Assert.AreEqual(0f, TerrainMicroDetailMath.RoadWear(3f, 2.5f));
            Assert.AreEqual(0f, TerrainMicroDetailMath.RoadWear(float.PositiveInfinity, 2.5f));
            Assert.AreEqual(0f, TerrainMicroDetailMath.RoadWear(-5f, 2.5f));
        }

        [Test]
        public void GrassSoilBand_PeaksInMiddle()
        {
            Assert.AreEqual(1f, TerrainMicroDetailMath.GrassSoilBand(0.5f), 1e-5f);
            Assert.AreEqual(0f, TerrainMicroDetailMath.GrassSoilBand(0f), 1e-5f);
            Assert.AreEqual(0f, TerrainMicroDetailMath.GrassSoilBand(1f), 1e-5f);
        }

        [Test]
        public void RockCrack_NeedsSteepSlope()
        {
            Assert.AreEqual(0f, TerrainMicroDetailMath.RockCrackScore(20f, 1f));
            Assert.Greater(TerrainMicroDetailMath.RockCrackScore(70f, 1f), 0.9f);
        }

        [Test]
        public void FindPits_DetectsBasin_AndRespectsTierBudget()
        {
            const int res = 21;
            var h = new float[res * res];
            for (var i = 0; i < h.Length; i++) h[i] = 10f;
            h[10 * res + 10] = 9.5f;
            var pits = TerrainMicroDetailMath.FindPits(h, res, 1f, 0f, 0f, 0.1f, 0f, 2, 1, 5);
            Assert.AreEqual(1, pits.Count);
            Assert.AreEqual(10f, pits[0].X, 1e-4f);
            Assert.Greater(pits[0].Depth, 0.4f);
            Assert.AreEqual(0, TerrainMicroDetailMath.FindPits(h, res, 1f, 0f, 0f, 0.1f, 20f, 2, 1, 5).Count);
            Assert.Greater(TerrainMicroDetailMath.Budget(3)[1], TerrainMicroDetailMath.Budget(1)[1]);
            Assert.AreEqual(0, TerrainMicroDetailMath.Budget(0)[0]);
        }
    }
}
