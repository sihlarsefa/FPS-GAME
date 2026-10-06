using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class MaviLimanTests
    {
        [Test]
        public void Catalog_ContainsMaviLiman()
        {
            Assert.AreEqual(MapCatalog.MaviLiman, MapCatalog.Normalize("Mavi Liman"));
            Assert.AreEqual(MapCatalog.MaviLiman, MapCatalog.Normalize("LIMAN"));
            Assert.AreEqual("Mavi Liman", MapCatalog.DisplayName("liman"));
            Assert.AreEqual(500f, MapCatalog.HalfSize("liman"));
            Assert.AreEqual(4, MapCatalog.Count);
        }

#if UNITY_EDITOR
        [Test]
        public void SceneNames_RouteMaviLiman()
        {
            Assert.AreEqual(Project.Presentation.Bootstrap.SceneNames.MaviLiman, Project.Presentation.Bootstrap.SceneNames.OperationSceneFor("liman"));
            Assert.IsTrue(Project.Presentation.Bootstrap.SceneNames.IsKnown(Project.Presentation.Bootstrap.SceneNames.MaviLiman));
            Assert.IsTrue(System.Linq.Enumerable.Contains(Project.Presentation.Bootstrap.SceneNames.BuildOrder, Project.Presentation.Bootstrap.SceneNames.MaviLiman));
        }

        [Test]
        public void Layout_HasTenLocationsSeaAndBeach()
        {
            var layout = Project.Infrastructure.World.MapLayout.Create("liman", 1);
            Assert.AreEqual("Mavi Liman", layout.Name);
            Assert.AreEqual(13, layout.Locations.Count); // 10 temel + 3 kimlik POI
            Assert.AreEqual(2, layout.Lakes.Count);
            Assert.AreEqual(1, layout.Rivers.Count);
            Assert.IsTrue(layout.SeaPlane);
            Assert.Greater(layout.BeachHeight, 0f);
            Assert.GreaterOrEqual(layout.Roads.Count, 7); // + kimlik POI çıkma yolları
        }

        [Test]
        public void BeachWeight_OnlyNearWaterline()
        {
            Assert.Greater(Project.Infrastructure.World.TerrainPainter.BeachWeight(18.5f, 18f, 3.2f, 3f), 0.9f);
            Assert.AreEqual(0f, Project.Infrastructure.World.TerrainPainter.BeachWeight(30f, 18f, 3.2f, 3f), 1e-4f);
            Assert.AreEqual(0f, Project.Infrastructure.World.TerrainPainter.BeachWeight(18.5f, 18f, 3.2f, 60f), 1e-4f);
            Assert.AreEqual(0f, Project.Infrastructure.World.TerrainPainter.BeachWeight(18.5f, 18f, 0f, 3f), 1e-4f);
        }

        [Test]
        public void FindShoreDistance_FindsWaterEdge()
        {
            var d = Project.Infrastructure.World.MaviLimanProps.FindShoreDistance((x, z) => 30f - x * 0.1f,
                new UnityEngine.Vector2(0f, 0f), UnityEngine.Vector2.right, 18f, 400f);
            Assert.That(d, Is.InRange(115f, 125f));
            Assert.Less(Project.Infrastructure.World.MaviLimanProps.FindShoreDistance((x, z) => 50f,
                UnityEngine.Vector2.zero, UnityEngine.Vector2.up, 18f, 100f), 0f);
        }
#endif
    }
}
