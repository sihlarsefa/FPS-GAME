namespace Harekat.Telemetry.Domain.Entities;

public sealed class PlayerSuspicionReport
{
    public required string PlayerId { get; init; }
    public required string MatchId { get; init; }
    public required double TotalScore { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required IReadOnlyList<SuspicionFinding> Findings { get; init; }

    public bool IsSuspect => TotalScore >= 50;
}
