using Project.Core.Domain;

namespace Project.Core.Events
{
    /// <summary>Bir savaşan öldüğünde yayınlanır. KillerId geçersizse ölüm çevreseldir (bölge, düşme).</summary>
    public readonly struct PlayerDiedEvent : IGameEvent
    {
        public PlayerId VictimId { get; }
        public PlayerId KillerId { get; }
        public string WeaponId { get; }
        public bool IsHeadshot { get; }

        public PlayerDiedEvent(PlayerId victimId, PlayerId killerId)
            : this(victimId, killerId, null, false)
        {
        }

        public PlayerDiedEvent(PlayerId victimId, PlayerId killerId, string weaponId, bool isHeadshot)
        {
            VictimId = victimId;
            KillerId = killerId;
            WeaponId = weaponId;
            IsHeadshot = isHeadshot;
        }
    }
}
