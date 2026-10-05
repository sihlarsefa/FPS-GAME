namespace Project.Core.Domain
{
    /// <summary>Uçağın harita üzerindeki düz uçuş hattı.</summary>
    public readonly struct PlaneRoute
    {
        public Float3 Start { get; }
        public Float3 End { get; }
        public float Speed { get; }

        public PlaneRoute(Float3 start, Float3 end, float speed)
        {
            Start = start;
            End = end;
            Speed = speed;
        }

        public float Length => Float3.Distance(Start, End);
        public float DurationSeconds => Speed > 0f ? Length / Speed : 0f;
        public Float3 Direction => (End - Start).Normalized;

        public Float3 PositionAt(float elapsedSeconds)
        {
            var duration = DurationSeconds;
            return duration <= 0f ? End : Float3.Lerp(Start, End, elapsedSeconds / duration);
        }

        /// <summary>Uçuş hattının, verilen yarım-boyut kare harita içinde kalan zaman aralığı.</summary>
        public bool TryGetTimeOverMap(float mapHalfSize, out float enterSeconds, out float exitSeconds)
        {
            enterSeconds = 0f;
            exitSeconds = 0f;
            var duration = DurationSeconds;
            if (duration <= 0f)
                return false;

            const int samples = 400;
            var found = false;
            for (var i = 0; i <= samples; i++)
            {
                var t = duration * i / samples;
                var p = PositionAt(t);
                var inside = p.X > -mapHalfSize && p.X < mapHalfSize && p.Z > -mapHalfSize && p.Z < mapHalfSize;
                if (!inside)
                    continue;

                if (!found)
                {
                    enterSeconds = t;
                    found = true;
                }

                exitSeconds = t;
            }

            return found;
        }
    }
}
