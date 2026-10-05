namespace Harekat.Telemetry.Domain.Entities;

public sealed class WeaponBalanceStats
{
    public required string WeaponId { get; init; }
    public required int Kills { get; init; }
    public required int Deaths { get; init; }
    public required int Shots { get; init; }
    public required int Hits { get; init; }
    public required double AverageKillDistance { get; init; }
    public required double AverageTtkMs { get; init; }
    public required IReadOnlyList<double> TtkSamplesMs { get; init; }

    public double Kd => Deaths == 0 ? Kills : (double)Kills / Deaths;
    public double HitRate => Shots == 0 ? 0 : (double)Hits / Shots;
}
