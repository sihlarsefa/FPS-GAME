using Project.Core.Domain;

namespace Project.Core.Events
{
    public readonly struct ExplosionEvent : IGameEvent
    {
        public Float3 Position { get; }
        public float Radius { get; }
        public PlayerId AttackerId { get; }

        public ExplosionEvent(Float3 position, float radius, PlayerId attackerId)
        {
            Position = position;
            Radius = radius;
            AttackerId = attackerId;
        }
    }
}
