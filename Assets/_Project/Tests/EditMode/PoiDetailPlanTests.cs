using NUnit.Framework;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class PoiDetailPlanTests
    {
        [Test]
        public void Density_IncreasesWithTier()
        {
            Assert.Greater(PoiDetailPlan.DensityForTier(3), PoiDetailPlan.DensityForTier(0));
            Assert.AreEqual(1f, PoiDetailPlan.DensityForTier(99));
        }

        [Test]
        public void Scaled_NeverZeroForPositive()
        {
            Assert.AreEqual(1, PoiDetailPlan.Scaled(1, 0));
            Assert.AreEqual(0, PoiDetailPlan.Scaled(0, 3));
            Assert.AreEqual(10, PoiDetailPlan.Scaled(10, 3));
        }

        [Test]
        public void ContainerGrid_IsDeterministicAndBounded()
        {
            var a = PoiDetailPlan.ContainerGrid(Vector2.zero, 0f, 4, 3, 7);
            var b = PoiDetailPlan.ContainerGrid(Vector2.zero, 0f, 4, 3, 7);
            Assert.AreEqual(a.Count, b.Count);
            Assert.IsTrue(a.Count <= 12 && a.Count > 0);
            Assert.AreEqual(0, PoiDetailPlan.ContainerGrid(Vector2.zero, 0f, 0, 3, 7).Count);
        }

        [Test]
        public void ScatterInDisc_RespectsRadiusAndSpacing()
        {
            var list = PoiDetailPlan.ScatterInDisc(new Vector2(10f, 10f), 20f, 8, 3, 3f, 1f, 2f);
            Assert.IsTrue(list.Count > 0);
            for (var i = 0; i < list.Count; i++)
            {
                Assert.IsTrue((list[i].Pos - new Vector2(10f, 10f)).magnitude <= 20.01f);
                for (var j = i + 1; j < list.Count; j++)
                    Assert.IsTrue((list[i].Pos - list[j].Pos).magnitude >= 2.99f);
            }
        }

        [Test]
        public void BulletHoles_StayOnWall()
        {
            var list = PoiDetailPlan.BulletHoleCluster(6f, 3f, 12, 5);
            Assert.AreEqual(12, list.Count);
            foreach (var s in list)
            {
                Assert.IsTrue(Mathf.Abs(s.Pos.x) <= 3.001f);
                Assert.IsTrue(s.Pos.y >= 0f && s.Pos.y <= 3.001f);
            }
        }
    }
}
