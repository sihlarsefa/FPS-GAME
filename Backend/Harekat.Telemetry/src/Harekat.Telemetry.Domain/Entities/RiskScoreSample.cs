namespace Harekat.Telemetry.Domain.Entities;

public sealed class RiskScoreSample
{
    public required string PlayerId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required double Score { get; init; }
    public string? MatchId { get; init; }
    public string? Reason { get; init; }
}
