using NUnit.Framework;
using Project.Presentation.UI.Play;

namespace Project.Tests.EditMode
{
    public sealed class PlayFlowTests
    {
        private static void Run(MatchmakingFlow f, float seconds, float step = 0.25f)
        {
            for (var t = 0f; t < seconds; t += step) f.Tick(step);
        }

        private static void RunUntil(MatchmakingFlow f, MatchPhase phase, float max = 200f)
        {
            for (var t = 0f; t < max && f.Phase != phase; t += 0.25f) f.Tick(0.25f);
        }

        [Test]
        public void Demand_NightLongerThanEvening()
        {
            Assert.Greater(QueueEstimator.DemandMultiplier(3), QueueEstimator.DemandMultiplier(20));
            Assert.AreEqual(QueueEstimator.DemandMultiplier(-1), QueueEstimator.DemandMultiplier(23), 0.0001f);
        }

        [Test]
        public void Estimate_SoloShorterThanTeam()
        {
            var e = new QueueEstimator();
            Assert.Less(e.Estimate(PlayQueueKind.Solo, 20), e.Estimate(PlayQueueKind.TeamBr, 20));
            Assert.AreEqual(0f, e.Estimate(PlayQueueKind.Training, 20), 0.0001f);
        }

        [Test]
        public void Observe_PullsEstimateTowardMeasurement()
        {
            var e = new QueueEstimator();
            e.Observe(PlayQueueKind.Duo, 100f);
            Assert.AreEqual(100f * QueueEstimator.DemandMultiplier(15), e.Estimate(PlayQueueKind.Duo, 15), 0.01f);
            e.Observe(PlayQueueKind.Duo, 0f);
            Assert.Less(e.Estimate(PlayQueueKind.Duo, 15), 100f);
        }

        [Test]
        public void SampleWait_StaysInsideRange()
        {
            var e = new QueueEstimator();
            var lo = e.SampleWait(PlayQueueKind.TeamBr, 20, 0f);
            var hi = e.SampleWait(PlayQueueKind.TeamBr, 20, 1f);
            Assert.Greater(lo, e.Low(PlayQueueKind.TeamBr, 20) - 0.01f);
            Assert.Less(hi, e.High(PlayQueueKind.TeamBr, 20) + 0.01f);
        }

        [Test]
        public void FormatClockAndRange()
        {
            Assert.AreEqual("1:05", QueueEstimator.FormatClock(65.4f));
            Assert.AreEqual("0:00", QueueEstimator.FormatClock(-3f));
            Assert.AreEqual("ANINDA", new QueueEstimator().FormatRange(PlayQueueKind.Training, 10));
            Assert.IsTrue(new QueueEstimator().FormatRange(PlayQueueKind.Solo, 20).StartsWith("~"));
        }

        [Test]
        public void Progress_MonotonicAndBelowOne()
        {
            Assert.Less(QueueEstimator.Progress(10f, 40f), QueueEstimator.Progress(40f, 40f));
            Assert.Less(QueueEstimator.Progress(10000f, 40f), 1f);
            Assert.AreEqual(0f, QueueEstimator.Progress(0f, 40f), 0.0001f);
        }

        [Test]
        public void Suggestion_OnlyAfterThresholdAndTowardFullerQueue()
        {
            Assert.IsFalse(QueueEstimator.SuggestAlternative(PlayQueueKind.Solo, 30f).HasValue);
            Assert.AreEqual(PlayQueueKind.Duo, QueueEstimator.SuggestAlternative(PlayQueueKind.Solo, 70f).Value);
            Assert.AreEqual(PlayQueueKind.TeamBr, QueueEstimator.SuggestAlternative(PlayQueueKind.Duo, 70f).Value);
            Assert.IsFalse(QueueEstimator.SuggestAlternative(PlayQueueKind.TeamBr, 500f).HasValue);
        }

