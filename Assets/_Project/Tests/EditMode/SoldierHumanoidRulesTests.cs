#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Characters;
using Project.Infrastructure.Rendering;

namespace Project.Tests.EditMode
{
    public sealed class SoldierHumanoidRulesTests
    {
        [Test]
        public void AnimTier_IncreasesWithDistance()
        {
            Assert.AreEqual(0, SoldierDetailRules.AnimTier(5f));
            Assert.AreEqual(1, SoldierDetailRules.AnimTier(20f));
            Assert.AreEqual(2, SoldierDetailRules.AnimTier(50f));
            Assert.AreEqual(3, SoldierDetailRules.AnimTier(120f));
        }

        [Test]
        public void Interval_And_SkinBones()
        {
            Assert.AreEqual(0f, SoldierDetailRules.AnimInterval(0), 1e-6f);
            Assert.Greater(SoldierDetailRules.AnimInterval(3), SoldierDetailRules.AnimInterval(1));
            Assert.AreEqual(4, SoldierDetailRules.SkinBonesFor(0));
            Assert.AreEqual(2, SoldierDetailRules.SkinBonesFor(1));
            Assert.AreEqual(1, SoldierDetailRules.SkinBonesFor(3));
        }

        [Test]
        public void DeadForcesFullTier_AndCulling()
        {
            Assert.AreEqual(0, SoldierDetailRules.EffectiveTier(200f, true));
            Assert.IsTrue(SoldierDetailRules.CullCompletely(false, false));
            Assert.IsFalse(SoldierDetailRules.CullCompletely(false, true));
            Assert.IsFalse(SoldierDetailRules.CullCompletely(true, false));
        }

        [Test]
        public void CamoMaterial_PerPalette_AndSlotName()
        {
            Assert.AreEqual(MaterialId.CamoWoodland, SoldierDetailRules.CamoMaterialFor(0));
            Assert.AreEqual(MaterialId.CamoDesert, SoldierDetailRules.CamoMaterialFor(2));
            Assert.AreEqual(MaterialId.CamoUrban, SoldierDetailRules.CamoMaterialFor(3));
            Assert.IsTrue(SoldierDetailRules.IsCamoSlotName("Soldier_Camo (Instance)"));
            Assert.IsFalse(SoldierDetailRules.IsCamoSlotName("Skin"));
            Assert.IsFalse(SoldierDetailRules.IsCamoSlotName(null));
        }

        [Test]
        public void AccessorySocket_Mapping()
        {
            Assert.AreEqual(0, SoldierDetailRules.AccessorySocketFor("Helmet"));
            Assert.AreEqual(0, SoldierDetailRules.AccessorySocketFor("BeretBadge"));
            Assert.AreEqual(1, SoldierDetailRules.AccessorySocketFor("Antenna"));
            Assert.AreEqual(1, SoldierDetailRules.AccessorySocketFor("Vest"));
            Assert.AreEqual(-1, SoldierDetailRules.AccessorySocketFor("Boots"));
        }
    }
}
#endif
