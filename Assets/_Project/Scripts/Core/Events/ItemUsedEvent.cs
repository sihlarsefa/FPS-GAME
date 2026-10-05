using Project.Core.Domain;

namespace Project.Core.Events
{
    /// <summary>Tıbbi/boost eşyası kullanımı başladı, bitti ya da iptal edildi.</summary>
    public readonly struct ItemUsedEvent : IGameEvent
    {
        public PlayerId UserId { get; }
        public string ItemId { get; }
        public bool Started { get; }
        public bool Completed { get; }

        public ItemUsedEvent(PlayerId userId, string itemId, bool started, bool completed)
        {
            UserId = userId;
            ItemId = itemId;
            Started = started;
            Completed = completed;
        }
    }
}
