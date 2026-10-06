using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class FlowModesTests
    {
        [Test]
        public void SeaFog_OnlyOnMaviLiman_DenserThanOtherMaps()
        {
            Assert.AreEqual(1f, AtmosphereRules.MapFogMultiplier(MapCatalog.Kuzgun, TimeOfDay.Safak));
            Assert.AreEqual(1f, AtmosphereRules.MapFogMultiplier(MapCatalog.AyazGecidi, TimeOfDay.Gunduz));
            foreach (TimeOfDay t in System.Enum.GetValues(typeof(TimeOfDay)))
                Assert.Greater(AtmosphereRules.MapFogMultiplier("Mavi Liman", t), 1f);
            Assert.Greater(AtmosphereRules.MapFogMultiplier(MapCatalog.MaviLiman, TimeOfDay.Safak), AtmosphereRules.MapFogMultiplier(MapCatalog.MaviLiman, TimeOfDay.Gunduz));
            Assert.IsTrue(AtmosphereRules.HasSeaFog("liman"));
            Assert.IsFalse(AtmosphereRules.HasSeaFog("ayaz"));
        }

#if UNITY_EDITOR
        [Test]
        public void MapIdForScene_RoundTripsWithOperationSceneFor()
        {
            for (var i = 0; i < MapCatalog.Count; i++)
            {
                var id = MapCatalog.IdAt(i);
                var scene = Project.Presentation.Bootstrap.SceneNames.OperationSceneFor(id);
                Assert.AreEqual(id, Project.Presentation.Bootstrap.SceneNames.MapIdForScene(scene));
                Assert.IsTrue(System.Linq.Enumerable.Contains(Project.Presentation.Bootstrap.SceneNames.BuildOrder, scene));
            }

            Assert.IsNull(Project.Presentation.Bootstrap.SceneNames.MapIdForScene(Project.Presentation.Bootstrap.SceneNames.Training));
            Assert.IsNull(Project.Presentation.Bootstrap.SceneNames.MapIdForScene(Project.Presentation.Bootstrap.SceneNames.MainMenu));
        }

        [Test]
        public void SkirmishUsesKuzgunScene()
        {
            Assert.AreEqual(Project.Presentation.Bootstrap.SceneNames.Operation, Project.Presentation.Bootstrap.SceneNames.Skirmish);
        }

        [Test]
        public void AlignConfigToMap_FixesMismatchedMapAndKeepsMatching()
        {
            var c = new MatchConfig { MapName = MapCatalog.AyazGecidiName, MapHalfSize = 500f, Weather = WeatherKind.Kar };
            Project.Presentation.Bootstrap.GameSession.AlignConfigToMap(c, MapCatalog.Kuzgun);
            Assert.AreEqual(MapCatalog.KuzgunName, c.MapName);
            Assert.AreEqual(512f, c.MapHalfSize);
            Assert.AreEqual(WeatherKind.Acik, c.Weather);

            var same = new MatchConfig { MapName = MapCatalog.AyazGecidiName, MapHalfSize = 500f, Weather = WeatherKind.Kar };
            Project.Presentation.Bootstrap.GameSession.AlignConfigToMap(same, MapCatalog.AyazGecidi);
            Assert.AreEqual(WeatherKind.Kar, same.Weather);

            Project.Presentation.Bootstrap.GameSession.AlignConfigToMap(same, null);
            Assert.AreEqual(MapCatalog.AyazGecidiName, same.MapName);
        }
#endif
    }
}
