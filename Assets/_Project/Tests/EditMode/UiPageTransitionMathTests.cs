using NUnit.Framework;
using Project.Presentation.UI;

namespace Project.Tests
{
    public sealed class UiPageTransitionMathTests
    {
        [Test]
        public void ItemCount_CappedAtEight()
        {
            Assert.That(UiPageTransitionMath.ItemCount(20), Is.EqualTo(8));
            Assert.That(UiPageTransitionMath.ItemCount(-1), Is.EqualTo(0));
        }

        [Test]
        public void ItemDelay_Is30msSteps()
        {
            Assert.That(UiPageTransitionMath.ItemDelay(3), Is.EqualTo(0.09f).Within(1e-5f));
        }

        [Test]
        public void ItemProgress_StartsZeroEndsOne()
        {
            Assert.That(UiPageTransitionMath.ItemProgress(0f, 2), Is.EqualTo(0f));
            Assert.That(UiPageTransitionMath.ItemProgress(1f, 2), Is.EqualTo(1f));
        }

        [Test]
        public void ItemOffset_ForwardStartsRightAndSettlesAtZero()
        {
            Assert.That(UiPageTransitionMath.ItemOffsetX(0f, 0, false), Is.EqualTo(12f).Within(1e-4f));
            Assert.That(UiPageTransitionMath.ItemOffsetX(0f, 0, true), Is.EqualTo(-12f).Within(1e-4f));
            Assert.That(UiPageTransitionMath.ItemOffsetX(1f, 0, false), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void OldAlpha_FadesInTenthSecond()
        {
            Assert.That(UiPageTransitionMath.OldAlpha(0f), Is.EqualTo(1f));
            Assert.That(UiPageTransitionMath.OldAlpha(0.1f), Is.EqualTo(0f));
        }

        [Test]
        public void WipeBand_EmptyOutsideAndMirroredWhenReverse()
        {
            UiPageTransitionMath.WipeBand(0f, false, out var a, out var b);
            Assert.That(b - a, Is.EqualTo(0f));
            UiPageTransitionMath.WipeBand(0.1f, false, out var f0, out var f1);
            UiPageTransitionMath.WipeBand(0.1f, true, out var r0, out var r1);
            Assert.That(r0, Is.EqualTo(1f - f1).Within(1e-5f));
            Assert.That(r1, Is.EqualTo(1f - f0).Within(1e-5f));
        }

        [Test]
        public void TotalDuration_CoversWipeAndLastItem()
        {
            Assert.That(UiPageTransitionMath.TotalDuration(0), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(UiPageTransitionMath.TotalDuration(8), Is.EqualTo(0.21f + 0.18f).Within(1e-5f));
        }

        [Test]
        public void ShouldSkip_WhenPausedOrBatch()
        {
            Assert.That(UiPageTransitionMath.ShouldSkip(0f, false), Is.True);
            Assert.That(UiPageTransitionMath.ShouldSkip(1f, true), Is.True);
            Assert.That(UiPageTransitionMath.ShouldSkip(1f, false), Is.False);
        }
    }
}
