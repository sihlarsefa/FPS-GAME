using NUnit.Framework;
using Project.Infrastructure.Rendering;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class WaterSurfaceMathTests
    {
        [Test]
        public void FoamWidth_AlwaysWithinBounds()
        {
            foreach (var d in new[] { 0f, 1f, 20f, 500f })
            foreach (var h in new[] { 0f, 720f, 2160f })
            {
                var w = WaterSurfaceMath.FoamWidth(d, 75f, h);
                Assert.GreaterOrEqual(w, 1.2f);
                Assert.LessOrEqual(w, 2.0f);
            }
        }

        [Test]
        public void FoamWidth_GrowsWithDistance()
        {
            Assert.GreaterOrEqual(WaterSurfaceMath.FoamWidth(80f, 75f, 1080f, 14f), WaterSurfaceMath.FoamWidth(5f, 75f, 1080f, 14f));
        }

        [Test]
        public void FoamPhasesAreHalfPeriodApartAndCrossfade()
        {
            for (var t = 0f; t < 20f; t += 0.7f)
            {
                var a = WaterSurfaceMath.FoamPhase(t, 9f, 0);
                var b = WaterSurfaceMath.FoamPhase(t, 9f, 1);
                Assert.That(a, Is.InRange(0f, 1f));
                Assert.That(b, Is.InRange(0f, 1f));
                var sum = WaterSurfaceMath.FoamWeight(a) + WaterSurfaceMath.FoamWeight(b);
                Assert.That(sum, Is.InRange(0.99f, 1.5f));
            }
        }

        [Test]
        public void WaveRotation_BoundedAndZeroAtReference()
        {
            Assert.AreEqual(0f, WaterSurfaceMath.WaveRotationRad(WaterSurfaceMath.ReferenceWindDeg), 1e-5f);
            for (var a = -720f; a <= 720f; a += 17f)
                Assert.LessOrEqual(System.Math.Abs(WaterSurfaceMath.WaveRotationRad(a)), 1.0472f + 1e-4f);
        }

        [Test]
        public void ApproachAngle_TakesShortestPathAndIsSlow()
        {
            var v = WaterSurfaceMath.ApproachAngle(350f, 10f, 1f);
            Assert.Greater(v, 350f);
            Assert.Less(v, 360f); // 20 derecelik kısa yol, 1 sn'de ~%3
            var v30 = WaterSurfaceMath.ApproachAngle(0f, 90f, 30f);
            Assert.AreEqual(90f * (1f - 1f / (float)System.Math.E), v30, 0.5f);
        }

        [Test]
        public void Glitter_MonotonicByTierAndInShaderRange()
        {
            Assert.AreEqual(0f, WaterSurfaceMath.GlitterStrength(0));
            for (var t = 1; t <= 3; t++)
            {
                Assert.GreaterOrEqual(WaterSurfaceMath.GlitterStrength(t), WaterSurfaceMath.GlitterStrength(t - 1));
                Assert.LessOrEqual(WaterSurfaceMath.GlitterStrength(t), 2f);
            }
        }

        [Test]
        public void LowSunFactor_PeaksAtLowSunAndVanishesAtZenithOrBelowHorizon()
        {
            Assert.AreEqual(0f, WaterSurfaceMath.LowSunFactor(0.9f), 1e-5f);
            Assert.AreEqual(0f, WaterSurfaceMath.LowSunFactor(-0.5f), 1e-5f);
            Assert.Greater(WaterSurfaceMath.LowSunFactor(0.15f), 0.6f);
        }

        [Test]
        public void Shafts_OnlyWhenUnderwaterSunVisibleAndHighTier()
        {
            Assert.AreEqual(0, WaterSurfaceMath.ShaftCount(1));
            Assert.AreEqual(3, WaterSurfaceMath.ShaftCount(3));
            Assert.AreEqual(0f, WaterSurfaceMath.ShaftAlpha(1f, false, -0.8f, 3));
            Assert.AreEqual(0f, WaterSurfaceMath.ShaftAlpha(0f, true, -0.8f, 3));
            Assert.AreEqual(0f, WaterSurfaceMath.ShaftAlpha(1f, true, -0.8f, 1));
            var a = WaterSurfaceMath.ShaftAlpha(1f, true, -0.8f, 3);
            Assert.Greater(a, 0f);
            Assert.LessOrEqual(a, 0.22f);
            for (var i = 0; i < 3; i++)
                Assert.That(WaterSurfaceMath.ShaftRadius(i), Is.InRange(3f, 7f));
        }
    }
}
