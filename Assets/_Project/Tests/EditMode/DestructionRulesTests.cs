using NUnit.Framework;
using Project.Application.Services;

namespace Project.Tests.EditMode
{
    public sealed class DestructionRulesTests
    {
        [Test]
        public void Glass_BreaksInOneBullet()
        {
            Assert.GreaterOrEqual(DestructionRules.BulletDamage(DestructibleKind.Glass, 5f), DestructionRules.DefaultHp(DestructibleKind.Glass));
        }

        [Test]
        public void Wood_NeedsManyBullets()
        {
            var d = DestructionRules.BulletDamage(DestructibleKind.Wood, 30f);
            Assert.Less(d, DestructionRules.DefaultHp(DestructibleKind.Wood));
            Assert.AreEqual(0f, DestructionRules.BulletDamage(DestructibleKind.Wood, 0f));
        }

        [Test]
        public void Explosion_FallsOffAndStopsAtRadius()
        {
            Assert.Greater(DestructionRules.ExplosionDamage(100f, 8f, 1f), DestructionRules.ExplosionDamage(100f, 8f, 6f));
            Assert.AreEqual(0f, DestructionRules.ExplosionDamage(100f, 8f, 8f));
        }

        [Test]
        public void Explosion_ZeroMaxDamage_StillBreaksNearbyWood()
        {
            Assert.GreaterOrEqual(DestructionRules.ExplosionDamage(0f, 8f, 0f), DestructionRules.DefaultHp(DestructibleKind.Wood));
        }

        [Test]
        public void VehicleRam_SlowDoesNothing_FastBreaksFence()
        {
            Assert.AreEqual(0f, DestructionRules.VehicleRamDamage(1f, 8000f));
            Assert.GreaterOrEqual(DestructionRules.VehicleRamDamage(6f, 8000f), DestructionRules.DefaultHp(DestructibleKind.Wood));
        }

        [Test]
        public void ChunkCount_IsBounded()
        {
            Assert.LessOrEqual(DestructionRules.ChunkCount(DestructibleKind.Glass, 100f), 18);
            Assert.GreaterOrEqual(DestructionRules.ChunkCount(DestructibleKind.Wood, 0f), 5);
        }
    }
}
