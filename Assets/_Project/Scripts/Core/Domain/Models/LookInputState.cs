namespace Project.Core.Domain
{
    public readonly struct LookInputState
    {
        public float PitchDelta { get; }
        public float YawDelta { get; }

        public LookInputState(float pitchDelta, float yawDelta)
        {
            PitchDelta = pitchDelta;
            YawDelta = yawDelta;
        }

        public static LookInputState Zero => new(0f, 0f);
    }
}