        [Test]
        public void NextQueued_WrapsAmongThree()
        {
            Assert.AreEqual(PlayQueueKind.Duo, PlayQueueModes.NextQueued(PlayQueueKind.TeamBr, 1));
            Assert.AreEqual(PlayQueueKind.Solo, PlayQueueModes.NextQueued(PlayQueueKind.TeamBr, -1));
            Assert.AreEqual(PlayQueueKind.TeamBr, PlayQueueModes.NextQueued(PlayQueueKind.Solo, 1));
        }

        [Test]
        public void Flow_SearchFindsMatchThenReadyCheck()
        {
            var f = new MatchmakingFlow(new QueueEstimator(), PlayQueueKind.Solo, 20, 1);
            f.Start();
            Assert.AreEqual(MatchPhase.Searching, f.Phase);
            RunUntil(f, MatchPhase.ReadyCheck);
            Assert.AreEqual(MatchPhase.ReadyCheck, f.Phase);
        }

        [Test]
        public void Flow_SoloAcceptStartsAndLaunchesOnce()
        {
            var f = new MatchmakingFlow(new QueueEstimator(), PlayQueueKind.Solo, 20, 2);
            var launches = 0;
            f.LaunchRequested += () => launches++;
            f.Start();
            RunUntil(f, MatchPhase.ReadyCheck);
            Assert.IsTrue(f.Accept());
            Assert.IsFalse(f.Accept());
            Run(f, 1f);
            Assert.AreEqual(MatchPhase.Starting, f.Phase);
            Run(f, 6f);
            Assert.AreEqual(1, launches);
            Assert.AreEqual(MatchPhase.Idle, f.Phase);
        }

        [Test]
        public void Flow_IgnoringReadyCheckTimesOut()
        {
            var f = new MatchmakingFlow(new QueueEstimator(), PlayQueueKind.Solo, 20, 3);
            f.Start();
            RunUntil(f, MatchPhase.ReadyCheck);
            Assert.AreEqual(MatchPhase.ReadyCheck, f.Phase);
            Run(f, MatchmakingFlow.ReadySeconds + 2f);
            Assert.AreEqual(MatchPhase.Cancelled, f.Phase);
            Assert.AreEqual(CancelReason.ReadyTimeout, f.Reason);
        }

        [Test]
        public void Flow_CancelWhileSearching_NoPenaltyReason()
        {
            var f = new MatchmakingFlow(new QueueEstimator(), PlayQueueKind.TeamBr, 20, 4);
            f.Start();
            Run(f, 1f);
            f.Cancel();
            Assert.AreEqual(MatchPhase.Cancelled, f.Phase);
            Assert.AreEqual(CancelReason.UserCancelled, f.Reason);
            Assert.IsFalse(f.Active);
        }

        [Test]
        public void Flow_TrainingLaunchesImmediately()
        {
            var f = new MatchmakingFlow(new QueueEstimator(), PlayQueueKind.Training, 20, 5);
            var launches = 0;
            f.LaunchRequested += () => launches++;
            f.Start();
            Assert.AreEqual(1, launches);
            Assert.IsFalse(f.Active);
        }

        [Test]
        public void Flow_TeamReadyCountNeverExceedsSquad()
        {
            var f = new MatchmakingFlow(new QueueEstimator(), PlayQueueKind.TeamBr, 20, 6);
            f.Start();
            Run(f, 120f);
            if (f.Phase == MatchPhase.ReadyCheck)
            {
                f.Accept();
                Run(f, 2f);
                Assert.LessOrEqual(f.ReadyCount, f.SquadSize);
                Assert.IsTrue(f.SlotReady(0));
            }
        }

        [Test]
        public void DodgePenalty_EscalatesAndDecays()
        {
            var p = new DodgePenalty();
            Assert.AreEqual(0f, p.Register(0f), 0.001f);
            Assert.AreEqual(30f, p.Register(10f), 0.001f);
            Assert.IsTrue(p.IsBlocked(20f));
            Assert.IsFalse(p.IsBlocked(60f));
            Assert.AreEqual(120f, p.Register(70f), 0.001f);
            Assert.AreEqual(0f, p.Register(70f + DodgePenalty.MemorySeconds + 50f), 0.001f);
        }
    }
}
