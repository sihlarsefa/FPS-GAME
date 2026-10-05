using Project.Core.Domain;

namespace Project.Core.Events
{
    public readonly struct ZoneStageChangedEvent : IGameEvent
    {
        public int PhaseIndex { get; }
        public int PhaseCount { get; }
        public ZoneStage Stage { get; }
        public float DurationSeconds { get; }

        public ZoneStageChangedEvent(int phaseIndex, int phaseCount, ZoneStage stage, float durationSeconds)
        {
            PhaseIndex = phaseIndex;
            PhaseCount = phaseCount;
            Stage = stage;
            DurationSeconds = durationSeconds;
        }
    }
}
