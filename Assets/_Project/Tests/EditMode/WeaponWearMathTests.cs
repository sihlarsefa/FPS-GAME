#if UNITY_EDITOR
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Weapons;

namespace Project.Tests.EditMode
{
    public sealed class WeaponWearMathTests
    {
        [Test]
        public void AddShot_Increases_AndSaturates()
        {
            var c = 0f;
            for (var i = 0; i < 5000; i++)
                c = WeaponWearMath.AddShot(c, 0.01f);
            Assert.IsTrue(c <= 1f && c > 0.9f);
            Assert.IsTrue(WeaponWearMath.AddShot(0.2f, 0.01f) > 0.2f);
        }

        [Test]
        public void AddShot_SlowsNearSaturation()
        {
            var low = WeaponWearMath.AddShot(0f, 0.01f) - 0f;
            var high = WeaponWearMath.AddShot(0.9f, 0.01f) - 0.9f;
            Assert.IsTrue(high < low);
        }

        [Test]
        public void Clean_DecreasesOverTime_NotBelowZero()
        {
            Assert.IsTrue(WeaponWearMath.Clean(0.5f, 10f) < 0.5f);
            Assert.AreEqual(0f, WeaponWearMath.Clean(0.1f, 100000f), 1e-6f);
            Assert.AreEqual(0.4f, WeaponWearMath.Clean(0.4f, -5f), 1e-6f);
        }

        [Test]
        public void CarbonPerShot_SniperAbovePistol()
        {
            Assert.IsTrue(WeaponWearMath.CarbonPerShot(WeaponCategory.Sniper) > WeaponWearMath.CarbonPerShot(WeaponCategory.Pistol));
        }

        [Test]
        public void MuzzleMask_RisesTowardMuzzle()
        {
            var back = WeaponWearMath.MuzzleMask(0.1f, 0.6f, 0.15f);
            var tip = WeaponWearMath.MuzzleMask(0.6f, 0.6f, 0.15f);
            Assert.AreEqual(0f, back, 1e-5f);
            Assert.IsTrue(tip > 0.9f);
        }

        [Test]
        public void EdgeMask_StrongAtEdge_ZeroInside()
        {
            Assert.IsTrue(WeaponWearMath.EdgeMask(0f, 0.002f, 0.5f) > 0.99f);
            Assert.AreEqual(0f, WeaponWearMath.EdgeMask(0.05f, 0.002f, 0.5f), 1e-5f);
        }

        [Test]
        public void EdgeWear_ScalesWithWearAmount()
        {
            Assert.AreEqual(0f, WeaponWearMath.EdgeWear(1f, 0f, 0.5f), 1e-5f);
            Assert.IsTrue(WeaponWearMath.EdgeWear(1f, 0.8f, 0.5f) > WeaponWearMath.EdgeWear(1f, 0.2f, 0.5f));
        }

        [Test]
        public void FillBias_ZeroOutdoors_PositiveInDarkInterior()
        {
            Assert.AreEqual(0f, WeaponWearMath.FillBias(0.5f), 1e-6f);
            var dark = WeaponWearMath.FillBias(0.01f);
            Assert.IsTrue(dark > 0f && dark <= 0.1f);
            Assert.AreEqual(0f, WeaponWearMath.FillBias(float.NaN) > 0.1f ? 1f : 0f, 1e-6f);
        }

        [Test]
        public void CarbonState_ShotsAccumulate_ThenClean()
        {
            WeaponCarbonState.Reset();
            for (var i = 0; i < 30; i++)
                WeaponCarbonState.RegisterShot("t_rifle", WeaponCategory.AssaultRifle, 10f);
            var after = WeaponCarbonState.Get("t_rifle", 10f);
            Assert.IsTrue(after > 0.1f);
            Assert.IsTrue(WeaponCarbonState.Get("t_rifle", 400f) < after);
            Assert.AreEqual(0f, WeaponCarbonState.Get("other", 10f), 1e-6f);
            WeaponCarbonState.Reset();
        }
    }
}
#endif
