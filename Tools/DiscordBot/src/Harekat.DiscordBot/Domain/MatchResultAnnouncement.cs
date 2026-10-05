namespace Harekat.DiscordBot.Domain;

public sealed class MatchResultAnnouncement
{
    public required string MatchId { get; init; }
    public required string WinningSquadName { get; init; }
    public required IReadOnlyList<string> WinningMembers { get; init; }
    public int WinningTeamKills { get; init; }
    public int TeamCount { get; init; }
    public string? MapName { get; init; }
    public DateTimeOffset EndedAt { get; init; } = DateTimeOffset.UtcNow;
}
