using NUnit.Framework;
using Project.Infrastructure.Vfx;
using Project.Infrastructure.Vfx.Impacts;

namespace Project.Tests.EditMode
{
    public sealed class ImpactSurfaceTests
    {
        [Test]
        public void MetalSparksMoreThanDirtAndDirtLingersLonger()
        {
            Assert.Greater(ImpactSurfaceTable.Get(SurfaceKind.Metal).SparkChance, 0.8f);
            Assert.AreEqual(0f, ImpactSurfaceTable.Get(SurfaceKind.Dirt).SparkChance, 1e-4f);
            Assert.Greater(ImpactSurfaceTable.Get(SurfaceKind.Dirt).DustLinger, ImpactSurfaceTable.Get(SurfaceKind.Metal).DustLinger);
        }

        [Test]
        public void WaterAndFoliageLeaveNoDecal()
        {
            Assert.IsFalse(ImpactSurfaceTable.Get(SurfaceKind.Water).LeavesDecal);
            Assert.IsFalse(ImpactSurfaceTable.Get(SurfaceKind.Foliage).LeavesDecal);
            Assert.IsTrue(ImpactSurfaceTable.Get(SurfaceKind.Concrete).LeavesDecal);
        }

        [Test]
        public void LodDegradesWithDistance()
        {
            Assert.AreEqual(ImpactLod.Full, ImpactLodRules.Classify(10f, SurfaceKind.Concrete, 2));
            Assert.AreEqual(ImpactLod.Reduced, ImpactLodRules.Classify(60f, SurfaceKind.Concrete, 2));
            Assert.AreEqual(ImpactLod.DecalOnly, ImpactLodRules.Classify(120f, SurfaceKind.Concrete, 2));
            Assert.AreEqual(ImpactLod.None, ImpactLodRules.Classify(400f, SurfaceKind.Concrete, 2));
        }

        [Test]
        public void LowQualityCullsEarlierAndFoliageNeverDecalOnly()
        {
            Assert.AreEqual(ImpactLod.Reduced, ImpactLodRules.Classify(40f, SurfaceKind.Concrete, 0));
            Assert.AreEqual(ImpactLod.None, ImpactLodRules.Classify(120f, SurfaceKind.Foliage, 3));
        }

        [Test]
        public void SurfaceAngleAndStretch()
        {
            Assert.AreEqual(90f, ImpactLodRules.SurfaceAngleDeg(1f), 0.1f);
            Assert.AreEqual(0f, ImpactLodRules.SurfaceAngleDeg(0f), 0.1f);
            Assert.AreEqual(1f, ImpactLodRules.DecalStretch(90f, 2.4f), 1e-3f);
            Assert.AreEqual(2.4f, ImpactLodRules.DecalStretch(0f, 2.4f), 1e-3f);
            Assert.IsTrue(ImpactLodRules.IsRicochetAngle(5f, SurfaceKind.Metal));
            Assert.IsFalse(ImpactLodRules.IsRicochetAngle(60f, SurfaceKind.Metal));
        }

        [Test]
        public void DecalPressureShortensLifetime()
        {
            Assert.AreEqual(1f, ImpactDecalLifetime.PressureFactor(10, 100), 1e-4f);
            Assert.AreEqual(0.35f, ImpactDecalLifetime.PressureFactor(100, 100), 1e-3f);
            Assert.Less(ImpactDecalLifetime.EffectiveLifetime(SurfaceKind.Metal, 90, 100), 180f);
            Assert.AreEqual(0f, ImpactDecalLifetime.EffectiveLifetime(SurfaceKind.Water, 0, 100), 1e-4f);
        }

        [Test]
        public void DecalAlphaFadesAtEnd()
        {
            Assert.AreEqual(1f, ImpactDecalLifetime.Alpha(SurfaceKind.Concrete, 10f, 120f), 1e-4f);
            Assert.AreEqual(0.5f, ImpactDecalLifetime.Alpha(SurfaceKind.Concrete, 112.5f, 120f), 1e-3f);
            Assert.AreEqual(0f, ImpactDecalLifetime.Alpha(SurfaceKind.Concrete, 130f, 120f), 1e-4f);
            Assert.IsTrue(ImpactDecalLifetime.IsExpired(130f, 120f));
            Assert.IsFalse(ImpactDecalLifetime.IsExpired(1000f, 0f));
        }

        [Test]
        public void BurstLimiterMergesAndCaps()
        {
            var l = new ImpactBurstLimiter(0.1f, 3, 0.3f, 16);
            Assert.IsTrue(l.TryAcceptParticles(0f, 0f, 0f, 0f, SurfaceKind.Concrete));
            Assert.IsFalse(l.TryAcceptParticles(0.01f, 0.1f, 0f, 0f, SurfaceKind.Concrete));
            Assert.IsTrue(l.TryAcceptParticles(0.02f, 5f, 0f, 0f, SurfaceKind.Concrete));
            Assert.IsTrue(l.TryAcceptParticles(0.03f, 9f, 0f, 0f, SurfaceKind.Concrete));
            Assert.IsFalse(l.TryAcceptParticles(0.04f, 20f, 0f, 0f, SurfaceKind.Concrete));
            Assert.IsTrue(l.TryAcceptParticles(0.5f, 20f, 0f, 0f, SurfaceKind.Concrete));
        }
    }
}
