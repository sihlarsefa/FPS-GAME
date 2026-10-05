using Project.Core.Domain;

namespace Project.Core.Events
{
    /// <summary>Otorite bir isabeti onayladığında yayınlanır (isabet işareti, istatistik).</summary>
    public readonly struct HitConfirmedEvent : IGameEvent
    {
        public PlayerId AttackerId { get; }
        public PlayerId VictimId { get; }
        public float Damage { get; }
        public bool IsHeadshot { get; }
        public bool IsKill { get; }
        public bool ArmorAbsorbed { get; }

        public HitConfirmedEvent(PlayerId attackerId, PlayerId victimId, float damage, bool isHeadshot, bool isKill, bool armorAbsorbed)
        {
            AttackerId = attackerId;
            VictimId = victimId;
            Damage = damage;
            IsHeadshot = isHeadshot;
            IsKill = isKill;
            ArmorAbsorbed = armorAbsorbed;
        }
    }
}
