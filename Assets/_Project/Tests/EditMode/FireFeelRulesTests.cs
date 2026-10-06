using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class FireFeelRulesTests
    {
        [Test]
        public void Rifle762_FirstShot_Is30PercentStronger()
        {
            var first = FireFeelRules.KickPitchMultiplier(WeaponCategory.AssaultRifle, AmmoType.Mm762, 1, false);
            var later = FireFeelRules.KickPitchMultiplier(WeaponCategory.AssaultRifle, AmmoType.Mm762, 5, false);
            Assert.AreEqual(1.3f, first / later, 1e-4f);
            Assert.Greater(FireFeelRules.FlashScale(WeaponCategory.AssaultRifle, AmmoType.Mm762, 1, false),
                FireFeelRules.FlashScale(WeaponCategory.AssaultRifle, AmmoType.Mm762, 4, false));
        }

        [Test]
        public void Rifle556_IsSmootherThan762()
        {
            Assert.Less(FireFeelRules.For(WeaponCategory.AssaultRifle, AmmoType.Mm556).KickPitch,
                FireFeelRules.For(WeaponCategory.AssaultRifle, AmmoType.Mm762).KickPitch);
            Assert.AreEqual(1f, FireFeelRules.FirstShotKick(WeaponCategory.AssaultRifle, AmmoType.Mm556, 1));
        }

        [Test]
        public void Suppressor_Cuts_Kick_To60Percent_AndFlashNeutral()
        {
            var open = FireFeelRules.KickPitchMultiplier(WeaponCategory.Smg, AmmoType.Mm9, 3, false);
            var sup = FireFeelRules.KickPitchMultiplier(WeaponCategory.Smg, AmmoType.Mm9, 3, true);
            Assert.AreEqual(0.6f, sup / open, 1e-4f);
            Assert.AreEqual(1f, FireFeelRules.FlashScale(WeaponCategory.Smg, AmmoType.Mm9, 3, true));
        }

        [Test]
        public void Shotgun_HeavySingle_WithPumpDelay()
        {
            var f = FireFeelRules.For(WeaponCategory.Shotgun, AmmoType.Gauge12);
            Assert.Greater(f.KickPitch, 1.2f);
            Assert.Greater(f.PumpDelay, 0.2f);
            Assert.Less(FireFeelRules.PumpReboundPitch(2f), 0f);
        }

        [Test]
        public void Lmg_ShakeAccumulates_CapsAndDecays()
        {
            var s = 0f;
            for (var i = 0; i < 100; i++)
                s = FireFeelRules.ShakeAccumulate(s, WeaponCategory.Lmg, AmmoType.Mm762);
            Assert.AreEqual(FireFeelRules.MaxShake, s, 1e-5f);
            Assert.AreEqual(0f, FireFeelRules.ShakeDecay(s, 10f), 1e-5f);
            Assert.AreEqual(0f, FireFeelRules.ShakeAccumulate(0f, WeaponCategory.AssaultRifle, AmmoType.Mm556));
        }

        [Test]
        public void SmokeRate_GrowsWithTempoAndSpray_SuppressedLess()
        {
            var cold = FireFeelRules.SmokeRate(WeaponCategory.AssaultRifle, AmmoType.Mm556, 1, 0.1f, false);
            var hot = FireFeelRules.SmokeRate(WeaponCategory.AssaultRifle, AmmoType.Mm556, 20, 0.1f, false);
            var fast = FireFeelRules.SmokeRate(WeaponCategory.AssaultRifle, AmmoType.Mm556, 20, 0.05f, false);
            var sup = FireFeelRules.SmokeRate(WeaponCategory.AssaultRifle, AmmoType.Mm556, 20, 0.1f, true);
            Assert.Greater(hot, cold);
            Assert.GreaterOrEqual(fast, hot);
            Assert.Less(sup, hot);
        }

        [Test]
        public void Table_AllCategories_AreFiniteAndPositive()
        {
            foreach (WeaponCategory c in System.Enum.GetValues(typeof(WeaponCategory)))
            foreach (AmmoType a in System.Enum.GetValues(typeof(AmmoType)))
            {
                var f = FireFeelRules.For(c, a);
                Assert.Greater(f.KickPitch, 0f);
                Assert.Greater(f.KickYaw, 0f);
                Assert.Greater(f.FlashScale, 0f);
                Assert.Greater(f.SmokeRate, 0f);
                Assert.Greater(FireFeelRules.SmokeRate(c, a, 0, float.NaN, false), 0f);
            }
        }
    }
}
