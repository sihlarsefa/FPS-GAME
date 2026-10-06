namespace Project.Core.Domain
{
    /// <summary>Envanter / yağma eşyası tanımı (silahlar hariç tüm eşyalar ve silahların eşya karşılığı).</summary>
    public sealed class ItemDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public ItemCategory Category { get; set; }
        public float Weight { get; set; }
        public int PickupQuantity { get; set; } = 1;
        public AmmoType AmmoType { get; set; }
        public int Level { get; set; }
        public float Capacity { get; set; }
        public float Durability { get; set; }
        public float DamageReduction { get; set; }
        public float HealAmount { get; set; }
        public float HealCap { get; set; } = 100f;
        public float BoostAmount { get; set; }
        public float UseSeconds { get; set; }
        public string WeaponId { get; set; }

        public bool IsStackable =>
            Category == ItemCategory.Ammunition ||
            Category == ItemCategory.Medical ||
            Category == ItemCategory.Boost ||
            Category == ItemCategory.Equipment ||
            Category == ItemCategory.Throwable;
    }
}
