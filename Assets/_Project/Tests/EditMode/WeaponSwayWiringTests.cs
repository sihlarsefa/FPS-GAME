using NUnit.Framework;
using Project.Application.Combat.Feel;

namespace Project.Tests.EditMode
{
    public class WeaponSwayWiringTests
    {
        [Test]
        public void KosuSonrasiSallanmaArtarVeDoner()
        {
            var f = 0f;
            for (var i = 0; i < 40; i++) f = WeaponSwayRules.StepFatigue(f, true, 0.1f);
            Assert.Greater(WeaponSwayRules.Combined(1f - f, 1f, false), 2f);
            for (var i = 0; i < 60; i++) f = WeaponSwayRules.StepFatigue(f, false, 0.1f);
            Assert.AreEqual(1f, WeaponSwayRules.Combined(1f - f, 1f, false), 0.01f);
        }

        [Test]
        public void NefesTutmaYorgunluguBastirir()
        {
            Assert.Less(WeaponSwayRules.Combined(0f, 0.25f, true), WeaponSwayRules.Combined(0f, 1f, false));
        }
    }
}
