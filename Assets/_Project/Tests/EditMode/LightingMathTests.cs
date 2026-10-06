using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class LightingMathTests
    {
        [Test]
        public void SunKelvin_HorizonToNoon_MonotonicAndBounded()
        {
            Assert.AreEqual(2000f, LightingMath.SunKelvin(0f), 1f);
            Assert.AreEqual(2000f, LightingMath.SunKelvin(-10f), 1f);
            Assert.AreEqual(5800f, LightingMath.SunKelvin(60f), 1f);
            Assert.AreEqual(5800f, LightingMath.SunKelvin(90f), 1f);
            var prev = 0f;
            for (var e = 0f; e <= 60f; e += 5f)
            {
                var k = LightingMath.SunKelvin(e);
                Assert.GreaterOrEqual(k, prev);
                prev = k;
            }
            Assert.Greater(LightingMath.SunKelvin(10f), 3000f);
        }

        [Test]
        public void KelvinToRgb_WarmIsRed_NoonNearWhite_ColdIsBlue()
        {
            var warm = LightingMath.KelvinToRgb(2000f);
            Assert.AreEqual(1f, warm[0], 1e-3f);
            Assert.Less(warm[2], 0.2f);
            Assert.Greater(warm[0], warm[1]);
            var noon = LightingMath.KelvinToRgb(5800f);
            Assert.Greater(noon[0], 0.95f);
            Assert.Greater(noon[1], 0.9f);
            Assert.Greater(noon[2], 0.8f);
            var cold = LightingMath.KelvinToRgb(12000f);
            Assert.Greater(cold[2], cold[0]);
        }

        [Test]
        public void KelvinTint_HasUnitLuminance()
        {
            foreach (var k in new[] { 2000f, 3500f, 5800f, 9000f })
            {
                var t = LightingMath.KelvinTint(k);
                Assert.AreEqual(1f, LightingMath.Luminance(t[0], t[1], t[2]), 1e-3f);
            }
        }

        [Test]
        public void SunIntensity_FullAtNoon_DimNearHorizon()
        {
            Assert.AreEqual(1f, LightingMath.SunIntensityFactor(90f), 1e-3f);
            Assert.Less(LightingMath.SunIntensityFactor(2f), 0.25f);
            Assert.Greater(LightingMath.SunIntensityFactor(10f), LightingMath.SunIntensityFactor(2f));
            Assert.Greater(LightingMath.SunIntensityFactor(45f), 0.85f);
        }

        [Test]
        public void BlendSunColor_ZeroWeightKeepsPreset_LowSunWarms()
        {
            var same = LightingMath.BlendSunColor(0.9f, 0.8f, 0.7f, 5f, 0f);
            Assert.AreEqual(0.9f, same[0], 1e-5f);
            Assert.AreEqual(0.7f, same[2], 1e-5f);
            var warm = LightingMath.BlendSunColor(1f, 1f, 1f, 3f, 1f);
            Assert.Greater(warm[0], warm[2]);
        }

        [Test]
        public void ShadowStrength_ClearFullRainSofter()
        {
            Assert.AreEqual(1f, LightingMath.ShadowStrength(WeatherKind.Acik, 50f), 1e-4f);
            Assert.Less(LightingMath.ShadowStrength(WeatherKind.Yagmur, 50f), LightingMath.ShadowStrength(WeatherKind.Kar, 50f));
            Assert.Less(LightingMath.ShadowStrength(WeatherKind.Kar, 50f), 1f);
            Assert.GreaterOrEqual(LightingMath.ShadowStrength(WeatherKind.Yagmur, 0f), 0.2f);
        }

        [Test]
        public void Ambient_OvercastBoosts_ShadeFillDayGreaterThanNight()
        {
            Assert.Greater(LightingMath.AmbientScale(40f, WeatherKind.Yagmur), LightingMath.AmbientScale(40f, WeatherKind.Acik));
            Assert.Greater(LightingMath.ShadeFill(45f, WeatherKind.Acik), LightingMath.ShadeFill(-5f, WeatherKind.Acik));
            Assert.LessOrEqual(LightingMath.ShadeFill(90f, WeatherKind.Kar), 0.3f);
        }

        [Test]
        public void TargetEv_BrightSceneNegative_DarkScenePositive_Clamped()
        {
            var p = LightingMath.ExposureFor(TimeOfDay.Gunduz, MapCatalog.Kuzgun);
            var refLog = (float)System.Math.Log(p.ReferenceLuma, 2.0);
            Assert.AreEqual(0f, LightingMath.TargetEv(refLog, p), 1e-4f);
            Assert.Less(LightingMath.TargetEv(refLog + 2f, p), 0f);
            Assert.Greater(LightingMath.TargetEv(refLog - 2f, p), 0f);
            Assert.AreEqual(p.MaxEv, LightingMath.TargetEv(-30f, p), 1e-4f);
            Assert.AreEqual(p.MinEv, LightingMath.TargetEv(30f, p), 1e-4f);
        }

        [Test]
        public void TargetEv_PartialStrength_InteriorStaysDarkerThanFullComp()
        {
            var p = LightingMath.ExposureFor(TimeOfDay.Gunduz, MapCatalog.Kuzgun);
            Assert.Less(p.Strength, 1f);
            var refLog = (float)System.Math.Log(p.ReferenceLuma, 2.0);
            // Ortalama referansın 1,5 EV altında: tam telafi +1,5 EV olurdu; kısmi telafi daha az açar (iç mekân koyu kalır).
            var ev = LightingMath.TargetEv(refLog - 1.5f, p);
            Assert.Greater(ev, 0f);
            Assert.Less(ev, 1.5f);
        }

        [Test]
        public void AdaptEv_DarkToBrightFasterThanBrightToDark_NoOvershoot()
        {
            var p = LightingMath.ExposureFor(TimeOfDay.Gunduz, MapCatalog.Kuzgun);
            var toBright = LightingMath.AdaptEv(1f, -1f, 0.1f, p); // EV düşer
            var toDark = LightingMath.AdaptEv(-1f, 1f, 0.1f, p);   // EV yükselir
            Assert.Greater(1f - toBright, toDark - -1f);
            Assert.GreaterOrEqual(toBright, -1f);
            Assert.LessOrEqual(toDark, 1f);
            Assert.AreEqual(0.5f, LightingMath.AdaptEv(0.5f, 0.5f, 0.05f, p), 1e-6f);
        }

        [Test]
        public void AdaptEv_ConvergesAndClampsDt()
        {
            var p = LightingMath.ExposureFor(TimeOfDay.Gece, MapCatalog.KartalYaylasi);
            var ev = 0f;
            for (var i = 0; i < 600; i++)
                ev = LightingMath.AdaptEv(ev, 1.2f, 0.016f, p);
            Assert.AreEqual(1.2f, ev, 0.01f);
            // dt çok büyük: tek kare 0,1 sn ile sınırlı
            var big = LightingMath.AdaptEv(0f, 1f, 10f, p);
            var capped = LightingMath.AdaptEv(0f, 1f, 0.1f, p);
            Assert.AreEqual(capped, big, 1e-6f);
        }

        [Test]
        public void ExposureFor_PerMapClampsDifferAndNightBrightens()
        {
            var day = LightingMath.ExposureFor(TimeOfDay.Gunduz, MapCatalog.Kuzgun);
            var snow = LightingMath.ExposureFor(TimeOfDay.Gunduz, MapCatalog.AyazGecidi);
            var night = LightingMath.ExposureFor(TimeOfDay.Gece, MapCatalog.Kuzgun);
            Assert.Less(snow.MinEv, day.MinEv);
            Assert.Less(night.ReferenceLuma, day.ReferenceLuma);
            Assert.Greater(night.MaxEv, day.MaxEv);
            Assert.Greater(day.SpeedToBright, day.SpeedToDark);
        }

        [Test]
        public void ExposureTier_LowOffMediumSmallGridHighLarge()
        {
            Assert.IsFalse(LightingMath.ExposureForTier(0).Enabled);
            Assert.IsTrue(LightingMath.ExposureForTier(1).Enabled);
            Assert.Less(LightingMath.ExposureForTier(1).GridSize, LightingMath.ExposureForTier(3).GridSize);
            Assert.AreEqual(0, LightingMath.ExposureForTier(3).GridSize % 8);
        }

        [Test]
        public void CenterWeight_CenterHigherThanCorner_Bounded()
        {
            Assert.Greater(LightingMath.CenterWeight(0.5f, 0.5f), LightingMath.CenterWeight(0f, 0f));
            Assert.GreaterOrEqual(LightingMath.CenterWeight(0f, 1f), 0.35f);
            Assert.LessOrEqual(LightingMath.CenterWeight(0.5f, 0.5f), 1f + 1e-5f);
        }

        [Test]
        public void EvToMultiplier_PowerOfTwo()
        {
            Assert.AreEqual(1f, LightingMath.EvToMultiplier(0f), 1e-6f);
            Assert.AreEqual(2f, LightingMath.EvToMultiplier(1f), 1e-5f);
            Assert.AreEqual(0.25f, LightingMath.EvToMultiplier(-2f), 1e-5f);
        }
    }
}
