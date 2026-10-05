namespace Project.Core.Domain
{
    public readonly struct HitScanRequest
    {
        public float OriginX { get; }
        public float OriginY { get; }
        public float OriginZ { get; }
        public float DirectionX { get; }
        public float DirectionY { get; }
        public float DirectionZ { get; }
        public float MaxRange { get; }

        public HitScanRequest(
            float originX, float originY, float originZ,
            float directionX, float directionY, float directionZ,
            float maxRange)
        {
            OriginX = originX;
            OriginY = originY;
            OriginZ = originZ;
            DirectionX = directionX;
            DirectionY = directionY;
            DirectionZ = directionZ;
            MaxRange = maxRange;
        }
    }
}
