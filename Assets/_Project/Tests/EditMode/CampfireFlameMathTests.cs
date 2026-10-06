#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Vfx.Fire;

namespace Project.Tests.EditMode
{
    public sealed class CampfireFlameMathTests
    {
        [Test]
        public void Counts_GrowWithTier()
        {
            CampfireFlameMath.CountsForTier(0, out var f0, out var s0, out var m0);
            CampfireFlameMath.CountsForTier(3, out var f3, out var s3, out var m3);
            Assert.Greater(f3, f0);
            Assert.Greater(s3, s0);
            Assert.Greater(m3, m0);
            CampfireFlameMath.CountsForTier(99, out var fx, out _, out _);
            Assert.AreEqual(f3, fx);
        }

        [Test]
        public void EmberPulse_StaysInRange()
        {
            for (var i = 0; i < 200; i++)
            {
                var p = CampfireFlameMath.EmberPulse(i * 0.37f, 1.2f);
                Assert.IsTrue(p >= 0f && p <= 1f);
            }
        }

        [Test]
        public void FlameAlpha_ZeroAtEdges_PositiveInCenter()
        {
            Assert.AreEqual(0f, CampfireFlameMath.FlameAlpha(0f, 0f, 3f), 0.0001f);
            Assert.AreEqual(0f, CampfireFlameMath.FlameAlpha(1f, 0.5f, 3f), 0.0001f);
            Assert.Greater(CampfireFlameMath.FlameAlpha(0.5f, 0.5f, 3f), 0.2f);
        }
    }
}
#endif
