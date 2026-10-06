#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public class PlanarReflectionMathTests
    {
        [Test]
        public void Tiers_LowDisabled_OthersSingleSurface()
        {
            Assert.IsFalse(PlanarReflectionMath.Get(0).Enabled);
            for (var t = 1; t < 4; t++)
            {
                Assert.IsTrue(PlanarReflectionMath.Get(t).Enabled);
                Assert.AreEqual(1, PlanarReflectionMath.Get(t).MaxActiveSurfaces);
            }

            Assert.AreEqual(4, PlanarReflectionMath.Get(1).ResolutionDivisor);
            Assert.AreEqual(2, PlanarReflectionMath.Get(3).ResolutionDivisor);
            Assert.IsTrue(PlanarReflectionMath.Get(3).FarClip > PlanarReflectionMath.Get(1).FarClip);
        }

        [Test]
        public void TierFromQuality_Maps()
        {
            Assert.AreEqual(3, PlanarReflectionMath.TierFromQuality(9, 4));
            Assert.AreEqual(0, PlanarReflectionMath.TierFromQuality(-2, 4));
            Assert.AreEqual(3, PlanarReflectionMath.TierFromQuality(2, 3));
            Assert.AreEqual(2, PlanarReflectionMath.TierFromQuality(0, 1));
        }

        [Test]
        public void ReflectPosition_MirrorsAcrossWaterPlane()
        {
            var plane = PlanarReflectionMath.PlaneFrom(new Vector3(0f, 18f, 0f), Vector3.up);
            var r = PlanarReflectionMath.ReflectPosition(new Vector3(3f, 28f, -5f), plane);
            Assert.AreEqual(3f, r.x, 1e-4f);
            Assert.AreEqual(8f, r.y, 1e-4f);
            Assert.AreEqual(-5f, r.z, 1e-4f);
            var d = PlanarReflectionMath.ReflectDirection(new Vector3(1f, -1f, 0f), Vector3.up);
            Assert.AreEqual(1f, d.y, 1e-5f);
        }

        [Test]
        public void ReflectionMatrix_IsInvolution()
        {
            var plane = PlanarReflectionMath.PlaneFrom(new Vector3(1f, 5f, 2f), new Vector3(0.2f, 1f, 0.1f));
            var m = PlanarReflectionMath.ReflectionMatrix(plane);
            var p = new Vector3(4f, 9f, -3f);
            var back = m.MultiplyPoint3x4(m.MultiplyPoint3x4(p));
            Assert.AreEqual(0f, (back - p).magnitude, 1e-3f);
        }

        [Test]
        public void CameraSpacePlane_IdentityView_UsesNormalAndSide()
        {
            var v = PlanarReflectionMath.CameraSpacePlane(Matrix4x4.identity, new Vector3(0f, 10f, 0f), Vector3.up, 1f, 0f);
            Assert.AreEqual(1f, v.y, 1e-5f);
            Assert.AreEqual(-10f, v.w, 1e-4f);
            Assert.AreEqual(-1f, PlanarReflectionMath.SideOf(Vector3.zero, new Vector3(0f, 1f, 0f), Vector3.up));
        }

        [Test]
        public void PickNearest_SkipsInvisibleAndFar()
        {
            var d = new List<float> { 10f, 30f, 5f, 400f };
            var v = new List<bool> { true, true, false, true };
            Assert.AreEqual(0, PlanarReflectionMath.PickNearest(d, v, 200f));
            v[0] = false;
            Assert.AreEqual(1, PlanarReflectionMath.PickNearest(d, v, 200f));
            v[1] = false;
            Assert.AreEqual(-1, PlanarReflectionMath.PickNearest(d, v, 200f));
            Assert.AreEqual(-1, PlanarReflectionMath.PickNearest(null, null, 200f));
        }

        [Test]
        public void TextureSize_And_UpdateInterval()
        {
            var s = PlanarReflectionMath.TextureSize(1920, 1080, 4);
            Assert.AreEqual(480, s.x);
            Assert.AreEqual(270, s.y);
            Assert.AreEqual(64, PlanarReflectionMath.TextureSize(100, 100, 4).x);
            Assert.IsTrue(PlanarReflectionMath.ShouldUpdate(7, 1));
            Assert.IsTrue(PlanarReflectionMath.ShouldUpdate(6, 3));
            Assert.IsFalse(PlanarReflectionMath.ShouldUpdate(7, 3));
        }

        [Test]
        public void Intensity_FallsToZeroAtMax()
        {
            Assert.AreEqual(1f, PlanarReflectionMath.IntensityByDistance(0f, 100f), 1e-5f);
            Assert.AreEqual(0f, PlanarReflectionMath.IntensityByDistance(100f, 100f), 1e-5f);
            Assert.AreEqual(0f, PlanarReflectionMath.IntensityByDistance(5f, 0f));
        }
    }

    public class ReflectionProbePlannerTests
    {
        private static List<ProbeSite> Sites()
        {
            var l = new List<ProbeSite>();
            for (var loc = 0; loc < 3; loc++)
            {
                l.Add(ReflectionProbePlanner.ExteriorSite("L" + loc, loc, new Vector3(loc * 200f, 0f, 0f), 60f));
                for (var b = 0; b < 8; b++)
                {
                    ProbeSite s;
                    if (ReflectionProbePlanner.TryInteriorSite("B" + loc + "_" + b, loc,
                            new Bounds(new Vector3(loc * 200f + b * 10f, 2f, 0f), new Vector3(6f + b, 4f, 6f)), out s))
                        l.Add(s);
                }
            }

            return l;
        }

        [Test]
        public void TinyBuilding_GetsNoInteriorProbe()
        {
            ProbeSite s;
            Assert.IsFalse(ReflectionProbePlanner.TryInteriorSite("x", 0, new Bounds(Vector3.zero, new Vector3(2f, 3f, 2f)), out s));
        }

        [Test]
        public void Select_RespectsBudgetAndKeepsExteriorsFirst()
        {
            for (var tier = 0; tier < 4; tier++)
            {
                var res = ReflectionProbePlanner.Select(Sites(), tier, Vector3.zero);
                Assert.IsTrue(res.Count <= ReflectionProbePlanner.GetBudget(tier).MaxTotal);
                var ext = 0;
                foreach (var s in res) if (s.Kind == ProbeSiteKind.Exterior) ext++;
                Assert.AreEqual(3, ext);
                var per = new Dictionary<int, int>();
                foreach (var s in res)
                {
                    if (s.Kind != ProbeSiteKind.Interior) continue;
                    int n;
                    per.TryGetValue(s.LocationIndex, out n);
                    per[s.LocationIndex] = n + 1;
                    Assert.IsTrue(per[s.LocationIndex] <= ReflectionProbePlanner.GetBudget(tier).InteriorsPerLocation);
                }
            }
        }

        [Test]
        public void ActiveSet_NearestFirst_CappedAndRadius()
        {
            var b = ReflectionProbePlanner.GetBudget(0);
            var centers = new List<Vector3>();
            for (var i = 0; i < 10; i++) centers.Add(new Vector3(i * 20f, 0f, 0f));
            centers.Add(new Vector3(5000f, 0f, 0f));
            var act = ReflectionProbePlanner.ActiveSet(centers, null, Vector3.zero, b);
            Assert.AreEqual(b.MaxActive, act.Count);
            Assert.AreEqual(0, act[0]);
            Assert.IsFalse(act.Contains(10));
        }

        [Test]
        public void NeedsRender_BakeOnceVsOnDemand()
        {
            var low = ReflectionProbePlanner.GetBudget(1);
            var high = ReflectionProbePlanner.GetBudget(3);
            Assert.IsTrue(ReflectionProbePlanner.NeedsRender(false, false, low));
            Assert.IsFalse(ReflectionProbePlanner.NeedsRender(true, true, low));
            Assert.IsTrue(ReflectionProbePlanner.NeedsRender(true, true, high));
            Assert.IsFalse(ReflectionProbePlanner.NeedsRender(true, false, high));
        }

        [Test]
        public void NearestLocation_UsesRadius()
        {
            var c = new List<Vector2> { new Vector2(0f, 0f), new Vector2(100f, 0f) };
            var r = new List<float> { 30f, 30f };
            Assert.AreEqual(1, ReflectionProbePlanner.NearestLocation(new Vector2(90f, 0f), c, r));
            Assert.AreEqual(-1, ReflectionProbePlanner.NearestLocation(new Vector2(50f, 0f), c, r));
        }
    }
}
#endif
