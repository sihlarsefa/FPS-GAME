using Harekat.Telemetry.Domain.Enums;

namespace Harekat.Telemetry.Domain.Entities;

public sealed class ReviewQueueItem
{
    public required Guid Id { get; init; }
    public required string PlayerId { get; init; }
    public required string MatchId { get; init; }
    public required double RiskScore { get; set; }
    public required DateTimeOffset EnqueuedAt { get; init; }
    public ReviewQueueStatus Status { get; set; } = ReviewQueueStatus.Pending;
    public string? Notes { get; set; }
}
