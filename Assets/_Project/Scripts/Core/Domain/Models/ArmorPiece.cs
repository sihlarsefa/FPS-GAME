namespace Project.Core.Domain
{
    /// <summary>Kuşanılmış yelek veya kask. Dayanıklılık hasar emdikçe azalır.</summary>
    public sealed class ArmorPiece
    {
        public string ItemId { get; }
        public int Level { get; }
        public float MaxDurability { get; }
        public float DamageReduction { get; }
        public float Durability { get; private set; }

        public ArmorPiece(string itemId, int level, float maxDurability, float damageReduction, float durability)
        {
            ItemId = itemId;
            Level = level;
            MaxDurability = maxDurability;
            DamageReduction = damageReduction;
            Durability = durability < 0f ? 0f : durability > maxDurability ? maxDurability : durability;
        }

        public bool IsBroken => Durability <= 0f;
        public float DurabilityNormalized => MaxDurability > 0f ? Durability / MaxDurability : 0f;

        public void Wear(float amount)
        {
            if (amount <= 0f)
                return;

            Durability = Durability - amount < 0f ? 0f : Durability - amount;
        }
    }
}
