using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    /// <summary>ItemCatalog, InventoryService, LoadoutCatalog ve LootSpawnService testleri.</summary>
    public sealed class InventoryTests
    {
        private sealed class RecordingBus : IEventBus
        {
            public readonly List<object> Events = new();
            public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent => Events.Add(gameEvent);
            public void Subscribe<TEvent>(System.Action<TEvent> handler) where TEvent : IGameEvent { }
            public void Unsubscribe<TEvent>(System.Action<TEvent> handler) where TEvent : IGameEvent { }
        }

        private RecordingBus _bus;
        private InventoryService _inventory;
        private List<LootItemData> _dropped;

        [SetUp]
        public void SetUp()
        {
            _bus = new RecordingBus();
            _inventory = new InventoryService(_bus, new PlayerId(7));
            _dropped = new List<LootItemData>();
        }

        private static readonly string[] AllWeaponIds =
        {
            WeaponIds.Sar9, WeaponIds.Tp9, WeaponIds.Sar109, WeaponIds.Mpt55, WeaponIds.Mpt76,
            WeaponIds.G3, WeaponIds.Knt76, WeaponIds.Jng90, WeaponIds.Pmt76, WeaponIds.Escort
        };

        // ------------------------------------------------------------------ ItemCatalog

        [Test]
        public void Catalog_IdsAreUnique_AndNetworkIndexRoundTrips()
        {
            var seen = new HashSet<string>();
            for (var i = 0; i < ItemCatalog.All.Count; i++)
            {
                var definition = ItemCatalog.All[i];
                Assert.IsTrue(seen.Add(definition.Id), "duplicate id " + definition.Id);
                Assert.AreEqual(i, ItemCatalog.GetNetworkIndex(definition.Id));
                Assert.AreEqual(definition.Id, ItemCatalog.FromNetworkIndex(i));
                Assert.IsFalse(string.IsNullOrEmpty(definition.DisplayName));
            }

            Assert.AreEqual(-1, ItemCatalog.GetNetworkIndex("does_not_exist"));
            Assert.AreEqual(-1, ItemCatalog.GetNetworkIndex(null));
            Assert.IsNull(ItemCatalog.FromNetworkIndex(-1));
            Assert.IsNull(ItemCatalog.FromNetworkIndex(ItemCatalog.All.Count));
        }

        [Test]
        public void Catalog_NetworkOrderIsStable()
        {
            Assert.AreEqual(0, ItemCatalog.GetNetworkIndex(ItemIds.Ammo9));
            Assert.AreEqual(3, ItemCatalog.GetNetworkIndex(ItemIds.Ammo12));
            Assert.AreEqual(4, ItemCatalog.GetNetworkIndex(ItemIds.Bandage));
            Assert.AreEqual(19, ItemCatalog.GetNetworkIndex(ItemIds.Backpack3));
            Assert.AreEqual(20, ItemCatalog.GetNetworkIndex(WeaponIds.Sar9));
        }

        [Test]
        public void Catalog_EveryWeaponIsAWeaponItem_WithMatchingAmmo()
        {
            foreach (var weaponId in AllWeaponIds)
            {
                Assert.IsTrue(ItemCatalog.TryGet(weaponId, out var item), weaponId);
                Assert.AreEqual(ItemCategory.Weapon, item.Category);
                Assert.AreEqual(weaponId, item.WeaponId);
                Assert.IsFalse(item.IsStackable);

                var weapon = WeaponCatalog.Get(weaponId);
                Assert.IsNotNull(weapon, weaponId);
                Assert.AreEqual(weapon.AmmoType, item.AmmoType, weaponId);
            }

            foreach (var weapon in WeaponCatalog.All)
                Assert.IsTrue(ItemCatalog.IsWeapon(weapon.WeaponId), "missing weapon item " + weapon.WeaponId);
        }

        [Test]
        public void Catalog_TurkishNamesAndValues()
        {
            Assert.AreEqual("9mm Mermi", ItemCatalog.Get(ItemIds.Ammo9).DisplayName);
            Assert.AreEqual("12 Kalibre Fişek", ItemCatalog.Get(ItemIds.Ammo12).DisplayName);
            Assert.AreEqual("Sargı Bezi", ItemCatalog.Get(ItemIds.Bandage).DisplayName);
            Assert.AreEqual("İlk Yardım Çantası", ItemCatalog.Get(ItemIds.FirstAid).DisplayName);
            Assert.AreEqual("Sıhhiye Çantası", ItemCatalog.Get(ItemIds.MedKit).DisplayName);
            Assert.AreEqual("Çelik Yelek (Sv.2)", ItemCatalog.Get(ItemIds.Vest2).DisplayName);
            Assert.AreEqual("Kask (Sv.3)", ItemCatalog.Get(ItemIds.Helmet3).DisplayName);
            Assert.AreEqual("Sırt Çantası (Sv.1)", ItemCatalog.Get(ItemIds.Backpack1).DisplayName);

            Assert.AreEqual(0.375f, ItemCatalog.Get(ItemIds.Ammo9).Weight, 1e-5f);
            Assert.AreEqual(1.25f, ItemCatalog.Get(ItemIds.Ammo12).Weight, 1e-5f);
            Assert.AreEqual(20f, ItemCatalog.Get(ItemIds.MedKit).Weight, 1e-5f);

            var bandage = ItemCatalog.Get(ItemIds.Bandage);
            Assert.AreEqual(10f, bandage.HealAmount, 1e-5f);
            Assert.AreEqual(75f, bandage.HealCap, 1e-5f);
            Assert.AreEqual(4f, bandage.UseSeconds, 1e-5f);

            Assert.AreEqual(250f, ItemCatalog.Get(ItemIds.Vest3).Durability, 1e-5f);
            Assert.AreEqual(0.55f, ItemCatalog.Get(ItemIds.Helmet3).DamageReduction, 1e-5f);
            Assert.AreEqual(150f, ItemCatalog.Get(ItemIds.Helmet2).Durability, 1e-5f);
            Assert.AreEqual(200f, ItemCatalog.Get(ItemIds.Backpack2).Capacity, 1e-5f);
        }

        [Test]
        public void Catalog_AmmoIdsAndCreateLoot()
        {
            Assert.AreEqual(ItemIds.Ammo9, ItemCatalog.AmmoItemId(AmmoType.Mm9));
            Assert.AreEqual(ItemIds.Ammo556, ItemCatalog.AmmoItemId(AmmoType.Mm556));
            Assert.AreEqual(ItemIds.Ammo762, ItemCatalog.AmmoItemId(AmmoType.Mm762));
            Assert.AreEqual(ItemIds.Ammo12, ItemCatalog.AmmoItemId(AmmoType.Gauge12));
            Assert.IsNull(ItemCatalog.AmmoItemId(AmmoType.None));

            Assert.AreEqual(30, ItemCatalog.CreateLoot(ItemIds.Ammo762).Quantity);
            Assert.AreEqual(10, ItemCatalog.CreateLoot(ItemIds.Ammo12).Quantity);
            Assert.AreEqual(5, ItemCatalog.CreateLoot(ItemIds.Bandage).Quantity);
            Assert.AreEqual(1, ItemCatalog.CreateLoot(ItemIds.MedKit).Quantity);
            Assert.AreEqual(12, ItemCatalog.CreateLoot(ItemIds.Ammo9, 12).Quantity);

            var loot = ItemCatalog.CreateLoot(ItemIds.Vest1);
            Assert.AreEqual(ItemCategory.Armor, loot.Category);
            Assert.AreEqual(-1f, loot.Durability, 1e-5f);
            Assert.IsFalse(ItemCatalog.CreateLoot("unknown").IsValid);
            Assert.IsNull(ItemCatalog.Get("unknown"));
            Assert.IsNull(ItemCatalog.Get(null));
        }

        // ------------------------------------------------------------------ Kapasite ve yığınlar

        [Test]
        public void Capacity_GrowsWithVestAndBackpack()
        {
            Assert.AreEqual(InventoryService.BaseCapacity, _inventory.Capacity, 1e-4f);

            _inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Vest1), _dropped);
            Assert.AreEqual(InventoryService.BaseCapacity + InventoryService.VestCapacityBonus, _inventory.Capacity, 1e-4f);

            _inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Backpack3), _dropped);
            Assert.AreEqual(3, _inventory.BackpackLevel);
            Assert.AreEqual(60f + 50f + 250f, _inventory.Capacity, 1e-4f);
        }

        [Test]
        public void Stack_PickupIsPartialWhenOverCapacity()
        {
            // 60 / 0.375 = 160 adet 9mm sığar.
            var result = _inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Ammo9, 200), _dropped);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(160, result.QuantityTaken);
            Assert.IsFalse(result.FullyTaken);
            Assert.AreEqual(160, _inventory.GetCount(ItemIds.Ammo9));
            Assert.AreEqual(60f, _inventory.CurrentWeight, 1e-3f);

            var full = _inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Bandage), _dropped);
            Assert.IsFalse(full.Accepted);
            Assert.AreEqual(0, full.QuantityTaken);
            Assert.AreEqual(0, _dropped.Count);
        }

        [Test]
        public void Stack_FullPickupAccumulates()
        {
            var a = _inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Bandage), _dropped);
            var b = _inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Bandage), _dropped);

            Assert.IsTrue(a.FullyTaken);
            Assert.IsTrue(b.FullyTaken);
            Assert.AreEqual(10, _inventory.GetCount(ItemIds.Bandage));
            Assert.AreEqual(20f, _inventory.CurrentWeight, 1e-4f);
            Assert.IsTrue(_inventory.HasItem(ItemIds.Bandage));
        }

        [Test]
        public void Pickup_InvalidOrUnknownIsRejected()
        {
            Assert.IsFalse(_inventory.TryPickup(default, _dropped).Accepted);
            Assert.IsFalse(_inventory.TryPickup(new LootItemData("nope", ItemCategory.Medical, "x", 1), _dropped).Accepted);
            Assert.IsFalse(_inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Bandage, 0), _dropped).Accepted);
        }

        [Test]
        public void ConsumeAndDropStack()
        {
            _inventory.GiveItem(ItemIds.Bandage, 5);

            Assert.IsTrue(_inventory.Consume(ItemIds.Bandage, 2));
            Assert.AreEqual(3, _inventory.GetCount(ItemIds.Bandage));
            Assert.IsFalse(_inventory.Consume(ItemIds.Bandage, 4));
            Assert.AreEqual(3, _inventory.GetCount(ItemIds.Bandage));
            Assert.IsFalse(_inventory.Consume(ItemIds.MedKit));

            Assert.IsTrue(_inventory.TryDropStack(ItemIds.Bandage, 10, out var dropped));
            Assert.AreEqual(3, dropped.Quantity);
            Assert.AreEqual(ItemIds.Bandage, dropped.ItemId);
            Assert.AreEqual(0, _inventory.GetCount(ItemIds.Bandage));
            Assert.AreEqual(0f, _inventory.CurrentWeight, 1e-5f);
            Assert.IsFalse(_inventory.TryDropStack(ItemIds.Bandage, 1, out _));
        }

        [Test]
        public void GetStacks_ClearsAndListsInCatalogOrder()
        {
            _inventory.GiveItem(ItemIds.MedKit, 1);
            _inventory.GiveItem(ItemIds.Ammo556, 40);
            var stacks = new List<KeyValuePair<string, int>> { new("junk", 1) };

            _inventory.GetStacks(stacks);

            Assert.AreEqual(2, stacks.Count);
            Assert.AreEqual(ItemIds.Ammo556, stacks[0].Key);
            Assert.AreEqual(40, stacks[0].Value);
            Assert.AreEqual(ItemIds.MedKit, stacks[1].Key);
        }

        [Test]
        public void GiveItem_IgnoresCapacity()
        {
            _inventory.GiveItem(ItemIds.Ammo762, 500);
            Assert.AreEqual(500, _inventory.GetCount(ItemIds.Ammo762));
            Assert.Greater(_inventory.CurrentWeight, _inventory.Capacity);
            Assert.AreEqual(0, _inventory.MaxAddable(ItemIds.Bandage));
        }

        [Test]
        public void Overweight_AfterDroppingBackpack_BlocksStacks()
        {
            _inventory.EquipBackpack(2);
            Assert.AreEqual(0f, _inventory.LoadFraction, 1e-5f);

            // 260 kapasite (60 + 200); 200 adet 7.62 = 140.
            Assert.IsTrue(_inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Ammo762, 200), _dropped).FullyTaken);
            Assert.IsFalse(_inventory.IsOverweight);
            Assert.AreEqual(140f / 260f, _inventory.LoadFraction, 1e-4f);

            Assert.IsTrue(_inventory.TryDropEquipment(ItemCategory.Backpack, out var bag));
            Assert.AreEqual(ItemIds.Backpack2, bag.ItemId);
            Assert.IsTrue(_inventory.IsOverweight, "çanta bırakılınca yük kapasiteyi aşar");
            Assert.Greater(_inventory.LoadFraction, 1f);
            Assert.AreEqual(0f, _inventory.FreeCapacity, 1e-5f);
            Assert.IsFalse(_inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Bandage), _dropped).Accepted);

            // Çantayı geri almak sorunu çözer.
            Assert.IsTrue(_inventory.TryPickup(bag, _dropped).Accepted);
            Assert.IsFalse(_inventory.IsOverweight);
        }

        [Test]
        public void Catalog_CategoryNamesAreTurkish()
        {
            Assert.AreEqual("Mühimmat", ItemCatalog.GetCategoryName(ItemCategory.Ammunition));
            Assert.AreEqual("Tıbbi Malzeme", ItemCatalog.GetCategoryName(ItemCategory.Medical));
            Assert.AreEqual("Silah", ItemCatalog.GetCategoryName(ItemCategory.Weapon));
            Assert.AreEqual("Kask", ItemCatalog.GetCategoryName(ItemCategory.Helmet));
            Assert.AreEqual("Eşya", ItemCatalog.GetCategoryName(ItemCategory.None));
            Assert.AreEqual("Sis Bombası", ItemCatalog.GetDisplayName(ItemIds.SmokeGrenade));
            Assert.AreEqual("JNG-90", ItemCatalog.GetDisplayName(WeaponIds.Jng90));
            Assert.AreEqual("bilinmeyen", ItemCatalog.GetDisplayName("bilinmeyen"));
        }

        // ------------------------------------------------------------------ Silahlar

        [Test]
        public void Weapon_FirstPickupGoesToSlotZeroAndAutoEquips()
        {
            var result = _inventory.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.Mpt76), _dropped);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(0, _inventory.ActiveSlot);
            Assert.IsNotNull(_inventory.ActiveWeapon);
            Assert.AreEqual(WeaponIds.Mpt76, _inventory.ActiveWeapon.WeaponId);
            Assert.AreEqual(new PlayerId(7), _inventory.ActiveWeapon.OwnerId);
            Assert.AreSame(_inventory, _inventory.ActiveWeapon.AmmoSource);
            Assert.AreEqual(_inventory.ActiveWeapon.MagazineSize, _inventory.ActiveWeapon.CurrentAmmo);
            Assert.IsTrue(_inventory.HasAnyWeapon);
            Assert.AreEqual(0, _dropped.Count);
        }

        [Test]
        public void Weapon_SecondPrimaryGoesToSlotOne_ActiveUnchanged()
        {
            _inventory.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.Mpt76), _dropped);
            _inventory.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.Mpt55), _dropped);

            Assert.AreEqual(0, _inventory.ActiveSlot);
            Assert.AreEqual(WeaponIds.Mpt55, _inventory.GetWeapon(InventoryService.PrimarySlotB).WeaponId);
            Assert.AreEqual(0, _dropped.Count);
        }

        [Test]
        public void Weapon_PistolGoesToSidearmSlot()
        {
            _inventory.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.Mpt76), _dropped);
            _inventory.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.Sar9), _dropped);

            Assert.AreEqual(WeaponIds.Sar9, _inventory.GetWeapon(InventoryService.SidearmSlot).WeaponId);
            Assert.IsNull(_inventory.GetWeapon(InventoryService.PrimarySlotB));
            Assert.AreEqual(0, _inventory.ActiveSlot);
        }

        [Test]
        public void Weapon_ThirdPrimaryReplacesActiveSlot_DropsOldWithLoadedAmmo()
        {
            _inventory.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.Mpt76), _dropped);
            _inventory.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.Mpt55), _dropped);
            Assert.IsTrue(_inventory.SetActiveSlot(1));
            _inventory.GetWeapon(1).SetLoadedAmmo(7);

            var result = _inventory.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.G3, 12), _dropped);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, _dropped.Count);
            Assert.AreEqual(WeaponIds.Mpt55, _dropped[0].ItemId);
            Assert.AreEqual(ItemCategory.Weapon, _dropped[0].Category);
            Assert.AreEqual(7, _dropped[0].LoadedAmmo);
            Assert.AreEqual(WeaponIds.G3, _inventory.GetWeapon(1).WeaponId);
            Assert.AreEqual(12, _inventory.GetWeapon(1).CurrentAmmo);
            Assert.AreEqual(1, _inventory.ActiveSlot);
            Assert.AreEqual(WeaponIds.Mpt76, _inventory.GetWeapon(0).WeaponId);
        }

        [Test]
        public void Weapon_WithSidearmActive_PrimaryReplacesSlotZero()
        {
            _inventory.GiveWeapon(WeaponIds.Mpt76, true);
            _inventory.GiveWeapon(WeaponIds.Mpt55, true);
            _inventory.GiveWeapon(WeaponIds.Sar9, true);
            Assert.IsTrue(_inventory.SetActiveSlot(InventoryService.SidearmSlot));

            _inventory.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.Knt76), _dropped);

            Assert.AreEqual(WeaponIds.Knt76, _inventory.GetWeapon(0).WeaponId);
            Assert.AreEqual(1, _dropped.Count);
            Assert.AreEqual(WeaponIds.Mpt76, _dropped[0].ItemId);
            Assert.AreEqual(InventoryService.SidearmSlot, _inventory.ActiveSlot);
        }

        [Test]
        public void Weapon_PickupWhileUnarmedAutoEquips()
        {
            _inventory.GiveWeapon(WeaponIds.Mpt76, true);
            Assert.AreEqual(-1, _inventory.ActiveSlot, "GiveWeapon kuşanmaz");

            _inventory.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.Sar9), _dropped);

            Assert.AreEqual(InventoryService.SidearmSlot, _inventory.ActiveSlot);
        }

        [Test]
        public void Weapon_SetActiveSlotRules()
        {
            Assert.IsFalse(_inventory.SetActiveSlot(0), "boş yuva seçilemez");
            Assert.IsTrue(_inventory.SetActiveSlot(-1));
            Assert.IsFalse(_inventory.SetActiveSlot(3));
            Assert.IsFalse(_inventory.SetActiveSlot(-2));

            _inventory.GiveWeapon(WeaponIds.Mpt55, true);
            Assert.IsTrue(_inventory.SetActiveSlot(0));
            Assert.IsTrue(_inventory.SetActiveSlot(-1));
            Assert.IsNull(_inventory.ActiveWeapon);
        }

        [Test]
        public void Weapon_CycleWeaponSkipsEmptySlots()
        {
            _inventory.GiveWeapon(WeaponIds.Mpt55, true);
            _inventory.GiveWeapon(WeaponIds.Sar9, true);

            Assert.IsTrue(_inventory.CycleWeapon(1));
            Assert.AreEqual(0, _inventory.ActiveSlot);
            Assert.IsTrue(_inventory.CycleWeapon(1));
            Assert.AreEqual(2, _inventory.ActiveSlot);
            Assert.IsTrue(_inventory.CycleWeapon(1));
            Assert.AreEqual(0, _inventory.ActiveSlot);
            Assert.IsTrue(_inventory.CycleWeapon(-1));
            Assert.AreEqual(2, _inventory.ActiveSlot);
        }

        [Test]
        public void Weapon_DropActiveWeaponLeavesUnarmed()
        {
            _inventory.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.Mpt76), _dropped);
            _inventory.ActiveWeapon.SetLoadedAmmo(3);

            var loot = _inventory.DropWeapon(0);

            Assert.IsTrue(loot.IsValid);
            Assert.AreEqual(WeaponIds.Mpt76, loot.ItemId);
            Assert.AreEqual(3, loot.LoadedAmmo);
            Assert.AreEqual(-1, _inventory.ActiveSlot);
            Assert.IsFalse(_inventory.HasAnyWeapon);
            Assert.IsFalse(_inventory.DropWeapon(0).IsValid);
            Assert.IsFalse(_inventory.DropWeapon(5).IsValid);
        }

        [Test]
        public void Weapon_ReloadDrawsAmmoFromInventory()
        {
            var weapon = _inventory.GiveWeapon(WeaponIds.Mpt76, false);
            Assert.AreEqual(0, weapon.CurrentAmmo);
            _inventory.GiveItem(ItemIds.Ammo762, 8);
            Assert.AreEqual(8, weapon.ReserveAmmo);

            Assert.IsTrue(weapon.TryBeginReload());
            for (var i = 0; i < 100 && weapon.IsReloading; i++)
                weapon.Tick(0.1f);

            Assert.AreEqual(8, weapon.CurrentAmmo);
            Assert.AreEqual(0, _inventory.GetCount(ItemIds.Ammo762));
        }

        [Test]
        public void Weapon_HasUsableWeaponDependsOnAmmo()
        {
            Assert.IsFalse(_inventory.HasUsableWeapon);
            var weapon = _inventory.GiveWeapon(WeaponIds.Mpt55, false);
            Assert.IsFalse(_inventory.HasUsableWeapon);

            _inventory.GiveItem(ItemIds.Ammo556, 1);
            Assert.IsTrue(_inventory.HasUsableWeapon);

            _inventory.Consume(ItemIds.Ammo556);
            weapon.SetLoadedAmmo(1);
            Assert.IsTrue(_inventory.HasUsableWeapon);
        }

        [Test]
        public void SelectBestWeapon_PrefersUsablePrimary()
        {
            _inventory.GiveWeapon(WeaponIds.Sar9, true);
            _inventory.GiveWeapon(WeaponIds.Mpt76, false);

            Assert.IsTrue(_inventory.SelectBestWeapon());
            Assert.AreEqual(InventoryService.SidearmSlot, _inventory.ActiveSlot, "mermisiz tüfek yerine dolu tabanca");

            _inventory.GiveItem(ItemIds.Ammo762, 30);
            Assert.IsTrue(_inventory.SelectBestWeapon());
            Assert.AreEqual(0, _inventory.ActiveSlot);
        }

        // ------------------------------------------------------------------ Mermi kaynağı

        [Test]
        public void AmmoSource_TakeAmmoIsBounded()
        {
            _inventory.GiveItem(ItemIds.Ammo556, 25);

            Assert.AreEqual(25, _inventory.GetAmmo(AmmoType.Mm556));
            Assert.AreEqual(0, _inventory.GetAmmo(AmmoType.Mm762));
            Assert.AreEqual(20, _inventory.TakeAmmo(AmmoType.Mm556, 20));
            Assert.AreEqual(5, _inventory.TakeAmmo(AmmoType.Mm556, 20));
            Assert.AreEqual(0, _inventory.TakeAmmo(AmmoType.Mm556, 20));
            Assert.AreEqual(0, _inventory.TakeAmmo(AmmoType.None, 20));
        }

        [Test]
        public void InfiniteAmmo_DetachesAmmoSource()
        {
            var weapon = _inventory.GiveWeapon(WeaponIds.Mpt55, true);
            Assert.AreSame(_inventory, weapon.AmmoSource);

            _inventory.InfiniteAmmo = true;
            Assert.IsNull(weapon.AmmoSource);
            Assert.AreEqual(30, _inventory.TakeAmmo(AmmoType.Mm556, 30));
            Assert.Greater(_inventory.GetAmmo(AmmoType.Mm556), 0);
            Assert.IsNull(_inventory.GiveWeapon(WeaponIds.Sar9, true).AmmoSource);

            _inventory.InfiniteAmmo = false;
            Assert.AreSame(_inventory, weapon.AmmoSource);
        }

        // ------------------------------------------------------------------ Zırh, kask, çanta

        [Test]
        public void Armor_EquipRules()
        {
            Assert.IsTrue(_inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Vest2), _dropped).Accepted);
            Assert.AreEqual(2, _inventory.Vest.Level);
            Assert.AreEqual(220f, _inventory.Vest.Durability, 1e-4f);

            Assert.IsFalse(_inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Vest1), _dropped).Accepted, "düşük seviye");
            Assert.IsFalse(_inventory.TryPickup(ItemCatalog.CreateArmorLoot(ItemIds.Vest2, 100f), _dropped).Accepted,
                "aynı seviye düşük dayanıklılık");

            _inventory.Vest.Wear(120f);
            Assert.IsTrue(_inventory.TryPickup(ItemCatalog.CreateArmorLoot(ItemIds.Vest2, 150f), _dropped).Accepted,
                "aynı seviye yüksek dayanıklılık");
            Assert.AreEqual(1, _dropped.Count);
            Assert.AreEqual(ItemIds.Vest2, _dropped[0].ItemId);
            Assert.AreEqual(100f, _dropped[0].Durability, 1e-3f);
            Assert.AreEqual(150f, _inventory.Vest.Durability, 1e-3f);

            Assert.IsTrue(_inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Vest3), _dropped).Accepted, "yüksek seviye");
            Assert.AreEqual(3, _inventory.Vest.Level);
            Assert.AreEqual(2, _dropped.Count);
        }

        [Test]
        public void Helmet_ProtectsHead_VestProtectsTorso()
        {
            _inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Helmet1), _dropped);
            _inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Vest1), _dropped);

            Assert.AreSame(_inventory.Helmet, _inventory.GetArmorFor(BodyPart.Head));
            Assert.AreSame(_inventory.Vest, _inventory.GetArmorFor(BodyPart.Torso));
            Assert.IsNull(_inventory.GetArmorFor(BodyPart.Leg));
            Assert.IsNull(_inventory.GetArmorFor(BodyPart.Arm));
            Assert.AreEqual(80f, _inventory.Helmet.MaxDurability, 1e-4f);
            Assert.AreEqual(0.30f, _inventory.Helmet.DamageReduction, 1e-4f);

            _inventory.Helmet.Wear(1000f);
            Assert.IsNull(_inventory.GetArmorFor(BodyPart.Head), "kırık kask korumaz");
        }

        [Test]
        public void Backpack_OnlyHigherLevelAccepted()
        {
            Assert.IsTrue(_inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Backpack2), _dropped).Accepted);
            Assert.IsFalse(_inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Backpack1), _dropped).Accepted);
            Assert.IsFalse(_inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Backpack2), _dropped).Accepted);
            Assert.AreEqual(0, _dropped.Count);

            Assert.IsTrue(_inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Backpack3), _dropped).Accepted);
            Assert.AreEqual(3, _inventory.BackpackLevel);
            Assert.AreEqual(1, _dropped.Count);
            Assert.AreEqual(ItemIds.Backpack2, _dropped[0].ItemId);
        }

        // ------------------------------------------------------------------ Ölüm, olaylar

        [Test]
        public void DropAll_OutputsEverythingAndEmpties()
        {
            _inventory.GiveWeapon(WeaponIds.Mpt76, true);
            _inventory.GiveWeapon(WeaponIds.Sar9, true);
            _inventory.EquipArmor(ItemIds.Vest2);
            _inventory.EquipArmor(ItemIds.Helmet1);
            _inventory.EquipBackpack(1);
            _inventory.GiveItem(ItemIds.Ammo762, 60);
            _inventory.GiveItem(ItemIds.Bandage, 3);
            _inventory.SetActiveSlot(0);

            var output = new List<LootItemData>();
            _inventory.DropAll(output);

            Assert.AreEqual(7, output.Count);
            Assert.IsFalse(_inventory.HasAnyWeapon);
            Assert.IsNull(_inventory.Vest);
            Assert.IsNull(_inventory.Helmet);
            Assert.AreEqual(0, _inventory.BackpackLevel);
            Assert.AreEqual(-1, _inventory.ActiveSlot);
            Assert.AreEqual(0f, _inventory.CurrentWeight, 1e-5f);
            Assert.AreEqual(0, _inventory.GetCount(ItemIds.Ammo762));

            var ammo = output.Find(i => i.ItemId == ItemIds.Ammo762);
            Assert.AreEqual(60, ammo.Quantity);
            foreach (var item in output)
                Assert.IsTrue(item.IsValid, item.ItemId);
        }

        [Test]
        public void Changed_FiresOnEveryMutation()
        {
            var count = 0;
            _inventory.Changed += () => count++;

            _inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Bandage), _dropped);
            Assert.AreEqual(1, count);
            _inventory.Consume(ItemIds.Bandage);
            Assert.AreEqual(2, count);
            _inventory.GiveWeapon(WeaponIds.Mpt55, true);
            Assert.AreEqual(3, count);
            _inventory.SetActiveSlot(0);
            Assert.AreEqual(4, count);
            _inventory.SetActiveSlot(0);
            Assert.AreEqual(4, count, "aynı yuva değişiklik değil");
            _inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Helmet2), _dropped);
            Assert.AreEqual(5, count);
            _inventory.TakeAmmo(AmmoType.Mm556, 5);
            Assert.AreEqual(5, count, "mermi yokken değişiklik yok");
            _inventory.DropAll(new List<LootItemData>());
            Assert.AreEqual(6, count);

            _inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Backpack1, 1), _dropped);
            Assert.AreEqual(7, count);
            Assert.IsFalse(_inventory.TryPickup(ItemCatalog.CreateLoot(ItemIds.Backpack1, 1), _dropped).Accepted);
            Assert.AreEqual(7, count, "reddedilen alım olay üretmez");
        }

        [Test]
        public void LegacyApi_AddRemoveHas()
        {
            Assert.IsTrue(_inventory.TryAddItem(ItemIds.Bandage, ItemCategory.Medical));
            Assert.IsTrue(_inventory.HasItem(ItemIds.Bandage));
            Assert.IsTrue(_inventory.TryRemoveItem(ItemIds.Bandage));
            Assert.IsFalse(_inventory.HasItem(ItemIds.Bandage));
            Assert.IsFalse(_inventory.TryRemoveItem(ItemIds.Bandage));

            Assert.IsTrue(_inventory.TryAddItem(WeaponIds.Mpt55, ItemCategory.Weapon));
            Assert.IsTrue(_inventory.HasItem(WeaponIds.Mpt55));
            Assert.IsTrue(_inventory.TryAddItem(ItemIds.Helmet1, ItemCategory.Helmet));
            Assert.IsFalse(_inventory.TryAddItem(ItemIds.Helmet2, ItemCategory.Helmet), "eski API değiştirmez");
            Assert.IsTrue(_inventory.TryRemoveItem(ItemIds.Helmet1));
            Assert.IsNull(_inventory.Helmet);
            Assert.IsFalse(_inventory.TryAddItem("unknown", ItemCategory.Medical));
            Assert.AreEqual(3, _inventory.SlotCount);
        }

        // ------------------------------------------------------------------ Bot kararları

        [Test]
        public void BestHealItem_PicksSensibly()
        {
            Assert.IsNull(_inventory.BestHealItem(40f));

            _inventory.GiveItem(ItemIds.Bandage, 5);
            _inventory.GiveItem(ItemIds.FirstAid, 1);
            _inventory.GiveItem(ItemIds.MedKit, 1);

            Assert.AreEqual(ItemIds.Bandage, _inventory.BestHealItem(65f));
            Assert.AreEqual(ItemIds.FirstAid, _inventory.BestHealItem(30f));
            Assert.AreEqual(ItemIds.MedKit, _inventory.BestHealItem(85f));
            Assert.IsNull(_inventory.BestHealItem(100f));

            _inventory.Consume(ItemIds.MedKit);
            Assert.IsNull(_inventory.BestHealItem(85f), "75 üstünde yalnızca sıhhiye çantası işe yarar");

            _inventory.Consume(ItemIds.FirstAid);
            Assert.AreEqual(ItemIds.Bandage, _inventory.BestHealItem(10f));
        }

        [Test]
        public void BestBoostItem_PicksAvailable()
        {
            Assert.IsNull(_inventory.BestBoostItem());
            _inventory.GiveItem(ItemIds.EnergyDrink, 1);
            Assert.AreEqual(ItemIds.EnergyDrink, _inventory.BestBoostItem());
            _inventory.GiveItem(ItemIds.Painkiller, 1);
            Assert.AreEqual(ItemIds.Painkiller, _inventory.BestBoostItem());
            Assert.AreEqual(ItemIds.EnergyDrink, _inventory.BestBoostItem(70f), "az eksikte enerji içeceği");
            Assert.IsNull(_inventory.BestBoostItem(100f));
        }

        [Test]
        public void WantsItem_Heuristics()
        {
            // Silahsız: her silahı ister; silahı olmayan mermiyi istemez.
            Assert.IsTrue(_inventory.WantsItem(ItemCatalog.CreateWeaponLoot(WeaponIds.Sar9)));
            Assert.IsFalse(_inventory.WantsItem(ItemCatalog.CreateLoot(ItemIds.Ammo762)));
            Assert.IsTrue(_inventory.WantsItem(ItemCatalog.CreateLoot(ItemIds.Bandage)));
            Assert.IsTrue(_inventory.WantsItem(ItemCatalog.CreateLoot(ItemIds.Vest1)));
            Assert.IsFalse(_inventory.WantsItem(default));

            _inventory.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.Mpt76), _dropped);
            Assert.IsTrue(_inventory.WantsItem(ItemCatalog.CreateLoot(ItemIds.Ammo762)));
            Assert.IsFalse(_inventory.WantsItem(ItemCatalog.CreateLoot(ItemIds.Ammo9)));
            Assert.IsFalse(_inventory.WantsItem(ItemCatalog.CreateWeaponLoot(WeaponIds.Mpt76)), "aynı silah");
            Assert.IsTrue(_inventory.WantsItem(ItemCatalog.CreateWeaponLoot(WeaponIds.Mpt55)), "boş ikinci yuva");

            _inventory.TryPickup(ItemCatalog.CreateWeaponLoot(WeaponIds.G3), _dropped);
            _inventory.GiveItem(ItemIds.Ammo9, 60);
            Assert.IsFalse(_inventory.WantsItem(ItemCatalog.CreateWeaponLoot(WeaponIds.Sar109)), "daha zayıf silah");

            _inventory.EquipArmor(ItemIds.Vest2);
            Assert.IsFalse(_inventory.WantsItem(ItemCatalog.CreateLoot(ItemIds.Vest1)));
            Assert.IsTrue(_inventory.WantsItem(ItemCatalog.CreateLoot(ItemIds.Vest3)));
            _inventory.EquipBackpack(2);
            Assert.IsFalse(_inventory.WantsItem(ItemCatalog.CreateLoot(ItemIds.Backpack1)));
            Assert.IsTrue(_inventory.WantsItem(ItemCatalog.CreateLoot(ItemIds.Backpack3)));

            _inventory.GiveItem(ItemIds.MedKit, 2);
            Assert.IsFalse(_inventory.WantsItem(ItemCatalog.CreateLoot(ItemIds.MedKit)), "yeterince var");
        }

        // ------------------------------------------------------------------ Görev teçhizatı

        [Test]
        public void Loadout_RoleForSlot()
        {
            Assert.AreEqual(TeamRole.Leader, LoadoutCatalog.RoleForSlot(0));
            Assert.AreEqual(TeamRole.Marksman, LoadoutCatalog.RoleForSlot(1));
            Assert.AreEqual(TeamRole.MachineGunner, LoadoutCatalog.RoleForSlot(2));
            Assert.AreEqual(TeamRole.Medic, LoadoutCatalog.RoleForSlot(3));
            Assert.AreEqual(TeamRole.Radioman, LoadoutCatalog.RoleForSlot(4));
            Assert.AreEqual(TeamRole.Grenadier, LoadoutCatalog.RoleForSlot(5));
            for (var i = 6; i < 12; i++)
                Assert.AreEqual(TeamRole.Rifleman, LoadoutCatalog.RoleForSlot(i));
            Assert.AreEqual(TeamRole.Rifleman, LoadoutCatalog.RoleForSlot(-3));
        }

        [Test]
        public void Loadout_RoleNamesAreTurkish()
        {
            Assert.AreEqual("Tim Komutanı", LoadoutCatalog.GetRoleName(TeamRole.Leader));
            Assert.AreEqual("Piyade", LoadoutCatalog.GetRoleName(TeamRole.Rifleman));
            Assert.AreEqual("Keskin Nişancı", LoadoutCatalog.GetRoleName(TeamRole.Marksman));
            Assert.AreEqual("Makineli Tüfekçi", LoadoutCatalog.GetRoleName(TeamRole.MachineGunner));
            Assert.AreEqual("Sıhhiyeci", LoadoutCatalog.GetRoleName(TeamRole.Medic));
            Assert.AreEqual("Telsizci", LoadoutCatalog.GetRoleName(TeamRole.Radioman));
            Assert.AreEqual("Bombacı", LoadoutCatalog.GetRoleName(TeamRole.Grenadier));
            Assert.AreEqual("Tim Komutanı", LoadoutCatalog.For(TeamRole.Leader).RoleName);
        }

        [Test]
        public void Loadout_RoleWeapons()
        {
            var leader = LoadoutCatalog.For(TeamRole.Leader);
            Assert.AreEqual(WeaponIds.Mpt76, leader.PrimaryWeaponId);
            Assert.AreEqual(WeaponIds.Sar9, leader.SidearmId);
            Assert.AreEqual(2, leader.VestLevel);
            Assert.AreEqual(2, leader.HelmetLevel);
            Assert.AreEqual(2, leader.BackpackLevel);

            Assert.AreEqual(WeaponIds.Jng90, LoadoutCatalog.For(TeamRole.Marksman).PrimaryWeaponId);
            Assert.AreEqual(WeaponIds.Pmt76, LoadoutCatalog.For(TeamRole.MachineGunner).PrimaryWeaponId);
            Assert.AreEqual(WeaponIds.Mpt55, LoadoutCatalog.For(TeamRole.Radioman).PrimaryWeaponId);
            Assert.AreEqual(WeaponIds.G3, LoadoutCatalog.For(TeamRole.Grenadier).PrimaryWeaponId);

            var medic = LoadoutCatalog.For(TeamRole.Medic);
            Assert.AreEqual(WeaponIds.Mpt55, medic.PrimaryWeaponId);
            Assert.AreEqual(4, medic.CountOf(ItemIds.FirstAid));
            Assert.AreEqual(2, medic.CountOf(ItemIds.MedKit));
            Assert.AreEqual(4, LoadoutCatalog.For(TeamRole.Grenadier).CountOf(ItemIds.FragGrenade));

            Assert.AreEqual(WeaponIds.Mpt76, LoadoutCatalog.ForSlot(6).PrimaryWeaponId);
            Assert.AreEqual(WeaponIds.Mpt55, LoadoutCatalog.ForSlot(7).PrimaryWeaponId);
        }

        [Test]
        public void Loadout_AllRolesValidAndFitCapacity()
        {
            for (var slot = 0; slot < LoadoutCatalog.SquadSize; slot++)
            {
                var loadout = LoadoutCatalog.ForSlot(slot);
                var inventory = new InventoryService(_bus, new PlayerId(100 + slot));
                inventory.ApplyLoadout(loadout);

                Assert.IsTrue(inventory.HasUsableWeapon, loadout.RoleName);
                Assert.AreEqual(0, inventory.ActiveSlot, loadout.RoleName);
                Assert.IsNotNull(inventory.Vest, loadout.RoleName);
                Assert.IsNotNull(inventory.Helmet, loadout.RoleName);
                Assert.GreaterOrEqual(inventory.Vest.Level, 1);
                Assert.LessOrEqual(inventory.Vest.Level, 2);
                Assert.LessOrEqual(inventory.CurrentWeight, inventory.Capacity, loadout.RoleName + " ağırlık");
                Assert.GreaterOrEqual(inventory.GetCount(ItemIds.Bandage), 3, loadout.RoleName);
                Assert.GreaterOrEqual(inventory.GetCount(ItemIds.FragGrenade), 1, loadout.RoleName);
                Assert.GreaterOrEqual(inventory.GetCount(ItemIds.SmokeGrenade), 1, loadout.RoleName);

                var primary = inventory.GetWeapon(0);
                var ammoId = ItemCatalog.AmmoItemId(primary.Definition.AmmoType);
                Assert.GreaterOrEqual(inventory.GetCount(ammoId), 100, loadout.RoleName + " mermi");

                foreach (var pair in loadout.Items)
                    Assert.IsTrue(ItemCatalog.Contains(pair.Key), pair.Key);
            }
        }

        [Test]
        public void Loadout_ApplyRaisesSingleChanged()
        {
            var count = 0;
            _inventory.Changed += () => count++;
            _inventory.ApplyLoadout(LoadoutCatalog.For(TeamRole.Medic));

            Assert.AreEqual(1, count);
            Assert.AreEqual(4, _inventory.GetCount(ItemIds.FirstAid));
            Assert.AreEqual(2, _inventory.GetCount(ItemIds.MedKit));
            Assert.AreEqual(WeaponIds.Sar9, _inventory.GetWeapon(InventoryService.SidearmSlot).WeaponId);
        }

        // ------------------------------------------------------------------ Yağma üretimi

        [Test]
        public void LootSpawn_SpawnChances()
        {
            var service = new LootSpawnService();
            Assert.AreEqual(0.38f, service.SpawnChance(LootTier.Low), 1e-5f);
            Assert.AreEqual(0.58f, service.SpawnChance(LootTier.Medium), 1e-5f);
            Assert.AreEqual(0.74f, service.SpawnChance(LootTier.High), 1e-5f);
            Assert.AreEqual(0.9f, service.SpawnChance(LootTier.Military), 1e-5f);
        }

        [TestCase(LootTier.Low)]
        [TestCase(LootTier.Medium)]
        [TestCase(LootTier.High)]
        [TestCase(LootTier.Military)]
        public void LootSpawn_RollsAreValidAndGroupsHaveAmmo(LootTier tier)
        {
            var service = new LootSpawnService();
            var random = new SeededRandom(1234 + (int)tier);
            var group = new List<LootItemData>();
            var weapons = 0;

            for (var i = 0; i < 400; i++)
            {
                var single = service.Roll(tier, random);
                Assert.IsTrue(single.IsValid);
                Assert.IsTrue(ItemCatalog.Contains(single.ItemId));

                group.Clear();
                service.RollSpawnGroup(tier, random, group);
                Assert.Greater(group.Count, 0);
                foreach (var item in group)
                    Assert.IsTrue(item.IsValid);

                if (group[0].Category != ItemCategory.Weapon)
                    continue;

                weapons++;
                Assert.AreEqual(1 + LootSpawnService.AmmoStacksPerWeapon, group.Count);
                var ammoId = ItemCatalog.AmmoItemId(ItemCatalog.WeaponAmmoType(group[0].ItemId));
                Assert.AreEqual(ammoId, group[1].ItemId);
                Assert.AreEqual(ammoId, group[2].ItemId);
                Assert.AreEqual(-1, group[0].LoadedAmmo);
            }

            Assert.Greater(weapons, 0, "silah hiç çıkmadı");
        }

        [Test]
        public void LootSpawn_MilitaryFavoursHeavyGear()
        {
            var service = new LootSpawnService();
            Assert.AreEqual(0f, service.GetWeight(LootTier.Low, WeaponIds.Jng90), 1e-5f);
            Assert.AreEqual(0f, service.GetWeight(LootTier.Low, WeaponIds.Pmt76), 1e-5f);
            Assert.AreEqual(0f, service.GetWeight(LootTier.Low, ItemIds.Vest3), 1e-5f);

            Assert.Greater(Share(service, LootTier.Military, WeaponIds.Knt76), Share(service, LootTier.High, WeaponIds.Knt76));
            Assert.Greater(Share(service, LootTier.Military, WeaponIds.Jng90), Share(service, LootTier.High, WeaponIds.Jng90));
            Assert.Greater(Share(service, LootTier.Military, ItemIds.Vest3), Share(service, LootTier.Medium, ItemIds.Vest3));
            Assert.Greater(Share(service, LootTier.Military, ItemIds.Ammo762), Share(service, LootTier.Low, ItemIds.Ammo762));
        }

        [Test]
        public void LootSpawn_IsDeterministicForSeed()
        {
            var service = new LootSpawnService();
            var a = new List<LootItemData>();
            var b = new List<LootItemData>();
            var ra = new SeededRandom(99);
            var rb = new SeededRandom(99);
            for (var i = 0; i < 50; i++)
            {
                service.RollSpawnGroup(LootTier.High, ra, a);
                service.RollSpawnGroup(LootTier.High, rb, b);
            }

            Assert.AreEqual(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++)
                Assert.AreEqual(a[i].ItemId, b[i].ItemId);

            Assert.DoesNotThrow(() => service.RollSpawnGroup(LootTier.Low, null, a));
            Assert.DoesNotThrow(() => service.RollSpawnGroup(LootTier.Low, ra, null));
        }

        [Test]
        public void LootSpawn_SpawnedLootCanBePickedUp()
        {
            var service = new LootSpawnService();
            var random = new SeededRandom(5);
            var group = new List<LootItemData>();
            for (var i = 0; i < 200; i++)
                service.RollSpawnGroup(LootTier.Military, random, group);

            var inventory = new InventoryService(_bus, new PlayerId(1));
            inventory.EquipBackpack(3);
            inventory.EquipArmor(ItemIds.Vest1);
            var accepted = 0;
            foreach (var item in group)
            {
                if (inventory.TryPickup(item, _dropped).Accepted)
                    accepted++;
            }

            Assert.Greater(accepted, 0);
            Assert.IsTrue(inventory.HasAnyWeapon);
            Assert.LessOrEqual(inventory.CurrentWeight, inventory.Capacity + 1e-3f);
        }

        [Test]
        public void LootSpawn_CustomTableOverridesAllTiers()
        {
            var table = new[]
            {
                new LootItemData(ItemIds.Bandage, ItemCategory.Medical, "Sargı Bezi", 2),
                new LootItemData(ItemIds.Ammo556, ItemCategory.Ammunition, "5.56 Mermi", 45),
                new LootItemData(ItemIds.Vest2, ItemCategory.Armor, "Çelik Yelek (Sv.2)", 3),
                new LootItemData("armor_l1", ItemCategory.Armor, "Eski Zırh"),
                default
            };
            var service = new LootSpawnService(table);
            Assert.IsTrue(service.UsesCustomTable);

            var random = new SeededRandom(17);
            var seen = new HashSet<string>();
            foreach (LootTier tier in new[] { LootTier.Low, LootTier.Medium, LootTier.High, LootTier.Military })
            {
                Assert.AreEqual(3f, service.GetTotalWeight(tier), 1e-5f);
                for (var i = 0; i < 60; i++)
                {
                    var item = service.Roll(tier, random);
                    Assert.IsTrue(item.IsValid);
                    seen.Add(item.ItemId);
                    if (item.ItemId == ItemIds.Bandage)
                        Assert.AreEqual(2, item.Quantity);
                    else if (item.ItemId == ItemIds.Ammo556)
                        Assert.AreEqual(45, item.Quantity);
                    else
                    {
                        Assert.AreEqual(ItemIds.Vest2, item.ItemId);
                        Assert.AreEqual(1, item.Quantity, "zırh tek adet");
                    }
                }
            }

            Assert.AreEqual(3, seen.Count);
            Assert.IsFalse(seen.Contains("armor_l1"));
        }

        [Test]
        public void LootSpawn_CustomTableWithoutValidItemsFallsBackToStandard()
        {
            var service = new LootSpawnService(new[] { new LootItemData("nope", ItemCategory.Medical, "x") });
            Assert.IsFalse(service.UsesCustomTable);
            Assert.Greater(service.GetWeight(LootTier.Military, WeaponIds.Pmt76), 0f);

            var empty = new LootSpawnService(null);
            Assert.IsFalse(empty.UsesCustomTable);
            Assert.IsTrue(empty.Roll(LootTier.Low, new SeededRandom(3)).IsValid);
        }

        private static float Share(LootSpawnService service, LootTier tier, string itemId) =>
            service.GetWeight(tier, itemId) / service.GetTotalWeight(tier);
    }
}
