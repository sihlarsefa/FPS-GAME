using NUnit.Framework;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests
{
    public sealed class MenuSceneMathTests
    {
        [Test]
        public void RidgeProfile_NeverNaN_AndBounded()
        {
            for (var i = 0; i <= 200; i++)
            {
                var p = MenuSceneMath.RidgeProfile(i / 200f);
                Assert.IsFalse(float.IsNaN(p));
                Assert.GreaterOrEqual(p, 0f);
                Assert.LessOrEqual(p, 1.0001f);
            }

            Assert.IsFalse(float.IsNaN(MenuSceneMath.RidgeProfile(float.NaN)));
            Assert.AreEqual(0f, MenuSceneMath.RidgeProfile(1f), 1e-3f);
        }

        [Test]
        public void CameraLoop_ClosesAfterThirtySeconds()
        {
            MenuSceneMath.CameraLoop(0f, out var p0, out var t0, out var f0);
            MenuSceneMath.CameraLoop(30f, out var p1, out var t1, out var f1);
            Assert.AreEqual(0f, (p0 - p1).magnitude, 1e-3f);
            Assert.AreEqual(0f, (t0 - t1).magnitude, 1e-3f);
            Assert.AreEqual(f0, f1, 1e-3f);
        }

        [Test]
        public void CameraLoop_VisitsDistinctAnglesAndStaysFinite()
        {
            MenuSceneMath.CameraLoop(10f, out var a, out _, out _);
            MenuSceneMath.CameraLoop(20f, out var b, out _, out _);
            Assert.Greater((a - b).magnitude, 1f);
            for (var t = -5f; t < 70f; t += 0.37f)
            {
                MenuSceneMath.CameraLoop(t, out var p, out var tg, out var f);
                Assert.IsTrue(MenuSceneMath.IsFinite(p) && MenuSceneMath.IsFinite(tg) && !float.IsNaN(f));
            }
        }

        [Test]
        public void YawToward_FacesTarget()
        {
            Assert.AreEqual(90f, MenuSceneMath.YawToward(Vector3.zero, new Vector3(2f, 0f, 0f)), 1e-3f);
            Assert.AreEqual(0f, MenuSceneMath.YawToward(Vector3.zero, new Vector3(0f, 5f, 3f)), 1e-3f);
        }
    }
}
