using Harekat.DiscordBot.Domain;

namespace Harekat.DiscordBot.Services;

public interface ILeaderboardService
{
    Task<IReadOnlyList<LeaderboardEntry>> GetOverallAsync(int top = 10, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntry>> GetSeasonAsync(int top = 10, CancellationToken ct = default);
    Task<IReadOnlyList<SquadKillLeaderboardEntry>> GetWeeklyTopKillerSquadsAsync(int top = 5, CancellationToken ct = default);
    Task<SeasonInfo> GetCurrentSeasonAsync(CancellationToken ct = default);
}

public sealed class LeaderboardService(InMemoryGameDataStore store, PerformanceMetrics metrics) : ILeaderboardService
{
    public Task<IReadOnlyList<LeaderboardEntry>> GetOverallAsync(int top = 10, CancellationToken ct = default) =>
        metrics.MeasureAsync("leaderboard.overall", () =>
        {
            ct.ThrowIfCancellationRequested();
            top = Math.Clamp(top, 1, 25);
            var entries = store.GetPlayers()
                .OrderByDescending(p => p.Stats.Experience)
                .ThenByDescending(p => p.Stats.Wins)
                .ThenByDescending(p => p.Stats.Kills)
                .Take(top)
                .Select((p, i) => new LeaderboardEntry
                {
                    Rank = i + 1,
                    DisplayName = p.DisplayName,
                    MilitaryRank = p.Stats.Rank,
                    Score = p.Stats.Experience,
                    Kills = p.Stats.Kills,
                    Wins = p.Stats.Wins
                })
                .ToArray();
            return Task.FromResult<IReadOnlyList<LeaderboardEntry>>(entries);
        });

    public Task<IReadOnlyList<LeaderboardEntry>> GetSeasonAsync(int top = 10, CancellationToken ct = default) =>
        metrics.MeasureAsync("leaderboard.season", () =>
        {
            ct.ThrowIfCancellationRequested();
            top = Math.Clamp(top, 1, 25);
            var entries = store.GetPlayers()
                .OrderByDescending(p => p.SeasonXp)
                .ThenByDescending(p => p.SeasonKills)
                .Take(top)
                .Select((p, i) => new LeaderboardEntry
                {
                    Rank = i + 1,
                    DisplayName = p.DisplayName,
                    MilitaryRank = p.Stats.Rank,
                    Score = p.SeasonXp,
                    Kills = p.SeasonKills,
                    Wins = p.Stats.Wins
                })
                .ToArray();
            return Task.FromResult<IReadOnlyList<LeaderboardEntry>>(entries);
        });

    public Task<IReadOnlyList<SquadKillLeaderboardEntry>> GetWeeklyTopKillerSquadsAsync(int top = 5, CancellationToken ct = default) =>
        metrics.MeasureAsync("leaderboard.weekly_squad_kills", () =>
        {
            ct.ThrowIfCancellationRequested();
            top = Math.Clamp(top, 1, 25);
            var entries = store.GetSquads()
                .OrderByDescending(s => s.WeeklyKills)
                .ThenByDescending(s => s.TotalKills)
                .Take(top)
                .Select((s, i) => new SquadKillLeaderboardEntry
                {
                    Rank = i + 1,
                    SquadName = s.Name,
                    WeeklyKills = s.WeeklyKills,
                    MemberCount = s.MemberCount
                })
                .ToArray();
            return Task.FromResult<IReadOnlyList<SquadKillLeaderboardEntry>>(entries);
        });

    public Task<SeasonInfo> GetCurrentSeasonAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(store.GetCurrentSeason());
    }
}
