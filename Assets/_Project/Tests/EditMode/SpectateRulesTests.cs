using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Application.Spectate;

namespace Project.Tests
{
    public sealed class SpectateRulesTests
    {
        private static List<SpectatorCandidate> Sample() => new List<SpectatorCandidate>
        {
            new SpectatorCandidate(1, 0, false),
            new SpectatorCandidate(2, 1, true),
            new SpectatorCandidate(3, 0, true),
            new SpectatorCandidate(4, 0, false),
            new SpectatorCandidate(5, 0, true),
        };

        [Test]
        public void TeamOrder_OnlyAliveTeammates()
        {
            var order = new List<int>();
            SpectateRules.BuildTeamOrder(Sample(), 0, 1, order);
            Assert.AreEqual("3,5", string.Join(",", order));
        }

        [Test]
        public void CanSpectate_RejectsEnemyDeadSelf()
        {
            Assert.IsTrue(SpectateRules.CanSpectate(new SpectatorCandidate(3, 0, true), 0, 1));
            Assert.IsTrue(!SpectateRules.CanSpectate(new SpectatorCandidate(2, 1, true), 0, 1));
            Assert.IsTrue(!SpectateRules.CanSpectate(new SpectatorCandidate(4, 0, false), 0, 1));
            Assert.IsTrue(!SpectateRules.CanSpectate(new SpectatorCandidate(1, 0, true), 0, 1));
        }

        [Test]
        public void Resolve_FollowsKillerThenTeam()
        {
            var order = new List<int> { 3, 5 };
            Assert.AreEqual(2, SpectateRules.Resolve(order, -1, true, 2, true));
            Assert.AreEqual(3, SpectateRules.Resolve(order, 2, true, 2, false));
            Assert.AreEqual(5, SpectateRules.Resolve(order, 5, false, 2, true));
            Assert.AreEqual(-1, SpectateRules.Resolve(new List<int>(), 2, false, 2, true));
        }
    }
}
