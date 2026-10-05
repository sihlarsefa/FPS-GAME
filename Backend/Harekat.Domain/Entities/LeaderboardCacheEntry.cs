namespace Harekat.Domain.Entities;

/// <summary>
/// XP / sezon sıralaması için denormalize önbellek tablosu.
/// Indexed view alternatifi: maç sonucu sonrası güncellenir, leaderboard sorgularını hızlandırır.
/// </summary>
public sealed class LeaderboardCacheEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    /// <summary>experience | kills | wins | elo | season_xp</summary>
    public string Metric { get; set; } = "experience";
    public int SeasonNumber { get; set; }
    public long Score { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
