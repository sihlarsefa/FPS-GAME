#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Vfx;

namespace Project.Tests
{
    public sealed class GpuVfxBudgetTests
    {
        [Test]
        public void LowTierIsOff()
        {
            Assert.IsFalse(GpuVfxBudget.Enabled(0));
            Assert.AreEqual(0, GpuVfxBudget.MaxInstances(GpuVfxEffect.Impact, 0));
            Assert.AreEqual(0, GpuVfxBudget.Capacity(GpuVfxEffect.Rain, 0));
        }

        [Test]
        public void BudgetGrowsWithTier()
        {
            Assert.IsTrue(GpuVfxBudget.Capacity(GpuVfxEffect.Explosion, 3) > GpuVfxBudget.Capacity(GpuVfxEffect.Explosion, 1));
            Assert.IsTrue(GpuVfxBudget.MaxInstances(GpuVfxEffect.Impact, 3) >= GpuVfxBudget.MaxInstances(GpuVfxEffect.Impact, 1));
            Assert.AreEqual(1, GpuVfxBudget.MaxInstances(GpuVfxEffect.Rain, 3));
        }

        [Test]
        public void NoBackendFallsBack()
        {
            GpuVfx.Backend = null;
            Assert.IsFalse(GpuVfx.TryPlay(GpuVfxEffect.Explosion, UnityEngine.Vector3.zero, UnityEngine.Vector3.up));
            Assert.AreEqual("VFX_Explosion", GpuVfxBudget.ResourceName(GpuVfxEffect.Explosion));
        }
    }
}
#endif
