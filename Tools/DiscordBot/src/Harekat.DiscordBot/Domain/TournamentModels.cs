namespace Harekat.DiscordBot.Domain;

public enum TournamentStatus
{
    Draft,
    Registration,
    InProgress,
    Completed,
    Cancelled
}

public enum MatchSlotStatus
{
    Pending,
    Ready,
    Completed,
    Bye
}

public sealed class Tournament
{
    public required string TournamentId { get; init; }
    public required string Name { get; set; }
    public TournamentStatus Status { get; set; } = TournamentStatus.Draft;
    public List<string> TeamNames { get; } = [];
    public List<TournamentMatch> Matches { get; } = [];
    public string? WinnerTeam { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class TournamentMatch
{
    public required string MatchId { get; init; }
    public required int Round { get; init; }
    public required int SlotIndex { get; init; }
    public string? TeamA { get; set; }
    public string? TeamB { get; set; }
    public string? Winner { get; set; }
    public MatchSlotStatus Status { get; set; } = MatchSlotStatus.Pending;
    public string? NextMatchId { get; set; }
    public bool AdvancesAsTeamA { get; set; }
}
