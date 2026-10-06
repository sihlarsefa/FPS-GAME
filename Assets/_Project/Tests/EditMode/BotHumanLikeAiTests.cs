#if UNITY_EDITOR
using NUnit.Framework;
using Project.Application.AI;

namespace Project.Tests
{
    public class BotHumanLikeAiTests
    {
        // ---------------------------------------------------------------- algı
        [Test]
        public void Detection_FoveaFullPeripheralLess_EdgeZero()
        {
            Assert.AreEqual(1f, BotDetectionModel.AngleGain(5f, 120f), 0.001f);
            var mid = BotDetectionModel.AngleGain(40f, 120f);
            Assert.Less(mid, 1f);
            Assert.Greater(mid, BotDetectionModel.PeripheralFloor);
            Assert.AreEqual(0f, BotDetectionModel.AngleGain(70f, 120f), 0.001f);
        }

        [Test]
        public void Detection_DistanceGain_Falls()
        {
            Assert.Greater(BotDetectionModel.DistanceGain(10f), BotDetectionModel.DistanceGain(80f));
            Assert.AreEqual(0.5f, BotDetectionModel.DistanceGain(BotDetectionModel.HalfGainDistance), 0.01f);
        }

        [Test]
        public void Detection_ProneStillHarderThanSprint()
        {
            Assert.Less(BotDetectionModel.MotionGain(0f, false, true), BotDetectionModel.MotionGain(6f, false, false));
        }

        [Test]
        public void Detection_Smoke_ReducesVisibility()
        {
            Assert.Less(BotDetectionModel.Visibility(1f, 1f, 0f), 0.15f);
            Assert.AreEqual(1f, BotDetectionModel.Visibility(1f, 0f, 0f), 0.001f);
        }

        [Test]
        public void Detection_AwarenessFillsThenDecays()
        {
            var g = BotDetectionModel.GainPerSecond(0f, 120f, 20f, 3f, false, false, 1f, 0.5f, false);
            Assert.Greater(g, 0.5f);
            var a = 0f;
            for (var i = 0; i < 100; i++)
                a = BotDetectionModel.Step(a, g, 0.05f);
            Assert.IsTrue(BotDetectionModel.IsDetected(a));
            var b = BotDetectionModel.Step(0.5f, 0f, 1f);
            Assert.Less(b, 0.5f);
            Assert.Greater(b, 0.2f);
        }

        [Test]
        public void Detection_GunfireFarFasterThanSilent()
        {
            var quiet = BotDetectionModel.GainPerSecond(0f, 120f, 30f, 2f, false, false, 1f, 0.5f, false);
            var loud = BotDetectionModel.GainPerSecond(0f, 120f, 30f, 2f, false, false, 1f, 0.5f, true);
            Assert.AreEqual(3f, loud / quiet, 0.01f);
        }

        [Test]
        public void Detection_FirstSightDelay_HumanRange()
        {
            var fast = BotDetectionModel.FirstSightDelay(1f, 0f, 10f, 0f);
            var slow = BotDetectionModel.FirstSightDelay(0f, 80f, 120f, 1f);
            Assert.GreaterOrEqual(fast, 0.15f);
            Assert.Less(fast, 0.3f);
            Assert.Greater(slow, 0.5f);
            Assert.Less(slow, 0.9f);
        }

        [Test]
        public void Detection_SearchUncertainty_GrowsAndCaps()
        {
            Assert.Less(BotDetectionModel.SearchUncertainty(1f, 4f), BotDetectionModel.SearchUncertainty(6f, 4f));
            Assert.AreEqual(30f, BotDetectionModel.SearchUncertainty(100f, 7f), 0.001f);
            Assert.Greater(BotDetectionModel.MemorySeconds(1f, true), BotDetectionModel.MemorySeconds(0f, false));
        }

        // ---------------------------------------------------------------- nişan
        [Test]
        public void Aim_StrafingTargetWorseThanStill()
        {
            var still = BotAimErrorModel.ConeDegrees(0.5f, 30f, 0f, 0f, false, 3f, 0f, false);
            var strafe = BotAimErrorModel.ConeDegrees(0.5f, 30f, 6f, 0f, false, 3f, 0f, false);
            Assert.Greater(strafe, still * 1.15f);
        }

