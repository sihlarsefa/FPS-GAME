using Project.Core.Domain;

namespace Project.Core.Events
{
    public readonly struct WeaponReloadStartedEvent : IGameEvent
    {
        public PlayerId ShooterId { get; }
        public string WeaponId { get; }
        public float DurationSeconds { get; }

        public WeaponReloadStartedEvent(PlayerId shooterId, string weaponId, float durationSeconds)
        {
            ShooterId = shooterId;
            WeaponId = weaponId;
            DurationSeconds = durationSeconds;
        }
    }
}
