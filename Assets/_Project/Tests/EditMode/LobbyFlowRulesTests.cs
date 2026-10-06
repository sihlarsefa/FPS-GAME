using NUnit.Framework;
using Project.Presentation.UI.Lobby;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class LobbyFlowRulesTests
    {
        [Test]
        public void Phases_FollowTimeline()
        {
            Assert.AreEqual(LobbyPhase.Gather, LobbyFlowRules.PhaseAt(0f));
            Assert.AreEqual(LobbyPhase.Briefing, LobbyFlowRules.PhaseAt(7f));
            Assert.AreEqual(LobbyPhase.Countdown, LobbyFlowRules.PhaseAt(11f));
            Assert.AreEqual(LobbyPhase.Done, LobbyFlowRules.PhaseAt(16f));
            Assert.AreEqual(16f, LobbyFlowRules.Duration, 0.0001f);
        }

        [Test]
        public void JoinedCount_GrowsOneByOneToNine()
        {
            Assert.AreEqual(0, LobbyFlowRules.JoinedCount(0.2f));
            Assert.AreEqual(1, LobbyFlowRules.JoinedCount(0.5f));
            Assert.AreEqual(2, LobbyFlowRules.JoinedCount(1.2f));
            Assert.AreEqual(9, LobbyFlowRules.JoinedCount(6.9f));
            Assert.AreEqual(9, LobbyFlowRules.JoinedCount(100f));
            Assert.Less(LobbyFlowRules.JoinTime(8) + LobbyFlowRules.CardSeconds, LobbyFlowRules.GatherEnd);
        }

        [Test]
        public void CardPop_StartsZeroEndsOne()
        {
            Assert.AreEqual(0f, LobbyFlowRules.CardPop(0f, 3), 0.0001f);
            Assert.AreEqual(1f, LobbyFlowRules.CardPop(10f, 3), 0.0001f);
        }

        [Test]
        public void CountdownNumber_FiveToOneThenZero()
        {
            Assert.AreEqual(0, LobbyFlowRules.CountdownNumber(10.9f));
            Assert.AreEqual(5, LobbyFlowRules.CountdownNumber(11.01f));
            Assert.AreEqual(4, LobbyFlowRules.CountdownNumber(12.5f));
            Assert.AreEqual(1, LobbyFlowRules.CountdownNumber(15.5f));
            Assert.AreEqual(0, LobbyFlowRules.CountdownNumber(16f));
        }

        [Test]
        public void Tick_FiresOncePerSecondInCountdown()
        {
            var ticks = 0;
            var prev = 0f;
            for (var t = 0f; t <= 17f; t += 0.02f)
            {
                if (LobbyFlowRules.CountdownTickCrossed(prev, t)) ticks++;
                prev = t;
            }
            Assert.AreEqual(5, ticks);
        }

        [Test]
        public void Skip_JumpsToNextBoundary()
        {
            Assert.AreEqual(7f, LobbyFlowRules.SkipTarget(2f), 0.0001f);
            Assert.AreEqual(11f, LobbyFlowRules.SkipTarget(8f), 0.0001f);
            Assert.AreEqual(16f, LobbyFlowRules.SkipTarget(13f), 0.0001f);
            Assert.IsTrue(LobbyFlowRules.IsDone(LobbyFlowRules.SkipTarget(13f)));
        }

        [Test]
        public void PhaseAlpha_FadesInAndGatherFadesOut()
        {
            Assert.AreEqual(0f, LobbyFlowRules.PhaseAlpha(0f, LobbyPhase.Gather), 0.0001f);
            Assert.AreEqual(1f, LobbyFlowRules.PhaseAlpha(3f, LobbyPhase.Gather), 0.0001f);
            Assert.AreEqual(0f, LobbyFlowRules.PhaseAlpha(7f, LobbyPhase.Gather), 0.0001f);
            Assert.AreEqual(1f, LobbyFlowRules.PhaseAlpha(12f, LobbyPhase.Briefing), 0.0001f);
        }

        [Test]
        public void BulletAlpha_RevealsInOrder()
        {
            Assert.AreEqual(0f, LobbyFlowRules.BulletAlpha(7f, 0), 0.0001f);
            Assert.AreEqual(1f, LobbyFlowRules.BulletAlpha(10f, 2), 0.0001f);
            Assert.Greater(LobbyFlowRules.BulletAlpha(8.2f, 0), LobbyFlowRules.BulletAlpha(8.2f, 2));
        }
    }
}
