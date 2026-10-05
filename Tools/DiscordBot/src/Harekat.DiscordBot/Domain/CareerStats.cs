namespace Harekat.DiscordBot.Domain;

/// <summary>
/// Kariyer istatistikleri — CareerStats.cs ile aynı alanlar.
/// </summary>
public sealed class CareerStats
{
    public int Matches { get; set; }
    public int Wins { get; set; }
    public int Kills { get; set; }
    public int Headshots { get; set; }
    public int BestPlacement { get; set; }
    public float TotalDamage { get; set; }
    public float LongestSurvivalSeconds { get; set; }
    public int Experience { get; set; }
    public MilitaryRank Rank { get; set; } = MilitaryRank.Er;

    public double WinRate => Matches <= 0 ? 0 : (double)Wins / Matches;
    public double HeadshotRate => Kills <= 0 ? 0 : (double)Headshots / Kills;
}
