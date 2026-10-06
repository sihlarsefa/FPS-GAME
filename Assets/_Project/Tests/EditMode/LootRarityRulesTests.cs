using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Loot;

namespace Project.Tests.EditMode
{
    public sealed class LootRarityRulesTests
    {
        [Test]
        public void BeamFade_FullNearZeroFar()
        {
            Assert.AreEqual(1f, LootRarityRules.BeamFade(10f), 1e-4f);
            Assert.AreEqual(0.5f, LootRarityRules.BeamFade(35f), 1e-4f);
            Assert.AreEqual(0f, LootRarityRules.BeamFade(45f), 1e-4f);
        }

        [Test]
        public void Counts_GrowWithTier()
        {
            for (var t = 0; t < 3; t++)
            {
                Assert.Less(LootRarityRules.MaxBeams(t), LootRarityRules.MaxBeams(t + 1));
                Assert.Less(LootRarityRules.MaxMarkers(t), LootRarityRules.MaxMarkers(t + 1));
            }
        }

        [Test]
        public void GearLevel_MapsToRarity()
        {
            Assert.AreEqual(LootRarity.Uncommon, LootRarityRules.FromGearLevel(1));
            Assert.AreEqual(LootRarity.Rare, LootRarityRules.FromGearLevel(2));
            Assert.AreEqual(LootRarity.Epic, LootRarityRules.FromGearLevel(3));
        }

        [Test]
        public void WeaponCategory_SniperEpic_PistolCommon()
        {
            Assert.AreEqual(LootRarity.Epic, LootRarityRules.FromWeaponCategory(WeaponCategory.Sniper));
            Assert.AreEqual(LootRarity.Common, LootRarityRules.FromWeaponCategory(WeaponCategory.Pistol));
        }

        [Test]
        public void PopScale_PeaksThenVanishes()
        {
            Assert.AreEqual(1f, LootRarityRules.PopScale(0f), 1e-4f);
            Assert.AreEqual(1.3f, LootRarityRules.PopScale(0.35f), 1e-4f);
            Assert.AreEqual(0f, LootRarityRules.PopScale(1f), 1e-4f);
        }

        [Test]
        public void Strobe_BlinksAndDescentScales()
        {
            Assert.IsTrue(LootRarityRules.StrobeOn(0.05f));
            Assert.IsFalse(LootRarityRules.StrobeOn(0.5f));
            Assert.AreEqual(75f, LootRarityRules.DescentHeight(150f, 30f, 60f), 1e-3f);
            Assert.AreEqual(0f, LootRarityRules.DescentHeight(150f, 0f, 60f), 1e-3f);
        }
    }
}
