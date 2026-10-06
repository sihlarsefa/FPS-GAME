using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class RecoilPatternTests
    {
        [Test]
        public void Pattern_IsDeterministicPerSeed()
        {
            var seed = RecoilPattern.SeedFor("MPT-76");
            for (var i = 0; i < RecoilPattern.PatternLength; i++)
            {
                RecoilPattern.GetStep(seed, i, out var v1, out var h1);
                RecoilPattern.GetStep(seed, i, out var v2, out var h2);
                Assert.AreEqual(v1, v2);
                Assert.AreEqual(h1, h2);
                Assert.LessOrEqual(System.Math.Abs(h1), 1f);
                Assert.Greater(v1, 0.7f);
                Assert.Less(v1, 1.5f);
            }
        }

        [Test]
        public void Pattern_DiffersBetweenWeapons()
        {
            var a = RecoilPattern.SeedFor("MPT-76");
            var b = RecoilPattern.SeedFor("G3");
            Assert.AreNotEqual(a, b);
            var different = false;
            for (var i = 1; i < RecoilPattern.PatternLength; i++)
            {
                RecoilPattern.GetStep(a, i, out _, out var ha);
                RecoilPattern.GetStep(b, i, out _, out var hb);
                if (System.Math.Abs(ha - hb) > 1e-4f)
                    different = true;
            }

            Assert.IsTrue(different);
        }

        [Test]
        public void Pattern_EndsAfterLength()
        {
            Assert.IsTrue(RecoilPattern.IsPatterned(0));
            Assert.IsFalse(RecoilPattern.IsPatterned(RecoilPattern.PatternLength));
            Assert.IsFalse(RecoilPattern.IsPatterned(-1));
        }

        [Test]
        public void AdsBlend_ReachesTargetInAdsTime()
        {
            var b = 0f;
            for (var i = 0; i < 10; i++)
                b = AdsBlend.Step(b, true, 0.02f, 0.2f);
            Assert.AreEqual(1f, b, 1e-4f);
            b = AdsBlend.Step(b, false, 0.1f, 0.2f);
            Assert.AreEqual(0.5f, b, 1e-4f);
            Assert.AreEqual(0.5f, AdsBlend.Smooth(0.5f), 1e-4f);
            Assert.AreEqual(10f, AdsBlend.Lerp(10f, 2f, 0f), 1e-4f);
            Assert.AreEqual(2f, AdsBlend.Lerp(10f, 2f, 1f), 1e-4f);
        }

        [Test]
        public void HoldBreath_SteadyThenPenaltyThenRecovers()
        {
            var s = new HoldBreathState();
            s.Update(0.1f, true, true);
            Assert.AreEqual(HoldBreathState.SteadySway, s.SwayMultiplier, 1e-4f);
            for (var i = 0; i < 45; i++)
                s.Update(0.1f, true, true);
            Assert.AreEqual(HoldBreathState.PenaltySway, s.SwayMultiplier, 1e-4f);
            Assert.IsFalse(s.IsHolding);
            for (var i = 0; i < 80; i++)
                s.Update(0.1f, false, true);
            Assert.AreEqual(HoldBreathState.NormalSway, s.SwayMultiplier, 1e-4f);
        }

        [Test]
        public void HoldBreath_NeedsScope()
        {
            var s = new HoldBreathState();
            s.Update(1f, true, false);
            Assert.IsFalse(s.IsHolding);
            Assert.AreEqual(HoldBreathState.MaxSeconds, s.Remaining, 1e-4f);
        }
    }
}
