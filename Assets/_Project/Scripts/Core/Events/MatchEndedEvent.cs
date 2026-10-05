using Project.Core.Domain;

namespace Project.Core.Events
{
    public readonly struct MatchEndedEvent : IGameEvent
    {
        /// <summary>Kazanan timin hayatta kalan bir üyesi; herkes aynı anda öldüyse geçersiz.</summary>
        public PlayerId WinnerId { get; }

        /// <summary>Kazanan tim (-1 = yok).</summary>
        public int WinnerTeam { get; }

        public MatchEndedEvent(PlayerId winnerId) : this(winnerId, -1)
        {
        }

        public MatchEndedEvent(PlayerId winnerId, int winnerTeam)
        {
            WinnerId = winnerId;
            WinnerTeam = winnerTeam;
        }
    }
}
