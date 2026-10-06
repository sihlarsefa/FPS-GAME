#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Presentation.UI;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class InventoryUiLogicTests
    {
        [Test]
        public void ResolveDrop_WeaponZones()
        {
            Assert.AreEqual(InventoryDropIntent.DropWeapon, InventoryUiLogic.ResolveDrop(InventoryDragKind.Weapon, InventoryDropZone.Drop));
            Assert.AreEqual(InventoryDropIntent.EquipWeapon, InventoryUiLogic.ResolveDrop(InventoryDragKind.Weapon, InventoryDropZone.Hand));
            Assert.AreEqual(InventoryDropIntent.None, InventoryUiLogic.ResolveDrop(InventoryDragKind.Weapon, InventoryDropZone.Backpack));
        }

        [Test]
        public void ResolveDrop_StackUseOnlyWhenUsable()
        {
            Assert.AreEqual(InventoryDropIntent.UseStack, InventoryUiLogic.ResolveDrop(InventoryDragKind.Stack, InventoryDropZone.Hand, true));
            Assert.AreEqual(InventoryDropIntent.None, InventoryUiLogic.ResolveDrop(InventoryDragKind.Stack, InventoryDropZone.Hand, false));
            Assert.AreEqual(InventoryDropIntent.DropStack, InventoryUiLogic.ResolveDrop(InventoryDragKind.Stack, InventoryDropZone.Drop));
        }

        [Test]
        public void ResolveDrop_GroundOnlyPicksUpIntoBackpack()
        {
            Assert.AreEqual(InventoryDropIntent.PickupGround, InventoryUiLogic.ResolveDrop(InventoryDragKind.Ground, InventoryDropZone.Backpack));
            Assert.AreEqual(InventoryDropIntent.None, InventoryUiLogic.ResolveDrop(InventoryDragKind.Ground, InventoryDropZone.Drop));
            Assert.AreEqual(InventoryDropIntent.DropGear, InventoryUiLogic.ResolveDrop(InventoryDragKind.Gear, InventoryDropZone.Drop));
            Assert.AreEqual(InventoryDropIntent.None, InventoryUiLogic.ResolveDrop(InventoryDragKind.None, InventoryDropZone.Drop));
        }

        [Test]
        public void SplitQuantity_OneHalfAll()
        {
            Assert.AreEqual(0, InventoryUiLogic.SplitQuantity(0, InventoryQuickAction.DropAll));
            Assert.AreEqual(1, InventoryUiLogic.SplitQuantity(7, InventoryQuickAction.DropOne));
            Assert.AreEqual(30, InventoryUiLogic.SplitQuantity(90, InventoryQuickAction.DropOne, 30));
            Assert.AreEqual(12, InventoryUiLogic.SplitQuantity(12, InventoryQuickAction.DropOne, 30));
            Assert.AreEqual(4, InventoryUiLogic.SplitQuantity(7, InventoryQuickAction.DropHalf));
            Assert.AreEqual(1, InventoryUiLogic.SplitQuantity(1, InventoryQuickAction.DropHalf));
            Assert.AreEqual(7, InventoryUiLogic.SplitQuantity(7, InventoryQuickAction.DropAll));
        }

        [Test]
        public void NavigateGrid_StaysInBoundsAndSnapsToLastItem()
        {
            // 7 öğe, 5 sütun: satır0 = 0..4, satır1 = 5..6.
            Assert.AreEqual(1, InventoryUiLogic.NavigateGrid(0, 7, 5, 1, 0));
            Assert.AreEqual(4, InventoryUiLogic.NavigateGrid(4, 7, 5, 1, 0));   // sağ kenar
            Assert.AreEqual(0, InventoryUiLogic.NavigateGrid(0, 7, 5, -1, 0));  // sol kenar
            Assert.AreEqual(5, InventoryUiLogic.NavigateGrid(0, 7, 5, 0, 1));
            Assert.AreEqual(6, InventoryUiLogic.NavigateGrid(4, 7, 5, 0, 1));   // eksik satırda son öğe
            Assert.AreEqual(6, InventoryUiLogic.NavigateGrid(6, 7, 5, 1, 0));   // satır sonu
            Assert.AreEqual(6, InventoryUiLogic.NavigateGrid(6, 7, 5, 0, 1));   // alt kenar
            Assert.AreEqual(1, InventoryUiLogic.NavigateGrid(6, 7, 5, 0, -1));
            Assert.AreEqual(-1, InventoryUiLogic.NavigateGrid(0, 0, 5, 1, 0));
        }

        [Test]
        public void Wrap_IsCircular()
        {
            Assert.AreEqual(0, InventoryUiLogic.Wrap(2, 3, 1));
            Assert.AreEqual(2, InventoryUiLogic.Wrap(0, 3, -1));
            Assert.AreEqual(-1, InventoryUiLogic.Wrap(0, 0, 1));
        }

        [Test]
        public void SortGround_ByDistanceThenRarity()
        {
            var list = new List<InventoryGroundEntry>
            {
                new InventoryGroundEntry { SpawnId = 1, Distance = 5f, Rarity = InventoryRarity.Common },
                new InventoryGroundEntry { SpawnId = 2, Distance = 2f, Rarity = InventoryRarity.Common },
                new InventoryGroundEntry { SpawnId = 3, Distance = 5f, Rarity = InventoryRarity.Epic },
                new InventoryGroundEntry { SpawnId = 4, Distance = 0.5f, Rarity = InventoryRarity.Rare }
            };
            InventoryUiLogic.SortGround(list);
            Assert.AreEqual(4, list[0].SpawnId);
            Assert.AreEqual(2, list[1].SpawnId);
            Assert.AreEqual(3, list[2].SpawnId); // eşit mesafede yüksek nadirlik önce
            Assert.AreEqual(1, list[3].SpawnId);
        }

        [Test]
        public void FormatDistance_UsesCommaAndClampsInvalid()
        {
            Assert.AreEqual("3,4 m", InventoryUiLogic.FormatDistance(3.4f));
            Assert.AreEqual("0,0 m", InventoryUiLogic.FormatDistance(-1f));
            Assert.AreEqual("0,0 m", InventoryUiLogic.FormatDistance(float.NaN));
        }

        [Test]
        public void DurabilityState_Thresholds()
        {
            Assert.AreEqual(0, InventoryUiLogic.DurabilityState(0.8f, false));
            Assert.AreEqual(1, InventoryUiLogic.DurabilityState(0.2f, false));
            Assert.AreEqual(2, InventoryUiLogic.DurabilityState(0.5f, true));
            Assert.AreEqual(2, InventoryUiLogic.DurabilityState(0f, false));
        }

        [Test]
        public void Caliber_OrderCoversAllAmmoTypes()
        {
            Assert.AreEqual(4, InventoryUiLogic.AmmoOrder.Length);
            foreach (var type in InventoryUiLogic.AmmoOrder)
                Assert.AreNotEqual("-", InventoryUiLogic.CaliberLabel(type));
            Assert.AreEqual("-", InventoryUiLogic.CaliberLabel(AmmoType.None));
        }

        [Test]
        public void Rarity_ArmorLevelsAndWeaponCategories()
        {
            Assert.AreEqual(InventoryRarity.Uncommon, InventoryRarityRules.Of(ItemCategory.Armor, ItemIds.Vest1));
            Assert.AreEqual(InventoryRarity.Rare, InventoryRarityRules.Of(ItemCategory.Helmet, ItemIds.Helmet2));
            Assert.AreEqual(InventoryRarity.Epic, InventoryRarityRules.Of(ItemCategory.Backpack, ItemIds.Backpack3));
            Assert.AreEqual(InventoryRarity.Common, InventoryRarityRules.OfWeapon(WeaponCategory.Pistol));
            Assert.AreEqual(InventoryRarity.Legendary, InventoryRarityRules.OfWeapon(WeaponCategory.Sniper));
            Assert.AreEqual(InventoryRarity.Rare, InventoryRarityRules.Of(ItemCategory.Medical, ItemIds.MedKit));
            Assert.AreEqual(InventoryRarity.Epic, InventoryRarityRules.Of(ItemCategory.Attachment, ItemIds.Scope4x));
        }

        [Test]
        public void Rarity_ColorsAreDistinct()
        {
            var seen = new HashSet<UnityEngine.Color>();
            for (var r = InventoryRarity.Common; r <= InventoryRarity.Legendary; r++)
            {
                Assert.IsTrue(seen.Add(InventoryRarityRules.ColorOf(r)), r.ToString());
                Assert.IsFalse(string.IsNullOrEmpty(InventoryRarityRules.NameOf(r)));
            }
        }

        [Test]
        public void ActionRequest_PickupLootCarriesSpawnId()
        {
            var request = InventoryActionRequest.PickupLoot(42);
            Assert.AreEqual(InventoryActionKind.PickupLoot, request.Kind);
            Assert.AreEqual(42, request.SpawnId);
            Assert.AreEqual(0, InventoryActionRequest.Cancel().SpawnId);
        }
    }
}
#endif
