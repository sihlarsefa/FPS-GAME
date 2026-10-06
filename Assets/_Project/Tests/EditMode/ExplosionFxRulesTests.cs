#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class ExplosionFxRulesTests
    {
        [Test]
        public void SmokeBlend_GoesFireToSmokeOverTransition()
        {
            Assert.AreEqual(0f, ExplosionFxRules.SmokeBlend(0f), 1e-5f);
            Assert.AreEqual(0.5f, ExplosionFxRules.SmokeBlend(ExplosionFxRules.SmokeTransition * 0.5f), 1e-4f);
            Assert.AreEqual(1f, ExplosionFxRules.SmokeBlend(ExplosionFxRules.SmokeTransition), 1e-5f);
            Assert.AreEqual(1f, ExplosionFxRules.SmokeBlend(5f), 1e-5f);
            Assert.AreEqual(1.2f, ExplosionFxRules.SmokeTransition, 1e-5f);
        }

        [Test]
        public void Heat_FallsMonotonically()
        {
            var prev = ExplosionFxRules.Heat(0f);
            Assert.AreEqual(1f, prev, 1e-5f);
            for (var t = 0.1f; t <= 1.5f; t += 0.1f)
            {
                var h = ExplosionFxRules.Heat(t);
                Assert.LessOrEqual(h, prev + 1e-6f);
                prev = h;
            }
            Assert.AreEqual(0f, ExplosionFxRules.Heat(1.5f), 1e-5f);
        }

        [Test]
        public void Expansion_EaseOutAndClamped()
        {
            Assert.AreEqual(0f, ExplosionFxRules.Expansion(0f, 1f), 1e-5f);
            Assert.AreEqual(1f, ExplosionFxRules.Expansion(2f, 1f), 1e-5f);
            Assert.Greater(ExplosionFxRules.Expansion(0.5f, 1f), 0.5f);
            Assert.AreEqual(1f, ExplosionFxRules.Expansion(1f, 0f));
        }

        [Test]
        public void AlphaEnvelope_RisesAndFades()
        {
            Assert.AreEqual(0f, ExplosionFxRules.AlphaEnvelope(0f, 3f, 1f), 1e-5f);
            Assert.AreEqual(1f, ExplosionFxRules.AlphaEnvelope(1.0f, 3f, 1f), 1e-4f);
            Assert.Less(ExplosionFxRules.AlphaEnvelope(2.9f, 3f, 1f), 0.2f);
            Assert.AreEqual(0f, ExplosionFxRules.AlphaEnvelope(3f, 3f, 1f));
            Assert.AreEqual(0f, ExplosionFxRules.AlphaEnvelope(-1f, 3f, 1f));
        }

        [Test]
        public void LingerAlpha_PeaksAtTransitionEndsAtEightSeconds()
        {
            Assert.AreEqual(8f, ExplosionFxRules.SmokeLinger, 1e-5f);
            Assert.AreEqual(0.5f, ExplosionFxRules.LingerAlpha(ExplosionFxRules.SmokeTransition, 0.5f), 1e-4f);
            Assert.Greater(ExplosionFxRules.LingerAlpha(2f, 0.5f), ExplosionFxRules.LingerAlpha(6f, 0.5f));
            Assert.AreEqual(0f, ExplosionFxRules.LingerAlpha(8f, 0.5f));
            Assert.IsTrue(ExplosionFxRules.Finished(8f));
            Assert.IsFalse(ExplosionFxRules.Finished(7.9f));
        }

        [Test]
        public void SmokeDrift_FollowsWindAndRisesSlowly()
        {
            var calm = ExplosionFxRules.SmokeDrift(6f, 1f, 0f, 0f, 2f);
            Assert.AreEqual(0f, calm.x, 1e-5f);
            Assert.Greater(calm.y, 1f);

            var windy = ExplosionFxRules.SmokeDrift(6f, 0f, 1f, 2f, 2f);
            Assert.AreEqual(0f, windy.x, 1e-4f);
            Assert.Greater(windy.z, 4f);
            Assert.Less(windy.z, 2f * 6f);

            var later = ExplosionFxRules.SmokeDrift(8f, 0f, 1f, 2f, 2f);
            Assert.Greater(later.z, windy.z);
            Assert.AreEqual(Vector3.zero, ExplosionFxRules.SmokeDrift(0f, 1f, 0f, 2f, 2f));
        }

        [Test]
        public void WindSpeed_OffIsZeroAndBounded()
        {
            Assert.AreEqual(0f, ExplosionFxRules.WindSpeed(false, 1f, 1f));
            Assert.LessOrEqual(ExplosionFxRules.WindSpeed(true, 9f, 9f), 2.4f);
            Assert.Greater(ExplosionFxRules.WindSpeed(true, 1f, 0.5f), ExplosionFxRules.WindSpeed(true, 0.1f, 0f));
        }

        [Test]
        public void SunLit_BrighterTowardSunDimmerAtNight()
        {
            Assert.Greater(ExplosionFxRules.SunLit(1f, 0f), ExplosionFxRules.SunLit(-1f, 0f));
            Assert.AreEqual(1f, ExplosionFxRules.SunLit(1f, 0f), 1e-5f);
            Assert.Less(ExplosionFxRules.SunLit(1f, 1f), ExplosionFxRules.SunLit(1f, 0f));
        }

        [Test]
        public void TierLimits_ScaleAndLowKeepsMinimal()
        {
            Assert.AreEqual(3, ExplosionFxRules.FireLayers(VfxTier.High));
            Assert.AreEqual(2, ExplosionFxRules.FireLayers(VfxTier.Medium));
            Assert.AreEqual(1, ExplosionFxRules.FireLayers(VfxTier.Low));
            Assert.Less(ExplosionFxRules.MaxInstances(VfxTier.Low), ExplosionFxRules.MaxInstances(VfxTier.High));
            Assert.AreEqual(0, ExplosionFxRules.SmokeLayers(VfxTier.Low));
            Assert.IsFalse(ExplosionFxRules.AllowDistortion(VfxTier.Low));
            Assert.IsFalse(ExplosionFxRules.AllowColumn(VfxTier.Medium));
            Assert.IsTrue(ExplosionFxRules.AllowColumn(VfxTier.High));
        }

        [Test]
        public void Distort_ShortPulse()
        {
            Assert.AreEqual(0f, ExplosionFxRules.DistortStrength(0f), 1e-5f);
            Assert.AreEqual(1f, ExplosionFxRules.DistortStrength(0.08f), 1e-4f);
            Assert.AreEqual(0f, ExplosionFxRules.DistortStrength(ExplosionFxRules.DistortLife));
        }

        [Test]
        public void Erosion_IncreasesWithAge()
        {
            Assert.Less(ExplosionFxRules.Erosion(0f, 3f), ExplosionFxRules.Erosion(2f, 3f));
            Assert.AreEqual(0.85f, ExplosionFxRules.Erosion(9f, 3f), 1e-5f);
        }
    }
}
#endif
