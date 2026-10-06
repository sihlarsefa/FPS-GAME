using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class MapIdentityTests
    {
        private static MapLayout[] Maps() => new[]
        {
            MapLayout.Create("ayaz", 1), MapLayout.Create("liman", 1), MapLayout.Create("kartal", 1)
        };

        [Test]
        public void IdentityPois_InsideMap_NoOverlap_WithinLayout()
        {
            foreach (var layout in Maps())
            {
                for (var i = 0; i < layout.Locations.Count; i++)
                {
                    var a = layout.Locations[i];
                    Assert.Less(Mathf.Abs(a.Center.x) + a.Radius, 500f, layout.Name + "/" + a.Name);
                    Assert.Less(Mathf.Abs(a.Center.y) + a.Radius, 500f, layout.Name + "/" + a.Name);
                    for (var j = i + 1; j < layout.Locations.Count; j++)
                    {
                        var b = layout.Locations[j];
                        if (!MapLayout.IsIdentityPoi(a.Name) && !MapLayout.IsIdentityPoi(b.Name))
                            continue;
                        var min = (a.Radius + b.Radius) * 0.6f;
                        Assert.Greater(Vector2.Distance(a.Center, b.Center), min, layout.Name + ": " + a.Name + " / " + b.Name);
                    }
                }
            }
        }

        [Test]
        public void IdentityPois_AreCountedPerMap_AndKuzgunUntouched()
        {
            Assert.AreEqual(14, MapLayout.Create("ayaz", 1).Locations.Count);
            var kuzgun = MapLayout.Create("kuzgun", 1);
            foreach (var l in kuzgun.Locations)
                Assert.IsFalse(MapLayout.IsIdentityPoi(l.Name), l.Name);
        }

        [Test]
        public void Layouts_AreDeterministic()
        {
            var a = MapLayout.Create("liman", 5);
            var b = MapLayout.Create("liman", 5);
            Assert.AreEqual(a.Roads.Count, b.Roads.Count);
            for (var i = 0; i < a.Roads.Count; i++)
                Assert.AreEqual(a.Roads[i].Points.Count, b.Roads[i].Points.Count, a.Roads[i].Name);
        }

        [Test]
        public void Spurs_ConnectNewPois()
        {
            var layout = MapLayout.Create("kartal", 1);
            Assert.IsTrue(layout.Roads.Exists(r => r.Name == MapLayout.KartalSenlikName + " Yolu"));
        }

        [Test]
        public void PlanSpur_NullWhenTooFarOrOnRoad()
        {
            var roads = new System.Collections.Generic.List<RoadSpec>
            {
                new RoadSpec { Points = new System.Collections.Generic.List<Vector2> { Vector2.zero, new Vector2(10f, 0f) } }
            };
            Assert.IsNull(MapLayout.PlanSpur(roads, 1, new Vector2(500f, 0f), 260f));
            Assert.IsNull(MapLayout.PlanSpur(roads, 1, new Vector2(10f, 3f), 260f));
            var ok = MapLayout.PlanSpur(roads, 1, new Vector2(10f, 80f), 260f);
            Assert.AreEqual(3, ok.Length);
        }

        [Test]
        public void ContainerMaze_DeterministicWithCorridors()
        {
            var a = MapIdentityProps.ContainerMaze(6, 9, 3, out var rc, out var rr);
            var b = MapIdentityProps.ContainerMaze(6, 9, 3, out var rc2, out var rr2);
            Assert.AreEqual(rc, rc2);
            Assert.AreEqual(rr, rr2);
            for (var c = 0; c < 6; c++)
                for (var r = 0; r < 9; r++)
                {
                    Assert.AreEqual(a[c, r], b[c, r]);
                    if (r % 3 == 2 || c % 3 == 2)
                        Assert.AreEqual(0, a[c, r]);
                    Assert.LessOrEqual(a[c, r], 3);
                }

            Assert.AreEqual(1, a[rc, rr]);
            Assert.Greater(MapIdentityProps.OpenRatio(a), 0.3f);
        }

        [Test]
        public void LinePoints_IncludeEndsAndRespectSpacing()
        {
            var pts = MapIdentityProps.LinePoints(Vector2.zero, new Vector2(100f, 0f), 38f);
            Assert.AreEqual(4, pts.Count);
            Assert.AreEqual(0f, pts[0].x, 1e-4f);
            Assert.AreEqual(100f, pts[pts.Count - 1].x, 1e-4f);
            Assert.AreEqual(2, MapIdentityProps.LinePoints(Vector2.zero, Vector2.zero, 10f).Count);
        }

        [Test]
        public void FindLandDistance_FindsShoreRise()
        {
            var d = MapIdentityProps.FindLandDistance((x, z) => 12f + x * 0.1f, Vector2.zero, Vector2.right, 15f, 200f);
            Assert.That(d, Is.InRange(30f, 34f));
            Assert.Less(MapIdentityProps.FindLandDistance((x, z) => 10f, Vector2.zero, Vector2.up, 15f, 100f), 0f);
        }

        [Test]
        public void BlizzardZone_OnlyAyaz()
        {
            Assert.IsTrue(MapIdentityProps.TryGetBlizzardZone(MapLayout.Create("ayaz", 1), out var c, out var r));
            Assert.Greater(r, 50f);
            Assert.IsFalse(MapIdentityProps.TryGetBlizzardZone(MapLayout.Create("kartal", 1), out _, out _));
        }

        [Test]
        public void StableHash_SameForSameName()
        {
            Assert.AreEqual(MapIdentityProps.StableHash("Balık Hali"), MapIdentityProps.StableHash("Balık Hali"));
            Assert.AreNotEqual(MapIdentityProps.StableHash("A"), MapIdentityProps.StableHash("B"));
        }
    }
}
