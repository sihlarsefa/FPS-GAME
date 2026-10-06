#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Vfx;

namespace Project.Tests.EditMode
{
    public sealed class VfxQualityTests
    {
        [Test]
        public void FromLevel_MapsToTiers()
        {
            Assert.AreEqual(VfxTier.Low, VfxQuality.FromLevel(0, 6));
            Assert.AreEqual(VfxTier.Medium, VfxQuality.FromLevel(3, 6));
            Assert.AreEqual(VfxTier.High, VfxQuality.FromLevel(5, 6));
            Assert.AreEqual(VfxTier.High, VfxQuality.FromLevel(0, 1));
        }

        [Test]
        public void Scale_ReducesOnLow_KeepsAtLeastOne()
        {
            Assert.AreEqual(10, VfxQuality.ScaleFor(10, VfxTier.High));
            Assert.AreEqual(4, VfxQuality.ScaleFor(10, VfxTier.Low));
            Assert.AreEqual(1, VfxQuality.ScaleFor(1, VfxTier.Low));
            Assert.AreEqual(0, VfxQuality.ScaleFor(0, VfxTier.Low));
        }

        [Test]
        public void Caps_GrowWithTier()
        {
            Assert.Less(VfxQuality.CasingCap(VfxTier.Low), VfxQuality.CasingCap(VfxTier.Medium));
            Assert.Less(VfxQuality.CasingCap(VfxTier.Medium), VfxQuality.CasingCap(VfxTier.High));
            Assert.AreEqual(GameVfx.MaxDecals, VfxQuality.DecalCap(VfxTier.High, GameVfx.MaxDecals));
            Assert.Less(VfxQuality.DecalCap(VfxTier.Low, GameVfx.MaxDecals), GameVfx.MaxDecals);
            Assert.IsFalse(VfxQuality.AllowOptional(VfxTier.Low));
        }
    }
}
#endif
