using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class RoadsidePlanTests
    {
        private static List<Vector3> Line(float length)
        {
            return new List<Vector3> { new Vector3(0f, 10f, 0f), new Vector3(0f, 10f, length) };
        }

        [Test]
        public void Smooth_KeepsEndpointsAndWaypoints()
        {
            var pts = new List<Vector2> { new Vector2(0, 0), new Vector2(50, 20), new Vector2(100, 0), new Vector2(150, 30) };
            var s = RoadsidePlan.Smooth(pts, 4f);
            Assert.AreEqual(pts[0], s[0]);
            Assert.AreEqual(pts[3], s[s.Count - 1]);
            foreach (var w in pts)
            {
                var best = float.MaxValue;
                foreach (var p in s) best = Mathf.Min(best, Vector2.Distance(p, w));
                Assert.Less(best, 0.01f);
            }
        }

        [Test]
        public void Smooth_IsDeterministicAndFinite()
        {
            var pts = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(11, 90), new Vector2(200, 100) };
            var a = RoadsidePlan.Smooth(pts, 4f);
            var b = RoadsidePlan.Smooth(pts, 4f);
            Assert.AreEqual(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i], b[i]);
                Assert.IsFalse(float.IsNaN(a[i].x) || float.IsNaN(a[i].y));
            }
        }

        [Test]
        public void Smooth_RoundsCorner()
        {
            var pts = new List<Vector2> { new Vector2(0, 0), new Vector2(100, 0), new Vector2(180, 80) };
            var s = RoadsidePlan.Smooth(pts, 4f);
            for (var i = 2; i < s.Count; i++)
            {
                var d1 = (s[i - 1] - s[i - 2]).normalized;
                var d2 = (s[i] - s[i - 1]).normalized;
                Assert.Less(Vector2.Angle(d1, d2), 15f);
            }
        }

        [Test]
        public void Resample_SpacingAndLength()
        {
            var s = RoadsidePlan.Resample(Line(100f), 4f);
            Assert.AreEqual(26, s.Count);
            Assert.AreEqual(0f, s[0].S, 1e-3f);
            Assert.AreEqual(100f, s[s.Count - 1].S, 1e-2f);
            Assert.AreEqual(1f, s[3].Dir.y, 1e-3f);
        }

        [Test]
        public void Poles_RespectBudgetAndBlocked()
        {
            var s = RoadsidePlan.Resample(Line(2000f), 4f);
            var poles = RoadsidePlan.PlanPoles(s, 7f, 1f, 10, null);
            Assert.LessOrEqual(poles.Count, 10);
            Assert.Greater(poles.Count, 5);
            var none = RoadsidePlan.PlanPoles(s, 7f, 1f, 50, (x, z) => true);
            Assert.AreEqual(0, none.Count);
        }

        [Test]
        public void Poles_SitBesideRoad()
        {
            var s = RoadsidePlan.Resample(Line(300f), 4f);
            var poles = RoadsidePlan.PlanPoles(s, 7f, 1f, 50, null);
            Assert.Greater(poles.Count, 3);
            foreach (var p in poles)
                Assert.AreEqual(7f * 0.5f + 2.6f, Mathf.Abs(p.Pos.x), 1e-3f);
        }

        [Test]
        public void KmStones_EveryHalfKm()
        {
            var s = RoadsidePlan.Resample(Line(1700f), 4f);
            var km = RoadsidePlan.PlanKmStones(s, 7f, 14, null);
            Assert.AreEqual(3, km.Count);
            Assert.AreEqual(1, km[0].Km);
            Assert.AreEqual(3, km[2].Km);
        }

        [Test]
        public void Guardrail_OnlyWhereTerrainDrops()
        {
            var s = RoadsidePlan.Resample(Line(200f), 4f);
            System.Func<float, float, float> h = (x, z) => (x > 4f && z > 60f && z < 140f) ? 0f : 10f;
            var runs = RoadsidePlan.PlanGuardrails(s, 7f, 10, h, null);
            Assert.AreEqual(1, runs.Count);
            Assert.AreEqual(1f, Mathf.Sign(runs[0].Start.x));
            Assert.GreaterOrEqual(runs[0].Start.z, 56f);
            Assert.LessOrEqual(runs[0].End.z, 144f);
            var flat = RoadsidePlan.PlanGuardrails(s, 7f, 10, (x, z) => 10f, null);
            Assert.AreEqual(0, flat.Count);
        }

        [Test]
        public void Wire_SagShape()
        {
            Assert.AreEqual(0f, RoadsidePlan.WireSag(0f, 1f), 1e-5f);
            Assert.AreEqual(0f, RoadsidePlan.WireSag(1f, 1f), 1e-5f);
            Assert.AreEqual(1f, RoadsidePlan.WireSag(0.5f, 1f), 1e-5f);
            Assert.Less(RoadsidePlan.SagFor(10f), RoadsidePlan.SagFor(40f));
        }

        [Test]
        public void WidthHierarchy()
        {
            Assert.AreEqual(7f, RoadsidePlan.WidthFor(RoadKind.Asphalt, 0f));
            Assert.AreEqual(4.5f, RoadsidePlan.WidthFor(RoadKind.Dirt, 0f));
            Assert.AreEqual(6f, RoadsidePlan.WidthFor(RoadKind.Asphalt, 6f));
            Assert.IsTrue(RoadsidePlan.IsMainRoad(RoadKind.Asphalt));
            Assert.IsFalse(RoadsidePlan.IsMainRoad(RoadKind.Dirt));
        }
    }
}
