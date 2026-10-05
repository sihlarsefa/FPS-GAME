namespace Project.Core.Domain
{
    public readonly struct ZoneState
    {
        public float CenterX { get; }
        public float CenterZ { get; }
        public float Radius { get; }
        public float DamagePerSecond { get; }

        public ZoneState(float centerX, float centerZ, float radius, float damagePerSecond)
        {
            CenterX = centerX;
            CenterZ = centerZ;
            Radius = radius;
            DamagePerSecond = damagePerSecond;
        }

        public bool Contains(float x, float z)
        {
            var dx = x - CenterX;
            var dz = z - CenterZ;
            return dx * dx + dz * dz <= Radius * Radius;
        }
    }
}
