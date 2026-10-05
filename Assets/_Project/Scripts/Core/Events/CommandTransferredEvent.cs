using Project.Core.Domain;

namespace Project.Core.Events
{
    /// <summary>Tim komutanı şehit düştüğünde komuta zincirindeki sıradaki en kıdemliye geçer.</summary>
    public readonly struct CommandTransferredEvent : IGameEvent
    {
        public int Team { get; }
        public PlayerId PreviousCommanderId { get; }
        public PlayerId NewCommanderId { get; }

        public CommandTransferredEvent(int team, PlayerId previousCommanderId, PlayerId newCommanderId)
        {
            Team = team;
            PreviousCommanderId = previousCommanderId;
            NewCommanderId = newCommanderId;
        }
    }
}
