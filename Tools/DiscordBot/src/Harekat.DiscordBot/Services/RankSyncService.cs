using Harekat.DiscordBot.Domain;

namespace Harekat.DiscordBot.Services;

/// <summary>
/// Discord rol senkronu için gereken işlemleri soyutlar (test edilebilir).
/// </summary>
public interface IDiscordRoleSyncGateway
{
    Task EnsureRankRolesExistAsync(ulong guildId, CancellationToken ct = default);
    Task ApplyPlayerRankRoleAsync(ulong guildId, ulong userId, MilitaryRank rank, CancellationToken ct = default);
}

/// <summary>Bot bağlı değilken no-op / kayıt tutan gateway.</summary>
public sealed class RecordingRoleSyncGateway : IDiscordRoleSyncGateway
{
    private readonly List<(ulong GuildId, ulong UserId, MilitaryRank Rank)> _applied = [];
    private readonly HashSet<ulong> _ensuredGuilds = [];

    public IReadOnlyList<(ulong GuildId, ulong UserId, MilitaryRank Rank)> Applied => _applied;
    public IReadOnlyCollection<ulong> EnsuredGuilds => _ensuredGuilds;

    public Task EnsureRankRolesExistAsync(ulong guildId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _ensuredGuilds.Add(guildId);
        return Task.CompletedTask;
    }

    public Task ApplyPlayerRankRoleAsync(ulong guildId, ulong userId, MilitaryRank rank, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _applied.Add((guildId, userId, rank));
        return Task.CompletedTask;
    }
}

public interface IRankSyncService
{
    Task SyncPlayerAsync(ulong guildId, ulong discordUserId, MilitaryRank rank, CancellationToken ct = default);
    Task EnsureRolesAsync(ulong guildId, CancellationToken ct = default);
    string FormatPromotionMessage(RankPromotionResult promotion);
}

public sealed class RankSyncService(IDiscordRoleSyncGateway gateway) : IRankSyncService
{
    public Task SyncPlayerAsync(ulong guildId, ulong discordUserId, MilitaryRank rank, CancellationToken ct = default) =>
        gateway.ApplyPlayerRankRoleAsync(guildId, discordUserId, rank, ct);

    public Task EnsureRolesAsync(ulong guildId, CancellationToken ct = default) =>
        gateway.EnsureRankRolesExistAsync(guildId, ct);

    public string FormatPromotionMessage(RankPromotionResult promotion)
    {
        ArgumentNullException.ThrowIfNull(promotion);
        if (!promotion.Promoted)
            return $"{promotion.DisplayName} rütbesi değişmedi ({RankCatalog.GetDisplayName(promotion.NewRank)}).";

        return $"🎖️ **Terfi!** {promotion.DisplayName}: " +
               $"{RankCatalog.GetDisplayName(promotion.PreviousRank)} → **{RankCatalog.GetDisplayName(promotion.NewRank)}** " +
               $"(XP: {promotion.Experience:N0})";
    }
}
