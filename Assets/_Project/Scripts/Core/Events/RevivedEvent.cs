using Project.Core.Domain;

namespace Project.Core.Events
{
    /// <summary>Yaralı bir savaşan müttefiki tarafından ayağa kaldırıldığında yayınlanır.</summary>
    public readonly struct RevivedEvent : IGameEvent
    {
        public PlayerId VictimId { get; }
        public PlayerId ReviverId { get; }

        public RevivedEvent(PlayerId victimId, PlayerId reviverId)
        {
            VictimId = victimId;
            ReviverId = reviverId;
        }
    }
}
