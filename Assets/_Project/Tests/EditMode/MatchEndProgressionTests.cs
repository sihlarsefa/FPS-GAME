using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class MatchEndProgressionTests
    {
        [Test]
        public void PodiumClass_WinnerAndTopThree()
        {
            Assert.AreEqual(1, MatchEndProgression.PodiumClass(5, true));
            Assert.AreEqual(2, MatchEndProgression.PodiumClass(2, false));
            Assert.AreEqual(3, MatchEndProgression.PodiumClass(3, false));
            Assert.AreEqual(0, MatchEndProgression.PodiumClass(4, false));
            Assert.AreEqual(0, MatchEndProgression.PodiumClass(0, false));
        }

        [Test]
        public void TiersCrossed_ListsEachTierAndCapsAtMax()
        {
            var t = MatchEndProgression.TiersCrossed(550, 1300, 600, 50);
            CollectionAssert.AreEqual(new[] { 1, 2 }, t);
            Assert.AreEqual(0, MatchEndProgression.TiersCrossed(10, 20, 600, 50).Count);
            Assert.AreEqual(0, MatchEndProgression.TiersCrossed(30000, 31000, 600, 50).Count);
        }

        [Test]
        public void CountT_ClampsAndEaseIsMonotonic()
        {
            Assert.AreEqual(0f, MatchEndProgression.CountT(0.1f, 0.5f));
            Assert.AreEqual(1f, MatchEndProgression.CountT(5f, 0.5f));
            Assert.AreEqual(0.5f, MatchEndProgression.CountT(0.8f, 0.5f), 1e-4f);
            Assert.Less(MatchEndProgression.EaseOut(0.3f), MatchEndProgression.EaseOut(0.6f));
            Assert.AreEqual(1f, MatchEndProgression.EaseOut(2f));
        }

        [Test]
        public void CareerUnlocks_OnlyCrossedCareerXpItems()
        {
            var items = new[]
            {
                new CosmeticDefinition { id = "a", slot = "beret", unlockMethod = CosmeticsService.MethodCareerXp, unlockXp = 500 },
                new CosmeticDefinition { id = "b", slot = "beret", unlockMethod = CosmeticsService.MethodCareerXp, unlockXp = 100 },
                new CosmeticDefinition { id = "c", slot = "beret", unlockMethod = CosmeticsService.MethodSeasonTrack, unlockXp = 500 }
            };
            var r = MatchEndProgression.CareerUnlocks(items, 200, 600);
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual("a", r[0].id);
        }
    }
}
