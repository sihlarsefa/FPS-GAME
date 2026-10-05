using Project.Core.Domain;

namespace Project.Core.Events
{
    /// <summary>Herhangi bir savaşan (oyuncu veya bot) hasar aldığında yayınlanır.</summary>
    public readonly struct PlayerDamagedEvent : IGameEvent
    {
        public PlayerId VictimId { get; }
        public PlayerId AttackerId { get; }
        public float DamageAmount { get; }
        public float RemainingHealth { get; }
        public BodyPart BodyPart { get; }
        public bool IsHeadshot { get; }
        public string WeaponId { get; }
        public Float3 SourcePosition { get; }
        public bool HasSourcePosition { get; }

        public PlayerDamagedEvent(PlayerId victimId, PlayerId attackerId, float damageAmount, float remainingHealth)
            : this(victimId, attackerId, damageAmount, remainingHealth, BodyPart.Torso, null, Float3.Zero, false)
        {
        }

        public PlayerDamagedEvent(PlayerId victimId, PlayerId attackerId, float damageAmount, float remainingHealth,
            BodyPart bodyPart, string weaponId, Float3 sourcePosition, bool hasSourcePosition)
        {
            VictimId = victimId;
            AttackerId = attackerId;
            DamageAmount = damageAmount;
            RemainingHealth = remainingHealth;
            BodyPart = bodyPart;
            IsHeadshot = bodyPart == BodyPart.Head;
            WeaponId = weaponId;
            SourcePosition = sourcePosition;
            HasSourcePosition = hasSourcePosition;
        }
    }
}