        [Test]
        public void Aim_SettlesOverTime()
        {
            var early = BotAimErrorModel.ConeDegrees(0.5f, 30f, 1f, 0f, false, 0f, 0f, false);
            var late = BotAimErrorModel.ConeDegrees(0.5f, 30f, 1f, 0f, false, 4f, 0f, false);
            Assert.Greater(early, late * 1.5f);
        }

        [Test]
        public void Aim_SkilledBetterAndSuppressionWorse()
        {
            var rookie = BotAimErrorModel.ConeDegrees(0f, 40f, 1f, 0f, false, 2f, 0f, false);
            var elite = BotAimErrorModel.ConeDegrees(1f, 40f, 1f, 0f, false, 2f, 0f, false);
            Assert.Greater(rookie, elite);
            var pinned = BotAimErrorModel.ConeDegrees(1f, 40f, 1f, 0f, false, 2f, 1f, false);
            Assert.Greater(pinned, elite * 1.8f);
        }

        [Test]
        public void Aim_RunningWorseThanCrouchedStill()
        {
            Assert.Greater(BotAimErrorModel.SelfMotionMultiplier(6f, false), BotAimErrorModel.SelfMotionMultiplier(0f, true) * 1.8f);
        }

        [Test]
        public void Aim_ConeClamped()
        {
            var c = BotAimErrorModel.ConeDegrees(0f, 400f, 30f, 6f, false, 0f, 1f, true);
            Assert.AreEqual(BotAimErrorModel.MaxConeDegrees, c, 0.001f);
            var m = BotAimErrorModel.ConeDegrees(1f, 1f, 0f, 0f, true, 50f, 0f, false);
            Assert.GreaterOrEqual(m, BotAimErrorModel.MinConeDegrees);
        }

        [Test]
        public void Aim_SampleOffsetWithinCone()
        {
            for (var i = 0; i < 20; i++)
            {
                BotAimErrorModel.SampleOffset(5f, i / 20f, (i * 7 % 20) / 20f, out var yaw, out var pitch);
                Assert.Less(System.Math.Abs(yaw), 5.001f);
                Assert.Less(System.Math.Abs(pitch), 5.001f * 0.7f + 0.001f);
            }
        }

        [Test]
        public void Aim_LeadScalesWithSkill()
        {
            Assert.Greater(BotAimErrorModel.LeadSeconds(100f, 800f, 1f), BotAimErrorModel.LeadSeconds(100f, 800f, 0f));
        }

        [Test]
        public void Aim_DeliberateMiss_OnlyFirstShots()
        {
            Assert.Greater(BotAimErrorModel.DeliberateMissWeight(0, 0.5f), BotAimErrorModel.DeliberateMissWeight(2, 0.5f));
            Assert.AreEqual(0f, BotAimErrorModel.DeliberateMissWeight(5, 0.5f), 0.0001f);
            Assert.Greater(BotAimErrorModel.NearMissOffsetDegrees(10f, 1f), BotAimErrorModel.NearMissOffsetDegrees(10f, 0f));
        }

        // ---------------------------------------------------------------- kanat
        [Test]
        public void Flank_InFiringConeGeometry()
        {
            // Düşman orijinde +Z'ye bakıyor (yaw 0).
            Assert.IsTrue(FlankRoutePlanner.InFiringCone(0f, 20f, 0f, 0f, 0f, 30f, 50f));
            Assert.IsFalse(FlankRoutePlanner.InFiringCone(20f, 0f, 0f, 0f, 0f, 30f, 50f));
            Assert.IsFalse(FlankRoutePlanner.InFiringCone(0f, 80f, 0f, 0f, 0f, 30f, 50f));
            Assert.IsFalse(FlankRoutePlanner.InFiringCone(0f, -20f, 0f, 0f, 0f, 30f, 50f));
        }

        [Test]
        public void Flank_ArcPointKeepsRadiusAndAngle()
        {
            FlankRoutePlanner.ArcPoint(0f, 40f, 0f, 0f, 60f, 1f, 30f, out var x, out var z);
            Assert.AreEqual(30f, (float)System.Math.Sqrt(x * x + z * z), 0.01f);
            FlankRoutePlanner.ArcPoint(0f, 40f, 0f, 0f, 60f, -1f, 30f, out var x2, out _);
            Assert.Less(x * x2, 0f);
        }

