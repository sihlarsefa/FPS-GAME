using Project.Core.Domain;

namespace Project.Core.Events
{
    public readonly struct SquadOrderIssuedEvent : IGameEvent
    {
        public int Team { get; }
        public SquadOrder Order { get; }
        public Float3 Target { get; }

        public SquadOrderIssuedEvent(int team, SquadOrder order, Float3 target)
        {
            Team = team;
            Order = order;
            Target = target;
        }
    }
}
