using Project.Core.Domain;

namespace Project.Core.Events
{
    /// <summary>Bir savaşan "Yaralı" (DBNO) duruma düştüğünde yayınlanır. AttackerId geçersizse çevresel hasardır.</summary>
    public readonly struct DownedEvent : IGameEvent
    {
        public PlayerId VictimId { get; }
        public PlayerId AttackerId { get; }
        public int Team { get; }

        public DownedEvent(PlayerId victimId, PlayerId attackerId, int team)
        {
            VictimId = victimId;
            AttackerId = attackerId;
            Team = team;
        }
    }
}
