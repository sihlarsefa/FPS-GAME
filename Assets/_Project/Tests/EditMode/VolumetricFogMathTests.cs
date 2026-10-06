using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class VolumetricFogMathTests
    {
        [Test]
        public void Phase_PeaksForward_WhenAnisotropyPositive()
        {
            Assert.Greater(VolumetricFogMath.HenyeyGreenstein(1f, 0.7f), VolumetricFogMath.HenyeyGreenstein(0f, 0.7f));
            Assert.Greater(VolumetricFogMath.HenyeyGreenstein(0f, 0.7f), VolumetricFogMath.HenyeyGreenstein(-1f, 0.7f));
            Assert.AreEqual(1f, VolumetricFogMath.HenyeyGreenstein(0.3f, 0f), 1e-4f);
        }

        [Test]
        public void HeightDensity_DecaysWithAltitude()
        {
            Assert.AreEqual(0.01f, VolumetricFogMath.HeightDensity(0.01f, 0f, 0.05f, -5f), 1e-6f);
            Assert.Less(VolumetricFogMath.HeightDensity(0.01f, 0f, 0.05f, 50f), VolumetricFogMath.HeightDensity(0.01f, 0f, 0.05f, 10f));
        }

        [Test]
        public void OpticalDepth_MatchesNumericIntegral()
        {
            // 100 m, yükseklik 0 -> 40
            float analytic = VolumetricFogMath.OpticalDepth(0.01f, 0f, 0.05f, 0f, 40f, 100f);
            float sum = 0f;
            const int n = 2000;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                sum += VolumetricFogMath.HeightDensity(0.01f, 0f, 0.05f, 40f * t) * (100f / n);
            }
            Assert.AreEqual(sum, analytic, 0.01f * sum);
            Assert.AreEqual(0f, VolumetricFogMath.OpticalDepth(0.01f, 0f, 0.05f, 0f, 10f, 0f));
        }

        [Test]
        public void Transmittance_IsBeerLambert()
        {
            Assert.AreEqual(1f, VolumetricFogMath.Transmittance(0f), 1e-6f);
            Assert.AreEqual(0.3678794f, VolumetricFogMath.Transmittance(1f), 1e-5f);
            Assert.AreEqual(1f, VolumetricFogMath.Transmittance(-3f), 1e-6f);
        }

        [Test]
        public void SliceDistance_MonotonicAndBounded()
        {
            Assert.AreEqual(0f, VolumetricFogMath.SliceDistance(0, 16, 200f), 1e-5f);
            Assert.AreEqual(200f, VolumetricFogMath.SliceDistance(16, 16, 200f), 1e-3f);
            float prev = -1f;
            for (int i = 0; i <= 16; i++)
            {
                float d = VolumetricFogMath.SliceDistance(i, 16, 200f);
                Assert.Greater(d, prev);
                prev = d;
            }
            // yakın dilimler daha sık
            Assert.Less(VolumetricFogMath.SliceDistance(1, 16, 200f) - VolumetricFogMath.SliceDistance(0, 16, 200f),
                        VolumetricFogMath.SliceDistance(16, 16, 200f) - VolumetricFogMath.SliceDistance(15, 16, 200f));
        }

        [Test]
        public void Tiers_DusukOff_CostGrowsWithTier()
        {
            Assert.IsFalse(VolumetricFogMath.ForTier(0).Enabled);
            Assert.IsFalse(VolumetricFogMath.ForTier(3, false).Enabled);
            var orta = VolumetricFogMath.ForTier(1);
            var yuksek = VolumetricFogMath.ForTier(2);
            var ultra = VolumetricFogMath.ForTier(3);
            Assert.IsTrue(orta.Enabled);
            Assert.AreEqual(4, orta.ResolutionDivisor);
            float baseline = (1920 / 4) * (1080 / 4) * 12f;
            Assert.AreEqual(1f, orta.RelativeCost(1920, 1080, baseline), 0.01f);
            Assert.Greater(yuksek.RelativeCost(1920, 1080, baseline), orta.RelativeCost(1920, 1080, baseline));
            Assert.Greater(ultra.RelativeCost(1920, 1080, baseline), yuksek.RelativeCost(1920, 1080, baseline));
            Assert.AreEqual(0f, VolumetricFogMath.ForTier(0).RelativeCost(1920, 1080, baseline));
        }

        [Test]
        public void LowRes_RoundsUp()
        {
            Assert.AreEqual(480, VolumetricFogMath.LowRes(1920, 4));
            Assert.AreEqual(270, VolumetricFogMath.LowRes(1080, 4));
            Assert.AreEqual(3, VolumetricFogMath.LowRes(9, 4));
            Assert.AreEqual(1, VolumetricFogMath.LowRes(0, 4));
        }

        [Test]
        public void HistoryWeight_InvalidIsZero_MotionReduces()
        {
            Assert.AreEqual(0f, VolumetricFogMath.HistoryWeight(false, 0f));
            Assert.Greater(VolumetricFogMath.HistoryWeight(true, 0f), VolumetricFogMath.HistoryWeight(true, 30f));
            Assert.Greater(VolumetricFogMath.HistoryWeight(true, 100f), 0.2f);
            Assert.IsTrue(VolumetricFogMath.HistoryUsable(480, 270, 480, 270, 1, 0.5f));
            Assert.IsFalse(VolumetricFogMath.HistoryUsable(480, 270, 960, 540, 1, 0.5f));
            Assert.IsFalse(VolumetricFogMath.HistoryUsable(480, 270, 480, 270, 1, 500f));
            Assert.IsFalse(VolumetricFogMath.HistoryUsable(480, 270, 480, 270, 30, 0.5f));
        }

        [Test]
        public void Bilateral_FavoursSimilarDepth()
        {
            Assert.Greater(VolumetricFogMath.BilateralWeight(50f, 50.5f), VolumetricFogMath.BilateralWeight(50f, 200f));
            Assert.AreEqual(1f, VolumetricFogMath.BilateralWeight(10f, 10f), 1e-6f);
        }

        [Test]
        public void Halton_InUnitRange_AndDistinct()
        {
            Assert.AreEqual(0.5f, VolumetricFogMath.Halton(1, 2), 1e-6f);
            Assert.AreEqual(0.25f, VolumetricFogMath.Halton(2, 2), 1e-6f);
            Assert.AreEqual(1f / 3f, VolumetricFogMath.Halton(1, 3), 1e-6f);
            for (int i = 0; i < 64; i++)
            {
                float h = VolumetricFogMath.TemporalNoiseOffset(i);
                Assert.GreaterOrEqual(h, 0f);
                Assert.Less(h, 1f);
            }
        }

        [Test]
        public void BlueNoise_UniformAndMoreAntiCorrelatedThanWhite()
        {
            const int size = 32;
            var blue = VolumetricFogMath.GenerateBlueNoise(size, 1234);
            var again = VolumetricFogMath.GenerateBlueNoise(size, 1234);
            float sum = 0f, minV = 1f, maxV = 0f;
            for (int i = 0; i < blue.Length; i++)
            {
                Assert.AreEqual(blue[i], again[i]);
                sum += blue[i];
                if (blue[i] < minV) minV = blue[i];
                if (blue[i] > maxV) maxV = blue[i];
            }
            Assert.AreEqual(0.5f, sum / blue.Length, 0.01f);
            Assert.Less(minV, 0.01f);
            Assert.Greater(maxV, 0.99f);

            // komşu piksel farkı: beyaz gürültüde ortalama |a-b| ≈ 1/3, mavide belirgin daha büyük
            float diff = 0f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    diff += System.Math.Abs(blue[y * size + x] - blue[y * size + (x + 1) % size]);
            diff /= blue.Length;
            Assert.Greater(diff, 0.36f);
        }

        [Test]
        public void Presets_FogierThanClear_AndNightDarker()
        {
            var clear = VolumetricFogPresets.Get(TimeOfDay.Gunduz, VolumetricWeather.Acik);
            var fog = VolumetricFogPresets.Get(TimeOfDay.Gunduz, VolumetricWeather.Sisli);
            var rain = VolumetricFogPresets.Get(TimeOfDay.Gunduz, VolumetricWeather.Yagmurlu);
            var night = VolumetricFogPresets.Get(TimeOfDay.Gece, VolumetricWeather.Acik);
            var dawn = VolumetricFogPresets.Get(TimeOfDay.Safak, VolumetricWeather.Acik);
            Assert.Greater(fog.Density, rain.Density);
            Assert.Greater(rain.Density, clear.Density);
            Assert.Less(night.SunIntensity, clear.SunIntensity);
            Assert.Greater(dawn.Density, clear.Density);
            Assert.Less(fog.SunIntensity, clear.SunIntensity);
            Assert.AreEqual(VolumetricWeather.Yagmurlu, VolumetricFogPresets.FromWeather(WeatherKind.Yagmur));
            Assert.AreEqual(VolumetricWeather.Sisli, VolumetricFogPresets.FromWeather(WeatherKind.Kar));
        }

        [Test]
        public void Params_LerpAndSanitize()
        {
            var a = VolumetricFogPresets.Get(TimeOfDay.Gunduz, VolumetricWeather.Acik);
            var b = VolumetricFogPresets.Get(TimeOfDay.Gunduz, VolumetricWeather.Sisli);
            var mid = VolumetricFogParams.Lerp(a, b, 0.5f);
            Assert.AreEqual((a.Density + b.Density) * 0.5f, mid.Density, 1e-6f);
            Assert.AreEqual(a.Density, VolumetricFogParams.Lerp(a, b, -3f).Density, 1e-6f);
            Assert.AreEqual(b.Density, VolumetricFogParams.Lerp(a, b, 9f).Density, 1e-6f);

            var bad = new VolumetricFogParams { Density = float.NaN, Anisotropy = 5f, MaxDistance = -4f, SunIntensity = float.PositiveInfinity }.Sanitized();
            Assert.LessOrEqual(bad.Anisotropy, 0.95f);
            Assert.GreaterOrEqual(bad.MaxDistance, 10f);
            Assert.LessOrEqual(bad.SunIntensity, 8f);
            Assert.GreaterOrEqual(bad.Density, 0f);
        }
    }
}
