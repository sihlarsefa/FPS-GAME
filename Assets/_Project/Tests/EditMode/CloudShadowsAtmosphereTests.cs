using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public class CloudShadowsAtmosphereTests
    {
        [Test]
        public void CloudShadows_OffOnLowAndAtNight()
        {
            Assert.IsFalse(CloudShadowsMath.ForTier(0).Enabled);
            Assert.IsTrue(CloudShadowsMath.ForTier(2).Enabled);
            Assert.AreEqual(800f, CloudShadowsMath.ForTier(3).CookieSize, 1e-3f);
            Assert.IsFalse(CloudShadowsMath.IsActive(TimeOfDay.Gece, 3));
            Assert.IsFalse(CloudShadowsMath.IsActive(TimeOfDay.Gunduz, 0));
            Assert.IsTrue(CloudShadowsMath.IsActive(TimeOfDay.Gunduz, 1));
        }

        [Test]
        public void CloudShadows_RainHasMoreCoverageButShallowerShadow()
        {
            Assert.Greater(CloudShadowsMath.Coverage(TimeOfDay.Gunduz, WeatherKind.Yagmur), CloudShadowsMath.Coverage(TimeOfDay.Gunduz, WeatherKind.Acik));
            Assert.Less(CloudShadowsMath.Darkness(TimeOfDay.Gunduz, WeatherKind.Yagmur), CloudShadowsMath.Darkness(TimeOfDay.Gunduz, WeatherKind.Acik));
        }

        [Test]
        public void CookieValue_BoundedAndDarkensWithDensity()
        {
            float clear = CloudShadowsMath.CookieValue(0f, 0.4f, 0.6f);
            float dense = CloudShadowsMath.CookieValue(1f, 0.4f, 0.6f);
            Assert.AreEqual(1f, clear, 1e-4f);
            Assert.AreEqual(0.4f, dense, 1e-3f);
            Assert.GreaterOrEqual(dense, 0f);
        }

        [Test]
        public void AdvanceOffset_WrapsAndFollowsWind()
        {
            var o = CloudShadowsMath.AdvanceOffset(Vector2.zero, 1f, 0f, 10f, 1f, Vector3.right, Vector3.forward, 800f);
            Assert.AreEqual(10f, o.x, 1e-3f);
            Assert.AreEqual(0f, o.y, 1e-3f);
            var w = CloudShadowsMath.AdvanceOffset(new Vector2(799f, 0f), 1f, 0f, 10f, 1f, Vector3.right, Vector3.forward, 800f);
            Assert.AreEqual(9f, w.x, 1e-3f);
            var back = CloudShadowsMath.AdvanceOffset(Vector2.zero, -1f, 0f, 10f, 1f, Vector3.right, Vector3.forward, 800f);
            Assert.AreEqual(790f, back.x, 1e-3f);
            Assert.AreEqual(3f, CloudShadowsMath.SpeedMps(0f), 1e-3f);
            Assert.Greater(CloudShadowsMath.SpeedMps(1.5f), CloudShadowsMath.SpeedMps(0.5f));
        }

        [Test]
        public void DetailTier_TableIsMonotonic()
        {
            Assert.AreEqual(0, AtmosphereDetailMath.ForTier(0).HazeRings);
            Assert.IsFalse(AtmosphereDetailMath.ForTier(0).Glare);
            Assert.AreEqual(0, AtmosphereDetailMath.ForTier(1).DustMax);
            Assert.Greater(AtmosphereDetailMath.ForTier(3).DustMax, AtmosphereDetailMath.ForTier(2).DustMax);
            Assert.GreaterOrEqual(AtmosphereDetailMath.ForTier(3).FlareGhosts, AtmosphereDetailMath.ForTier(2).FlareGhosts);
        }

        [Test]
        public void Dust_RequiresSunAndFog()
        {
            Assert.IsTrue(AtmosphereDetailMath.DustVisible(TimeOfDay.Gunduz, WeatherKind.Acik, 0.5f, true));
            Assert.IsFalse(AtmosphereDetailMath.DustVisible(TimeOfDay.Gunduz, WeatherKind.Acik, 0.5f, false));
            Assert.IsFalse(AtmosphereDetailMath.DustVisible(TimeOfDay.Gunduz, WeatherKind.Yagmur, 0.5f, true));
            Assert.IsFalse(AtmosphereDetailMath.DustVisible(TimeOfDay.Gece, WeatherKind.Acik, 0.5f, true));
            Assert.IsFalse(AtmosphereDetailMath.DustVisible(TimeOfDay.Gunduz, WeatherKind.Acik, 0.0f, true));
            Assert.Greater(AtmosphereDetailMath.DustAlpha(1f, 0.5f), AtmosphereDetailMath.DustAlpha(-1f, 0.5f));
        }

        [Test]
        public void Glare_ConeAndOcclusion()
        {
            Assert.AreEqual(0f, AtmosphereDetailMath.GlareIntensity(0f, 1f, WeatherKind.Acik, TimeOfDay.Gunduz), 1e-4f);
            Assert.AreEqual(1f, AtmosphereDetailMath.GlareIntensity(1f, 1f, WeatherKind.Acik, TimeOfDay.Gunduz), 1e-4f);
            Assert.AreEqual(0f, AtmosphereDetailMath.GlareIntensity(1f, 0f, WeatherKind.Acik, TimeOfDay.Gunduz), 1e-4f);
            Assert.AreEqual(0f, AtmosphereDetailMath.GlareIntensity(1f, 1f, WeatherKind.Acik, TimeOfDay.Gece), 1e-4f);
            var g = AtmosphereDetailMath.GhostViewport(new Vector2(0.9f, 0.5f), -1f);
            Assert.AreEqual(0.1f, g.x, 1e-4f);
        }

        [Test]
        public void HazeRing_IndicesValidAndAlphaBounded()
        {
            var d = AtmosphereDetailMath.BuildHazeRing(1100f, -40f, 20f, 150f, 32);
            Assert.AreEqual(AtmosphereDetailMath.HazeRows * 33, d.Vertices.Length);
            Assert.AreEqual(d.Vertices.Length, d.Alpha.Length);
            foreach (int i in d.Triangles) Assert.That(i, Is.InRange(0, d.Vertices.Length - 1));
            foreach (float a in d.Alpha) Assert.That(a, Is.InRange(0f, 1f));
            Assert.AreEqual(0f, d.Alpha[2 * 33], 1e-6f);
        }

        [Test]
        public void Cirrus_NoiseIsTileableAndBounded()
        {
            float a = AtmosphereDetailMath.PeriodicValueNoise(0.25f, 0.5f, 4, 4, 3);
            float b = AtmosphereDetailMath.PeriodicValueNoise(4.25f, 4.5f, 4, 4, 3);
            Assert.AreEqual(a, b, 1e-5f);
            for (int i = 0; i < 50; i++)
            {
                float d = AtmosphereDetailMath.CirrusDensity(i / 50f, (i * 7 % 50) / 50f, 91);
                Assert.That(d, Is.InRange(0f, 1f));
            }

            Assert.AreEqual(0f, AtmosphereDetailMath.CirrusCoverage(TimeOfDay.Gunduz, WeatherKind.Yagmur), 1e-6f);
        }
    
        [Test]
        public void Cirrus_AlphaIsSoftAndLow()
        {
            foreach (TimeOfDay t in new[] { TimeOfDay.Gunduz, TimeOfDay.Gece, TimeOfDay.Safak, TimeOfDay.Aksam })
            {
                float a = AtmosphereDetailMath.CirrusAlpha(t, WeatherKind.Acik);
                Assert.That(a, Is.InRange(0.15f, 0.3f));
            }

            float prev = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float v = AtmosphereDetailMath.CirrusSoftAlpha(i / 100f, 0.4f);
                Assert.That(v, Is.InRange(0f, 0.9001f));
                Assert.That(v - prev, Is.LessThan(0.05f), "sert kenar yok");
                prev = v;
            }
        }

        [Test]
        public void Cumulus_PixelsBoundedAndSoft()
        {
            var px = AtmosphereDetailMath.BuildCumulusPixels(32, 0.5f, 5);
            Assert.AreEqual(32 * 32, px.Length);
            foreach (var c in px) Assert.That(c.r, Is.GreaterThan(100));
            float prev = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float a = AtmosphereDetailMath.CumulusAlpha(i / 100f, 0.5f);
                Assert.That(a, Is.InRange(0f, 1f));
                Assert.That(a - prev, Is.LessThan(0.04f));
                prev = a;
            }
        }
    }
}
