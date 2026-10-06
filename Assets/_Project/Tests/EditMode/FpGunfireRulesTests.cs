using NUnit.Framework;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class FpGunfireRulesTests
    {
        [Test]
        public void FlashDuration_IsOneToTwoFrames()
        {
            Assert.That(FpGunfireRules.FlashDuration(1f / 60f, MuzzleDevice.None), Is.EqualTo(2f / 60f).Within(1e-4f));
            Assert.That(FpGunfireRules.FlashDuration(1f / 60f, MuzzleDevice.Suppressor), Is.EqualTo(1f / 60f).Within(1e-4f));
            Assert.That(FpGunfireRules.FlashDuration(float.NaN, MuzzleDevice.None), Is.LessThanOrEqualTo(0.04f));
        }

        [Test]
        public void Device_ShapesFlash()
        {
            Assert.AreEqual(5, FpGunfireRules.FlashPetals(MuzzleDevice.FlashHider));
            Assert.AreEqual(0, FpGunfireRules.FlashPetals(MuzzleDevice.Suppressor));
            Assert.Greater(FpGunfireRules.SuppressorPuffScale(MuzzleDevice.Suppressor), 0f);
            Assert.AreEqual(0f, FpGunfireRules.SuppressorPuffScale(MuzzleDevice.None));
            Assert.Less(FpGunfireRules.FlashScale(MuzzleDevice.Suppressor), FpGunfireRules.FlashScale(MuzzleDevice.None));
        }

        [Test]
        public void Heat_BuildsAndDecays()
        {
            var h = 0f;
            for (var i = 0; i < 20; i++) h = FpGunfireRules.Heat(h, 1, 0.1f);
            Assert.Greater(h, FpGunfireRules.HeatVisibleThreshold);
            Assert.Greater(FpGunfireRules.ShimmerAlpha(h, VfxTier.High), 0f);
            Assert.AreEqual(0f, FpGunfireRules.ShimmerAlpha(h, VfxTier.Low));
            Assert.AreEqual(0f, FpGunfireRules.Heat(h, 0, 10f));
            Assert.AreEqual(0f, FpGunfireRules.ShimmerAlpha(0.1f, VfxTier.High));
        }

        [Test]
        public void Burst_And_Caps()
        {
            Assert.IsFalse(FpGunfireRules.BurstEnded(2, 1f));
            Assert.IsFalse(FpGunfireRules.BurstEnded(5, 0.05f));
            Assert.IsTrue(FpGunfireRules.BurstEnded(5, 0.3f));
            Assert.AreEqual(0, FpGunfireRules.WispCap(VfxTier.Low));
            Assert.Greater(FpGunfireRules.WispCap(VfxTier.High), FpGunfireRules.WispCap(VfxTier.Medium));
            Assert.AreEqual(0f, FpGunfireRules.WispAlpha(1f));
            Assert.Greater(FpGunfireRules.WispAlpha(0.2f), 0f);
        }

        [Test]
        public void WallSpill_FallsWithDistance()
        {
            Assert.AreEqual(1f, FpGunfireRules.WallSpill(0.2f), 1e-4f);
            Assert.AreEqual(0f, FpGunfireRules.WallSpill(5f), 1e-4f);
            Assert.Greater(FpGunfireRules.WallSpill(1f), FpGunfireRules.WallSpill(2f));
        }

        [Test]
        public void TracerStart_PushedOutOfCamera()
        {
            var cam = Vector3.zero;
            var s = FpGunfireRules.TracerStart(new Vector3(0, 0, 0.1f), new Vector3(0, 0, 50f), cam, 0.7f);
            Assert.GreaterOrEqual(s.z, 0.7f);
            var far = new Vector3(0, 0, 3f);
            Assert.AreEqual(far, FpGunfireRules.TracerStart(far, new Vector3(0, 0, 50f), cam, 0.7f));
            var short1 = FpGunfireRules.TracerStart(new Vector3(0, 0, 0.1f), new Vector3(0, 0, 0.5f), cam, 0.7f);
            Assert.LessOrEqual(short1.z, 0.5f);
        }

        [Test]
        public void Glint_ArcFallsAndFades()
        {
            var p = FpGunfireRules.GlintPosition(Vector3.zero, new Vector3(2, 2, 0), 0.5f);
            Assert.AreEqual(1f, p.x, 1e-4f);
            Assert.Less(p.y, 1f);
            Assert.AreEqual(0f, FpGunfireRules.GlintBrightness(0.55f, 0.55f, 40f), 1e-4f);
        }
    }
}
