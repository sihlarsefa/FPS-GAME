namespace Harekat.Telemetry.Domain.Entities;

public sealed class HeatmapCell
{
    public required int GridX { get; init; }
    public required int GridZ { get; init; }
    public required int DeathCount { get; init; }
    public required int LandingCount { get; init; }

    public int Total => DeathCount + LandingCount;
}
