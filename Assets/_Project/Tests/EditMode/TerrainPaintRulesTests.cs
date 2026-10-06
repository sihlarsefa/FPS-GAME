#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.World;

namespace Project.Tests.EditMode
{
    public sealed class TerrainPaintRulesTests
    {
        [Test]
        public void MacroVariation_SinirliVeSifirdaKapali()
        {
            var old = TerrainPaintRules.MacroStrength;
            try
            {
                TerrainPaintRules.MacroStrength = 0.1f;
                Assert.IsTrue(TerrainPaintRules.MacroVariation(5f) <= 0.1001f);
                Assert.IsTrue(TerrainPaintRules.MacroVariation(-5f) >= -0.1001f);
                TerrainPaintRules.MacroStrength = 0f;
                Assert.AreEqual(0f, TerrainPaintRules.MacroVariation(0.7f));
            }
            finally { TerrainPaintRules.MacroStrength = old; }
        }

        [Test]
        public void SlopeRockVeKar_Monoton()
        {
            Assert.IsTrue(TerrainPaintRules.SlopeRock(10f) < 0.01f);
            Assert.IsTrue(TerrainPaintRules.SlopeRock(45f) > 0.99f);
            Assert.IsTrue(TerrainPaintRules.HeightSnow(50f, 135f) < 0.01f);
            Assert.IsTrue(TerrainPaintRules.HeightSnow(160f, 135f) > 0.99f);
        }

        [Test]
        public void KademeTablosu_BasemapArtar_DetailKapaliDusuk()
        {
            for (var i = 1; i <= 3; i++)
                Assert.IsTrue(TerrainPaintRules.BasemapDistance(i) > TerrainPaintRules.BasemapDistance(i - 1));
            Assert.AreEqual(0f, TerrainPaintRules.DetailDistance(0));
            Assert.IsTrue(TerrainPaintRules.DetailDensity(3, 5f) <= 1f);
            Assert.IsTrue(TerrainPaintRules.DetailDistance(3, 0.5f) < TerrainPaintRules.DetailDistance(3, 1f));
        }
    }
}
#endif
