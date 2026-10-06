using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests.EditMode
{
    public sealed class ViewmodelDynamicsTests
    {
        private static float Simulate(int fps, float seconds)
        {
            var s = new SpringAxis { Position = 0f, Velocity = 0f };
            s.Kick(5f, 0.1f);
            var dt = 1f / fps;
            var n = (int)(seconds * fps);
            for (var i = 0; i < n; i++)
                s.Step(0f, 180f, 0.7f, dt);
            return s.Position;
        }

        [Test]
        public void Spring_FrameRateIndependent()
        {
            var a = Simulate(60, 0.25f);
            var b = Simulate(144, 0.25f);
            var c = Simulate(240, 0.25f);
            Assert.IsTrue(System.Math.Abs(a - c) < 0.0005f, "60 vs 240: " + a + " " + c);
            Assert.IsTrue(System.Math.Abs(b - c) < 0.0005f, "144 vs 240: " + b + " " + c);
        }

        [Test]
        public void Spring_ConvergesToTarget()
        {
            var x = 1f; var v = 0f;
            for (var i = 0; i < 120; i++)
                ViewmodelDynamics.StepSpring(ref x, ref v, 0f, 200f, 1f, 1f / 60f);
            Assert.IsTrue(System.Math.Abs(x) < 0.001f);
        }

        [Test]
        public void Spring_NaNRecovers()
        {
            var x = float.NaN; var v = 0f;
            ViewmodelDynamics.StepSpring(ref x, ref v, 2f, 100f, 1f, 0.016f);
            Assert.AreEqual(2f, x);
        }

        [Test]
        public void AdsSeconds_UsesWeaponData()
        {
            Assert.AreEqual(0.3f, ViewmodelDynamics.AdsSeconds(0.3f, false), 1e-4f);
            Assert.AreEqual(0.18f, ViewmodelDynamics.AdsSeconds(0f, false), 1e-4f);
            Assert.IsTrue(ViewmodelDynamics.AdsSeconds(0.3f, true) > 0.3f);
        }

        [Test]
        public void WallLower_Weights()
        {
            Assert.AreEqual(0f, ViewmodelDynamics.WallLowerWeight(1f, 0.8f, 0.3f), 1e-4f);
            Assert.AreEqual(1f, ViewmodelDynamics.WallLowerWeight(0.2f, 0.8f, 0.3f), 1e-4f);
            var mid = ViewmodelDynamics.WallLowerWeight(0.55f, 0.8f, 0.3f);
            Assert.IsTrue(mid > 0.4f && mid < 0.6f);
        }

        [Test]
        public void ViewmodelFov_Clamps()
        {
            Assert.AreEqual(54f, ViewmodelDynamics.ClampViewmodelFov(0f));
            Assert.AreEqual(90f, ViewmodelDynamics.ClampViewmodelFov(200f));
            Assert.IsTrue(ViewmodelDynamics.AdsViewmodelFov(60f, 1f) < 60f);
        }

        [Test]
        public void CameraRecoil_KicksAndRecovers()
        {
            var r = new CameraRecoilSpring();
            r.Kick(2f, 0.5f);
            Assert.IsTrue(r.PitchOffset > 0f);
            for (var i = 0; i < 240; i++)
                r.Step(1f / 120f);
            Assert.IsTrue(System.Math.Abs(r.PitchOffset) < 0.01f);
        }
    }
}
