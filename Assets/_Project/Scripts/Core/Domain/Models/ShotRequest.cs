namespace Project.Core.Domain
{
    /// <summary>
    /// Bir atış isteği. Otorite (sunucu) ateş hızını/mermiyi doğrular, sonra balistiği kendisi simüle eder.
    /// İstemciden gelen isabet bilgisine asla güvenilmez.
    /// </summary>
    public readonly struct ShotRequest
    {
        public PlayerId ShooterId { get; }
        public string WeaponId { get; }
        public Float3 Origin { get; }
        public Float3 Direction { get; }
        public uint Tick { get; }

        public ShotRequest(PlayerId shooterId, string weaponId, Float3 origin, Float3 direction, uint tick)
        {
            ShooterId = shooterId;
            WeaponId = weaponId;
            Origin = origin;
            Direction = direction;
            Tick = tick;
        }
    }
}
