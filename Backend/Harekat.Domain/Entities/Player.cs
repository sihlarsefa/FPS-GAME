using Harekat.Domain.Catalogs;
using Harekat.Domain.Enums;
using Harekat.Domain.ValueObjects;

namespace Harekat.Domain.Entities;

public sealed class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public CareerStats Stats { get; set; } = new();
    public int EloRating { get; set; } = 1000;
    public Guid? SquadId { get; set; }
    public bool EmailVerified { get; set; }
    public string? EmailVerificationToken { get; set; }
    public string? RefreshTokenHash { get; set; }
    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }
    public bool IsBanned { get; set; }
    public DateTimeOffset? BanExpiresAt { get; set; }
    public string? BanReason { get; set; }
    public bool IsMuted { get; set; }
    public DateTimeOffset? MuteExpiresAt { get; set; }
    public string Role { get; set; } = "Player";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsOnline { get; set; }
    public string Region { get; set; } = "tr";
    public List<string> OwnedCosmetics { get; set; } = ["camo_standard", "beret_green"];
    public string EquippedCamo { get; set; } = "camo_standard";
    public string EquippedBeret { get; set; } = "beret_green";
    public Dictionary<string, int> AchievementProgress { get; set; } = new();
    public HashSet<string> UnlockedAchievements { get; set; } = [];
    public int SeasonXp { get; set; }

    public MilitaryRank Rank => Stats.Rank;
    public MilitaryRank SeasonRank => RankCatalog.RankForXp(SeasonXp);

    public bool IsCurrentlyBanned =>
        IsBanned && (BanExpiresAt is null || BanExpiresAt > DateTimeOffset.UtcNow);

    public bool IsCurrentlyMuted =>
        IsMuted && (MuteExpiresAt is null || MuteExpiresAt > DateTimeOffset.UtcNow);
}
