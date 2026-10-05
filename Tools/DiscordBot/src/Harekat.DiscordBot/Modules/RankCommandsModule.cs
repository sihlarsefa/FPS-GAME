using Discord;
using Discord.Interactions;
using Harekat.DiscordBot.Configuration;
using Harekat.DiscordBot.Domain;
using Harekat.DiscordBot.Formatting;
using Harekat.DiscordBot.Services;
using Microsoft.Extensions.Options;

namespace Harekat.DiscordBot.Modules;

public sealed class RankCommandsModule(
    IProfileService profiles,
    IRankSyncService rankSync,
    IAnnouncementSink announcements,
    IOptions<DiscordBotOptions> options,
    PerformanceMetrics metrics) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("rutbe-senkron", "Oyuncu rütbesini Discord rolüne yansıtır")]
    public async Task RutbeSenkronAsync(
        [Summary("isim", "Oyuncu adı")] string isim)
    {
        var profile = await profiles.GetByNameAsync(isim).ConfigureAwait(false);
        if (profile is null)
        {
            await RespondAsync("Oyuncu bulunamadı.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var guildId = Context.Guild?.Id ?? options.Value.GuildId;
        if (guildId is null or 0 || profile.DiscordUserId is null)
        {
            await RespondAsync(
                "Guild veya oyuncu Discord bağlantısı eksik. Önce `/hesap-bagla` kullanın.",
                ephemeral: true).ConfigureAwait(false);
            return;
        }

        await rankSync.EnsureRolesAsync(guildId.Value).ConfigureAwait(false);
        await rankSync.SyncPlayerAsync(guildId.Value, profile.DiscordUserId.Value, profile.Stats.Rank)
            .ConfigureAwait(false);

        await RespondAsync(
                $"{profile.DisplayName} → rol: **{RankCatalog.GetDiscordRoleName(profile.Stats.Rank)}**")
            .ConfigureAwait(false);
    }

    [SlashCommand("hesap-bagla", "Oyun profilini Discord hesabına bağlar")]
    public async Task HesapBaglaAsync(
        [Summary("isim", "Oyuncu adı")] string isim)
    {
        try
        {
            await profiles.LinkDiscordAsync(isim, Context.User.Id).ConfigureAwait(false);
            await RespondAsync($"`{isim}` hesabı {Context.User.Mention} ile bağlandı.", ephemeral: true)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync(ex.Message, ephemeral: true).ConfigureAwait(false);
        }
    }

    [SlashCommand("bot-metrik", "Bot performans özetini gösterir")]
    public async Task BotMetrikAsync()
    {
        await RespondAsync(EmbedTextFormatter.FormatMetrics(metrics.Snapshot()), ephemeral: true)
            .ConfigureAwait(false);
    }

    /// <summary>Webhook/test için terfi duyurusu yardımcısı (slash).</summary>
    [SlashCommand("terfi-simule", "XP ekleyip terfi bildirimi simüle eder (yönetici)")]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    public async Task TerfiSimuleAsync(
        [Summary("isim", "Oyuncu")] string isim,
        [Summary("xp", "Eklenecek XP")] int xp)
    {
        var profile = await profiles.GetByNameAsync(isim).ConfigureAwait(false);
        if (profile is null)
        {
            await RespondAsync("Oyuncu bulunamadı.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var result = await profiles.ApplyXpAndPromoteAsync(profile.PlayerId, xp).ConfigureAwait(false);
        if (result is null)
        {
            await RespondAsync("İşlem başarısız.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var msg = rankSync.FormatPromotionMessage(result);
        if (result.Promoted)
            await announcements.PublishAsync("rank-promo", msg).ConfigureAwait(false);

        if (result.Promoted &&
            Context.Guild is not null &&
            profile.DiscordUserId is ulong discordId)
        {
            await rankSync.SyncPlayerAsync(Context.Guild.Id, discordId, result.NewRank).ConfigureAwait(false);
        }

        await RespondAsync(msg).ConfigureAwait(false);
    }
}
