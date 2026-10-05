using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface IMatchService
    {
        MatchPhase CurrentPhase { get; }
        MatchConfig Config { get; }
        int AlivePlayerCount { get; }
        int TotalPlayers { get; }
        int AliveTeamCount { get; }
        int TeamCount { get; }
        PlayerId WinnerId { get; }
        int WinnerTeam { get; }
        float PhaseElapsedSeconds { get; }
        float MatchElapsedSeconds { get; }
        IReadOnlyCollection<PlayerId> AliveIds { get; }

        void RegisterCombatant(PlayerId id, string displayName, bool isLocalPlayer, int team, TeamRole role);
        void TransitionTo(MatchPhase phase);
        void RegisterPlayerDeath(PlayerId playerId);
        bool IsAlive(PlayerId id);
        bool IsTeamAlive(int team);
        int GetAliveCountInTeam(int team);

        /// <summary>Ölen için bireysel sıralama (#n). Hayattaysa 0.</summary>
        int GetPlacement(PlayerId id);

        /// <summary>Elenen tim için sıralama; hayattaysa 0.</summary>
        int GetTeamPlacement(int team);
    }
}
