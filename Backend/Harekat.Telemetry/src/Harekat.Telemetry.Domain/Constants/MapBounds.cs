namespace Harekat.Telemetry.Domain.Constants;

/// <summary>Kuzgun Vadisi harita sınırları (-512..512).</summary>
public static class MapBounds
{
    public const float MinX = -512f;
    public const float MaxX = 512f;
    public const float MinZ = -512f;
    public const float MaxZ = 512f;
    public const float Size = 1024f;
    public const float HeatmapGridMeters = 10f;

    public static bool Contains(float x, float z) =>
        x is >= MinX and <= MaxX && z is >= MinZ and <= MaxZ;

    public static (int GridX, int GridZ) ToGrid(float x, float z, float cellSize = HeatmapGridMeters)
    {
        var gx = (int)MathF.Floor((x - MinX) / cellSize);
        var gz = (int)MathF.Floor((z - MinZ) / cellSize);
        var max = (int)(Size / cellSize) - 1;
        return (Math.Clamp(gx, 0, max), Math.Clamp(gz, 0, max));
    }

    public static int GridDimension(float cellSize = HeatmapGridMeters) =>
        (int)(Size / cellSize);
}
