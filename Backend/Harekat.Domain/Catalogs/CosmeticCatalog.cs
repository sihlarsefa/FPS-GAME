namespace Harekat.Domain.Catalogs;

/// <summary>Yalnızca kozmetik kamuflaj ve bere açılımları.</summary>
public static class CosmeticCatalog
{
    public sealed record Item(string Id, string Name, string Slot, int UnlockXp);

    public static readonly IReadOnlyList<Item> All =
    [
        new("camo_standard", "Standart Kamuflaj", "camo", 0),
        new("camo_desert", "Çöl Kamuflajı", "camo", 2000),
        new("camo_forest", "Orman Kamuflajı", "camo", 5000),
        new("camo_urban", "Kent Kamuflajı", "camo", 10000),
        new("camo_snow", "Kar Kamuflajı", "camo", 20000),
        new("camo_night", "Gece Operasyonu", "camo", 35000),
        new("beret_green", "Yeşil Bere", "beret", 0),
        new("beret_maroon", "Bordo Bere", "beret", 3000),
        new("beret_black", "Siyah Bere", "beret", 8000),
        new("beret_navy", "Lacivert Bere", "beret", 15000),
        new("beret_gold", "Altın Bere", "beret", 50000)
    ];
}
