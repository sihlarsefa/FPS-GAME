#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Vfx;

namespace Project.Tests.EditMode
{
    public sealed class VfxTierLimitTests
    {
        [Test]
        public void ScorchCap_GrowsWithTier()
        {
            Assert.Less(VfxQuality.ScorchCap(VfxTier.Low, 16), VfxQuality.ScorchCap(VfxTier.Medium, 16));
            Assert.Less(VfxQuality.ScorchCap(VfxTier.Medium, 16), VfxQuality.ScorchCap(VfxTier.High, 16));
            Assert.AreEqual(16, VfxQuality.ScorchCap(VfxTier.High, 16));
        }

        [Test]
        public void RotorDust_OnlyBelow15m()
        {
            Assert.IsTrue(VfxQuality.RotorDustAllowed(0f));
            Assert.IsTrue(VfxQuality.RotorDustAllowed(14.9f));
            Assert.IsFalse(VfxQuality.RotorDustAllowed(15f));
            Assert.IsFalse(VfxQuality.RotorDustAllowed(float.PositiveInfinity));
            Assert.IsFalse(VfxQuality.RotorDustAllowed(float.NaN));
        }

        [Test]
        public void MuzzleSmoke_DisabledOnLow_ThrottledOtherwise()
        {
            Assert.IsTrue(float.IsPositiveInfinity(VfxQuality.MuzzleSmokeInterval(VfxTier.Low)));
            Assert.Less(VfxQuality.MuzzleSmokeInterval(VfxTier.High), VfxQuality.MuzzleSmokeInterval(VfxTier.Medium));
        }
    }
}
#endif
