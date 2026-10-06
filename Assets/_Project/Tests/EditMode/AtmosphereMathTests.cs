using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class AtmosphereMathTests
    {
        [Test]
        public void HeightFog_DecreasesWithAltitude()
        {
            Assert.AreEqual(1f, AtmosphereMath.HeightFogFactor(0f, 0f, 0.01f));
            Assert.Less(AtmosphereMath.HeightFogFactor(100f, 0f, 0.01f), AtmosphereMath.HeightFogFactor(10f, 0f, 0.01f));
            Assert.GreaterOrEqual(AtmosphereMath.HeightFogFactor(5000f, 0f, 0.01f), 0.25f);
        }

        [Test]
        public void AerialBlend_MonotonicAndBounded()
        {
            Assert.AreEqual(0f, AtmosphereMath.AerialBlend(0f, 0.002f));
            var near = AtmosphereMath.AerialBlend(50f, 0.0024f);
            var far = AtmosphereMath.AerialBlend(400f, 0.0024f);
            Assert.Greater(far, near);
            Assert.Less(far, 1f);
            Assert.Greater(far, 0.3f);
        }

        [Test]
        public void MiePhase_PeaksTowardsSun()
        {
            Assert.Greater(AtmosphereMath.MiePhase(1f, 0.76f), AtmosphereMath.MiePhase(0f, 0.76f));
        }

        [Test]
        public void SkyId_MapsTimeAndWeather()
        {
            Assert.AreEqual("day_clear", AtmosphereMath.SkyId(TimeOfDay.Gunduz, WeatherKind.Acik));
            Assert.AreEqual("cloudy", AtmosphereMath.SkyId(TimeOfDay.Gunduz, WeatherKind.Yagmur));
            Assert.AreEqual("sunset", AtmosphereMath.SkyId(TimeOfDay.Aksam, WeatherKind.Acik));
        }

        [Test]
        public void Volumetric_OnlyHighAndUltra()
        {
            Assert.IsFalse(AtmosphereMath.VolumetricAllowed(0, true));
            Assert.IsTrue(AtmosphereMath.VolumetricAllowed(1, true));
            Assert.IsTrue(AtmosphereMath.VolumetricAllowed(3, true));
            Assert.IsFalse(AtmosphereMath.VolumetricAllowed(3, false));
        }

        [Test]
        public void MieHaloGain_StrongerAtDusk_NoneAtNight()
        {
            Assert.Greater(AtmosphereMath.MieHaloGain(TimeOfDay.Aksam, WeatherKind.Acik), AtmosphereMath.MieHaloGain(TimeOfDay.Gunduz, WeatherKind.Acik));
            Assert.AreEqual(1f, AtmosphereMath.MieHaloGain(TimeOfDay.Gece, WeatherKind.Acik));
        }

        [Test]
        public void AerialTint_PerTimeOfDay()
        {
            var dawn = AtmosphereMath.AerialTint(TimeOfDay.Safak);
            var dusk = AtmosphereMath.AerialTint(TimeOfDay.Aksam);
            var night = AtmosphereMath.AerialTint(TimeOfDay.Gece);
            var noon = AtmosphereMath.AerialTint(TimeOfDay.Gunduz);
            Assert.Greater(dusk[0], dusk[2]);
            Assert.Greater(noon[2], noon[0]);
            Assert.Greater(night[2], night[0]);
            Assert.Greater(dawn[0], dawn[1]);
            Assert.Less(night[0], dawn[0]);
        }

        [Test]
        public void ApplyAerialTint_MovesTowardTint_Bounded()
        {
            var b = new[] { 0.5f, 0.5f, 0.5f };
            var o = AtmosphereMath.ApplyAerialTint(b, TimeOfDay.Aksam, WeatherKind.Acik);
            Assert.Greater(o[0], 0.5f);
            Assert.Less(o[2], 0.5f);
            Assert.Less(AtmosphereMath.AerialTintStrength(TimeOfDay.Safak, WeatherKind.Yagmur), AtmosphereMath.AerialTintStrength(TimeOfDay.Safak, WeatherKind.Acik));
        }

        [Test]
        public void ValleyFog_DenserLowAndAtDawn()
        {
            Assert.AreEqual(1f, AtmosphereMath.ValleyDepth01(2f), 1e-4f);
            Assert.AreEqual(0f, AtmosphereMath.ValleyDepth01(60f), 1e-4f);
            var lowDawn = AtmosphereMath.ValleyFogMultiplier(2f, TimeOfDay.Safak, WeatherKind.Acik);
            Assert.Greater(lowDawn, AtmosphereMath.ValleyFogMultiplier(2f, TimeOfDay.Gunduz, WeatherKind.Acik));
            Assert.Greater(lowDawn, AtmosphereMath.ValleyFogMultiplier(30f, TimeOfDay.Safak, WeatherKind.Acik));
            Assert.AreEqual(1f, AtmosphereMath.ValleyFogMultiplier(100f, TimeOfDay.Safak, WeatherKind.Acik), 1e-4f);
        }

        [Test]
        public void RidgeHaze_StartsAt600m_Capped()
        {
            Assert.AreEqual(1f, AtmosphereMath.RidgeHazeMultiplier(599f), 1e-4f);
            Assert.Greater(AtmosphereMath.RidgeHazeMultiplier(750f), 1f);
            Assert.AreEqual(1.4f, AtmosphereMath.RidgeHazeMultiplier(5000f), 1e-4f);
        }
    }
}
