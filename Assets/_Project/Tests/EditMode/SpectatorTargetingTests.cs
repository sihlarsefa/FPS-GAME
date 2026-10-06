using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests
{
    public sealed class SpectatorTargetingTests
    {
        private static List<SpectatorCandidate> Sample() => new List<SpectatorCandidate>
        {
            new SpectatorCandidate(1, 0, false), // yerel
            new SpectatorCandidate(2, 1, true),
            new SpectatorCandidate(3, 0, true),
            new SpectatorCandidate(4, 0, false),
            new SpectatorCandidate(5, 0, true),
        };

        [Test]
        public void BuildOrder_TeammatesFirstThenOthers()
        {
            var order = new List<int>();
            SpectatorTargeting.BuildOrder(Sample(), 0, 1, order);
            Assert.AreEqual("3,5,2", string.Join(",", order));
        }

        [Test]
        public void Cycle_WrapsBothDirections()
        {
            var order = new List<int> { 3, 5, 2 };
            Assert.AreEqual(5, SpectatorTargeting.Cycle(order, 3, 1));
            Assert.AreEqual(3, SpectatorTargeting.Cycle(order, 2, 1));
            Assert.AreEqual(2, SpectatorTargeting.Cycle(order, 3, -1));
            Assert.AreEqual(3, SpectatorTargeting.Cycle(order, 99, 1));
        }

        [Test]
        public void EmptyOrder_ReturnsMinusOne()
        {
            var order = new List<int>();
            Assert.AreEqual(-1, SpectatorTargeting.Cycle(order, 3, 1));
            Assert.AreEqual(-1, SpectatorTargeting.Resolve(order, 3));
        }

        [Test]
        public void Resolve_KeepsCurrentOrFallsBack()
        {
            var order = new List<int> { 3, 5 };
            Assert.AreEqual(5, SpectatorTargeting.Resolve(order, 5));
            Assert.AreEqual(3, SpectatorTargeting.Resolve(order, 2));
        }
    }
}
