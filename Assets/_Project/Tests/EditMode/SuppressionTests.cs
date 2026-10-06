#if UNITY_EDITOR
using NUnit.Framework;
using Project.Application.Combat.Suppression;

namespace Project.Tests
{
    public class SuppressionTests
    {
        [Test]
        public void MissDistance_PerpendicularPass()
        {
            var d = SuppressionRules.MissDistance(0, 0, 0, 0, 0, 1, 100f, 1f, 0, 50f);
            Assert.IsTrue(System.Math.Abs(d - 1f) < 0.001f);
        }

        [Test]
        public void MissDistance_BehindShooter_UsesOriginDistance()
        {
            var d = SuppressionRules.MissDistance(0, 0, 0, 0, 0, 1, 100f, 0, 0, -3f);
            Assert.IsTrue(System.Math.Abs(d - 3f) < 0.001f);
        }

        [Test]
        public void Intensity_ZeroOutsideRadius_AndMonotonic()
        {
            var cfg = new SuppressionConfig();
            Assert.AreEqual(0f, SuppressionRules.ShotIntensity(cfg.NearMissRadius + 0.1f, cfg));
            Assert.Greater(SuppressionRules.ShotIntensity(0.3f, cfg), SuppressionRules.ShotIntensity(1.5f, cfg));
        }

        [Test]
        public void NearMiss_SetsLevel_Muffle_AndSway()
        {
            var s = new SuppressionState();
            s.RegisterNearMiss(0.2f);
            Assert.Greater(s.Level, 0f);
            Assert.IsTrue(s.IsMuffled);
            Assert.Greater(s.AimSwayMultiplier, 1f);
        }

        [Test]
        public void Muffle_EndsAfterHalfSecond()
        {
            var s = new SuppressionState();
            s.RegisterNearMiss(0.2f);
            s.Tick(0.3f);
            Assert.IsTrue(s.IsMuffled);
            s.Tick(0.25f);
            Assert.IsTrue(!s.IsMuffled);
        }

        [Test]
        public void Level_DecaysToZero_AndClamps()
        {
            var s = new SuppressionState();
            for (var i = 0; i < 10; i++) s.RegisterNearMiss(0.1f);
            Assert.IsTrue(System.Math.Abs(s.Level - 1f) < 0.0001f);
            for (var i = 0; i < 100; i++) s.Tick(0.1f);
            Assert.AreEqual(0f, s.Level);
        }

        [Test]
        public void NpcCover_TriggersOnSustainedFire()
        {
            var s = new SuppressionState();
            s.RegisterNearMiss(1.5f);
            Assert.IsTrue(!s.ShouldSeekCover);
            for (var i = 0; i < 4; i++) s.RegisterNearMiss(0.5f);
            Assert.IsTrue(s.ShouldSeekCover);
        }

        [Test]
        public void Vignette_OffOnLowestTier_OnOtherwise()
        {
            var s = new SuppressionState();
            s.RegisterNearMiss(0.1f);
            Assert.AreEqual(0f, s.VignetteAlpha(0));
            Assert.Greater(s.VignetteAlpha(2), 0f);
        }
    }
}
#endif
