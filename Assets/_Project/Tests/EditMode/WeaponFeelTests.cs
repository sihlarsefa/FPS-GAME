using NUnit.Framework;
using Project.Application.Combat.Feel;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class WeaponFeelTests
    {
        [Test]
        public void Rifle762_HarsherThan556()
        {
            var h = FireFeelRules.KickPitchMultiplier(WeaponCategory.AssaultRifle, AmmoType.Mm762, 5, false);
            var l = FireFeelRules.KickPitchMultiplier(WeaponCategory.AssaultRifle, AmmoType.Mm556, 5, false);
            Assert.Greater(h, l * 1.2f);
        }

        [Test]
        public void FirstShotKick_GreaterThanLater()
        {
            var first = FireFeelRules.KickPitchMultiplier(WeaponCategory.AssaultRifle, AmmoType.Mm762, 1, false);
            var later = FireFeelRules.KickPitchMultiplier(WeaponCategory.AssaultRifle, AmmoType.Mm762, 6, false);
            Assert.Greater(first, later);
            RecoilPattern.GetStep(RecoilPattern.SeedFor("MPT-76"), 0, out var v0, out _);
            RecoilPattern.GetStep(RecoilPattern.SeedFor("MPT-76"), 10, out var v10, out _);
            Assert.Greater(v0, v10);
        }

        [Test]
        public void Pattern_IsSeededAndDiffersPerWeapon()
        {
            RecoilPattern.GetStep(RecoilPattern.SeedFor("A"), 7, out var v1, out var h1);
            RecoilPattern.GetStep(RecoilPattern.SeedFor("A"), 7, out var v2, out var h2);
            RecoilPattern.GetStep(RecoilPattern.SeedFor("B"), 7, out _, out var h3);
            Assert.AreEqual(v1, v2, 0.0001f);
            Assert.AreEqual(h1, h2, 0.0001f);
            Assert.IsTrue(System.Math.Abs(h1 - h3) > 0.0001f);
        }

        [Test]
        public void Fatigue_GrowsWhileSprintingAndRecovers()
        {
            var f = 0f;
            for (var i = 0; i < 30; i++) f = WeaponSwayRules.StepFatigue(f, true, 0.1f);
            Assert.Greater(f, 0.9f);
            for (var i = 0; i < 60; i++) f = WeaponSwayRules.StepFatigue(f, false, 0.1f);
            Assert.AreEqual(0f, f, 0.001f);
        }

        [Test]
        public void StaminaSway_TiredIsWorse_HoldBreathCalms()
        {
            Assert.AreEqual(1f, WeaponSwayRules.StaminaSwayMultiplier(1f), 0.001f);
            Assert.Greater(WeaponSwayRules.StaminaSwayMultiplier(0f), 2f);
            var normal = WeaponSwayRules.Combined(0f, HoldBreathState.NormalSway, false);
            var held = WeaponSwayRules.Combined(0f, HoldBreathState.SteadySway, true);
            Assert.Less(held, normal * 0.4f);
        }

        [Test]
        public void HoldBreath_LastsThreeToFourSeconds()
        {
            Assert.IsTrue(HoldBreathState.MaxSeconds >= 3f && HoldBreathState.MaxSeconds <= 4f);
        }
    }
}
