using NUnit.Framework;
using Project.Presentation.UI;

namespace Project.Tests
{
    public sealed class MenuIntroTimelineTests
    {
        [Test] public void TotalIsUnderLimit() => Assert.That(MenuIntroTimeline.Total, Is.LessThanOrEqualTo(1.8f));

        [Test]
        public void LineDrawsInFirst400ms()
        {
            Assert.That(MenuIntroTimeline.LineProgress(0f), Is.EqualTo(0f));
            Assert.That(MenuIntroTimeline.LineProgress(0.4f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(MenuIntroTimeline.LineProgress(0.2f), Is.InRange(0.1f, 0.99f));
        }

        [Test]
        public void TitleThenTaglineOrdered()
        {
            Assert.That(MenuIntroTimeline.TitleAlpha(0.39f), Is.EqualTo(0f));
            Assert.That(MenuIntroTimeline.TitleAlpha(0.95f), Is.EqualTo(1f));
            Assert.That(MenuIntroTimeline.TaglineAlpha(0.9f), Is.EqualTo(0f));
            Assert.That(MenuIntroTimeline.TaglineAlpha(1.25f), Is.EqualTo(1f));
            Assert.That(MenuIntroTimeline.TaglineEnd - MenuIntroTimeline.TaglineStart, Is.EqualTo(0.3f).Within(1e-4f));
        }

        [Test]
        public void LetterSpacingTightens()
        {
            Assert.That(MenuIntroTimeline.LetterSpacing(0.4f), Is.GreaterThan(1.8f));
            Assert.That(MenuIntroTimeline.LetterSpacing(1.2f), Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void PulseAndThud()
        {
            Assert.That(MenuIntroTimeline.PulseScale(1.0f), Is.EqualTo(1f));
            Assert.That(MenuIntroTimeline.PulseScale(1.35f), Is.GreaterThan(2f));
            Assert.That(MenuIntroTimeline.PulseScale(1.6f), Is.EqualTo(1f));
            Assert.That(MenuIntroTimeline.ThudDue(1.2f), Is.False);
            Assert.That(MenuIntroTimeline.ThudDue(1.25f), Is.True);
        }

        [Test]
        public void FadeRevealsMenu()
        {
            Assert.That(MenuIntroTimeline.CoverAlpha(1.0f), Is.EqualTo(1f));
            Assert.That(MenuIntroTimeline.CoverAlpha(1.75f), Is.EqualTo(0f));
            Assert.That(MenuIntroTimeline.IsDone(1.74f), Is.False);
            Assert.That(MenuIntroTimeline.IsDone(1.75f), Is.True);
        }
    }
}
