using NUnit.Framework;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests
{
    public class FrontlinePlanTests
    {
        private static readonly Vector2 A = new Vector2(0f, 0f);
        private static readonly Vector2 B = new Vector2(200f, 50f);

        [Test]
        public void Create_IsDeterministic()
        {
            var p1 = FrontlinePlan.Create(A, B, 7, 3);
            var p2 = FrontlinePlan.Create(A, B, 7, 3);
            Assert.AreEqual(p1.Sandbags.Count, p2.Sandbags.Count);
            Assert.AreEqual(p1.Craters.Count, p2.Craters.Count);
            Assert.AreEqual(p1.Trench[1].x, p2.Trench[1].x, 0.0001f);
        }

        [Test]
        public void Create_ProducesAllElementKinds()
        {
            var p = FrontlinePlan.Create(A, B, 3, 3);
            Assert.Greater(p.Trench.Count, 3);
            Assert.Greater(p.Sandbags.Count, 0);
            Assert.Greater(p.Foxholes.Count, 0);
            Assert.Greater(p.WireLines.Count, 0);
            Assert.Greater(p.Hedgehogs.Count, 0);
            Assert.Greater(p.Craters.Count, 0);
            Assert.Greater(p.Logs.Count, 0);
        }

        [Test]
        public void Trench_Zigzags()
        {
            var p = FrontlinePlan.Create(A, B, 5, 3);
            var side = new Vector2(-(B - A).normalized.y, (B - A).normalized.x);
            var flips = 0;
            for (var i = 1; i < p.Trench.Count; i++)
                if (Vector2.Dot(p.Trench[i] - A, side) * Vector2.Dot(p.Trench[i - 1] - A, side) < 0f)
                    flips++;
            Assert.IsTrue(flips >= p.Trench.Count - 2);
        }

        [Test]
        public void LowerTier_HasFewerItems()
        {
            var lo = FrontlinePlan.Create(A, B, 9, 0);
            var hi = FrontlinePlan.Create(A, B, 9, 3);
            Assert.Less(lo.Sandbags.Count, hi.Sandbags.Count);
        }

        [Test]
        public void IsClear_Predicate_IsRespected()
        {
            var p = FrontlinePlan.Create(A, B, 4, 3, (x, z) => false);
            Assert.AreEqual(0, p.Sandbags.Count);
            Assert.AreEqual(0, p.Craters.Count);
            Assert.AreEqual(0, p.WireLines.Count);
        }

        [Test]
        public void TooShort_ReturnsEmpty()
        {
            var p = FrontlinePlan.Create(A, new Vector2(5f, 0f), 1, 3);
            Assert.AreEqual(0, p.Trench.Count);
        }
    }
}
