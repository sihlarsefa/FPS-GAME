using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    /// <summary>InventoryService kapasite/eklenti/zırh kuralları; ItemUseService ve BoostService uç durumları.</summary>
    [TestFixture]
    public sealed class CoverageInventoryTests
    {
        private sealed class NullBus : IEventBus
        {
            public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent { }
            public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent { }
            public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent { }
        }

        private InventoryService _inv;

        [SetUp]
        public void SetUp() => _inv = new InventoryService(new NullBus(), new PlayerId(1));

        [Test]
        public void EmptyInventory_HasBaseCapacity()
        {
            Assert.AreEqual(InventoryService.BaseCapacity, _inv.Capacity, 1e-4f);
            Assert.AreEqual(0f, _inv.CurrentWeight, 1e-6f);
            Assert.AreEqual(InventoryService.BaseCapacity, _inv.FreeCapacity, 1e-4f);
            Assert.IsFalse(_inv.IsOverweight);
        }

        [Test]
        public void Vest_AddsCapacityBonus()
        {
            _inv.EquipArmor(ItemIds.Vest1);
            Assert.AreEqual(InventoryService.BaseCapacity + InventoryService.VestCapacityBonus, _inv.Capacity, 1e-4f);
        }

        [Test]
        public void Backpack_AddsCatalogCapacity()
        {
            var bp = ItemCatalog.Get(ItemIds.Backpack2);
            _inv.EquipBackpack(2);
            Assert.AreEqual(InventoryService.BaseCapacity + bp.Capacity, _inv.Capacity, 1e-4f);
            Assert.AreEqual(2, _inv.BackpackLevel);
        }

        [Test]
        public void EquipBackpack_ClampsLevel()
        {
            _inv.EquipBackpack(99);
            Assert.AreEqual(3, _inv.BackpackLevel);
            _inv.EquipBackpack(-4);
            Assert.AreEqual(0, _inv.BackpackLevel);
        }

        [Test]
        public void MaxAddable_EqualsCapacityDividedByWeight()
        {
            var w = ItemCatalog.Get(ItemIds.Ammo9).Weight;
            Assert.AreEqual((int)Math.Floor(InventoryService.BaseCapacity / w + 1e-3f), _inv.MaxAddable(ItemIds.Ammo9));
        }

        [Test]
        public void MaxAddable_UnknownItem_IsZero()
        {
            Assert.AreEqual(0, _inv.MaxAddable("yok_boyle_esya"));
            Assert.AreEqual(0, _inv.MaxAddable(null));
        }

        [Test]
        public void PickupStack_PartialWhenCapacityShort()
        {
            var item = new LootItemData(ItemIds.MedKit, ItemCategory.Medical, "Sıhhiye", 10);
            var result = _inv.TryPickup(item, new List<LootItemData>());
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(3, result.QuantityTaken); // 60 / 20
            Assert.IsFalse(result.FullyTaken);
            Assert.AreEqual(3, _inv.GetCount(ItemIds.MedKit));
        }

        [Test]
        public void PickupStack_WhenFull_IsRejected()
        {
            _inv.TryPickup(new LootItemData(ItemIds.MedKit, ItemCategory.Medical, "m", 3), null);
            var r = _inv.TryPickup(new LootItemData(ItemIds.MedKit, ItemCategory.Medical, "m", 1), null);
            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(0, r.QuantityTaken);
        }

        [Test]
        public void Pickup_InvalidItem_IsRejected()
        {
            Assert.IsFalse(_inv.TryPickup(default, null).Accepted);
            Assert.IsFalse(_inv.TryPickup(new LootItemData("bilinmeyen", ItemCategory.Medical, "x", 1), null).Accepted);
            Assert.IsFalse(_inv.TryPickup(new LootItemData(ItemIds.Bandage, ItemCategory.Medical, "x", 0), null).Accepted);
        }

        [Test]
        public void Consume_ReducesCountAndWeight()
        {
            _inv.GiveItem(ItemIds.Bandage, 5);
            var weight = _inv.CurrentWeight;
            Assert.IsTrue(_inv.Consume(ItemIds.Bandage, 2));
            Assert.AreEqual(3, _inv.GetCount(ItemIds.Bandage));
            Assert.Less(_inv.CurrentWeight, weight);
        }

        [Test]
        public void Consume_MoreThanOwned_FailsWithoutChange()
        {
            _inv.GiveItem(ItemIds.Bandage, 2);
            Assert.IsFalse(_inv.Consume(ItemIds.Bandage, 3));
            Assert.IsFalse(_inv.Consume(ItemIds.Bandage, 0));
            Assert.IsFalse(_inv.Consume(ItemIds.Bandage, -1));
            Assert.AreEqual(2, _inv.GetCount(ItemIds.Bandage));
        }

        [Test]
        public void GiveItem_IgnoresCapacity_AndMarksOverweight()
        {
            _inv.GiveItem(ItemIds.MedKit, 10);
            Assert.AreEqual(10, _inv.GetCount(ItemIds.MedKit));
            Assert.IsTrue(_inv.IsOverweight);
            Assert.Greater(_inv.LoadFraction, 1f);
        }

        [Test]
        public void Overweight_BlocksNewStacks()
        {
            _inv.GiveItem(ItemIds.MedKit, 10);
            var r = _inv.TryPickup(new LootItemData(ItemIds.Bandage, ItemCategory.Medical, "s", 1), null);
            Assert.IsFalse(r.Accepted);
        }

        [Test]
        public void GiveItem_NonPositiveOrUnknown_DoesNothing()
        {
            _inv.GiveItem(ItemIds.Bandage, 0);
            _inv.GiveItem(ItemIds.Bandage, -3);
            _inv.GiveItem("yok", 4);
            Assert.AreEqual(0f, _inv.CurrentWeight, 1e-6f);
        }

        [Test]
        public void ArmorPickup_BetterReplacesAndDropsOld()
        {
            _inv.EquipArmor(ItemIds.Vest1);
            var dropped = new List<LootItemData>();
            var r = _inv.TryPickup(ItemCatalog.CreateLoot(ItemIds.Vest3, 1), dropped);
            Assert.IsTrue(r.Accepted);
            Assert.AreEqual(1, dropped.Count);
            Assert.AreEqual(ItemIds.Vest1, dropped[0].ItemId);
            Assert.AreEqual(3, _inv.Vest.Level);
        }

        [Test]
        public void ArmorPickup_LowerLevel_Rejected()
        {
            _inv.EquipArmor(ItemIds.Vest3);
            var r = _inv.TryPickup(ItemCatalog.CreateLoot(ItemIds.Vest1, 1), new List<LootItemData>());
            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(3, _inv.Vest.Level);
        }

        [Test]
        public void ArmorPickup_BrokenItem_Rejected()
        {
            var broken = new LootItemData(ItemIds.Vest2, ItemCategory.Armor, "Yelek", 1, -1, 0f);
            Assert.IsFalse(_inv.TryPickup(broken, null).Accepted);
        }

        [Test]
        public void ArmorPickup_SameLevelMoreDurable_Replaces()
        {
            _inv.EquipArmor(ItemIds.Vest2, 10f);
            var r = _inv.TryPickup(ItemCatalog.CreateLoot(ItemIds.Vest2, 1), new List<LootItemData>());
            Assert.IsTrue(r.Accepted);
            Assert.Greater(_inv.Vest.Durability, 10f);
        }

        [Test]
        public void GetArmorFor_BrokenPieceReturnsNull()
        {
            _inv.EquipArmor(ItemIds.Vest1, 0f);
            Assert.IsNull(_inv.GetArmorFor(BodyPart.Torso));
            Assert.IsNull(_inv.GetArmorFor(BodyPart.Leg));
            Assert.IsNull(_inv.GetArmorFor(BodyPart.Head));
        }

        [Test]
        public void GetArmorFor_MapsHeadToHelmetAndTorsoToVest()
        {
            _inv.EquipArmor(ItemIds.Helmet2);
            _inv.EquipArmor(ItemIds.Vest1);
            Assert.AreEqual(ItemIds.Helmet2, _inv.GetArmorFor(BodyPart.Head).ItemId);
            Assert.AreEqual(ItemIds.Vest1, _inv.GetArmorFor(BodyPart.Torso).ItemId);
        }

        [Test]
        public void BackpackPickup_LowerOrEqual_Rejected()
        {
            _inv.EquipBackpack(2);
            Assert.IsFalse(_inv.TryPickup(ItemCatalog.CreateLoot(ItemIds.Backpack1, 1), null).Accepted);
            Assert.IsFalse(_inv.TryPickup(ItemCatalog.CreateLoot(ItemIds.Backpack2, 1), null).Accepted);
        }

        [Test]
        public void BackpackPickup_Higher_DropsOld()
        {
            _inv.EquipBackpack(1);
            var dropped = new List<LootItemData>();
            Assert.IsTrue(_inv.TryPickup(ItemCatalog.CreateLoot(ItemIds.Backpack3, 1), dropped).Accepted);
            Assert.AreEqual(3, _inv.BackpackLevel);
            Assert.AreEqual(1, dropped.Count);
            Assert.AreEqual(ItemIds.Backpack1, dropped[0].ItemId);
        }

        [Test]
        public void Attachment_WithoutWeapon_Rejected()
        {
            Assert.IsFalse(_inv.TryPickup(ItemCatalog.CreateLoot(ItemIds.RedDot, 1), null).Accepted);
            Assert.IsFalse(_inv.WantsItem(ItemCatalog.CreateLoot(ItemIds.RedDot, 1)));
        }

        [Test]
        public void Attachment_AttachesToActiveWeapon()
        {
            _inv.GiveWeapon(WeaponIds.Mpt55, true);
            _inv.SetActiveSlot(_inv.FindWeaponSlot(WeaponIds.Mpt55));
            var r = _inv.TryPickup(ItemCatalog.CreateLoot(ItemIds.RedDot, 1), null);
            Assert.IsTrue(r.Accepted);
            Assert.IsNotNull(_inv.ActiveWeapon.GetAttachment(AttachmentCatalog.Get(ItemIds.RedDot).Slot));
        }

        [Test]
        public void Attachment_SecondInSameSlot_Rejected()
        {
            _inv.GiveWeapon(WeaponIds.Mpt55, true);
            _inv.SetActiveSlot(_inv.FindWeaponSlot(WeaponIds.Mpt55));
            _inv.TryPickup(ItemCatalog.CreateLoot(ItemIds.RedDot, 1), null);
            var second = _inv.TryPickup(ItemCatalog.CreateLoot(ItemIds.RedDot, 1), null);
            Assert.IsFalse(second.Accepted);
        }

        [Test]
        public void DetachFromActive_ReturnsLootAndFreesSlot()
        {
            _inv.GiveWeapon(WeaponIds.Mpt55, true);
            _inv.SetActiveSlot(_inv.FindWeaponSlot(WeaponIds.Mpt55));
            _inv.TryPickup(ItemCatalog.CreateLoot(ItemIds.RedDot, 1), null);
            var slot = AttachmentCatalog.Get(ItemIds.RedDot).Slot;
            Assert.IsTrue(_inv.TryDetachFromActive(slot, out var loot));
            Assert.AreEqual(ItemIds.RedDot, loot.ItemId);
            Assert.IsNull(_inv.ActiveWeapon.GetAttachment(slot));
            Assert.IsFalse(_inv.TryDetachFromActive(slot, out _));
        }

        [Test]
        public void WeaponPickup_ReplacesSlotAndDropsOldWithAttachments()
        {
            _inv.GiveWeapon(WeaponIds.Mpt55, true);
            _inv.GiveWeapon(WeaponIds.Mpt76, true);
            _inv.SetActiveSlot(_inv.FindWeaponSlot(WeaponIds.Mpt55));
            _inv.TryPickup(ItemCatalog.CreateLoot(ItemIds.RedDot, 1), null);
            var dropped = new List<LootItemData>();
            var r = _inv.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.G3), dropped);
            Assert.IsTrue(r.Accepted);
            Assert.GreaterOrEqual(dropped.Count, 2, "eski silah + eklenti");
        }

        [Test]
        public void FindWeaponSlot_UnknownReturnsMinusOne()
        {
            Assert.AreEqual(-1, _inv.FindWeaponSlot(WeaponIds.Mpt55));
            Assert.AreEqual(-1, _inv.FindWeaponSlot(null));
        }

        [Test]
        public void SetActiveSlot_EmptyOrOutOfRange_Fails()
        {
            Assert.IsFalse(_inv.SetActiveSlot(1));
            Assert.IsFalse(_inv.SetActiveSlot(-2));
            Assert.IsFalse(_inv.SetActiveSlot(99));
            Assert.IsTrue(_inv.SetActiveSlot(-1), "silahsız duruş geçerli");
        }

        [Test]
        public void DropAll_EmptiesInventory()
        {
            _inv.GiveItem(ItemIds.Bandage, 4);
            _inv.GiveWeapon(WeaponIds.Mpt55, true);
            _inv.EquipArmor(ItemIds.Vest1);
            var output = new List<LootItemData>();
            _inv.DropAll(output);
            Assert.Greater(output.Count, 0);
            Assert.AreEqual(0, _inv.GetCount(ItemIds.Bandage));
            Assert.IsNull(_inv.Vest);
            Assert.IsFalse(_inv.HasAnyWeapon);
        }

        [Test]
        public void TryDropStack_MoreThanOwned_ClampsToOwned()
        {
            _inv.GiveItem(ItemIds.Bandage, 3);
            Assert.IsFalse(_inv.TryDropStack(ItemIds.Bandage, 0, out _));
            Assert.IsFalse(_inv.TryDropStack("yok", 1, out _));
            Assert.IsTrue(_inv.TryDropStack(ItemIds.Bandage, 2, out var dropped));
            Assert.AreEqual(2, dropped.Quantity);
            Assert.AreEqual(1, _inv.GetCount(ItemIds.Bandage));
            Assert.IsTrue(_inv.TryDropStack(ItemIds.Bandage, 5, out var rest));
            Assert.AreEqual(1, rest.Quantity);
            Assert.AreEqual(0, _inv.GetCount(ItemIds.Bandage));
        }

        [Test]
        public void TakeAmmo_NeverExceedsOwned()
        {
            _inv.GiveItem(ItemIds.Ammo556, 40);
            Assert.AreEqual(25, _inv.TakeAmmo(AmmoType.Mm556, 25));
            Assert.AreEqual(15, _inv.TakeAmmo(AmmoType.Mm556, 100));
            Assert.AreEqual(0, _inv.TakeAmmo(AmmoType.Mm556, 10));
        }

        [Test]
        public void InfiniteAmmo_ReportsConstant()
        {
            _inv.InfiniteAmmo = true;
            Assert.AreEqual(InventoryService.InfiniteAmmoReport, _inv.GetAmmo(AmmoType.Mm9));
        }

        [Test]
        public void BestHealItem_PrefersBandageForSmallDeficit()
        {
            _inv.GiveItem(ItemIds.Bandage, 3);
            _inv.GiveItem(ItemIds.MedKit, 1);
            Assert.AreEqual(ItemIds.Bandage, _inv.BestHealItem(70f));
            Assert.IsNull(new InventoryService(new NullBus(), new PlayerId(2)).BestHealItem(10f));
        }

        [Test]
        public void Changed_FiresOnMutation()
        {
            var n = 0;
            _inv.Changed += () => n++;
            _inv.GiveItem(ItemIds.Bandage, 1);
            _inv.Consume(ItemIds.Bandage, 1);
            Assert.GreaterOrEqual(n, 2);
        }

        [Test]
        public void GetStacks_ListsOnlyPositive_InCatalogOrder()
        {
            _inv.GiveItem(ItemIds.Bandage, 2);
            _inv.GiveItem(ItemIds.Ammo9, 10);
            var list = new List<KeyValuePair<string, int>>();
            _inv.GetStacks(list);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(ItemIds.Ammo9, list[0].Key);
            Assert.AreEqual(ItemIds.Bandage, list[1].Key);
        }
    }

    [TestFixture]
    public sealed class CoverageItemUseTests
    {
        private sealed class NullBus : IEventBus
        {
            public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent { }
            public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent { }
            public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent { }
        }

        private sealed class Target : IHealable
        {
            public float Total;
            public void Heal(float amount) => Total += amount;
        }

        private static readonly PlayerId Owner = new(5);
        private InventoryService _inv;
        private HealthService _health;
        private BoostService _boost;
        private ItemUseService _use;

        [SetUp]
        public void SetUp()
        {
            var bus = new NullBus();
            _inv = new InventoryService(bus, Owner);
            _health = new HealthService(Owner, 100f, bus);
            _boost = new BoostService();
            _use = new ItemUseService(Owner, _inv, _health, _boost, bus);
        }

        private void Hurt(float amount) => _health.ApplyDamage(new DamageInfo(amount, PlayerId.Invalid, "t"));

        [Test]
        public void CannotUse_UnknownOrUnownedItem()
        {
            Assert.IsFalse(_use.TryBegin("yok"));
            Assert.IsFalse(_use.TryBegin(null));
            Hurt(50f);
            Assert.IsFalse(_use.TryBegin(ItemIds.Bandage));
        }

        [Test]
        public void CannotUse_HealWhenAtOrAboveCap()
        {
            _inv.GiveItem(ItemIds.Bandage, 1);
            Assert.IsFalse(_use.CanUse(ItemIds.Bandage)); // 100 can
            Hurt(20f); // 80 > bandage cap 75
            Assert.IsFalse(_use.CanUse(ItemIds.Bandage));
            Hurt(10f); // 70
            Assert.IsTrue(_use.CanUse(ItemIds.Bandage));
        }

        [Test]
        public void CannotUse_Ammo_Or_Grenade()
        {
            _inv.GiveItem(ItemIds.Ammo9, 5);
            _inv.GiveItem(ItemIds.FragGrenade, 1);
            Assert.IsFalse(_use.CanUse(ItemIds.Ammo9));
            Assert.IsFalse(_use.CanUse(ItemIds.FragGrenade));
        }

        [Test]
        public void Progress_AdvancesAndMovementSlows()
        {
            Hurt(50f);
            _inv.GiveItem(ItemIds.Bandage, 1);
            Assert.IsTrue(_use.TryBegin(ItemIds.Bandage));
            Assert.IsTrue(_use.IsUsing);
            Assert.AreEqual(ItemUseService.UsingSpeedMultiplier, _use.MovementSpeedMultiplier, 1e-6f);
            _use.Tick(2f);
            Assert.AreEqual(0.5f, _use.Progress, 0.01f);
            Assert.AreEqual(2f, _use.RemainingSeconds, 0.01f);
        }

        [Test]
        public void Idle_HasNoSlowdown()
        {
            Assert.AreEqual(1f, _use.MovementSpeedMultiplier, 1e-6f);
            Assert.IsFalse(_use.IsUsing);
            Assert.IsNull(_use.CurrentItemId);
        }

        [Test]
        public void Beginning_SameItemAgain_IsRejected()
        {
            Hurt(50f);
            _inv.GiveItem(ItemIds.Bandage, 3);
            Assert.IsTrue(_use.TryBegin(ItemIds.Bandage));
            Assert.IsFalse(_use.TryBegin(ItemIds.Bandage));
        }

        [Test]
        public void Beginning_Different_CancelsPrevious()
        {
            Hurt(50f);
            _inv.GiveItem(ItemIds.Bandage, 1);
            _inv.GiveItem(ItemIds.FirstAid, 1);
            var cancelled = new List<string>();
            _use.Cancelled += cancelled.Add;
            _use.TryBegin(ItemIds.Bandage);
            Assert.IsTrue(_use.TryBegin(ItemIds.FirstAid));
            Assert.AreEqual(1, cancelled.Count);
            Assert.AreEqual(ItemIds.Bandage, cancelled[0]);
            Assert.AreEqual(ItemIds.FirstAid, _use.CurrentItemId);
        }

        [Test]
        public void Cancel_DoesNotConsumeItem()
        {
            Hurt(50f);
            _inv.GiveItem(ItemIds.Bandage, 1);
            _use.TryBegin(ItemIds.Bandage);
            _use.Tick(1f);
            _use.Cancel();
            Assert.AreEqual(1, _inv.GetCount(ItemIds.Bandage));
            Assert.IsFalse(_use.IsUsing);
            _use.Cancel();
        }

        [Test]
        public void Death_CancelsUse_WithoutConsuming()
        {
            Hurt(50f);
            _inv.GiveItem(ItemIds.FirstAid, 1);
            _use.TryBegin(ItemIds.FirstAid);
            Hurt(500f);
            _use.Tick(0.1f);
            Assert.IsFalse(_use.IsUsing);
            Assert.AreEqual(1, _inv.GetCount(ItemIds.FirstAid));
        }

        [Test]
        public void ItemDropped_DuringUse_Cancels()
        {
            Hurt(50f);
            _inv.GiveItem(ItemIds.Bandage, 1);
            _use.TryBegin(ItemIds.Bandage);
            _inv.Consume(ItemIds.Bandage, 1);
            _use.Tick(0.1f);
            Assert.IsFalse(_use.IsUsing);
        }

        [Test]
        public void Completion_ConsumesAndHealsCapped()
        {
            Hurt(50f);
            _inv.GiveItem(ItemIds.Bandage, 2);
            var completed = 0;
            _use.Completed += _ => completed++;
            _use.TryBegin(ItemIds.Bandage);
            _use.Tick(10f);
            Assert.AreEqual(1, completed);
            Assert.AreEqual(1, _inv.GetCount(ItemIds.Bandage));
            Assert.AreEqual(60f, _health.Current, 0.01f);
        }

        [Test]
        public void Heal_NeverExceedsItemCap()
        {
            Hurt(28f); // 72
            _inv.GiveItem(ItemIds.Bandage, 1);
            _use.TryBegin(ItemIds.Bandage);
            _use.Tick(10f);
            Assert.AreEqual(75f, _health.Current, 0.01f);
        }

        [Test]
        public void Boost_Completion_AddsBoostAmount()
        {
            _inv.GiveItem(ItemIds.EnergyDrink, 1);
            Assert.IsTrue(_use.TryBegin(ItemIds.EnergyDrink));
            _use.Tick(10f);
            Assert.AreEqual(ItemCatalog.Get(ItemIds.EnergyDrink).BoostAmount, _boost.Value, 1e-3f);
        }

        [Test]
        public void Boost_NotUsableWhenFull()
        {
            _boost.Add(500f);
            _inv.GiveItem(ItemIds.EnergyDrink, 1);
            Assert.IsFalse(_use.CanUse(ItemIds.EnergyDrink));
        }

        [Test]
        public void TryBeginBestHeal_NoItems_False()
        {
            Hurt(50f);
            Assert.IsFalse(_use.TryBeginBestHeal());
        }

        [Test]
        public void TryBeginBestHeal_DeadOwner_False()
        {
            _inv.GiveItem(ItemIds.MedKit, 1);
            Hurt(999f);
            Assert.IsFalse(_use.TryBeginBestHeal());
        }

        [Test]
        public void TryBeginBestBoost_PicksAvailable()
        {
            _inv.GiveItem(ItemIds.Painkiller, 1);
            Assert.IsTrue(_use.TryBeginBestBoost());
            Assert.AreEqual(ItemIds.Painkiller, _use.CurrentItemId);
        }

        [Test]
        public void ZeroDeltaTick_DoesNotAdvance()
        {
            Hurt(50f);
            _inv.GiveItem(ItemIds.Bandage, 1);
            _use.TryBegin(ItemIds.Bandage);
            _use.Tick(0f);
            _use.Tick(-1f);
            Assert.AreEqual(0f, _use.Progress, 1e-6f);
        }

        // ------------------------------------------------------------------ BoostService

        [Test]
        public void Boost_AddClampsAtMax_AndIgnoresBadAmounts()
        {
            _boost.Add(250f);
            Assert.AreEqual(BoostService.Max, _boost.Value, 1e-4f);
            var b = new BoostService();
            b.Add(-5f); b.Add(0f); b.Add(float.NaN); b.Add(float.PositiveInfinity);
            Assert.AreEqual(0f, b.Value, 1e-6f);
            Assert.IsFalse(b.IsActive);
        }

        [Test]
        public void Boost_HealRateTiers()
        {
            Assert.AreEqual(0f, BoostService.HealRateFor(0f), 1e-6f);
            Assert.AreEqual(BoostService.HealTier1Rate, BoostService.HealRateFor(10f), 1e-6f);
            Assert.AreEqual(BoostService.HealTier2Rate, BoostService.HealRateFor(50f), 1e-6f);
            Assert.AreEqual(BoostService.HealTier3Rate, BoostService.HealRateFor(90f), 1e-6f);
        }

        [Test]
        public void Boost_SpeedBonusOnlyAboveThreshold()
        {
            var b = new BoostService();
            b.Add(BoostService.SpeedBonusThreshold);
            Assert.AreEqual(1f, b.SpeedMultiplier, 1e-6f);
            b.Add(1f);
            Assert.AreEqual(BoostService.SpeedBonusMultiplier, b.SpeedMultiplier, 1e-6f);
        }

        [Test]
        public void Boost_DecaysToZero_AndHeals()
        {
            var b = new BoostService();
            var t = new Target();
            b.Add(30f);
            for (var i = 0; i < 2000 && b.IsActive; i++) b.Tick(0.1f, t, true);
            Assert.IsFalse(b.IsActive);
            Assert.Greater(t.Total, 0f);
        }

        [Test]
        public void Boost_DeadTarget_DecaysWithoutHealing()
        {
            var b = new BoostService();
            var t = new Target();
            b.Add(50f);
            b.Tick(10f, t, false);
            Assert.AreEqual(0f, t.Total, 1e-6f);
            Assert.Less(b.Value, 50f);
        }

        [Test]
        public void Boost_LargeStep_HealsSameAsManySmallSteps()
        {
            var a = new BoostService(); var b = new BoostService();
            var ta = new Target(); var tb = new Target();
            a.Add(100f); b.Add(100f);
            a.Tick(40f, ta, true);
            for (var i = 0; i < 400; i++) b.Tick(0.1f, tb, true);
            Assert.AreEqual(tb.Total, ta.Total, 1.5f);
        }

        [Test]
        public void Boost_Reset_ZerosAndNotifies()
        {
            var n = 0;
            _boost.Changed += () => n++;
            _boost.Add(40f);
            _boost.Reset();
            Assert.AreEqual(0f, _boost.Value, 1e-6f);
            Assert.AreEqual(2, n);
            _boost.Reset();
            Assert.AreEqual(2, n, "boş sıfırlama olay yaratmaz");
        }

        [Test]
        public void Boost_Normalized_IsFraction()
        {
            _boost.Add(25f);
            Assert.AreEqual(0.25f, _boost.Normalized, 1e-5f);
        }
    }
}
