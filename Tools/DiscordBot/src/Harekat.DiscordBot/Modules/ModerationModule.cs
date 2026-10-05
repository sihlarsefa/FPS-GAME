using Discord;
using Discord.Interactions;
using Harekat.DiscordBot.Configuration;
using Harekat.DiscordBot.Domain;
using Harekat.DiscordBot.Formatting;
using Harekat.DiscordBot.Services;
using Microsoft.Extensions.Options;

namespace Harekat.DiscordBot.Modules;

[Group("mod", "Moderasyon komutları")]
[DefaultMemberPermissions(GuildPermission.ModerateMembers)]
public sealed class ModerationModule(
    IModerationService moderation,
    IAnnouncementSink announcements,
    IOptions<DiscordBotOptions> options) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("uyari", "Oyuncuya uyarı kaydı ekler")]
    public async Task UyariAsync(
        [Summary("kullanici", "Hedef kullanıcı")] IUser kullanici,
        [Summary("sebep", "Sebep")] string sebep)
    {
        var entry = await moderation.LogActionAsync(
            ModerationActionType.Warn,
            kullanici.Id,
            kullanici.Username,
            Context.User.Id,
            Context.User.Username,
            sebep).ConfigureAwait(false);

        await PublishLogAsync(entry).ConfigureAwait(false);
        await RespondAsync($"Uyarı kaydedildi: {kullanici.Mention} — {sebep}", ephemeral: true).ConfigureAwait(false);
    }

    [SlashCommand("sustur", "Kullanıcıyı süreyle susturur (log)")]
    public async Task SusturAsync(
        [Summary("kullanici", "Hedef")] IUser kullanici,
        [Summary("dakika", "Süre (dakika)")] int dakika,
        [Summary("sebep", "Sebep")] string sebep)
    {
        if (dakika <= 0 || dakika > 60 * 24 * 28)
        {
            await RespondAsync("Süre 1 dakika ile 28 gün arasında olmalı.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        if (Context.Guild?.GetUser(kullanici.Id) is { } member)
        {
            await member.SetTimeOutAsync(TimeSpan.FromMinutes(dakika)).ConfigureAwait(false);
        }

        var entry = await moderation.LogActionAsync(
            ModerationActionType.Mute,
            kullanici.Id,
            kullanici.Username,
            Context.User.Id,
            Context.User.Username,
            sebep,
            TimeSpan.FromMinutes(dakika)).ConfigureAwait(false);

        await PublishLogAsync(entry).ConfigureAwait(false);
        await RespondAsync($"{kullanici.Mention} {dakika} dk susturuldu. Sebep: {sebep}", ephemeral: true)
            .ConfigureAwait(false);
    }

    [SlashCommand("ban", "Kullanıcıyı yasaklar (log)")]
    public async Task BanAsync(
        [Summary("kullanici", "Hedef")] IUser kullanici,
        [Summary("sebep", "Sebep")] string sebep)
    {
        if (Context.Guild is not null)
            await Context.Guild.AddBanAsync(kullanici, 0, sebep).ConfigureAwait(false);

        var entry = await moderation.LogActionAsync(
            ModerationActionType.Ban,
            kullanici.Id,
            kullanici.Username,
            Context.User.Id,
            Context.User.Username,
            sebep).ConfigureAwait(false);

        await PublishLogAsync(entry).ConfigureAwait(false);
        await RespondAsync($"{kullanici.Username} yasaklandı. Sebep: {sebep}", ephemeral: true).ConfigureAwait(false);
    }

    [SlashCommand("log", "Son moderasyon kayıtlarını gösterir")]
    public async Task LogAsync(
        [Summary("adet", "Kayıt sayısı")] int adet = 15)
    {
        var entries = await moderation.GetRecentAsync(adet).ConfigureAwait(false);
        await RespondAsync(EmbedTextFormatter.FormatModerationLog(entries), ephemeral: true).ConfigureAwait(false);
    }

    private async Task PublishLogAsync(ModerationLogEntry entry)
    {
        var text =
            $"🛡️ **[{entry.Action}]** {entry.TargetDisplayName} ({entry.TargetUserId})\n" +
            $"Sebep: {entry.Reason}\n" +
            $"Mod: {entry.ModeratorDisplayName}";
        await announcements.PublishAsync("logs", text).ConfigureAwait(false);

        // LogChannelId tanımlıysa DiscordAnnouncementSink zaten oraya yazar; options doğrulama için okunur.
        _ = options.Value.LogChannelId;
    }
}
