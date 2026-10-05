using System.ComponentModel.DataAnnotations;
using Harekat.Telemetry.Domain.Enums;

namespace Harekat.Telemetry.Application.Contracts;

public sealed class EventBatchRequest
{
    [Required, MinLength(1)]
    public string MatchId { get; set; } = "";

    [Required, MinLength(1)]
    public List<MatchEventDto> Events { get; set; } = [];
}

public sealed class MatchEventDto
{
    [Required, MinLength(1)]
    public string PlayerId { get; set; } = "";

    [Required]
    public MatchEventType EventType { get; set; }

    public DateTimeOffset? Timestamp { get; set; }

    public float? X { get; set; }
    public float? Y { get; set; }
    public float? Z { get; set; }

    public float? TargetX { get; set; }
    public float? TargetY { get; set; }
    public float? TargetZ { get; set; }

    public string? TargetPlayerId { get; set; }
    public string? WeaponId { get; set; }
    public bool IsHeadshot { get; set; }
    public bool ThroughWall { get; set; }
    public float? Damage { get; set; }
    public float? TimeToKillMs { get; set; }
    public float? DistanceMeters { get; set; }
}

public sealed class EventBatchResponse
{
    public required string MatchId { get; init; }
    public required int Accepted { get; init; }
    public required int Rejected { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
    public required IReadOnlyList<PlayerSuspicionSummaryDto> SuspicionSummaries { get; init; }
}

public sealed class PlayerSuspicionSummaryDto
{
    public required string PlayerId { get; init; }
    public required double Score { get; init; }
    public required bool IsSuspect { get; init; }
    public required int FindingCount { get; init; }
}

public sealed class SuspicionReportDto
{
    public required string PlayerId { get; init; }
    public required string MatchId { get; init; }
    public required double TotalScore { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required IReadOnlyList<SuspicionFindingDto> Findings { get; init; }
}

public sealed class SuspicionFindingDto
{
    public required string RuleId { get; init; }
    public required string Rule { get; init; }
    public required double ScoreContribution { get; init; }
    public required string Detail { get; init; }
    public IReadOnlyDictionary<string, double>? Metrics { get; init; }
}

public sealed class HeatmapResponse
{
    public required string MatchId { get; init; }
    public required float CellSizeMeters { get; init; }
    public required int GridWidth { get; init; }
    public required IReadOnlyList<HeatmapCellDto> Cells { get; init; }
}

public sealed class HeatmapCellDto
{
    public required int GridX { get; init; }
    public required int GridZ { get; init; }
    public required int Deaths { get; init; }
    public required int Landings { get; init; }
}

public sealed class WeaponBalanceReportDto
{
    public required string Scope { get; init; }
    public required IReadOnlyList<WeaponBalanceItemDto> Weapons { get; init; }
}

public sealed class WeaponBalanceItemDto
{
    public required string WeaponId { get; init; }
    public required int Kills { get; init; }
    public required int Deaths { get; init; }
    public required double Kd { get; init; }
    public required double AverageKillDistance { get; init; }
    public required double AverageTtkMs { get; init; }
    public required double HitRate { get; init; }
    public required int Shots { get; init; }
    public required int Hits { get; init; }
    public required IReadOnlyList<double> TtkPercentiles { get; init; }
}

public sealed class RiskSeriesDto
{
    public required string PlayerId { get; init; }
    public required IReadOnlyList<RiskPointDto> Points { get; init; }
}

public sealed class RiskPointDto
{
    public required DateTimeOffset Timestamp { get; init; }
    public required double Score { get; init; }
    public string? MatchId { get; init; }
    public string? Reason { get; init; }
}

public sealed class ReviewQueueItemDto
{
    public required Guid Id { get; init; }
    public required string PlayerId { get; init; }
    public required string MatchId { get; init; }
    public required double RiskScore { get; init; }
    public required DateTimeOffset EnqueuedAt { get; init; }
    public required string Status { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateReviewStatusRequest
{
    [Required]
    public ReviewQueueStatus Status { get; set; }
    public string? Notes { get; set; }
}

public sealed class ReplaySaveRequest
{
    [Required]
    public string MatchId { get; set; } = "";
    public string MapId { get; set; } = "kuzgun_vadisi";
    public DateTimeOffset? StartedAt { get; set; }
    [Required, MinLength(1)]
    public List<ReplayFrameDto> Frames { get; set; } = [];
}

public sealed class ReplayFrameDto
{
    public int Sequence { get; set; }
    public float TimeSeconds { get; set; }
    [Required]
    public string PlayerId { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public MatchEventType? EventHint { get; set; }
    public string? WeaponId { get; set; }
}

public sealed class ReplayMetaDto
{
    public required string MatchId { get; init; }
    public required string FormatVersion { get; init; }
    public required int FrameCount { get; init; }
    public required long CompressedBytes { get; init; }
}
