#if UNITY_EDITOR
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio.Foley;

namespace Project.Tests.EditMode
{
    public sealed class ViewmodelPoseTimelineTests
    {
        [Test]
        public void KeysInterpolateAndClamp()
        {
            var tr = new PoseTrack().Key(0.2f, 1f, PoseEase.Linear).Key(0.6f, 3f, PoseEase.Linear);
            Assert.AreEqual(1f, tr.Evaluate(0f), 1e-4f);
            Assert.AreEqual(2f, tr.Evaluate(0.4f), 1e-4f);
            Assert.AreEqual(3f, tr.Evaluate(1f), 1e-4f);
        }

        [Test]
        public void OvershootExceedsTargetThenSettles()
        {
            var peak = 0f;
            for (var i = 0; i <= 100; i++)
            {
                var v = PoseTrack.Curve(PoseEase.Overshoot, i / 100f);
                if (v > peak) peak = v;
            }

            Assert.IsTrue(peak > 1.02f);
            Assert.AreEqual(1f, PoseTrack.Curve(PoseEase.Overshoot, 1f), 1e-4f);
        }

        [Test]
        public void AnticipateDipsBelowZero()
        {
            Assert.IsTrue(PoseTrack.Curve(PoseEase.Anticipate, 0.2f) < 0f);
        }

        [Test]
        public void KeysSortedWhenAddedOutOfOrder()
        {
            var tr = new PoseTrack().Key(0.8f, 1f, PoseEase.Linear).Key(0.2f, 0f, PoseEase.Linear);
            Assert.AreEqual(0.5f, tr.Evaluate(0.5f), 1e-4f);
        }

        [Test]
        public void MagReleasesAndSeatsMatchFoleyPlan()
        {
            AssertPlan(WeaponCategory.Pistol, ReloadKind.Pistol, ViewmodelPoseTimeline.PistolMagOut, ViewmodelPoseTimeline.PistolMagIn);
            AssertPlan(WeaponCategory.AssaultRifle, ReloadKind.Rifle, ViewmodelPoseTimeline.RifleMagOut, ViewmodelPoseTimeline.RifleMagIn);
        }

        private static void AssertPlan(WeaponCategory cat, ReloadKind kind, float outT, float inT)
        {
            var plan = ReloadFoleyPlanner.Plan(cat, false, 2.5f);
            float foleyOut = -1f, foleyIn = -1f;
            for (var i = 0; i < plan.Length; i++)
            {
                if (plan[i].Step == FoleyStep.MagOut) foleyOut = plan[i].Fraction;
                if (plan[i].Step == FoleyStep.MagIn) foleyIn = plan[i].Fraction;
            }

            Assert.AreEqual(foleyOut, outT, 0.03f);
            Assert.AreEqual(foleyIn, inT, 0.03f);
            var tl = ViewmodelPoseTimeline.Build(kind, false);
            Assert.AreEqual(0f, tl.MagTravel.Evaluate(0f), 1e-4f);
            Assert.AreEqual(1f, tl.MagTravel.Evaluate(inT - 0.1f), 0.2f);
            Assert.AreEqual(0f, tl.MagTravel.Evaluate(1f), 1e-4f);
        }

        [Test]
        public void EmptyAddsMechanismTacticalDoesNot()
        {
            var empty = ViewmodelPoseTimeline.Build(ReloadKind.Rifle, true);
            var tac = ViewmodelPoseTimeline.Build(ReloadKind.Rifle, false);
            Assert.IsTrue(empty.Mechanism.Evaluate(0.88f) > 0.5f);
            Assert.AreEqual(0f, tac.Mechanism.Evaluate(0.88f), 1e-4f);
        }

        [Test]
        public void ShellCountMatchesFoleyAndInterruptCounts()
        {
            Assert.AreEqual(5, ViewmodelPoseTimeline.ShotgunShellCount(3f));
            Assert.AreEqual(8, ViewmodelPoseTimeline.ShotgunShellCount(60f));
            Assert.AreEqual(0, ViewmodelPoseTimeline.ShellsLoaded(0.02f, 5));
            Assert.AreEqual(5, ViewmodelPoseTimeline.ShellsLoaded(1f, 5));
        }

        [Test]
        public void MachinegunCoverOpensThenClosesAndBeltLays()
        {
            var tl = ViewmodelPoseTimeline.Build(ReloadKind.Machinegun, false);
            Assert.AreEqual(0f, tl.Cover.Evaluate(0.05f), 1e-4f);
            Assert.IsTrue(tl.Cover.Evaluate(0.5f) > 0.95f);
            Assert.AreEqual(0f, tl.Cover.Evaluate(0.9f), 1e-4f);
            Assert.IsTrue(tl.BeltLay.Evaluate(0.74f) > 0.9f);
            Assert.AreEqual(0.64f, tl.SeatTime, 1e-4f);
        }

        [Test]
        public void ShellStageCoversLoopAndInterruptNeedsLoadedShell()
        {
            Assert.IsFalse(ViewmodelPoseTimeline.ShellStage(0.05f, 4, out _, out _));
            Assert.IsTrue(ViewmodelPoseTimeline.ShellStage(0.5f, 4, out var idx, out var s));
            Assert.IsTrue(idx >= 0 && idx < 4 && s >= 0f && s <= 1f);
            Assert.IsFalse(ViewmodelPoseTimeline.CanInterruptShotgun(0.02f, 4));
            Assert.IsTrue(ViewmodelPoseTimeline.CanInterruptShotgun(0.5f, 4));
            Assert.IsFalse(ViewmodelPoseTimeline.CanInterruptShotgun(0.95f, 4));
        }

        [Test]
        public void InspectHasTwoPhasesWithOppositeYaw()
        {
            var tl = ViewmodelPoseTimeline.Inspect();
            Assert.IsTrue(tl.Yaw.Evaluate(0.45f) > 40f);
            Assert.IsTrue(tl.Yaw.Evaluate(0.8f) < -30f);
            Assert.AreEqual(0f, tl.Yaw.Evaluate(1f), 1e-4f);
            Assert.AreEqual(0f, tl.Raise.Evaluate(0f), 1e-4f);
        }

        [Test]
        public void DrawOvershootsAndHolsterAnticipates()
        {
            var peak = 0f;
            for (var i = 0; i <= 100; i++) peak = System.Math.Max(peak, ViewmodelPoseTimeline.DrawProgress(i / 100f));
            Assert.IsTrue(peak > 1.01f);
            Assert.AreEqual(1f, ViewmodelPoseTimeline.DrawProgress(1f), 1e-4f);
            Assert.IsTrue(ViewmodelPoseTimeline.HolsterProgress(0.18f) < 0f);
            Assert.AreEqual(1f, ViewmodelPoseTimeline.HolsterProgress(1f), 1e-4f);
        }

        [Test]
        public void SprintWeightReachesOneAndOvershootsWhenEntering()
        {
            Assert.AreEqual(0f, ViewmodelPoseTimeline.SprintWeight(0f, true), 1e-4f);
            Assert.AreEqual(1f, ViewmodelPoseTimeline.SprintWeight(1f, true), 1e-4f);
            Assert.IsTrue(ViewmodelPoseTimeline.SprintWeight(0.85f, true) > 1f);
            Assert.AreEqual(1f, ViewmodelPoseTimeline.SprintWeight(1f, false), 1e-4f);
            Assert.AreEqual(0f, ViewmodelPoseTimeline.SprintWeight(0f, false), 1e-4f);
        }
    }
}
#endif
