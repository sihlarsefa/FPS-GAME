using Project.Core.Domain;

namespace Project.Core.Events
{
    /// <summary>Bir silah ateşlendiğinde yayınlanır (ses, botların duyması, istatistik).</summary>
    public readonly struct WeaponFiredEvent : IGameEvent
    {
        public PlayerId ShooterId { get; }
        public string WeaponId { get; }
        public int RemainingAmmo { get; }
        public HitScanResult Hit { get; }
        public Float3 Origin { get; }
        public bool HasOrigin { get; }

        public WeaponFiredEvent(PlayerId shooterId, string weaponId, int remainingAmmo, HitScanResult hit)
        {
            ShooterId = shooterId;
            WeaponId = weaponId;
            RemainingAmmo = remainingAmmo;
            Hit = hit;
            Origin = Float3.Zero;
            HasOrigin = false;
        }

        public WeaponFiredEvent(PlayerId shooterId, string weaponId, int remainingAmmo, Float3 origin)
        {
            ShooterId = shooterId;
            WeaponId = weaponId;
            RemainingAmmo = remainingAmmo;
            Hit = HitScanResult.Miss;
            Origin = origin;
            HasOrigin = true;
        }
    }
}
