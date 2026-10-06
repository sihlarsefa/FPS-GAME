using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>Bölge duvarı saf geometri: segment konumları, küçülme interpolasyonu, desen.</summary>
    public sealed class ZoneWallVisualTests
    {
        [Test]
        public void Direction_QuarterPoints()
        {
            ZoneWallGeometry.Direction(0, 128, out var x, out var z);
            Assert.AreEqual(1f, x, 1e-5f); Assert.AreEqual(0f, z, 1e-5f);
            ZoneWallGeometry.Direction(32, 128, out x, out z);
            Assert.AreEqual(0f, x, 1e-5f); Assert.AreEqual(1f, z, 1e-5f);
            ZoneWallGeometry.Direction(64, 128, out x, out z);
            Assert.AreEqual(-1f, x, 1e-5f);
        }

        [Test]
        public void WorldPoint_OnRadius_AndBandHeight()
        {
            var bottom = ZoneWallGeometry.WorldPoint(10, 128, 100f, -50f, 200f, 5f, false);
            var top = ZoneWallGeometry.WorldPoint(10, 128, 100f, -50f, 200f, 5f, true);
            var flat = new Vector2(bottom.x - 100f, bottom.z + 50f).magnitude;
            Assert.AreEqual(200f, flat, 0.01f);
            Assert.AreEqual(60f, top.y - bottom.y, 1e-4f);
        }

        [Test]
        public void Fill_CountsAndIndicesInRange_AndSeamUvs()
        {
            const int n = ZoneWallGeometry.Segments;
            var v = new Vector3[ZoneWallGeometry.VertexCount(n)];
            var uv = new Vector2[v.Length];
            var t = new int[ZoneWallGeometry.IndexCount(n)];
            ZoneWallGeometry.Fill(n, v, uv, t);
            Assert.AreEqual(258, v.Length);
            Assert.AreEqual(768, t.Length);
            foreach (var i in t) Assert.That(i, Is.InRange(0, v.Length - 1));
            Assert.AreEqual(0f, uv[0].x); Assert.AreEqual(1f, uv[v.Length - 1].x);
            Assert.AreEqual(v[0].x, v[v.Length - 2].x, 1e-4f);
            Assert.AreEqual(1f, v[1].y);
        }

        [Test]
        public void Smooth_ConvergesWithoutOvershoot()
        {
            var r = 1000f;
            for (var i = 0; i < 600; i++)
            {
                var next = ZoneWallGeometry.Smooth(r, 400f, 1f / 60f, 5f);
                Assert.GreaterOrEqual(next, 400f);
                Assert.LessOrEqual(next, r);
                r = next;
            }
            Assert.AreEqual(400f, r, 0.1f);
            Assert.AreEqual(7f, ZoneWallGeometry.Smooth(7f, 100f, 0f, 5f));
        }

        [Test]
        public void Lerp_ClampsAndInterpolates()
        {
            var a = new ZoneState(0f, 0f, 100f, 1f);
            var b = new ZoneState(100f, 50f, 20f, 3f);
            var m = ZoneWallGeometry.Lerp(a, b, 0.5f);
            Assert.AreEqual(50f, m.CenterX, 1e-4f);
            Assert.AreEqual(60f, m.Radius, 1e-4f);
            Assert.AreEqual(20f, ZoneWallGeometry.Lerp(a, b, 9f).Radius, 1e-4f);
        }

        [Test]
        public void Follow_SnapsOnFirstGrowthAndJump_ElseSmooths()
        {
            var shown = new ZoneState(0f, 0f, 100f, 1f);
            var smaller = new ZoneState(0f, 0f, 80f, 1f);
            var f = ZoneWallGeometry.Follow(shown, smaller, 0.1f, 5f, false);
            Assert.Less(f.Radius, 100f); Assert.Greater(f.Radius, 80f);
            Assert.AreEqual(80f, ZoneWallGeometry.Follow(shown, smaller, 0.1f, 5f, true).Radius);
            var bigger = new ZoneState(0f, 0f, 300f, 1f);
            Assert.AreEqual(300f, ZoneWallGeometry.Follow(shown, bigger, 0.1f, 5f, false).Radius);
            Assert.AreEqual(100f, ZoneWallGeometry.Follow(default, shown, 0.1f, 5f, false).Radius);
        }

        [Test]
        public void TilesAround_IntegerAndMinOne()
        {
            Assert.AreEqual(1, ZoneWallGeometry.TilesAround(0f));
            Assert.AreEqual(Mathf.RoundToInt(2f * Mathf.PI * 600f / 60f), ZoneWallGeometry.TilesAround(600f));
        }

        [Test]
        public void ProximityFade_DimsNearWall()
        {
            Assert.AreEqual(0.45f, ZoneWallGeometry.ProximityFade(0f), 1e-4f);
            Assert.AreEqual(1f, ZoneWallGeometry.ProximityFade(-50f), 1e-4f);
            Assert.Less(ZoneWallGeometry.ProximityFade(5f), ZoneWallGeometry.ProximityFade(15f));
        }

        [Test]
        public void SignedDistance_InsideNegative()
        {
            var z = new ZoneState(10f, 10f, 100f, 1f);
            Assert.Less(ZoneWallGeometry.SignedDistance(z, 10f, 10f), 0f);
            Assert.AreEqual(50f, ZoneWallGeometry.SignedDistance(z, 160f, 10f), 1e-3f);
        }

        [Test]
        public void Pattern_FadesAtEdges_TilesInU_AndIsBounded()
        {
            Assert.AreEqual(0f, ZoneWallPattern.Sample(0.3f, 0f), 1e-4f);
            Assert.AreEqual(0f, ZoneWallPattern.Sample(0.3f, 1f), 1e-4f);
            for (var i = 0; i < 50; i++)
            {
                var v = 0.3f + i * 0.008f;
                var a = ZoneWallPattern.Sample(0.001f * i, v);
                Assert.That(a, Is.InRange(0f, 1f));
                Assert.AreEqual(a, ZoneWallPattern.Sample(0.001f * i + 1f, v), 1e-3f);
            }
        }
    }
}
