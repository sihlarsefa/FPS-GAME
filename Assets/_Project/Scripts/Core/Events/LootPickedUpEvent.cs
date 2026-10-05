using Project.Core.Domain;

namespace Project.Core.Events
{
    public readonly struct LootPickedUpEvent : IGameEvent
    {
        public PlayerId PlayerId { get; }
        public LootItemData Item { get; }

        public LootPickedUpEvent(PlayerId playerId, LootItemData item)
        {
            PlayerId = playerId;
            Item = item;
        }
    }
}
