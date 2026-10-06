using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public sealed class NightVisionRulesTests
    {
        [Test]
        public void Battery_DrainsInOneTwentySeconds_AndShutsOff()
        {
            var b = new NightVisionBattery();
            Assert.IsTrue(b.Toggle());
            Assert.IsFalse(b.Tick(119f));
            Assert.IsTrue(b.IsOn);
            Assert.IsTrue(b.Tick(2f));
            Assert.IsFalse(b.IsOn);
            Assert.AreEqual(0f, b.Charge, 1e-4f);
        }

        [Test]
        public void Battery_RechargesWhenOff_AndNeedsThresholdToRestart()
        {
            var b = new NightVisionBattery();
            b.Toggle();
            b.Tick(130f);
            Assert.IsFalse(b.Toggle());
            b.Tick(10f);
            Assert.IsFalse(b.CanTurnOn);
            b.Tick(10f);
            Assert.IsTrue(b.CanTurnOn);
            b.Tick(200f);
            Assert.AreEqual(1f, b.Charge, 1e-4f);
        }

        [Test]
        public void Perception_WorseAtNightWithoutGoggles()
        {
            Assert.Less(NightVisionRules.PerceptionMultiplier(TimeOfDay.Gece, false), 1f);
            Assert.AreEqual(1f, NightVisionRules.PerceptionMultiplier(TimeOfDay.Gece, true));
            Assert.AreEqual(1f, NightVisionRules.PerceptionMultiplier(TimeOfDay.Gunduz, false));
        }

        [Test]
        public void Loadout_LeaderAndRadioman_GetGogglesAtNightOnce()
        {
            var l = NightVisionRules.IssueFor(LoadoutCatalog.For(TeamRole.Leader), TimeOfDay.Gece);
            NightVisionRules.IssueFor(l, TimeOfDay.Gece);
            Assert.AreEqual(1, l.CountOf(ItemIds.NightVision));
            Assert.AreEqual(1, NightVisionRules.IssueFor(LoadoutCatalog.For(TeamRole.Radioman), TimeOfDay.Gece).CountOf(ItemIds.NightVision));
            Assert.AreEqual(0, NightVisionRules.IssueFor(LoadoutCatalog.For(TeamRole.Rifleman), TimeOfDay.Gece).CountOf(ItemIds.NightVision));
            Assert.AreEqual(0, NightVisionRules.IssueFor(LoadoutCatalog.For(TeamRole.Leader), TimeOfDay.Gunduz).CountOf(ItemIds.NightVision));
        }

        [Test]
        public void Catalog_HasGoggles_AsStackableEquipment()
        {
            var d = ItemCatalog.Get(ItemIds.NightVision);
            Assert.IsNotNull(d);
            Assert.AreEqual(ItemCategory.Equipment, d.Category);
            Assert.IsTrue(d.IsStackable);
        }
    }
}
