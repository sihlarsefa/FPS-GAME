using NUnit.Framework;
using Project.Presentation.Bootstrap;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class MatchIntroTimelineTests
    {
        [Test]
        public void ShotAt_SplitsEightSecondsIntoThreeShots()
        {
            Assert.AreEqual(0, MatchIntroTimeline.ShotAt(0f));
            Assert.AreEqual(0, MatchIntroTimeline.ShotAt(2.99f));
            Assert.AreEqual(1, MatchIntroTimeline.ShotAt(3f));
            Assert.AreEqual(2, MatchIntroTimeline.ShotAt(5.5f));
            Assert.AreEqual(2, MatchIntroTimeline.ShotAt(7.9f));
            Assert.AreEqual(8f, MatchIntroTimeline.Duration, 0.0001f);
        }

        [Test]
        public void ShotProgress_IsZeroToOnePerShot()
        {
            Assert.AreEqual(0.5f, MatchIntroTimeline.ShotProgress(1.5f), 0.001f);
            Assert.AreEqual(0f, MatchIntroTimeline.ShotProgress(3f), 0.001f);
            Assert.AreEqual(1f, MatchIntroTimeline.ShotProgress(8f), 0.001f);
        }

        [Test]
        public void Darkness_StartsBlack_DipsAtCuts_AndFadesAtEnd()
        {
            Assert.AreEqual(1f, MatchIntroTimeline.Darkness(0f), 0.001f);
            Assert.AreEqual(0f, MatchIntroTimeline.Darkness(1.5f), 0.001f);
            Assert.AreEqual(1f, MatchIntroTimeline.Darkness(3f), 0.001f);
            Assert.AreEqual(1f, MatchIntroTimeline.Darkness(5.5f), 0.001f);
            Assert.AreEqual(1f, MatchIntroTimeline.Darkness(8f), 0.001f);
            Assert.AreEqual(0f, MatchIntroTimeline.Darkness(7.5f), 0.001f);
        }

        [Test]
        public void CardPop_IsStaggeredAndSettlesAtOne()
        {
            Assert.AreEqual(0f, MatchIntroTimeline.CardPop(5.4f, 0), 0.0001f);
            Assert.Greater(MatchIntroTimeline.CardPop(5.9f, 0), 0.5f);
            Assert.AreEqual(0f, MatchIntroTimeline.CardPop(5.9f, 2), 0.0001f);
            Assert.AreEqual(1f, MatchIntroTimeline.CardPop(7.9f, 0), 0.0001f);
        }

        [Test]
        public void SkipTarget_JumpsToFinalFadeOnly()
        {
            Assert.AreEqual(MatchIntroTimeline.Duration - MatchIntroTimeline.FinalFade, MatchIntroTimeline.SkipTarget(1f), 0.0001f);
            Assert.AreEqual(7.9f, MatchIntroTimeline.SkipTarget(7.9f), 0.0001f);
        }

        [Test]
        public void Letterbox_AndWindow_AreEasedAndBounded()
        {
            Assert.AreEqual(0f, MatchIntroTimeline.Letterbox(0f), 0.0001f);
            Assert.AreEqual(1f, MatchIntroTimeline.Letterbox(2f), 0.0001f);
            Assert.AreEqual(0f, MatchIntroTimeline.Window(1f, 2f, 0.5f, 1f, 0.5f), 0.0001f);
            Assert.AreEqual(1f, MatchIntroTimeline.Window(3f, 2f, 0.5f, 1f, 0.5f), 0.0001f);
            Assert.AreEqual(0f, MatchIntroTimeline.Window(4f, 2f, 0.5f, 1f, 0.5f), 0.0001f);
        }

        [Test]
        public void CardCount_ClampsToFive()
        {
            Assert.AreEqual(0, MatchIntroTimeline.CardCount(-1));
            Assert.AreEqual(3, MatchIntroTimeline.CardCount(3));
            Assert.AreEqual(5, MatchIntroTimeline.CardCount(10));
        }
    }
}
