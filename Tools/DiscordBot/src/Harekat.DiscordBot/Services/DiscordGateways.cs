using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Harekat.DiscordBot.Domain;
using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Services;

/// <summary>Gerçek Discord API üzerinden rütbe rol senkronu.</summary>
public sealed class DiscordRoleSyncGateway(DiscordSocketClient client) : IDiscordRoleSyncGateway
{
    public async Task EnsureRankRolesExistAsync(ulong guildId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return;

        foreach (var rank in RankCatalog.AllRanks)
        {
            var roleName = RankCatalog.GetDiscordRoleName(rank);
            if (guild.Roles.Any(r => string.Equals(r.Name, roleName, StringComparison.Ordinal)))
                continue;

            await guild.CreateRoleAsync(
                roleName,
                GuildPermissions.None,
                color: Color.DarkGreen,
                isMentionable: false,
                options: new RequestOptions { CancelToken = ct }).ConfigureAwait(false);
        }
    }

    public async Task ApplyPlayerRankRoleAsync(ulong guildId, ulong userId, MilitaryRank rank, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var guild = client.GetGuild(guildId);
        var user = guild?.GetUser(userId);
        if (guild is null || user is null)
            return;

        var targetRoleName = RankCatalog.GetDiscordRoleName(rank);
        var rankRoleNames = RankCatalog.AllRanks
            .Select(RankCatalog.GetDiscordRoleName)
            .ToHashSet(StringComparer.Ordinal);

        var toRemove = user.Roles
            .Where(r => rankRoleNames.Contains(r.Name) && !string.Equals(r.Name, targetRoleName, StringComparison.Ordinal))
            .ToArray();

        if (toRemove.Length > 0)
            await user.RemoveRolesAsync(toRemove, new RequestOptions { CancelToken = ct }).ConfigureAwait(false);

        var target = guild.Roles.FirstOrDefault(r => string.Equals(r.Name, targetRoleName, StringComparison.Ordinal));
        if (target is null)
        {
            var created = await guild.CreateRoleAsync(
                targetRoleName,
                GuildPermissions.None,
                color: Color.DarkGreen,
                isMentionable: false,
                options: new RequestOptions { CancelToken = ct }).ConfigureAwait(false);
            target = guild.GetRole(created.Id) ?? guild.Roles.First(r => r.Id == created.Id);
        }

        if (!user.Roles.Any(r => r.Id == target.Id))
            await user.AddRoleAsync(target, new RequestOptions { CancelToken = ct }).ConfigureAwait(false);
    }
}

/// <summary>Duyuruları Discord kanalına iletir; kanal yoksa belleğe yazar.</summary>
public sealed class DiscordAnnouncementSink(
    DiscordSocketClient client,
    InMemoryAnnouncementSink fallback,
    Microsoft.Extensions.Options.IOptions<Configuration.DiscordBotOptions> options) : IAnnouncementSink
{
    public async Task PublishAsync(string channelKey, string content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var opts = options.Value;
        ulong? channelId = channelKey switch
        {
            MatchAnnouncementService.ChannelKey => opts.AnnouncementChannelId,
            "logs" => opts.LogChannelId,
            "rank-promo" => opts.RankPromoChannelId ?? opts.AnnouncementChannelId,
            _ => opts.AnnouncementChannelId
        };

        if (channelId is null or 0 || client.ConnectionState != ConnectionState.Connected)
        {
            await fallback.PublishAsync(channelKey, content, ct).ConfigureAwait(false);
            return;
        }

        if (client.GetChannel(channelId.Value) is not IMessageChannel channel)
        {
            await fallback.PublishAsync(channelKey, content, ct).ConfigureAwait(false);
            return;
        }

        await channel.SendMessageAsync(content, options: new RequestOptions { CancelToken = ct }).ConfigureAwait(false);
    }
}
