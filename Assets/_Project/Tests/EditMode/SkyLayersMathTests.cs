using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public class SkyLayersMathTests
    {
        [Test]
        public void Tier_LowHasFewerStarsAndNoHighLayerOrRays()
        {
            var low = SkyLayersMath.ForTier(0);
            var ultra = SkyLayersMath.ForTier(3);
            Assert.IsFalse(low.HighCloudLayer);
            Assert.IsFalse(low.GodRays);
            Assert.IsTrue(ultra.HighCloudLayer);
            Assert.Greater(ultra.StarCount, low.StarCount);
        }

        [Test]
        public void CloudLayers_HighIsSlowerAndWindSpeedsUp()
        {
            var calm = SkyLayersMath.LowLayerSpeed(1f, 0.35f);
            var gale = SkyLayersMath.LowLayerSpeed(1f, 1f);
            Assert.Greater(gale, calm);
            Assert.Greater(SkyLayersMath.LowLayerSpeed(1f, 0.5f), SkyLayersMath.HighLayerSpeed(1f, 0.5f));
        }

        [Test]
        public void StormDarken_ClampedAndMonotonic()
        {
            var clear = SkyLayersMath.StormDarken(0.2f, 0f, 0f);
            var storm = SkyLayersMath.StormDarken(1f, 1f, 1f);
            Assert.Greater(clear, storm);
            Assert.Greater(storm, 0.34f);
            Assert.IsTrue(SkyLayersMath.StormDarken(0f, 0f, 0f) <= 1f);
            Assert.Greater(SkyLayersMath.HighLayerDarken(storm), storm);
        }

        [Test]
        public void GodRays_NoneAtNightOrHeavyRain_StrongerFacingSun()
        {
            Assert.AreEqual(0f, SkyLayersMath.GodRayAlpha(TimeOfDay.Gece, 0.3f, 0f, 1f), 1e-5f);
            Assert.AreEqual(0f, SkyLayersMath.GodRayAlpha(TimeOfDay.Gunduz, 0.5f, 1f, 1f), 1e-5f);
            var away = SkyLayersMath.GodRayAlpha(TimeOfDay.Gunduz, 0.4f, 0f, 0f);
            var toward = SkyLayersMath.GodRayAlpha(TimeOfDay.Gunduz, 0.4f, 0f, 1f);
            Assert.Greater(toward, away);
            Assert.Greater(SkyLayersMath.GodRayAlpha(TimeOfDay.Aksam, 0.4f, 0f, 1f), toward);
        }

        [Test]
        public void RayIntensity_FadesWithRadiusAndIsDeterministic()
        {
            Assert.AreEqual(0f, SkyLayersMath.RayIntensity(0.3f, 1f, 5), 1e-6f);
            Assert.Greater(SkyLayersMath.RayIntensity(0.3f, 0.1f, 5), SkyLayersMath.RayIntensity(0.3f, 0.8f, 5));
            Assert.AreEqual(SkyLayersMath.RayIntensity(0.3f, 0.4f, 5), SkyLayersMath.RayIntensity(0.3f, 0.4f, 5), 1e-6f);
        }

        [Test]
        public void Stars_OnlyAtNightAndCoveredByCloudStorm()
        {
            Assert.AreEqual(0f, SkyLayersMath.StarVisibility(TimeOfDay.Gunduz, 0f, 0f), 1e-6f);
            Assert.AreEqual(1f, SkyLayersMath.StarVisibility(TimeOfDay.Gece, 0f, 0f), 1e-6f);
            Assert.Greater(SkyLayersMath.StarVisibility(TimeOfDay.Gece, 0f, 0f), SkyLayersMath.StarVisibility(TimeOfDay.Gece, 0.8f, 0f));
            Assert.AreEqual(0f, SkyLayersMath.StarVisibility(TimeOfDay.Gece, 0f, 1f), 1e-6f);
        }

        [Test]
        public void StarDirection_UnitAboveHorizon()
        {
            for (var i = 0; i < 200; i++)
            {
                var d = SkyLayersMath.StarDirection(i, 101);
                Assert.IsTrue(d.magnitude >= 0.999f && d.magnitude <= 1.001f);
                Assert.IsTrue(d.y >= 0.139f);
                var b = SkyLayersMath.StarBrightness(i, 101);
                Assert.IsTrue(b >= 0.35f && b <= 1f);
            }
        }

        [Test]
        public void MoonPhase_KnownNewMoonAndFullMoon()
        {
            Assert.IsTrue(SkyLayersMath.MoonPhase(SkyLayersMath.KnownNewMoonUnixDays) >= -0.001f && SkyLayersMath.MoonPhase(SkyLayersMath.KnownNewMoonUnixDays) <= 0.001f);
            var full = SkyLayersMath.MoonPhase(SkyLayersMath.KnownNewMoonUnixDays + SkyLayersMath.SynodicMonthDays * 0.5);
            Assert.IsTrue(full >= 0.499f && full <= 0.501f);
            Assert.Greater(SkyLayersMath.MoonIllumination(0.5f), 0.99f);
            Assert.IsTrue(SkyLayersMath.MoonIllumination(0f) >= -0.001f && SkyLayersMath.MoonIllumination(0f) <= 0.001f);
        }

        [Test]
        public void MoonPixel_WaxingLightsRightSide_NewIsDark_OutsideIsNegative()
        {
            Assert.Greater(SkyLayersMath.MoonPixel(0.6f, 0f, 0.25f), SkyLayersMath.MoonPixel(-0.6f, 0f, 0.25f));
            Assert.Greater(SkyLayersMath.MoonPixel(-0.6f, 0f, 0.75f), SkyLayersMath.MoonPixel(0.6f, 0f, 0.75f));
            Assert.Greater(SkyLayersMath.MoonPixel(0f, 0f, 0.5f), 0.7f);
            Assert.Greater(0.2f, SkyLayersMath.MoonPixel(0f, 0f, 0f));
            Assert.Greater(0f, SkyLayersMath.MoonPixel(1f, 1f, 0.5f));
        }
    }
}
