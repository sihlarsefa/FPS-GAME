using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface IInventory
    {
        int SlotCount { get; }
        bool TryAddItem(string itemId, ItemCategory category);
        bool TryRemoveItem(string itemId);
        bool HasItem(string itemId);
    }
}
