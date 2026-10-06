using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class AtmosphereRulesTests
    {
        [Test]
        public void AyazGecidi_DefaultsToSnow()
        {
            Assert.AreEqual(WeatherKind.Kar, AtmosphereRules.DefaultWeatherForMap(MapCatalog.AyazGecidi));
            Assert.AreEqual(WeatherKind.Acik, AtmosphereRules.DefaultWeatherForMap(MapCatalog.Kuzgun));
        }

        [Test]
        public void Night_HasDenserFogAndStrongerFlash()
        {
            Assert.Greater(AtmosphereRules.FogMultiplier(TimeOfDay.Gece, WeatherKind.Acik), AtmosphereRules.FogMultiplier(TimeOfDay.Gunduz, WeatherKind.Acik));
            Assert.Greater(AtmosphereRules.MuzzleFlashBoost(TimeOfDay.Gece), 1.5f);
            Assert.AreEqual(1f, AtmosphereRules.MuzzleFlashBoost(TimeOfDay.Gunduz));
        }

        [Test]
        public void Indexing_IsClamped()
        {
            Assert.AreEqual(TimeOfDay.Gece, AtmosphereRules.TimeFromIndex(99));
            Assert.AreEqual(WeatherKind.Acik, AtmosphereRules.WeatherFromIndex(-1));
            Assert.AreEqual("Şafak", AtmosphereRules.Name(TimeOfDay.Safak));
        }

        [Test]
        public void MatchConfig_DefaultsDay()
        {
            var c = new MatchConfig();
            Assert.AreEqual(TimeOfDay.Gunduz, c.TimeOfDay);
            Assert.AreEqual(WeatherKind.Acik, c.Weather);
        }
    }
}
