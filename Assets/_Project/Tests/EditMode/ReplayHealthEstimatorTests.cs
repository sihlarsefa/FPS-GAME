using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Replay;
using Project.Presentation.Replay;

namespace Project.Tests
{
    public sealed class ReplayHealthEstimatorTests
    {
        private static List<ReplayEvent> Events()
        {
            return new List<ReplayEvent>
            {
                new ReplayEvent { Time = 2f, Type = ReplayEventType.Hit, Actor = 1, Target = 2, Value = 30f },
                new ReplayEvent { Time = 4f, Type = ReplayEventType.Hit, Actor = 1, Target = 2, Value = 40f },
                new ReplayEvent { Time = 6f, Type = ReplayEventType.Death, Actor = 1, Target = 2 },
                new ReplayEvent { Time = 20f, Type = ReplayEventType.Death, Actor = 1, Target = 3 }
            };
        }

        [Test]
        public void FractionFollowsDamageAndDeath()
        {
            var h = new ReplayHealthEstimator(Events());
            Assert.AreEqual(1f, h.Fraction(2, 1f), 1e-4f);
            Assert.AreEqual(0.7f, h.Fraction(2, 3f), 1e-4f);
            Assert.AreEqual(0.3f, h.Fraction(2, 5f), 1e-4f);
            Assert.AreEqual(0f, h.Fraction(2, 7f), 1e-4f);
            Assert.AreEqual(1f, h.Fraction(9, 7f), 1e-4f);
        }

        [Test]
        public void NextKillSeekJumpsForwardAndBack()
        {
            var e = Events();
            Assert.AreEqual(3f, ReplayHealthEstimator.NextKillSeek(e, 0f, 3f, 1), 1e-4f);
            Assert.AreEqual(17f, ReplayHealthEstimator.NextKillSeek(e, 3f, 3f, 1), 1e-4f);
            Assert.AreEqual(-1f, ReplayHealthEstimator.NextKillSeek(e, 17f, 3f, 1), 1e-4f);
            Assert.AreEqual(3f, ReplayHealthEstimator.NextKillSeek(e, 10f, 3f, -1), 1e-4f);
        }
    }
}
