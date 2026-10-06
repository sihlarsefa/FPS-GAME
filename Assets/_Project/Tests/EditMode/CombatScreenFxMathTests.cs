#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Rendering;

namespace Project.Tests.EditMode
{
    public sealed class CombatScreenFxMathTests
    {
        [Test]
        public void LowHealth_AboveThreshold_IsZero_AndZeroHealthIsOne()
        {
            Assert.AreEqual(0f, CombatScreenFxMath.LowHealth01(0.6f));
            Assert.AreEqual(1f, CombatScreenFxMath.LowHealth01(0f));
        }

        [Test]
        public void Concussion_CloserIsStronger_AndFarIsZero()
        {
            Assert.Greater(CombatScreenFxMath.ConcussionFor(2f, 6f), CombatScreenFxMath.ConcussionFor(12f, 6f));
            Assert.AreEqual(0f, CombatScreenFxMath.ConcussionFor(500f, 6f));
        }

        [Test]
        public void Concussion_Decays()
        {
            Assert.AreEqual(0f, CombatScreenFxMath.DecayConcussion(0.3f, 5f));
        }

        [Test]
        public void SwayMultiplier_NormalWhenCalm_AndGrowsWithSuppression()
        {
            Assert.AreEqual(1f, CombatScreenFxMath.SwayMultiplier(0f));
            Assert.Greater(CombatScreenFxMath.SwayMultiplier(1f), 2f);
            Assert.AreEqual(1f, CombatScreenFxMath.SwayMultiplier(1f, 0f));
        }

        [Test]
        public void Compute_TierGatesBlurAndChromatic()
        {
            var low = CombatScreenFxMath.Compute(1f, 0f, 0f, 0.5f, 0f, 1f, 0);
            var high = CombatScreenFxMath.Compute(1f, 0f, 0f, 0.5f, 0f, 1f, 3);
            Assert.AreEqual(0f, low.Blur);
            Assert.AreEqual(0f, low.Chromatic);
            Assert.Greater(high.Blur, 0f);
            Assert.Greater(high.Chromatic, 0f);
            Assert.Greater(low.Vignette, 0f);
        }

        [Test]
        public void Compute_Idle_IsInactive_AndZeroIntensityDisables()
        {
            Assert.IsFalse(CombatScreenFxMath.Compute(0f, 0f, 0f, 0f, 0f, 1f, 3).Any);
            Assert.IsFalse(CombatScreenFxMath.Compute(1f, 1f, 1f, 1f, 1f, 0f, 3).Any);
        }

        [Test]
        public void HeartPulse_ZeroWhenHealthy_InRangeWhenLow()
        {
            Assert.AreEqual(0f, CombatScreenFxMath.HeartPulse(1f, 0f));
            var p = CombatScreenFxMath.HeartPulse(0.09f, 1f);
            Assert.GreaterOrEqual(p, 0f);
            Assert.LessOrEqual(p, 1f);
        }
    
        [Test]
        public void Quadrant_MapsBearings()
        {
            Assert.AreEqual(0, CombatScreenFxMath.QuadrantOf(0f));
            Assert.AreEqual(1, CombatScreenFxMath.QuadrantOf(90f));
            Assert.AreEqual(2, CombatScreenFxMath.QuadrantOf(180f));
            Assert.AreEqual(2, CombatScreenFxMath.QuadrantOf(-180f));
            Assert.AreEqual(3, CombatScreenFxMath.QuadrantOf(-90f));
            Assert.AreEqual(0, CombatScreenFxMath.QuadrantOf(-30f));
            Assert.AreEqual(0, CombatScreenFxMath.QuadrantOf(float.NaN));
        }

        [Test]
        public void Splatter_FadesToZeroInTwoSeconds()
        {
            Assert.AreEqual(0.8f, CombatScreenFxMath.SplatterAlpha(0.1f, 0.8f), 1e-4f);
            Assert.Less(CombatScreenFxMath.SplatterAlpha(1f, 0.8f), 0.8f);
            Assert.AreEqual(0f, CombatScreenFxMath.SplatterAlpha(2f, 0.8f));
            Assert.Greater(CombatScreenFxMath.SplatterStrength(50f), CombatScreenFxMath.SplatterStrength(5f));
        }

        [Test]
        public void HealWipe_And_ArmorBreak_Curves()
        {
            Assert.AreEqual(1f, CombatScreenFxMath.HealWipe(-1f));
            Assert.AreEqual(0f, CombatScreenFxMath.HealWipe(0.4f));
            Assert.AreEqual(1f, CombatScreenFxMath.ArmorBreakFlash(0f), 1e-4f);
            Assert.AreEqual(0f, CombatScreenFxMath.ArmorBreakFlash(1f));
            Assert.Greater(CombatScreenFxMath.ArmorBreakRing(0.3f), CombatScreenFxMath.ArmorBreakRing(0.05f));
        }

        [Test]
        public void Downed_DarkensEdges_AndRespectsIntensity()
        {
            var on = CombatScreenFxMath.Compute(0, 0, 0, 0, 0, 1f, 2, 1f);
            var off = CombatScreenFxMath.Compute(0, 0, 0, 0, 0, 0f, 2, 1f);
            Assert.Greater(on.Vignette, 0.7f);
            Assert.Less(on.Saturation, -30f);
            Assert.Greater(on.Blur, 0.2f);
            Assert.AreEqual(0f, off.Vignette);
            Assert.Greater(CombatScreenFxMath.Compute(0, 0, 0, 0, 0, 1f, 0, 0f, 1f).Flash, 0.3f);
        }

        [Test]
        public void HeartbeatEdge_And_HitShake()
        {
            Assert.IsTrue(CombatScreenFxMath.HeartbeatEdge(0.2f, 0.9f));
            Assert.IsFalse(CombatScreenFxMath.HeartbeatEdge(0.9f, 0.95f));
            Assert.AreEqual(0f, CombatScreenFxMath.HitShake(30f, 0f));
            Assert.Greater(CombatScreenFxMath.HitShake(60f, 1f), CombatScreenFxMath.HitShake(10f, 1f));
        }
}
}
#endif
