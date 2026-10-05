using Harekat.Telemetry.Domain.Enums;
using Harekat.Telemetry.Domain.ValueObjects;

namespace Harekat.Telemetry.Domain.Entities;

public sealed class MatchEvent
{
    public required Guid Id { get; init; }
    public required string MatchId { get; init; }
    public required string PlayerId { get; init; }
    public required MatchEventType EventType { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public WorldPosition? Position { get; init; }
    public WorldPosition? TargetPosition { get; init; }
    public string? TargetPlayerId { get; init; }
    public string? WeaponId { get; init; }
    public bool IsHeadshot { get; init; }
    public bool ThroughWall { get; init; }
    public float? Damage { get; init; }
    public float? TimeToKillMs { get; init; }
    public float? DistanceMeters { get; init; }
}