        [Test]
        public void Flank_BehindFactor()
        {
            Assert.AreEqual(0f, FlankRoutePlanner.BehindFactor(0f, 10f, 0f, 0f, 0f), 0.001f);
            Assert.AreEqual(1f, FlankRoutePlanner.BehindFactor(0f, -10f, 0f, 0f, 0f), 0.001f);
        }

        [Test]
        public void Flank_BlockedAndTooLongRejected()
        {
            Assert.AreEqual(float.MaxValue, FlankRoutePlanner.Score(30f, 0f, 0f, 0, 100f, true));
            Assert.AreEqual(float.MaxValue, FlankRoutePlanner.Score(300f, 0f, 0f, 0, 100f, false));
            Assert.Less(FlankRoutePlanner.Score(60f, 0f, 1f, 0, 100f, false), FlankRoutePlanner.Score(60f, 0f, 0f, 0, 100f, false));
        }

        [Test]
        public void Flank_TryChoose_AvoidsAllySideAndBlocked()
        {
            var angles = new[] { 45f, 80f };
            // Bot (0,40) düşman (0,0) yaw 180: düşman bota arkasını dönük -> bot ateş konisi dışında; yön seçimini dost sayısı belirler.
            var ok = FlankRoutePlanner.TryChoose(0f, 40f, 0f, 0f, 0f, 35f, 60f, 35f, angles, 2, 0, 200f, 0UL,
                out _, out var side, out _, out _);
            Assert.IsTrue(ok);
            Assert.AreEqual(1f, side, 0.001f); // sol tarafta 2 dost var -> sağ
            // Sağ yöndeki iki aday kapalı -> sol seçilir.
            ulong mask = (1UL << 1) | (1UL << 3);
            ok = FlankRoutePlanner.TryChoose(0f, 40f, 0f, 0f, 0f, 35f, 60f, 35f, angles, 2, 0, 200f, mask,
                out _, out side, out _, out _);
            Assert.IsTrue(ok);
            Assert.AreEqual(-1f, side, 0.001f);
            // Hepsi kapalı -> bulunamaz.
            Assert.IsFalse(FlankRoutePlanner.TryChoose(0f, 40f, 0f, 0f, 0f, 35f, 60f, 35f, angles, 0, 0, 200f, 0xFUL,
                out _, out _, out _, out _));
            Assert.IsFalse(FlankRoutePlanner.TryChoose(0f, 40f, 0f, 0f, 0f, 35f, 60f, 35f, null, 0, 0, 200f, 0UL,
                out _, out _, out _, out _));
        }

        [Test]
        public void Flank_CommitAndAbort()
        {
            Assert.IsTrue(FlankRoutePlanner.ShouldCommitFire(30f, 50f, true));
            Assert.IsFalse(FlankRoutePlanner.ShouldCommitFire(30f, 50f, false));
            Assert.IsFalse(FlankRoutePlanner.ShouldCommitFire(30f, 20f, true));
            Assert.IsTrue(FlankRoutePlanner.ShouldAbort(0.9f, 1f, false, 1f, 30f));
            Assert.IsTrue(FlankRoutePlanner.ShouldAbort(0f, 1f, true, 1f, 30f));
            Assert.IsFalse(FlankRoutePlanner.ShouldAbort(0.1f, 1f, false, 5f, 30f));
        }

        // ---------------------------------------------------------------- peek
        [Test]
        public void Peek_StyleByContext()
        {
            Assert.AreEqual(PeekStyle.Shoulder, BotPeekPlanner.ChooseStyle(true, 0f, 0.5f, false, 0, 0.05f, 0.5f));
            Assert.AreEqual(PeekStyle.BlindFire, BotPeekPlanner.ChooseStyle(true, 0.9f, 0.5f, false, 0, 1f, 0.1f));
            Assert.AreEqual(PeekStyle.Shoulder, BotPeekPlanner.ChooseStyle(true, 0.1f, 0.5f, false, 3, 1f, 0.1f));
            Assert.AreEqual(PeekStyle.WidePie, BotPeekPlanner.ChooseStyle(false, 0f, 0.9f, false, 0, 1f, 0.1f));
            Assert.AreEqual(PeekStyle.PreAim, BotPeekPlanner.ChooseStyle(true, 0f, 0.9f, true, 0, 1f, 0.1f));
        }

