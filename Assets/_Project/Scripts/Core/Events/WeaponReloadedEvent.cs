using Project.Core.Domain;

namespace Project.Core.Events
{
    public readonly struct WeaponReloadedEvent : IGameEvent
    {
        public PlayerId ShooterId { get; }
        public string WeaponId { get; }
        public int AmmoAfterReload { get; }

        public WeaponReloadedEvent(PlayerId shooterId, string weaponId, int ammoAfterReload)
        {
            ShooterId = shooterId;
            WeaponId = weaponId;
            AmmoAfterReload = ammoAfterReload;
        }
    }
}
