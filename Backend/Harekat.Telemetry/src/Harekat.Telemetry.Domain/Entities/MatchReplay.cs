using Harekat.Telemetry.Domain.Enums;
using Harekat.Telemetry.Domain.ValueObjects;

namespace Harekat.Telemetry.Domain.Entities;

/// <summary>Maç tekrarı karesi — sıkıştırılmış saklama için kompakt format.</summary>
public sealed class ReplayFrame
{
    public required int Sequence { get; init; }
    public required float TimeSeconds { get; init; }
    public required string PlayerId { get; init; }
    public required WorldPosition Position { get; init; }
    public float Yaw { get; init; }
    public float Pitch { get; init; }
    public MatchEventType? EventHint { get; init; }
    public string? WeaponId { get; init; }
}

public sealed class MatchReplay
{
    public required string MatchId { get; init; }
    public required string MapId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required IReadOnlyList<ReplayFrame> Frames { get; init; }
    public string FormatVersion { get; init; } = "harekat-replay-v1";
}