        [Test]
        public void Peek_ExposureOrdering()
        {
            var sh = BotPeekPlanner.ExposureSeconds(PeekStyle.Shoulder, 0.5f, 0.5f);
            var lf = BotPeekPlanner.ExposureSeconds(PeekStyle.LeanFire, 0.5f, 0.5f);
            Assert.Less(sh, 0.5f);
            Assert.Greater(lf, sh * 2f);
            Assert.AreEqual(0f, BotPeekPlanner.AccuracyMultiplier(PeekStyle.Shoulder), 0.0001f);
            Assert.Less(BotPeekPlanner.BodyExposure(PeekStyle.BlindFire), BotPeekPlanner.BodyExposure(PeekStyle.PreAim));
        }

        [Test]
        public void Peek_HiddenGrowsWithRepeats()
        {
            Assert.Greater(BotPeekPlanner.HiddenSeconds(3, 0f, 0.5f, 0.5f), BotPeekPlanner.HiddenSeconds(0, 0f, 0.5f, 0.5f));
            Assert.Greater(BotPeekPlanner.HiddenSeconds(0, 1f, 0.5f, 0.5f), BotPeekPlanner.HiddenSeconds(0, 0f, 0.5f, 0.5f));
        }

        [Test]
        public void Peek_RelocateAndEdgeChoice()
        {
            Assert.IsTrue(BotPeekPlanner.ShouldRelocate(3, false, 0f));
            Assert.IsTrue(BotPeekPlanner.ShouldRelocate(0, true, 0f));
            Assert.IsFalse(BotPeekPlanner.ShouldRelocate(1, false, 0.2f));
            Assert.AreEqual(-1, BotPeekPlanner.ChooseEdge(true, false, 0, true));
            Assert.AreEqual(1, BotPeekPlanner.ChooseEdge(true, true, -1, true));
            Assert.AreEqual(1, BotPeekPlanner.ChooseEdge(true, true, 0, true));
            Assert.AreEqual(0, BotPeekPlanner.ChooseEdge(false, false, 0, true));
        }

        // ---------------------------------------------------------------- bomba
        [Test]
        public void Grenade_LethalityFalloff()
        {
            Assert.AreEqual(1f, BotGrenadeTactics.Lethality(1f, false, false), 0.001f);
            Assert.Less(BotGrenadeTactics.Lethality(6f, false, false), BotGrenadeTactics.Lethality(3f, false, false));
            Assert.AreEqual(0f, BotGrenadeTactics.Lethality(10f, false, false), 0.001f);
            Assert.Less(BotGrenadeTactics.Lethality(3f, true, false), BotGrenadeTactics.Lethality(3f, false, false));
        }

        [Test]
        public void Grenade_IntentSelection()
        {
            Assert.AreEqual(GrenadeIntent.Smoke, BotGrenadeTactics.ChooseIntent(true, true, 20f, true, false, 30f, 0f, true, 0.5f));
            Assert.AreEqual(GrenadeIntent.Clear, BotGrenadeTactics.ChooseIntent(true, false, 8f, true, true, 30f, 0f, false, 0.5f));
            Assert.AreEqual(GrenadeIntent.Flush, BotGrenadeTactics.ChooseIntent(true, false, 20f, true, false, 30f, 5f, false, 0.2f));
            Assert.AreEqual(GrenadeIntent.None, BotGrenadeTactics.ChooseIntent(true, false, 20f, true, false, 3f, 5f, false, 0.9f));
            Assert.AreEqual(GrenadeIntent.None, BotGrenadeTactics.ChooseIntent(false, false, 20f, true, false, 30f, 5f, false, 0.9f));
        }

        [Test]
        public void Grenade_CookRespectsFuse()
        {
            var cook = BotGrenadeTactics.CookSeconds(GrenadeIntent.Flush, 1.2f, 1f);
            Assert.LessOrEqual(cook + 1.2f, BotGrenadeTactics.FuseSeconds - 0.49f);
            Assert.AreEqual(0f, BotGrenadeTactics.CookSeconds(GrenadeIntent.Smoke, 1f, 1f), 0.0001f);
            Assert.AreEqual(0f, BotGrenadeTactics.CookSeconds(GrenadeIntent.Flush, 4f, 1f), 0.0001f);
        }

