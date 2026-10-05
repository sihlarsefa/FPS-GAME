namespace Project.Core.Domain
{
    public readonly struct HealthState
    {
        public float Current { get; }
        public float Max { get; }

        public HealthState(float current, float max)
        {
            Current = current;
            Max = max;
        }

        public bool IsAlive => Current > 0f;
        public float Normalized => Max > 0f ? Current / Max : 0f;

        public HealthState WithCurrent(float current) => new(current, Max);
    }
}
