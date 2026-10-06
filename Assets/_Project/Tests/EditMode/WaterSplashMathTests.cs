using NUnit.Framework;
using Project.Infrastructure.Rendering;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class WaterSplashMathTests
    {
        [Test]
        public void Crossed_DetectsEntryAndExit()
        {
            Assert.IsTrue(WaterSplashMath.Crossed(1f, -1f, 0f));
            Assert.IsTrue(WaterSplashMath.Crossed(-1f, 1f, 0f));
            Assert.IsFalse(WaterSplashMath.Crossed(1f, 2f, 0f));
        }

        [Test]
        public void RingScale_IsClampedAndGrowsWithSpeed()
        {
            Assert.GreaterOrEqual(WaterSplashMath.RingScale(0f, 0f), 0.4f);
            Assert.LessOrEqual(WaterSplashMath.RingScale(1000f, 50f), 6f);
            Assert.Greater(WaterSplashMath.RingScale(8f, 0.4f), WaterSplashMath.RingScale(1f, 0.4f));
        }

        [Test]
        public void RingRadiusAndAlpha_FollowLifetime()
        {
            Assert.AreEqual(0f, WaterSplashMath.RingRadius(0f, 1f, 3f), 1e-4f);
            Assert.AreEqual(3f, WaterSplashMath.RingRadius(2f, 1f, 3f), 1e-4f);
            Assert.AreEqual(0f, WaterSplashMath.RingAlpha(1f, 1f), 1e-4f);
            Assert.Greater(WaterSplashMath.RingAlpha(0.1f, 1f), 0.9f);
        }
    }
}