        [Test]
        public void Grenade_DodgeDecisions()
        {
            Assert.AreEqual(BotGrenadeTactics.DodgeAction.Ignore, BotGrenadeTactics.DecideDodge(30f, 3f, 0.3f, true, false, 0.5f));
            Assert.AreEqual(BotGrenadeTactics.DodgeAction.Flee, BotGrenadeTactics.DecideDodge(5f, 3f, 0.3f, true, false, 0.5f));
            Assert.AreEqual(BotGrenadeTactics.DodgeAction.DropProne, BotGrenadeTactics.DecideDodge(5f, 0.3f, 0.3f, true, false, 0.5f));
            Assert.AreEqual(BotGrenadeTactics.DodgeAction.KickBack, BotGrenadeTactics.DecideDodge(1.5f, 3f, 0.3f, true, false, 0.9f));
            Assert.AreEqual(BotGrenadeTactics.DodgeAction.Ignore, BotGrenadeTactics.DecideDodge(6f, 3f, 0.3f, true, true, 0.5f));
        }

        [Test]
        public void Grenade_EscapeMath()
        {
            Assert.IsTrue(BotGrenadeTactics.CanOutrun(5f, 2.5f, 6f));
            Assert.IsFalse(BotGrenadeTactics.CanOutrun(2f, 0.6f, 6f));
            Assert.Greater(BotGrenadeTactics.DodgeReactionSeconds(0f, 100f, 1f), BotGrenadeTactics.DodgeReactionSeconds(1f, 0f, 0f));
            Assert.IsTrue(BotGrenadeTactics.ShouldBounceThrow(true, true, 15f));
            Assert.IsFalse(BotGrenadeTactics.ShouldBounceThrow(false, true, 15f));
            Assert.Greater(BotGrenadeTactics.FollowUpPushDelay(GrenadeIntent.Smoke), BotGrenadeTactics.FollowUpPushDelay(GrenadeIntent.Clear));
        }

        [Test]
        public void Grenade_HoldAfterThrowIncludesFuse()
        {
            Assert.Greater(BotGrenadeTactics.HoldAfterThrow(20f, 1f, 1.2f), 1.4f);
            Assert.Greater(BotGrenadeTactics.HoldAfterThrow(10f, 1f, 1.2f), BotGrenadeTactics.HoldAfterThrow(20f, 1f, 1.2f));
        }

        // ---------------------------------------------------------------- takım
        [Test]
        public void Squad_RoleAssignment()
        {
            Assert.AreEqual(SquadRole.Anchor, BotSquadCoordinator.AssignRole(0, true, 0, 2));
            Assert.AreEqual(SquadRole.Suppressor, BotSquadCoordinator.AssignRole(3, true, 0, 2));
            Assert.AreEqual(SquadRole.Pointman, BotSquadCoordinator.AssignRole(1, false, 0, 2));
            Assert.AreEqual(SquadRole.Flanker, BotSquadCoordinator.AssignRole(2, false, 0, 2));
            Assert.AreEqual(SquadRole.Support, BotSquadCoordinator.AssignRole(3, false, 0, 2));
            Assert.AreEqual(SquadRole.Support, BotSquadCoordinator.AssignRole(5, true, 2, 2));
        }

        [Test]
        public void Squad_FocusFirePenalizesStacking()
        {
            var fresh = BotSquadCoordinator.TargetScore(30f, false, true, 0, 1f, false);
            var stacked = BotSquadCoordinator.TargetScore(30f, false, true, 3, 1f, false);
            Assert.Greater(fresh, stacked + 50f);
            Assert.Greater(BotSquadCoordinator.TargetScore(30f, true, true, 0, 1f, false), fresh);
            Assert.IsFalse(BotSquadCoordinator.MayEngageTarget(3, false, 40f));
            Assert.IsTrue(BotSquadCoordinator.MayEngageTarget(3, true, 40f));
            Assert.IsTrue(BotSquadCoordinator.MayEngageTarget(5, false, 8f));
        }

        [Test]
        public void Squad_CalloutThrottle()
        {
            Assert.IsFalse(BotSquadCoordinator.MayCallout(10f, 9f, false, 0));
            Assert.IsTrue(BotSquadCoordinator.MayCallout(10f, 6f, false, 0));
            Assert.IsTrue(BotSquadCoordinator.MayCallout(10f, 8.5f, true, 0));
            Assert.IsFalse(BotSquadCoordinator.MayCallout(100f, 0f, false, 2));
        }

