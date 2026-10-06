using NUnit.Framework;
using Project.Infrastructure.Rendering;

namespace Project.Tests.EditMode
{
    public class NightLightingMathTests
    {
        [Test]
        public void Kelvin4300_IsWarm()
        {
            NightLightingMath.KelvinToRgb(4300f, out var r, out var g, out var b);
            Assert.AreEqual(1f, r, 1e-4f);
            Assert.Greater(r, g);
            Assert.Greater(g, b);
            Assert.Greater(b, 0.3f);
            Assert.Less(b, 0.8f);
        }

        [Test]
        public void Phosphor_LiftsShadows_MonotonicAndEndsAtOne()
        {
            Assert.Greater(NightLightingMath.Phosphor(0f, 0.08f, 2.2f), 0.02f);
            Assert.AreEqual(1f, NightLightingMath.Phosphor(1f, 0.08f, 2.2f), 1e-4f);
            var prev = -1f;
            for (var i = 0; i <= 20; i++)
            {
                var v = NightLightingMath.Phosphor(i / 20f, 0.08f, 2.2f);
                Assert.GreaterOrEqual(v, prev);
                Assert.LessOrEqual(v, 1f);
                prev = v;
            }
            // Gölge bölgesinde doğrusaldan yüksek (kaldırılmış).
            Assert.Greater(NightLightingMath.Phosphor(0.1f, 0.08f, 2.2f), 0.1f);
        }

        [Test]
        public void AmplificationEv_DarkerAmbientMeansMoreExposure()
        {
            var dark = NightLightingMath.AmplificationEv(0f, 1.6f, 3.2f);
            var mid = NightLightingMath.AmplificationEv(0.1f, 1.6f, 3.2f);
            var bright = NightLightingMath.AmplificationEv(0.5f, 1.6f, 3.2f);
            Assert.AreEqual(3.2f, dark, 1e-4f);
            Assert.AreEqual(1.6f, bright, 1e-4f);
            Assert.Greater(dark, mid);
            Assert.Greater(mid, bright);
        }

        [Test]
        public void BatteryFlicker_OffAbove10Percent_FlickersBelow()
        {
            for (var i = 0; i < 100; i++)
                Assert.AreEqual(1f, NightLightingMath.BatteryFlicker(0.5f, i * 0.07f));
            var min = 1f;
            var dips = 0;
            for (var i = 0; i < 400; i++)
            {
                var f = NightLightingMath.BatteryFlicker(0.02f, i * 0.05f);
                Assert.GreaterOrEqual(f, 0.2f);
                Assert.LessOrEqual(f, 1f);
                if (f < min) min = f;
                if (f < 0.9f) dips++;
            }
            Assert.Less(min, 0.6f);
            Assert.Greater(dips, 20);
        }

        [Test]
        public void Sway_SmallAndBoundedAndGrowsWithSpeed()
        {
            float maxIdle = 0f, maxRun = 0f;
            for (var i = 0; i < 500; i++)
            {
                NightLightingMath.Sway(i * 0.05f, 0f, out var p0, out var y0);
                NightLightingMath.Sway(i * 0.05f, 1f, out var p1, out var y1);
                maxIdle = System.Math.Max(maxIdle, System.Math.Max(System.Math.Abs(p0), System.Math.Abs(y0)));
                maxRun = System.Math.Max(maxRun, System.Math.Max(System.Math.Abs(p1), System.Math.Abs(y1)));
            }
            Assert.Less(maxRun, 0.6f);
            Assert.Greater(maxRun, maxIdle);
        }

        [Test]
        public void ConeAlpha_ZeroWithoutVolumetric_BoundedWith()
        {
            Assert.AreEqual(0f, NightLightingMath.ConeAlpha(0.02f, false));
            Assert.AreEqual(0f, NightLightingMath.ConeAlpha(0f, true));
            Assert.Greater(NightLightingMath.ConeAlpha(0.03f, true), NightLightingMath.ConeAlpha(0.005f, true));
            Assert.LessOrEqual(NightLightingMath.ConeAlpha(10f, true), 0.3f);
            Assert.AreEqual(0f, NightLightingMath.ConeFalloff(0f), 1e-5f);
            Assert.AreEqual(0f, NightLightingMath.ConeFalloff(1f), 1e-5f);
            Assert.Greater(NightLightingMath.ConeFalloff(0.2f), NightLightingMath.ConeFalloff(0.9f));
        }

        [Test]
        public void ConeRadius_MatchesTan()
        {
            Assert.AreEqual(25f * 0.19438f, NightLightingMath.ConeRadius(25f, 22f), 1e-3f);
        }
    }
}
