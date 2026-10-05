using Harekat.Telemetry.Domain.Enums;

namespace Harekat.Telemetry.Domain.Entities;

public sealed class SuspicionFinding
{
    public required SuspicionRuleKind Rule { get; init; }
    public required string RuleId { get; init; }
    public required string PlayerId { get; init; }
    public required string MatchId { get; init; }
    public required double ScoreContribution { get; init; }
    public required string Detail { get; init; }
    public required DateTimeOffset DetectedAt { get; init; }
    public IReadOnlyDictionary<string, double>? Metrics { get; init; }
}
