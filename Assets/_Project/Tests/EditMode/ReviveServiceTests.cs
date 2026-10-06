using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Tests.Match;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class ReviveServiceTests
    {
        private static PlayerId P(int i) => new PlayerId(i);

        [Test]
        public void TryDown_NoAliveAlly_ReturnsFalse()
        {
            var s = new ReviveService();
            Assert.IsFalse(s.TryDown(P(1), P(9), 0, 0));
            Assert.IsFalse(s.IsDowned(P(1)));
        }

        [Test]
        public void TryDown_PublishesEvent_AndTracks()
        {
            var bus = new MatchTestBus();
            var s = new ReviveService(bus);
            Assert.IsTrue(s.TryDown(P(1), P(9), 2, 1));
            Assert.IsTrue(s.IsDowned(P(1)));
            Assert.AreEqual(1, bus.Of<DownedEvent>().Count);
            Assert.IsFalse(s.TryDown(P(1), P(9), 2, 1));
        }

        [Test]
        public void BleedOut_After45Seconds()
        {
            var s = new ReviveService();
            PlayerId dead = PlayerId.Invalid;
            s.BledOut += (v, a) => dead = v;
            s.TryDown(P(1), P(9), 0, 1);
            s.Tick(44f);
            Assert.IsTrue(s.IsDowned(P(1)));
            s.Tick(1.1f);
            Assert.IsFalse(s.IsDowned(P(1)));
            Assert.AreEqual(P(1), dead);
        }

        [Test]
        public void Revive_Takes6Seconds_MedicTakes3()
        {
            var bus = new MatchTestBus();
            var s = new ReviveService(bus);
            s.TryDown(P(1), P(9), 0, 1);
            Assert.IsTrue(s.BeginRevive(P(2), TeamRole.Rifleman, P(1)));
            s.Tick(5.9f);
            Assert.IsTrue(s.IsDowned(P(1)));
            s.Tick(0.2f);
            Assert.IsFalse(s.IsDowned(P(1)));
            Assert.AreEqual(1, bus.Of<RevivedEvent>().Count);

            s.TryDown(P(3), P(9), 0, 1);
            s.BeginRevive(P(2), TeamRole.Medic, P(3));
            s.Tick(3.1f);
            Assert.IsFalse(s.IsDowned(P(3)));
        }

        [Test]
        public void Revive_PausesBleed_AndCancelResetsProgress()
        {
            var s = new ReviveService();
            s.TryDown(P(1), P(9), 0, 1);
            s.Tick(10f);
            s.BeginRevive(P(2), TeamRole.Rifleman, P(1));
            s.Tick(4f);
            Assert.AreEqual(35f, s.BleedRemaining(P(1)), 0.01f);
            Assert.AreEqual(4f / 6f, s.ReviveProgress(P(1)), 0.01f);
            s.CancelRevive(P(2));
            Assert.AreEqual(0f, s.ReviveProgress(P(1)));
        }

        [Test]
        public void SecondReviver_IsRejected()
        {
            var s = new ReviveService();
            s.TryDown(P(1), P(9), 0, 1);
            Assert.IsTrue(s.BeginRevive(P(2), TeamRole.Rifleman, P(1)));
            Assert.IsFalse(s.BeginRevive(P(3), TeamRole.Rifleman, P(1)));
            Assert.IsFalse(s.BeginRevive(P(1), TeamRole.Rifleman, P(1)));
        }

        [Test]
        public void MatchService_AllAliveDowned_EliminatesTeam()
        {
            var bus = new MatchTestBus();
            var m = new MatchService(new MatchConfig(), bus);
            m.AutoEndOnElimination = false;
            m.RegisterCombatant(P(1), "A", true, 0, TeamRole.Leader);
            m.RegisterCombatant(P(2), "B", false, 0, TeamRole.Rifleman);
            m.RegisterCombatant(P(3), "C", false, 1, TeamRole.Rifleman);
            bus.Publish(new DownedEvent(P(1), P(3), 0));
            Assert.IsTrue(m.IsAlive(P(1)));
            Assert.IsTrue(m.IsTeamAlive(0));
            bus.Publish(new DownedEvent(P(2), P(3), 0));
            Assert.IsFalse(m.IsAlive(P(1)));
            Assert.IsFalse(m.IsAlive(P(2)));
            Assert.IsFalse(m.IsTeamAlive(0));
            Assert.IsTrue(m.IsTeamAlive(1));
        }

        [Test]
        public void MatchService_RevivedMember_KeepsTeamAlive()
        {
            var bus = new MatchTestBus();
            var m = new MatchService(new MatchConfig(), bus);
            m.AutoEndOnElimination = false;
            m.RegisterCombatant(P(1), "A", true, 0, TeamRole.Leader);
            m.RegisterCombatant(P(2), "B", false, 0, TeamRole.Rifleman);
            m.RegisterCombatant(P(3), "C", false, 1, TeamRole.Rifleman);
            bus.Publish(new DownedEvent(P(1), P(3), 0));
            bus.Publish(new RevivedEvent(P(1), P(2)));
            bus.Publish(new DownedEvent(P(2), P(3), 0));
            Assert.IsTrue(m.IsTeamAlive(0));
        }
    }
}
