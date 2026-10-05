using Harekat.Domain.Enums;

namespace Harekat.Domain.ValueObjects;

/// <summary>Oyuncu kariyer istatistikleri — Unity CareerStats ile birebir aynı alanlar.</summary>
public sealed class CareerStats
{
    public int Matches { get; set; }
    public int Wins { get; set; }
    public int Kills { get; set; }
    public int Headshots { get; set; }
    public int BestPlacement { get; set; }
    public float TotalDamage { get; set; }
    public float LongestSurvivalSeconds { get; set; }

    /// <summary>Tecrübe puanı — rütbe terfisini belirler.</summary>
    public int Experience { get; set; }

    public MilitaryRank Rank { get; set; } = MilitaryRank.Er;
}
