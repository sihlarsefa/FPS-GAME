#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public class BuildingWeatheringMathTests
    {
        [Test]
        public void Classify_GroupsMaterials()
        {
            Assert.AreEqual(WeatherWallClass.Plaster, BuildingWeatheringMath.Classify(MaterialId.PlasterWarm));
            Assert.AreEqual(WeatherWallClass.Stone, BuildingWeatheringMath.Classify(MaterialId.StoneDark));
            Assert.AreEqual(WeatherWallClass.Concrete, BuildingWeatheringMath.Classify(MaterialId.Brick));
            Assert.AreEqual(WeatherWallClass.Other, BuildingWeatheringMath.Classify(MaterialId.MetalDark));
        }

        [Test]
        public void Profile_LowTierKeepsOnlyCheapLayers()
        {
            var low = BuildingWeatheringMath.ProfileFor(BuildingStyle.VillageHouse, 0);
            Assert.AreEqual(0f, low.PlasterDamage);
            Assert.AreEqual(0f, low.Windows);
            Assert.AreEqual(0f, low.TileDetail);
            Assert.Greater(low.Damp, 0f);
            var high = BuildingWeatheringMath.ProfileFor(BuildingStyle.VillageHouse, 3);
            Assert.Greater(high.PlasterDamage, 0f);
            Assert.Greater(high.Interior, 0f);
        }

        [Test]
        public void SagY_EndsAtAnchorsAndDipsInMiddle()
        {
            Assert.AreEqual(3f, BuildingWeatheringMath.SagY(3f, 3f, 0f, 0.5f), 1e-4f);
            Assert.AreEqual(3f, BuildingWeatheringMath.SagY(3f, 3f, 1f, 0.5f), 1e-4f);
            Assert.AreEqual(2.5f, BuildingWeatheringMath.SagY(3f, 3f, 0.5f, 0.5f), 1e-4f);
        }

        [Test]
        public void PatchCount_IsBounded()
        {
            Assert.AreEqual(0, BuildingWeatheringMath.PatchCount(40f, 0f));
            Assert.LessOrEqual(BuildingWeatheringMath.PatchCount(5000f, 1f), 7);
        }

        [Test]
        public void PlanPatches_StaysInBoundsAndAvoidsOpenings()
        {
            var avoid = new List<WeatherRect> { new WeatherRect(2f, 3f, 1f, 2f) };
            var res = new List<WeatherRect>();
            for (var seed = 0; seed < 20; seed++)
            {
                BuildingWeatheringMath.PlanPatches(new System.Random(seed), 8f, 0.3f, 3f, avoid, 5, res);
                foreach (var r in res)
                {
                    Assert.GreaterOrEqual(r.U0, 0.1f);
                    Assert.LessOrEqual(r.U1, 7.9f);
                    Assert.GreaterOrEqual(r.Y0, 0.3f - 0.01f);
                    Assert.LessOrEqual(r.Y1, 3f + 0.01f);
                    Assert.IsFalse(r.Overlaps(avoid[0]));
                }
            }
        }

        [Test]
        public void JaggedPieces_StayInsideRect()
        {
            var r = new WeatherRect(1f, 2f, 0.5f, 1.5f);
            var res = new List<WeatherRect>();
            BuildingWeatheringMath.JaggedPieces(new System.Random(3), r, res);
            Assert.GreaterOrEqual(res.Count, 3);
            foreach (var q in res)
            {
                Assert.GreaterOrEqual(q.U0, r.U0 - 1e-4f);
                Assert.LessOrEqual(q.U1, r.U1 + 1e-4f);
                Assert.GreaterOrEqual(q.Y0, r.Y0 - 1e-4f);
                Assert.LessOrEqual(q.Y1, r.Y1 + 1e-4f);
            }
        }

        [Test]
        public void SubtractSpan_SplitsAndRemoves()
        {
            var spans = new List<Vector2> { new Vector2(0f, 10f) };
            BuildingWeatheringMath.SubtractSpan(spans, 4f, 6f);
            Assert.AreEqual(2, spans.Count);
            BuildingWeatheringMath.SubtractSpan(spans, -1f, 11f);
            Assert.AreEqual(0, spans.Count);
        }

        [Test]
        public void DampSegments_CoverRangeContiguously()
        {
            var res = new List<WeatherRect>();
            BuildingWeatheringMath.DampSegments(new System.Random(1), 0.5f, 5f, 0.2f, 0.6f, res);
            Assert.AreEqual(0.5f, res[0].U0, 1e-4f);
            Assert.AreEqual(5f, res[res.Count - 1].U1, 1e-4f);
            for (var i = 1; i < res.Count; i++)
                Assert.AreEqual(res[i - 1].U1, res[i].U0, 1e-4f);
        }

        [Test]
        public void RainStreaks_RespectFloorLimit()
        {
            var res = new List<WeatherRect>();
            BuildingWeatheringMath.RainStreaks(new System.Random(5), 1f, 2f, 2f, 1.5f, res);
            foreach (var r in res)
                Assert.GreaterOrEqual(r.Y0, 1.5f - 1e-4f);
        }

        [Test]
        public void RoofCounts_AreSane()
        {
            Assert.AreEqual(0, BuildingWeatheringMath.TailCount(0.3f, 0.7f));
            Assert.GreaterOrEqual(BuildingWeatheringMath.TailCount(6f, 0.7f), 2);
            Assert.AreEqual(0, BuildingWeatheringMath.CourseCount(0.4f, 0.3f));
            Assert.Greater(BuildingWeatheringMath.CourseCount(3f, 0.3f), 5);
            Assert.AreEqual(10f, BuildingWeatheringMath.HipCourseHalfLength(2f, 10f, 4f, 0f), 1e-4f);
            Assert.AreEqual(2f, BuildingWeatheringMath.HipCourseHalfLength(2f, 10f, 4f, 4f), 1e-4f);
        }

        [Test]
        public void PlanGrid_ConnectsNearbyBuildingsAndAddsPoles()
        {
            var anchors = new List<Vector3> { new Vector3(0f, 5f, 0f), new Vector3(20f, 5f, 0f), new Vector3(500f, 5f, 0f) };
            var nodes = new List<GridNode>();
            var spans = new List<GridSpan>();
            BuildingWeatheringMath.PlanGrid(anchors, 48f, 26f, nodes, spans);
            Assert.GreaterOrEqual(spans.Count, 1);
            // Uzak bina bağlanmaz
            foreach (var s in spans)
            {
                Assert.AreNotEqual(2, s.A);
                Assert.AreNotEqual(2, s.B);
            }
        }
    }
}
#endif
