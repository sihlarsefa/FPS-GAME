namespace Project.Core.Domain
{
    public readonly struct HitScanResult
    {
        public bool HasHit { get; }
        public PlayerId TargetId { get; }
        public bool IsHeadshot { get; }
        public float HitX { get; }
        public float HitY { get; }
        public float HitZ { get; }

        public HitScanResult(bool hasHit, PlayerId targetId, bool isHeadshot, float hitX, float hitY, float hitZ)
        {
            HasHit = hasHit;
            TargetId = targetId;
            IsHeadshot = isHeadshot;
            HitX = hitX;
            HitY = hitY;
            HitZ = hitZ;
        }

        public static HitScanResult Miss => new(false, PlayerId.Invalid, false, 0f, 0f, 0f);
    }
}
