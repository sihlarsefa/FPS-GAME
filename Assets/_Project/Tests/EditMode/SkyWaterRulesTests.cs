using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class SkyWaterRulesTests
    {
        [Test]
        public void Coverage_RainDenserThanClear()
        {
            Assert.Greater(SkyWaterRules.CloudCoverage(TimeOfDay.Gunduz, WeatherKind.Yagmur), SkyWaterRules.CloudCoverage(TimeOfDay.Gunduz, WeatherKind.Acik));
        }

        [Test]
        public void Shafts_OnlyDawnDusk()
        {
            Assert.AreEqual(0f, SkyWaterRules.ShaftStrength(TimeOfDay.Gunduz, WeatherKind.Acik));
            Assert.AreEqual(0f, SkyWaterRules.ShaftStrength(TimeOfDay.Gece, WeatherKind.Acik));
            Assert.AreEqual(1f, SkyWaterRules.ShaftStrength(TimeOfDay.Aksam, WeatherKind.Acik));
            Assert.Less(SkyWaterRules.ShaftStrength(TimeOfDay.Safak, WeatherKind.Yagmur), 1f);
        }

        [Test]
        public void Underwater_Blend()
        {
            Assert.AreEqual(0f, SkyWaterRules.UnderwaterBlend(20f, 18f));
            Assert.AreEqual(1f, SkyWaterRules.UnderwaterBlend(17f, 18f));
            Assert.Greater(SkyWaterRules.UnderwaterFogDensity(0.002f, 1f), 0.05f);
        }

        [Test]
        public void Smoothness_HigherAtGrazing()
        {
            Assert.Greater(SkyWaterRules.WaterSmoothness(0.05f), SkyWaterRules.WaterSmoothness(0.95f));
        }

        [Test]
        public void FindCrossing_Interpolates()
        {
            var h = new[] { 10f, 12f, 16f, 20f };
            Assert.AreEqual(1.5f * 2f, SkyWaterRules.FindCrossing(h, 2f, 14f), 1e-4f);
            Assert.AreEqual(-1f, SkyWaterRules.FindCrossing(h, 2f, 50f));
            Assert.AreEqual(-1f, SkyWaterRules.FindCrossing(null, 2f, 1f));
        }

        [Test]
        public void Noise_TilesAndStaysInRange()
        {
            for (int i = 0; i < 20; i++)
            {
                float v = i / 20f;
                float a = SkyWaterRules.TileableFbm(v, 0.3f, 4, 4, 7);
                float b = SkyWaterRules.TileableFbm(v + 1f, 0.3f, 4, 4, 7);
                Assert.AreEqual(a, b, 1e-4f);
                Assert.IsTrue(a >= 0f && a <= 1f);
            }
        }

        [Test]
        public void CloudAlpha_MonotonicInDensity()
        {
            Assert.LessOrEqual(SkyWaterRules.CloudAlpha(0.3f, 0.5f), SkyWaterRules.CloudAlpha(0.8f, 0.5f));
            Assert.AreEqual(0f, SkyWaterRules.CloudAlpha(0.1f, 0.3f));
        }
    }
}
