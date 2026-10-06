using Project.Core.Domain;

namespace Project.Presentation.UI
{
    /// <summary>Envanter ekranından istenebilecek eylemler.</summary>
    public enum InventoryActionKind
    {
        /// <summary>Silah yuvasını ele al (<see cref="InventoryActionRequest.Slot"/>).</summary>
        EquipWeapon = 0,
        /// <summary>Silah yuvasındaki silahı yere bırak.</summary>
        DropWeapon = 1,
        /// <summary>Yelek/kask/çantayı çıkarıp yere bırak (<see cref="InventoryActionRequest.Category"/>).</summary>
        DropEquipment = 2,
        /// <summary>Yığından <see cref="InventoryActionRequest.Quantity"/> adet bırak.</summary>
        DropStack = 3,
        /// <summary>Tıbbi malzeme / takviye kullanmaya başla.</summary>
        UseItem = 4,
        /// <summary>Süren eşya kullanımını iptal et.</summary>
        CancelUse = 5,
        /// <summary>Yerdeki eşyayı al (<see cref="InventoryActionRequest.SpawnId"/>; sunucu mesafeyi doğrular).</summary>
        PickupLoot = 6
    }

    /// <summary>
    /// Envanter ekranının istediği eylem. Otorite (sunucu / çevrimdışı) tarafında ekran eylemi doğrudan uygular;
    /// otoritesiz istemcide (Windows dedicated server'a bağlı oyun) eylem uygulanmaz,
    /// <see cref="InventoryView.RemoteActionRequested"/> ile ağ katmanına iletilir ve sunucu doğrular.
    /// </summary>
    public readonly struct InventoryActionRequest
    {
        public InventoryActionKind Kind { get; }

        /// <summary>Silah yuvası (Equip/DropWeapon), yoksa -1.</summary>
        public int Slot { get; }

        /// <summary>Teçhizat türü (DropEquipment), yoksa <see cref="ItemCategory.None"/>.</summary>
        public ItemCategory Category { get; }

        /// <summary>Eşya kimliği (DropStack/UseItem), yoksa null.</summary>
        public string ItemId { get; }

        /// <summary>Bırakılacak adet (DropStack), yoksa 0.</summary>
        public int Quantity { get; }

        /// <summary>Yerdeki eşyanın <c>LootPickupComponent.SpawnId</c> değeri (PickupLoot), yoksa 0.</summary>
        public int SpawnId { get; }

        public InventoryActionRequest(InventoryActionKind kind, int slot, ItemCategory category, string itemId, int quantity, int spawnId = 0)
        {
            SpawnId = spawnId;
            Kind = kind;
            Slot = slot;
            Category = category;
            ItemId = itemId;
            Quantity = quantity;
        }

        public static InventoryActionRequest Equip(int slot) => new InventoryActionRequest(InventoryActionKind.EquipWeapon, slot, ItemCategory.None, null, 0);
        public static InventoryActionRequest DropWeapon(int slot) => new InventoryActionRequest(InventoryActionKind.DropWeapon, slot, ItemCategory.Weapon, null, 1);
        public static InventoryActionRequest DropEquipment(ItemCategory category) => new InventoryActionRequest(InventoryActionKind.DropEquipment, -1, category, null, 1);
        public static InventoryActionRequest DropStack(string itemId, int quantity) => new InventoryActionRequest(InventoryActionKind.DropStack, -1, ItemCategory.None, itemId, quantity);
        public static InventoryActionRequest Use(string itemId) => new InventoryActionRequest(InventoryActionKind.UseItem, -1, ItemCategory.None, itemId, 1);
        public static InventoryActionRequest PickupLoot(int spawnId) => new InventoryActionRequest(InventoryActionKind.PickupLoot, -1, ItemCategory.None, null, 1, spawnId);
        public static InventoryActionRequest Cancel() => new InventoryActionRequest(InventoryActionKind.CancelUse, -1, ItemCategory.None, null, 0);

        public override string ToString() => Kind + " slot=" + Slot + " cat=" + Category + " item=" + (ItemId ?? "-") + " x" + Quantity + (SpawnId != 0 ? " spawn=" + SpawnId : string.Empty);
    }
}
