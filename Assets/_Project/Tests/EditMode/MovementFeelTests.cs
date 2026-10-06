#if UNITY_EDITOR
using NUnit.Framework;
using Project.Application.Movement;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class MovementFeelTests
    {
        [Test]
        public void SprintToFire_BlocksDuringSprint_ThenReadyAfterRecover()
        {
            var t = new SprintToFireTimer();
            for (var i = 0; i < 10; i++) t.Tick(0.1f, true, 3.5f, 0f);
            Assert.IsFalse(t.CanFire);
            t.Tick(0.01f, false, 3.5f, 0f);
            Assert.IsFalse(t.CanFire);
            for (var i = 0; i < 100; i++) t.Tick(0.01f, false, 3.5f, 0f);
            Assert.IsTrue(t.CanFire);
            Assert.AreEqual(1f, t.ReadyProgress, 1e-3f);
        }

        [Test]
        public void SprintToFire_HeavyWeaponAndTacticalAreSlower()
        {
            var light = SprintToFireTimer.RecoverSeconds(2.5f, 0.5f, SprintExitKind.Sprint, 0f);
            var heavy = SprintToFireTimer.RecoverSeconds(7f, 0.5f, SprintExitKind.Sprint, 0f);
            var tactical = SprintToFireTimer.RecoverSeconds(3.5f, 4f, SprintExitKind.Sprint, 0f);
            var normal = SprintToFireTimer.RecoverSeconds(3.5f, 0.5f, SprintExitKind.Sprint, 0f);
            Assert.Greater(heavy, light);
            Assert.Greater(tactical, normal * 1.4f);
            Assert.AreEqual(0.22f, normal, 0.005f);
        }

        [Test]
        public void SprintToFire_SlideExitIsFasterThanHardLanding()
        {
            Assert.Less(SprintToFireTimer.RecoverSeconds(3.5f, 0f, SprintExitKind.Slide, 0f),
                SprintToFireTimer.RecoverSeconds(3.5f, 0f, SprintExitKind.HardLanding, 0f));
        }

        [Test]
        public void SprintToFire_ResumedSprintResetsRemaining()
        {
            var t = new SprintToFireTimer();
            t.Tick(1f, true, 3.5f, 0f);
            t.Tick(0.05f, false, 3.5f, 0f);
            Assert.Greater(t.Remaining, 0f);
            t.Tick(0.05f, true, 3.5f, 0f);
            Assert.AreEqual(0f, t.ReadyProgress, 1e-4f);
        }

        [Test]
        public void Ads_FullSpeedFactorsOrdered()
        {
            var stand = AdsMovementRules.FullAdsSpeedFactor(0, 3.5f, 1f);
            var crouch = AdsMovementRules.FullAdsSpeedFactor(1, 3.5f, 1f);
            var prone = AdsMovementRules.FullAdsSpeedFactor(2, 3.5f, 1f);
            Assert.AreEqual(0.64f, stand, 0.01f);
            Assert.Greater(crouch, stand);
            Assert.Greater(prone, crouch);
            Assert.Less(AdsMovementRules.FullAdsSpeedFactor(0, 3.5f, 6f), stand);
            Assert.Less(AdsMovementRules.FullAdsSpeedFactor(0, 8f, 1f), stand);
        }

        [Test]
        public void Ads_SpeedBlendsWithProgress()
        {
            Assert.AreEqual(1f, AdsMovementRules.SpeedFactor(0f, 0, 3.5f, 1f), 1e-4f);
            var half = AdsMovementRules.SpeedFactor(0.5f, 0, 3.5f, 1f);
            var full = AdsMovementRules.SpeedFactor(1f, 0, 3.5f, 1f);
            Assert.Greater(half, full);
            Assert.Less(half, 1f);
            Assert.IsFalse(AdsMovementRules.SprintAllowed(0.5f));
            Assert.IsTrue(AdsMovementRules.SprintAllowed(0.05f));
        }

        [Test]
        public void AirAccuracy_JumpRaisesSpread_LandingRecovers()
        {
            var m = new AirAccuracyModel();
            m.Tick(0.016f, true, 0f);
            Assert.AreEqual(1f, m.SpreadMultiplier, 1e-4f);
            for (var i = 0; i < 20; i++) m.Tick(0.03f, false, 0f);
            Assert.AreEqual(AirAccuracyModel.AirborneSpread, m.SpreadMultiplier, 0.01f);
            m.Tick(0.016f, true, 6f);
            Assert.Greater(m.SpreadMultiplier, 1.5f);
            for (var i = 0; i < 100; i++) m.Tick(0.02f, true, 0f);
            Assert.AreEqual(1f, m.SpreadMultiplier, 1e-3f);
        }

        [Test]
        public void AirAccuracy_HardLandingRecoversLonger_ShortHopNoPenalty()
        {
            Assert.Greater(AirAccuracyModel.RecoverSeconds(12f), AirAccuracyModel.RecoverSeconds(4f));
            Assert.Greater(AirAccuracyModel.LandingPeak(12f), AirAccuracyModel.LandingPeak(4f));
            var m = new AirAccuracyModel();
            m.Tick(0.016f, true, 0f);
            m.Tick(0.05f, false, 0f);
            m.Tick(0.016f, true, 3f);
            Assert.AreEqual(1f, m.SpreadMultiplier, 1e-4f);
        }

        [Test]
        public void Slide_FlatDecaysAndEndsWithinMax()
        {
            var s = new SlideModel();
            s.Begin(7.2f, 7.2f, 1.08f, 1f);
            Assert.Greater(s.Speed, 7f);
            var t = 0f;
            while (s.Tick(0.02f, 0f, 0f) && t < 5f) t += 0.02f;
            Assert.IsFalse(s.Active);
            Assert.Less(t, SlideModel.MaxSecondsFlat + 0.05f);
            Assert.Greater(t, 0.6f);
        }

        [Test]
        public void Slide_DownhillLastsLongerThanUphill()
        {
            var down = RunSlide(25f, 1f);
            var up = RunSlide(25f, -1f);
            Assert.Greater(down, up + 0.3f);
        }

        private static float RunSlide(float slope, float dot)
        {
            var s = new SlideModel();
            s.Begin(7.2f, 7.2f, 1.08f, 1f);
            var t = 0f;
            while (s.Tick(0.02f, slope, dot) && t < 6f) t += 0.02f;
            return t;
        }

        [Test]
        public void Slide_SpeedNeverExceedsCap()
        {
            var s = new SlideModel();
            s.Begin(7.2f, 7.2f, 1.08f, 1f);
            for (var i = 0; i < 40; i++) s.Tick(0.02f, 45f, 1f);
            Assert.IsTrue(s.Speed <= 7.2f * SlideModel.SpeedCapFactor + 1e-3f);
        }

        [Test]
        public void Mantle_ClassifiesByHeight()
        {
            Assert.AreEqual(MantleKind.Vault, MantleRules.Classify(0.6f));
            Assert.AreEqual(MantleKind.Mantle, MantleRules.Classify(1.0f));
            Assert.AreEqual(MantleKind.Climb, MantleRules.Classify(1.3f));
        }

        [Test]
        public void Mantle_RunningIsFasterAndVaultKeepsMomentum()
        {
            var still = MantleRules.Plan(0.7f, 0f, 7.2f, 0f, false);
            var run = MantleRules.Plan(0.7f, 7.2f, 7.2f, 0f, false);
            Assert.Less(run.Duration, still.Duration);
            Assert.Greater(run.ExitSpeed, 2f);
            Assert.AreEqual(0f, still.ExitSpeed, 1e-4f);
            var climb = MantleRules.Plan(1.3f, 0f, 7.2f, 0f, false);
            Assert.Greater(climb.Duration, run.Duration);
            Assert.Greater(climb.StaminaCost, run.StaminaCost);
        }

        [Test]
        public void Mantle_LoadAndExhaustionSlow()
        {
            var a = MantleRules.Plan(1.0f, 3f, 7.2f, 0f, false).Duration;
            var b = MantleRules.Plan(1.0f, 3f, 7.2f, 1f, true).Duration;
            Assert.Greater(b, a * 1.4f);
        }

        [Test]
        public void Lean_ScalesWithStance()
        {
            Assert.AreEqual(0f, LeanRules.Offset(0.35f, 2, 0f), 1e-5f);
            Assert.Less(LeanRules.Offset(0.35f, 1, 0f), LeanRules.Offset(0.35f, 0, 0f));
            Assert.Greater(LeanRules.Angle(12f, 0, 1f), LeanRules.Angle(12f, 0, 0f));
            Assert.AreEqual(0.88f, LeanRules.MoveSpeedFactor(1f), 1e-4f);
            Assert.AreEqual(0.88f, LeanRules.MoveSpeedFactor(-1f), 1e-4f);
        }

        [Test]
        public void Lean_ReturnIsFasterThanLeanOut()
        {
            Assert.Greater(LeanRules.Speed(5.5f, 0f, 0f, true), LeanRules.Speed(5.5f, 0f, 0f, false));
            Assert.Less(LeanRules.Speed(5.5f, 1f, 0f, false), LeanRules.Speed(5.5f, 0f, 0f, false));
        }

        [Test]
        public void Fatigue_BuildsWhenLow_DecaysWhenRested()
        {
            var f = new FatigueModel();
            for (var i = 0; i < 100; i++) f.Tick(0.1f, 0.1f);
            Assert.AreEqual(1f, f.Value, 1e-3f);
            Assert.AreEqual(1.35f, f.DrainMultiplier, 1e-3f);
            for (var i = 0; i < 100; i++) f.Tick(0.1f, 0.9f);
            Assert.AreEqual(0f, f.Value, 1e-3f);
        }

        [Test]
        public void BreathHold_ReducesSway_ThenWinds()
        {
            var b = new BreathHoldModel();
            b.Tick(0.1f, true, true, 1f);
            Assert.IsTrue(b.Holding);
            Assert.Less(b.SwayMultiplier, 0.5f);
            for (var i = 0; i < 60; i++) b.Tick(0.1f, true, true, 1f);
            Assert.IsFalse(b.Holding);
            Assert.IsTrue(b.Winded);
            Assert.Greater(b.SwayMultiplier, 1f);
        }

        [Test]
        public void BreathHold_LowStaminaShortensHold()
        {
            Assert.Less(BreathHoldModel.HoldLimit(0.2f), BreathHoldModel.HoldLimit(1f));
            Assert.AreEqual(4.5f, BreathHoldModel.HoldLimit(1f), 1e-3f);
        }
    }
}
#endif
