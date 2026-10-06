using NUnit.Framework;
using Project.Infrastructure.Rendering.Grading;

namespace Project.Tests.EditMode
{
    public sealed class GradeSpecBlenderTests
    {
        [Test]
        public void IlkHedef_AnindaUygulanir()
        {
            var b = new GradeSpecBlender();
            b.SetTarget(GradingPresets.Base(GradeStage.Gece));
            Assert.IsTrue(b.HasTarget);
            Assert.IsFalse(b.Blending);
            Assert.AreEqual(GradingPresets.Base(GradeStage.Gece).Temperature, b.Current.Temperature, 0.001f);
        }

        [Test]
        public void YeniHedef_YumusakGecer()
        {
            var b = new GradeSpecBlender { Duration = 2f };
            b.SetTarget(GradingPresets.Base(GradeStage.Gunduz));
            var start = b.Current.Temperature;
            b.SetTarget(GradingPresets.Base(GradeStage.Gece));
            Assert.IsTrue(b.Blending);
            b.Advance(1f);
            var mid = b.Current.Temperature;
            Assert.Less(mid, start - 1f);
            Assert.Greater(mid, GradingPresets.Base(GradeStage.Gece).Temperature + 1f);
            b.Advance(5f);
            Assert.IsFalse(b.Blending);
            Assert.AreEqual(GradingPresets.Base(GradeStage.Gece).Temperature, b.Current.Temperature, 0.01f);
        }

        [Test]
        public void AyniHedef_GecisiYenidenBaslatmaz()
        {
            var b = new GradeSpecBlender();
            b.SetTarget(GradingPresets.Base(GradeStage.Gunduz));
            b.SetTarget(GradingPresets.Base(GradeStage.Gunduz));
            Assert.IsFalse(b.Blending);
        }
    }
}
