using NUnit.Framework;
using Project.Infrastructure.World.Lighting;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class PoiLightPlannerTests
    {
        [Test]
        public void Plan_IsDeterministic_AndNonEmpty()
        {
            var a = PoiLightPlanner.Plan(PoiSiteType.Village, Vector3.zero, 40f, 7);
            var b = PoiLightPlanner.Plan(PoiSiteType.Village, Vector3.zero, 40f, 7);
            Assert.Greater(a.Count, 0);
            Assert.AreEqual(a.Count, b.Count);
            Assert.AreEqual(a[0].Position.x, b[0].Position.x, 0.0001f);
        }

        [Test]
        public void Budget_NearestFourShadowed_AndTierLimits()
        {
            var all = PoiLightPlanner.Plan(PoiSiteType.Village, Vector3.zero, 80f, 1);
            var ultra = PoiLightPlanner.ApplyBudget(all, Vector3.zero, 3);
            int sh = 0;
            foreach (var s in ultra) if (s.Shadow) sh++;
            Assert.AreEqual(4, sh);
            var low = PoiLightPlanner.ApplyBudget(all, Vector3.zero, 0);
            Assert.IsTrue(low.Count <= PoiLightPlanner.MaxLights(0));
            foreach (var s in low) Assert.IsFalse(s.Shadow);
        }

        [Test]
        public void Flicker_StaysInRange()
        {
            for (int i = 0; i < 200; i++)
            {
                float f = PoiLightPlanner.FlickerFactor(i * 0.1f, 1.3f, 0.35f);
                Assert.IsTrue(f >= 0.65f - 0.001f && f <= 1.35f + 0.001f);
            }
        }

        [Test]
        public void Probes_RespectTierLimit_AndPreferInterior()
        {
            var l = new System.Collections.Generic.List<ProbeSpec>();
            for (int i = 0; i < 10; i++) l.Add(ReflectionProbePlanner.ForPoi(new Vector3(i * 10, 0, 0), 20f));
            l.Add(ReflectionProbePlanner.ForInterior(new Bounds(new Vector3(500, 0, 0), new Vector3(5, 3, 5))));
            var r = ReflectionProbePlanner.ApplyBudget(l, Vector3.zero, 0);
            Assert.AreEqual(2, r.Count);
            Assert.IsTrue(r[0].Interior);
        }
    }
}
