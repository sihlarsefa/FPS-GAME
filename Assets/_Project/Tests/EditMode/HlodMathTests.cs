using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class HlodMathTests
    {
        [Test]
        public void Hysteresis_NoFlicker()
        {
            var s = 200f;
            Assert.IsFalse(HlodMath.ShouldUseProxy(false, 205f, s));
            Assert.IsTrue(HlodMath.ShouldUseProxy(false, 225f, s));
            Assert.IsTrue(HlodMath.ShouldUseProxy(true, 190f, s));
            Assert.IsFalse(HlodMath.ShouldUseProxy(true, 170f, s));
        }

        [Test]
        public void SwapDistance_GrowsWithTier_AndClamps()
        {
            Assert.Less(HlodMath.SwapDistance(0), HlodMath.SwapDistance(3));
            Assert.AreEqual(HlodMath.SwapDistance(3), HlodMath.SwapDistance(9), 0.001f);
            Assert.AreEqual(HlodMath.SwapDistance(0), HlodMath.SwapDistance(-4), 0.001f);
            Assert.AreEqual(8f, HlodMath.Hysteresis(10f), 0.001f);
        }

        [Test]
        public void SelectParts_DropsSmall_AndRespectsBudget()
        {
            var ext = new[] { 10f, 0.3f, 5f, 8f };
            var tris = new[] { 100, 10, 100, 100 };
            var keep = HlodMath.SelectParts(ext, tris, 1f, 0);
            Assert.IsTrue(keep[0]); Assert.IsFalse(keep[1]); Assert.IsTrue(keep[2]); Assert.IsTrue(keep[3]);
            keep = HlodMath.SelectParts(ext, tris, 1f, 200);
            Assert.IsTrue(keep[0]); Assert.IsTrue(keep[3]); Assert.IsFalse(keep[2]);
        }

        [Test]
        public void KeepPart_UsesLargestExtent()
        {
            Assert.IsTrue(HlodMath.KeepPart(0.1f, 3f, 0.1f, 1f));
            Assert.IsFalse(HlodMath.KeepPart(0.5f, 0.5f, 0.5f, 1f));
            Assert.IsTrue(HlodMath.FitsUInt16(65535));
            Assert.IsFalse(HlodMath.FitsUInt16(65536));
        }

        [Test]
        public void StreamingBudget_CappedByVram()
        {
            Assert.AreEqual(1024, HlodMath.EffectiveStreamingBudgetMb(2, 0));
            Assert.AreEqual(1400, HlodMath.EffectiveStreamingBudgetMb(3, 4000));
            Assert.AreEqual(128, HlodMath.EffectiveStreamingBudgetMb(0, 100));
            Assert.Greater(HlodMath.StreamingMaxLevelReduction(0), HlodMath.StreamingMaxLevelReduction(3));
        }

        [Test]
        public void TilePlan_Counts()
        {
            Assert.AreEqual(10, HlodMath.TileCount(10000f, 1000f));
            Assert.AreEqual(9, HlodMath.TilesInRadius(500f, 1000f));
            Assert.AreEqual(25, HlodMath.TilesInRadius(1500f, 1000f));
            Assert.AreEqual(9, HlodMath.TileIndex1D(99999f, 1000f, 10));
            Assert.AreEqual(0, HlodMath.TileIndex1D(-5f, 1000f, 10));
            Assert.IsTrue(HlodMath.TileShouldBeLoaded(true, 1400f, 1000f, 1000f));
            Assert.IsFalse(HlodMath.TileShouldBeLoaded(false, 1400f, 1000f, 1000f));
            Assert.AreEqual(8f, HlodMath.SmallestOccluderForMap(9000f), 0.001f);
        }
    }
}
