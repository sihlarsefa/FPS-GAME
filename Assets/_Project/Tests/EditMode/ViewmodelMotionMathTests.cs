using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests.EditMode
{
    public sealed class ViewmodelMotionMathTests
    {
        private static float MaxPos(ViewmodelStance st, bool sprint)
        {
            var p = ViewmodelMotionMath.BobProfile(st, sprint);
            var m = 0f;
            for (var i = 0; i < 720; i++)
            {
                ViewmodelMotionMath.SampleBob(p, i * 0.01f * 6.28318f, 1.5f, out var x, out var y, out _, out _, out _);
                m = System.Math.Max(m, System.Math.Max(System.Math.Abs(x), System.Math.Abs(y)));
            }
            return m;
        }

        [Test]
        public void Bob_BoundedAndStanceOrdered()
        {
            var sprint = MaxPos(ViewmodelStance.Stand, true);
            var walk = MaxPos(ViewmodelStance.Stand, false);
            var crouch = MaxPos(ViewmodelStance.Crouch, false);
            var prone = MaxPos(ViewmodelStance.Prone, false);
            Assert.Less(sprint, 0.03f);
            Assert.Greater(sprint, walk);
            Assert.Greater(walk, crouch);
            Assert.Greater(crouch, prone);
        }

        [Test]
        public void Bob_RollWithinLimit()
        {
            var p = ViewmodelMotionMath.BobProfile(ViewmodelStance.Stand, true);
            for (var i = 0; i < 360; i++)
            {
                ViewmodelMotionMath.SampleBob(p, i * 0.0175f, 5f, out _, out _, out var pi, out var ya, out var ro);
                Assert.LessOrEqual(System.Math.Abs(ro), 4f);
                Assert.LessOrEqual(System.Math.Abs(pi), 2f);
                Assert.LessOrEqual(System.Math.Abs(ya), 2.5f);
            }
        }

        [Test]
        public void TurnLagAndStrafe_Clamped()
        {
            Assert.AreEqual(ViewmodelMotionMath.MaxTurnLagDeg, ViewmodelMotionMath.TurnLagTarget(100000f), 1e-4f);
            Assert.AreEqual(-ViewmodelMotionMath.MaxTurnLagDeg, ViewmodelMotionMath.TurnLagTarget(-100000f), 1e-4f);
            Assert.AreEqual(0f, ViewmodelMotionMath.TurnLagTarget(float.NaN));
            Assert.LessOrEqual(System.Math.Abs(ViewmodelMotionMath.StrafeRollTarget(9f)), 2f);
            Assert.Greater(System.Math.Abs(ViewmodelMotionMath.StrafeRollTarget(1f)), 1f);
        }

        [Test]
        public void AdsBreath_SlowBoundedAndHoldTightens()
        {
            float maxFree = 0f, maxHold = 0f;
            for (var i = 0; i < 1000; i++)
            {
                ViewmodelMotionMath.AdsBreath(i * 0.01f, 0f, 1f, out var p, out _);
                maxFree = System.Math.Max(maxFree, System.Math.Abs(p));
                ViewmodelMotionMath.AdsBreath(i * 0.01f, 1f, 1f, out var p2, out _);
                maxHold = System.Math.Max(maxHold, System.Math.Abs(p2));
            }
            Assert.Less(maxFree, 0.3f);
            Assert.Less(maxHold, maxFree * 0.3f);
            ViewmodelMotionMath.AdsBreath(1f, 0f, float.NaN, out var n, out _);
            Assert.IsFalse(float.IsNaN(n));
        }

        [Test]
        public void LandImpulse_NegativeAndBounded()
        {
            Assert.Less(ViewmodelMotionMath.LandImpulse(0f), 0f);
            Assert.GreaterOrEqual(ViewmodelMotionMath.LandImpulse(500f), -0.5001f);
            Assert.Greater(ViewmodelMotionMath.JumpLift(1f), 0f);
            Assert.AreEqual(ViewmodelMotionMath.JumpLift(1f), ViewmodelMotionMath.JumpLift(5f));
        }
    }
}
