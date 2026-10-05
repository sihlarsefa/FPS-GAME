using Harekat.DiscordBot.Domain;

namespace Harekat.DiscordBot.Services;

public interface IProfileService
{
    Task<PlayerProfile?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<PlayerProfile?> GetByDiscordIdAsync(ulong discordUserId, CancellationToken ct = default);
    Task<RankPromotionResult?> ApplyXpAndPromoteAsync(string playerId, int xpGain, CancellationToken ct = default);
    Task LinkDiscordAsync(string playerName, ulong discordUserId, CancellationToken ct = default);
}

public sealed record RankPromotionResult(
    string DisplayName,
    MilitaryRank PreviousRank,
    MilitaryRank NewRank,
    int Experience,
    bool Promoted);

public sealed class ProfileService(InMemoryGameDataStore store, PerformanceMetrics metrics) : IProfileService
{
    public Task<PlayerProfile?> GetByNameAsync(string name, CancellationToken ct = default) =>
        metrics.MeasureAsync("profile.get_by_name", () =>
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(name))
                return Task.FromResult<PlayerProfile?>(null);
            return Task.FromResult(store.FindPlayerByName(name.Trim()));
        });

    public Task<PlayerProfile?> GetByDiscordIdAsync(ulong discordUserId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(store.FindPlayerByDiscordId(discordUserId));
    }

    public Task LinkDiscordAsync(string playerName, ulong discordUserId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var player = store.FindPlayerByName(playerName)
            ?? throw new InvalidOperationException($"Oyuncu bulunamadı: {playerName}");
        player.DiscordUserId = discordUserId;
        store.UpsertPlayer(player);
        return Task.CompletedTask;
    }

    public Task<RankPromotionResult?> ApplyXpAndPromoteAsync(string playerId, int xpGain, CancellationToken ct = default) =>
        metrics.MeasureAsync("profile.promote", () =>
        {
            ct.ThrowIfCancellationRequested();
            if (xpGain < 0)
                throw new ArgumentOutOfRangeException(nameof(xpGain), "XP kazancı negatif olamaz.");

            var player = store.FindPlayerById(playerId);
            if (player is null)
                return Task.FromResult<RankPromotionResult?>(null);

            var previous = player.Stats.Rank;
            player.Stats.Experience += xpGain;
            player.SeasonXp += xpGain;
            var next = RankCatalog.RankForXp(player.Stats.Experience);
            player.Stats.Rank = next;
            store.UpsertPlayer(player);

            return Task.FromResult<RankPromotionResult?>(new RankPromotionResult(
                player.DisplayName,
                previous,
                next,
                player.Stats.Experience,
                next > previous));
        });
}
