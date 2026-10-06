using NUnit.Framework;
using Project.Infrastructure.Characters.Animation;

namespace Project.Tests.EditMode
{
    public sealed class WearyAnimationTests
    {
        [Test]
        public void Idle_ZeroWeary_IsZero()
        {
            var p = WearyPoseLibrary.Idle(WearyIdleVariant.SlumpedShoulders, 3f, 0f);
            Assert.AreEqual(0f, p.SpinePitch);
            Assert.AreEqual(0f, p.HeadPitch);
        }

        [Test]
        public void Idle_Slumped_MoreThanAlert()
        {
            var a = WearyPoseLibrary.Idle(WearyIdleVariant.SlumpedShoulders, 1f, 1f);
            var b = WearyPoseLibrary.Idle(WearyIdleVariant.Alert, 1f, 1f);
            Assert.Greater(a.SpinePitch, b.SpinePitch);
        }

        [Test]
        public void Limp_ScalesWithWeary()
        {
            var lo = WearyPoseLibrary.Limp(0.25f, 0.2f, 1);
            var hi = WearyPoseLibrary.Limp(0.25f, 1f, 1);
            Assert.Greater(hi.Lameness, lo.Lameness);
            Assert.Greater(hi.Lameness, 0f);
        }

        [Test]
        public void Flinch_DecaysToZero()
        {
            var s = new WearyReactionState();
            s.Flinch(1f, 1f);
            Assert.Greater(s.FlinchAmount, 0.9f);
            for (var i = 0; i < 200; i++) s.Step(0.05f);
            Assert.AreEqual(0f, s.FlinchAmount);
        }

        [Test]
        public void Stagger_ProducesPoseThenSettles()
        {
            var s = new WearyReactionState();
            s.Stagger(-1f, 60f);
            Assert.Greater(s.Evaluate(0f).SpinePitch, 5f);
            for (var i = 0; i < 400; i++) s.Step(0.05f);
            Assert.AreEqual(0f, s.StaggerAmount);
        }

        [Test]
        public void Crouch_SmoothsTowardTarget()
        {
            var s = new WearyReactionState();
            s.SetCrouchTarget(1f);
            s.Step(0.05f);
            Assert.Greater(s.Crouch, 0f);
            Assert.IsTrue(s.Crouch < 1f);
            for (var i = 0; i < 100; i++) s.Step(0.05f);
            Assert.AreEqual(1f, s.Crouch, 0.01f);
        }
    }
}
