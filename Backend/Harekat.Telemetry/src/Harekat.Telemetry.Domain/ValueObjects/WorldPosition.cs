namespace Harekat.Telemetry.Domain.ValueObjects;

/// <summary>Kuzgun Vadisi dünya konumu (metre).</summary>
public readonly record struct WorldPosition(float X, float Y, float Z)
{
    public float HorizontalDistanceTo(WorldPosition other)
    {
        var dx = X - other.X;
        var dz = Z - other.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    public float DistanceTo(WorldPosition other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
