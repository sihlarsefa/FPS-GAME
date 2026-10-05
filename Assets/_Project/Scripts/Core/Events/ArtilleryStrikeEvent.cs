using Project.Core.Domain;

namespace Project.Core.Events
{
    /// <summary>Telsizle topçu desteği istendi (Started) ya da tek bir mermi düştü (Impact).</summary>
    public readonly struct ArtilleryStrikeEvent : IGameEvent
    {
        public int Team { get; }
        public PlayerId CallerId { get; }
        public Float3 Target { get; }
        public bool IsImpact { get; }

        public ArtilleryStrikeEvent(int team, PlayerId callerId, Float3 target, bool isImpact)
        {
            Team = team;
            CallerId = callerId;
            Target = target;
            IsImpact = isImpact;
        }
    }
}
