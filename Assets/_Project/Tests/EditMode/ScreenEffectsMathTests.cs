#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Rendering;

namespace Project.Tests.EditMode
{
    public sealed class ScreenEffectsMathTests
    {
        [Test]
        public void NearMiss_OutsideRadius_IsZero_AndCloserIsStronger()
        {
            Assert.AreEqual(0f, ScreenEffectsMath.NearMissImpulse(4f, 3f));
            Assert.Greater(ScreenEffectsMath.NearMissImpulse(0.3f, 3f), ScreenEffectsMath.NearMissImpulse(2.5f, 3f));
        }

        [Test]
        public void Suppression_ClampsAndDecays()
        {
            var m = 0f;
            for (var i = 0; i < 10; i++)
                m = ScreenEffectsMath.AddSuppression(m, 0.4f);
            Assert.AreEqual(1f, m);
            m = ScreenEffectsMath.DecaySuppression(m, 100f);
            Assert.AreEqual(0f, m);
        }

        [Test]
        public void Sway_NoneBelowThreshold_FullAtMax()
        {
            Assert.AreEqual(0f, ScreenEffectsMath.SwayAmount(0.1f));
            Assert.AreEqual(1f, ScreenEffectsMath.SwayAmount(1f), 1e-4f);
            Assert.AreEqual(0f, ScreenEffectsMath.VignetteAlpha(0f));
        }

        [Test]
        public void Muffle_OffAboveThreshold_ShrinksWithHealth()
        {
            Assert.AreEqual(22000f, ScreenEffectsMath.MuffleCutoff(0.5f));
            Assert.Less(ScreenEffectsMath.MuffleCutoff(0.1f), ScreenEffectsMath.MuffleCutoff(0.2f));
            Assert.AreEqual(1200f, ScreenEffectsMath.MuffleCutoff(0f), 1e-2f);
        }

        [Test]
        public void Punch_SideHitRollsOpposite_FrontHitPitchesUp()
        {
            ScreenEffectsMath.PunchFor(30f, 90f, out var p, out var y, out var r);
            Assert.Less(y, 0f);
            Assert.Less(r, 0f);
            ScreenEffectsMath.PunchFor(30f, -90f, out _, out var y2, out var r2);
            Assert.Greater(y2, 0f);
            Assert.Greater(r2, 0f);
            ScreenEffectsMath.PunchFor(30f, 0f, out var pf, out var yf, out _);
            Assert.Greater(pf, p);
            Assert.AreEqual(0f, yf, 1e-4f);
        }

        [Test]
        public void SsaoFor_InteriorDarkerThanOpen_AndLowStaysOff()
        {
            Assert.IsFalse(ScreenEffectsMath.SsaoFor(0, 1f, 12f).Enabled);
            for (var tier = 1; tier <= 3; tier++)
            {
                var open = ScreenEffectsMath.SsaoFor(tier, 0f, 12f);
                var inner = ScreenEffectsMath.SsaoFor(tier, 1f, 12f);
                Assert.Greater(inner.Intensity, open.Intensity);
                Assert.Greater(inner.Radius, open.Radius);
            }
        }

        [Test]
        public void SsaoHourFactor_NoonLowest_NightHigh()
        {
            Assert.AreEqual(0.9f, ScreenEffectsMath.SsaoHourFactor(12f), 1e-3f);
            Assert.AreEqual(1.1f, ScreenEffectsMath.SsaoHourFactor(2f), 1e-3f);
            Assert.AreEqual(ScreenEffectsMath.SsaoHourFactor(1f), ScreenEffectsMath.SsaoHourFactor(25f), 1e-4f);
        }

        [Test]
        public void ContactStrength_DayAndNightTargets()
        {
            Assert.AreEqual(0.35f, ScreenEffectsMath.ContactStrengthTarget(1f), 1e-4f);
            Assert.AreEqual(0.5f, ScreenEffectsMath.ContactStrengthTarget(0f), 1e-4f);
            Assert.Greater(ScreenEffectsMath.ContactLengthScale(0f), ScreenEffectsMath.ContactLengthScale(1f));
            Assert.AreEqual(0.35f, ScreenEffectsMath.ContactStrength(0.8f, 1f), 1e-4f);
            Assert.Less(ScreenEffectsMath.ContactStrength(0.55f, 1f), 0.35f);
        }

        [Test]
        public void ShadowBias_DecreasesWithQuality_NearPlaneNonNegative()
        {
            for (var i = 1; i <= 3; i++)
            {
                Assert.LessOrEqual(PipelineTiers.BiasFor(i).Depth, PipelineTiers.BiasFor(i - 1).Depth);
                Assert.LessOrEqual(PipelineTiers.BiasFor(i).Normal, PipelineTiers.BiasFor(i - 1).Normal);
                Assert.GreaterOrEqual(PipelineTiers.BiasFor(i).NearPlane, 0f);
            }
            Assert.AreEqual(PipelineTiers.BiasFor(3).Depth, PipelineTiers.BiasFor(99).Depth);
        }
    }
}
#endif
