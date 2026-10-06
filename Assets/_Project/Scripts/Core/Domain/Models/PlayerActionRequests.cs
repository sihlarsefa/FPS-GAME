namespace Project.Core.Domain
{
    /// <summary>Bomba türü (ağ üzerinde bayt olarak taşınır): 0 = parçalı, 1 = sis, 2 = flaş, 3 = molotof, 4 = yem (yeni kodlar sona eklenir).</summary>
    public enum ThrowKindCode : byte
    {
        Frag = 0,
        Smoke = 1,
        Flash = 2,
        Molotov = 3,
        Decoy = 4
    }

    /// <summary>Otorite olmayan istemcinin sunucudan istediği bomba atışı.</summary>
    public readonly struct ThrowRequest
    {
        public PlayerId ThrowerId { get; }
        public ThrowKindCode Kind { get; }
        public Float3 Origin { get; }
        public Float3 Velocity { get; }
        public uint Tick { get; }

        public ThrowRequest(PlayerId throwerId, ThrowKindCode kind, Float3 origin, Float3 velocity, uint tick)
        {
            ThrowerId = throwerId;
            Kind = kind;
            Origin = origin;
            Velocity = velocity;
            Tick = tick;
        }
    }

    /// <summary>Otorite olmayan istemcinin sunucudan istediği yakın dövüş vuruşu.</summary>
    public readonly struct MeleeRequest
    {
        public PlayerId AttackerId { get; }
        public Float3 Origin { get; }
        public Float3 Direction { get; }
        public float Range { get; }
        public uint Tick { get; }

        public MeleeRequest(PlayerId attackerId, Float3 origin, Float3 direction, float range, uint tick)
        {
            AttackerId = attackerId;
            Origin = origin;
            Direction = direction;
            Range = range;
            Tick = tick;
        }
    }
}
