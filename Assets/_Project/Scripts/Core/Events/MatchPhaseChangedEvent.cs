using Project.Core.Domain;

namespace Project.Core.Events
{
    public readonly struct MatchPhaseChangedEvent : IGameEvent
    {
        public MatchPhase PreviousPhase { get; }
        public MatchPhase CurrentPhase { get; }

        public MatchPhaseChangedEvent(MatchPhase previousPhase, MatchPhase currentPhase)
        {
            PreviousPhase = previousPhase;
            CurrentPhase = currentPhase;
        }
    }
}
