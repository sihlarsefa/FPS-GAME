using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Audio.Ambience;
using Project.Infrastructure.World;

namespace Project.Tests
{
    public sealed class AmbienceLinksTests
    {
        [Test]
        public void BaseZone_RadiusAndHysteresis()
        {
            Assert.IsTrue(BaseZoneMath.Inside(40f, 0f, 30f, false));
            Assert.IsFalse(BaseZoneMath.Inside(60f, 0f, 30f, false));
            Assert.IsTrue(BaseZoneMath.Inside(60f, 0f, 30f, true));
            Assert.IsTrue(BaseZoneMath.IsBase(LocationKind.ForwardBase));
            Assert.IsFalse(BaseZoneMath.IsBase(LocationKind.Village));
        }

        [Test]
        public void LiveRain_EnablesRainBedInClearWeather()
        {
            var c = new AmbienceContext { Biome = AmbienceBiome.DagCam, Time = TimeOfDay.Gunduz, Weather = WeatherKind.Acik, RainLevel = 0.8f };
            Assert.Greater(AmbienceRules.Build(c).Loop(AmbienceLoop.RainOutdoor), 0.3f);
            c.RainLevel = 0f;
            Assert.AreEqual(0f, AmbienceRules.Build(c).Loop(AmbienceLoop.RainOutdoor));
        }

        [Test]
        public void LiveWind_ScalesGust()
        {
            var c = new AmbienceContext { Biome = AmbienceBiome.Yayla, Time = TimeOfDay.Gunduz, Weather = WeatherKind.Acik, WindLevel = 0.2f };
            var calm = AmbienceRules.Build(c).Loop(AmbienceLoop.WindGust);
            c.WindLevel = 1f;
            Assert.Greater(AmbienceRules.Build(c).Loop(AmbienceLoop.WindStrong), 0f);
            Assert.Greater(calm, 0f);
        }

        [Test]
        public void DayNight_BirdsVsOwlCrickets()
        {
            var day = AmbienceRules.Build(new AmbienceContext { Biome = AmbienceBiome.DagCam, Time = TimeOfDay.Gunduz });
            var night = AmbienceRules.Build(new AmbienceContext { Biome = AmbienceBiome.DagCam, Time = TimeOfDay.Gece });
            Assert.Greater(day.Rate(AmbienceOneShot.Bird), 0f);
            Assert.AreEqual(0f, night.Rate(AmbienceOneShot.Bird));
            Assert.Greater(night.Rate(AmbienceOneShot.Owl), 0f);
            Assert.Greater(night.Loop(AmbienceLoop.Insects), day.Loop(AmbienceLoop.Insects));
        }
    }
}
