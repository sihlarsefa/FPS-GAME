using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class KartalYaylasiTests
    {
        [Test]
        public void Catalog_ContainsKartalYaylasi()
        {
            Assert.AreEqual(MapCatalog.KartalYaylasi, MapCatalog.Normalize("Kartal Yaylası"));
            Assert.AreEqual(MapCatalog.KartalYaylasi, MapCatalog.Normalize("KARTAL"));
            Assert.AreEqual("Kartal Yaylası", MapCatalog.DisplayName("kartal"));
            Assert.AreEqual(500f, MapCatalog.HalfSize("kartal"));
            Assert.AreEqual(MapCatalog.Count, MapCatalog.DisplayNames().Length);
        }

        [Test]
        public void Grade_IsWarmGoldenGreen()
        {
            var g = MapGradeTable.For(MapCatalog.KartalYaylasi);
            var k = MapGradeTable.For(MapCatalog.Kuzgun);
            Assert.Greater(g.Temperature, 0f);
            Assert.Greater(g.Saturation, k.Saturation);
            Assert.Less(g.FogMul, 1f);
            Assert.AreNotEqual(MapGradeTable.For(MapCatalog.MaviLiman).Saturation, g.Saturation);
        }

#if UNITY_EDITOR
        [Test]
        public void SceneNames_RouteKartalYaylasi()
        {
            var s = Project.Presentation.Bootstrap.SceneNames.KartalYaylasi;
            Assert.AreEqual(s, Project.Presentation.Bootstrap.SceneNames.OperationSceneFor("kartal"));
            Assert.AreEqual(MapCatalog.KartalYaylasi, Project.Presentation.Bootstrap.SceneNames.MapIdForScene(s));
            Assert.IsTrue(Project.Presentation.Bootstrap.SceneNames.IsKnown(s));
            Assert.IsTrue(System.Linq.Enumerable.Contains(Project.Presentation.Bootstrap.SceneNames.BuildOrder, s));
        }

        [Test]
        public void Layout_HasTenLocationsAndRoads_NoWaterNoSnow()
        {
            var layout = Project.Infrastructure.World.MapLayout.Create("kartal", 1);
            Assert.AreEqual("Kartal Yaylası", layout.Name);
            Assert.AreEqual(16, layout.Locations.Count); // 10 temel + 6 kimlik POI
            Assert.GreaterOrEqual(layout.Roads.Count, 9); // + kimlik POI çıkma yolları
            Assert.AreEqual(0, layout.Lakes.Count);
            Assert.Greater(layout.SnowLine, layout.MaxHeight);
            foreach (var l in layout.Locations)
                Assert.IsTrue(System.Math.Abs(l.Center.x) < 500f && System.Math.Abs(l.Center.y) < 500f, l.Name);
        }

        [Test]
        public void TurbinePositions_AreCountedAndSpread()
        {
            var c = new UnityEngine.Vector2(10f, 20f);
            var list = Project.Infrastructure.World.KartalYaylasiProps.TurbinePositions(c, 7, 100f, 0f);
            Assert.AreEqual(7, list.Count);
            foreach (var p in list)
                Assert.IsTrue((p - c).magnitude >= 50f && (p - c).magnitude <= 101f);
            Assert.AreEqual(0, Project.Infrastructure.World.KartalYaylasiProps.TurbinePositions(c, 0, 100f, 0f).Count);
        }

        [Test]
        public void OutwardDirection_UnitAndSafeAtOrigin()
        {
            Assert.AreEqual(1f, Project.Infrastructure.World.KartalYaylasiProps.OutwardDirection(new UnityEngine.Vector2(-300f, -400f)).magnitude, 1e-4f);
            Assert.AreEqual(UnityEngine.Vector2.right, Project.Infrastructure.World.KartalYaylasiProps.OutwardDirection(UnityEngine.Vector2.zero));
        }
#endif
    }
}
