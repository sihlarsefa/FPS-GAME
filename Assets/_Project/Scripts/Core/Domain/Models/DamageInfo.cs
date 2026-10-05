namespace Project.Core.Domain
{
    public readonly struct DamageInfo
    {
        public float Amount { get; }
        public PlayerId AttackerId { get; }
        public string SourceWeaponId { get; }
        public bool IsHeadshot { get; }
        public BodyPart BodyPart { get; }
        public Float3 SourcePosition { get; }
        public bool HasSourcePosition { get; }

        public DamageInfo(float amount, PlayerId attackerId, string sourceWeaponId, bool isHeadshot = false)
            : this(amount, attackerId, sourceWeaponId, isHeadshot ? BodyPart.Head : BodyPart.Torso, Float3.Zero, false)
        {
        }

        public DamageInfo(float amount, PlayerId attackerId, string sourceWeaponId, BodyPart bodyPart,
            Float3 sourcePosition, bool hasSourcePosition)
        {
            Amount = amount;
            AttackerId = attackerId;
            SourceWeaponId = sourceWeaponId;
            BodyPart = bodyPart;
            IsHeadshot = bodyPart == BodyPart.Head;
            SourcePosition = sourcePosition;
            HasSourcePosition = hasSourcePosition;
        }

        public DamageInfo WithAmount(float amount) =>
            new(amount, AttackerId, SourceWeaponId, BodyPart, SourcePosition, HasSourcePosition);
    }
}
