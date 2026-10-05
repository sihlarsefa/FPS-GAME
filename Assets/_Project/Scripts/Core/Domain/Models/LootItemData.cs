namespace Project.Core.Domain
{
    public readonly struct LootItemData
    {
        public string ItemId { get; }
        public ItemCategory Category { get; }
        public string DisplayName { get; }
        public int Quantity { get; }

        /// <summary>Silahlar için şarjördeki mermi; -1 = tam şarjör.</summary>
        public int LoadedAmmo { get; }

        /// <summary>Zırh/kask için kalan dayanıklılık; -1 = yeni.</summary>
        public float Durability { get; }

        public LootItemData(string itemId, ItemCategory category, string displayName, int quantity = 1)
            : this(itemId, category, displayName, quantity, -1, -1f)
        {
        }

        public LootItemData(string itemId, ItemCategory category, string displayName, int quantity, int loadedAmmo, float durability)
        {
            ItemId = itemId;
            Category = category;
            DisplayName = displayName;
            Quantity = quantity;
            LoadedAmmo = loadedAmmo;
            Durability = durability;
        }

        public bool IsValid => !string.IsNullOrEmpty(ItemId) && Quantity > 0;

        public LootItemData WithQuantity(int quantity) =>
            new(ItemId, Category, DisplayName, quantity, LoadedAmmo, Durability);
    }
}