        [Test]
        public void Squad_FireAndMove()
        {
            Assert.IsFalse(BotSquadCoordinator.MayMove(0, 0, 6));
            Assert.IsTrue(BotSquadCoordinator.MayMove(1, 2, 6));
            Assert.IsFalse(BotSquadCoordinator.MayMove(3, 2, 6));
            Assert.IsTrue(BotSquadCoordinator.MayMove(0, 0, 1));
        }

        [Test]
        public void Squad_MoraleAndRoleBias()
        {
            Assert.AreEqual(1f, BotSquadCoordinator.MoraleMultiplier(10, 10, 0f), 0.001f);
            Assert.Less(BotSquadCoordinator.MoraleMultiplier(3, 10, 0f), BotSquadCoordinator.MoraleMultiplier(3, 10, 30f));
            Assert.GreaterOrEqual(BotSquadCoordinator.MoraleMultiplier(0, 10, 0f), 0.5f);
            BotSquadCoordinator.RoleBias(SquadRole.Flanker, out _, out var flank, out _, out _);
            BotSquadCoordinator.RoleBias(SquadRole.Anchor, out _, out var flankAnchor, out _, out _);
            Assert.Greater(flank, flankAnchor);
        }

        [Test]
        public void Squad_FriendlyLineOfFire()
        {
            Assert.IsTrue(BotSquadCoordinator.LineOfFireBlockedByAlly(40f, 15f, 2f));
            Assert.IsFalse(BotSquadCoordinator.LineOfFireBlockedByAlly(40f, 15f, 30f));
            Assert.IsFalse(BotSquadCoordinator.LineOfFireBlockedByAlly(40f, 50f, 0f));
        }

        // ---------------------------------------------------------------- siper-siper
        [Test]
        public void CoverHop_ScoringRules()
        {
            Assert.AreEqual(float.MinValue, BotCoverHopPlanner.Score(2f, 5f, 0f, 10f, false, 0f, false));
            Assert.AreEqual(float.MinValue, BotCoverHopPlanner.Score(30f, 5f, 0f, 10f, false, 0f, false));
            var safe = BotCoverHopPlanner.Score(8f, 8f, 0.1f, 10f, false, 0f, false);
            var exposed = BotCoverHopPlanner.Score(8f, 8f, 0.9f, 10f, false, 0f, false);
            Assert.Greater(safe, exposed);
            var covered = BotCoverHopPlanner.Score(8f, 8f, 0.9f, 10f, false, 0f, true);
            Assert.Greater(covered, exposed);
            Assert.Greater(BotCoverHopPlanner.Score(8f, 8f, 0.1f, 10f, false, 0f, false), BotCoverHopPlanner.Score(8f, 8f, 0.1f, 1f, false, 0f, false));
            Assert.Greater(BotCoverHopPlanner.Score(8f, 8f, 0.1f, 10f, false, 0f, false), BotCoverHopPlanner.Score(8f, 8f, 0.1f, 10f, false, 0.9f, false));
        }

        [Test]
        public void CoverHop_Timing()
        {
            Assert.AreEqual(2f, BotCoverHopPlanner.HopSeconds(10f, 5f), 0.001f);
            Assert.IsFalse(BotCoverHopPlanner.ShouldHop(true, 0f, 0.5f, 1f, false));
            Assert.IsTrue(BotCoverHopPlanner.ShouldHop(true, 0.2f, 2f, 1f, false));
            Assert.IsFalse(BotCoverHopPlanner.ShouldHop(false, 0.9f, 3f, 1f, true));
            Assert.IsFalse(BotCoverHopPlanner.ShouldHop(true, 0.2f, 2f, 0.2f, false));
            Assert.IsTrue(BotCoverHopPlanner.ShouldHop(false, 0.2f, 2f, 1f, true));
        }

        [Test]
        public void CoverHop_AbortAndSprint()
        {
            Assert.IsTrue(BotCoverHopPlanner.ShouldAbortHop(0.2f, 0.3f, 0f));
            Assert.IsFalse(BotCoverHopPlanner.ShouldAbortHop(0.8f, 0.5f, 1f));
            Assert.IsFalse(BotCoverHopPlanner.UseSprint(12f, 0.1f, true));
            Assert.IsTrue(BotCoverHopPlanner.UseSprint(6f, 0.1f, true));
            Assert.IsFalse(BotCoverHopPlanner.UseSprint(6f, 0.8f, false));
        }
    }
}
#endif
