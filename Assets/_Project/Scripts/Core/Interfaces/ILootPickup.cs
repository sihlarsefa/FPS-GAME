using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface ILootPickup
    {
        LootItemData Item { get; }
        bool IsAvailable { get; }
        bool TryPickup(PlayerId playerId, IInventory inventory);
    }
}
