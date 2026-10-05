namespace Project.Core.Domain
{
    /// <summary>Mavi bölgenin tek bir fazı: bekleme, daralma süresi, hedef yarıçap ve hasar.</summary>
    public readonly struct ZonePhase
    {
        public float WaitSeconds { get; }
        public float ShrinkSeconds { get; }
        public float TargetRadius { get; }
        public float DamagePerSecond { get; }

        public ZonePhase(float waitSeconds, float shrinkSeconds, float targetRadius, float damagePerSecond)
        {
            WaitSeconds = waitSeconds;
            ShrinkSeconds = shrinkSeconds;
            TargetRadius = targetRadius;
            DamagePerSecond = damagePerSecond;
        }
    }
}
