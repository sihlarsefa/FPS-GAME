using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// Yerdeki eşyayı alma isteğini işler ve başarılıysa LootPickedUpEvent yayınlar.
    /// (Yeni akışta Infrastructure/Loot doğrudan InventoryService.TryPickup kullanır; bu sınıf eski API içindir.)
    /// </summary>
    public sealed class LootInteractionService
    {
        private readonly IEventBus _eventBus;

        public LootInteractionService(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public bool TryPickup(PlayerId playerId, ILootPickup pickup, IInventory inventory)
        {
            if (pickup == null || inventory == null || !pickup.IsAvailable)
                return false;

            // Alımdan sonra yerdeki veri değişebilir (kısmi alma); olaya alım öncesi eşyayı koy.
            var item = pickup.Item;
            if (!pickup.TryPickup(playerId, inventory))
                return false;

            _eventBus?.Publish(new LootPickedUpEvent(playerId, item));
            return true;
        }
    }
}
