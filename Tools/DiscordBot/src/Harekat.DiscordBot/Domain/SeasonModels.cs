namespace Harekat.DiscordBot.Domain;

public sealed class SeasonInfo
{
    public required string SeasonId { get; init; }
    public required string Name { get; set; }
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public bool IsActive { get; set; }

    public bool Contains(DateTimeOffset moment) =>
        moment >= StartsAt && moment <= EndsAt;
}

public sealed class LeaderboardEntry
{
    public required int Rank { get; init; }
    public required string DisplayName { get; init; }
    public required MilitaryRank MilitaryRank { get; init; }
    public required int Score { get; init; }
    public int Kills { get; init; }
    public int Wins { get; init; }
}

public sealed class SquadKillLeaderboardEntry
{
    public required int Rank { get; init; }
    public required string SquadName { get; init; }
    public required int WeeklyKills { get; init; }
    public int MemberCount { get; init; }
}
