using Discord;
using Discord.Interactions;
using Harekat.DiscordBot.Formatting;
using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Modules;

[Group("tim-ara", "Discord üzerinden 10 kişilik tim toplama")]
public sealed class TimAraModule(ISquadRecruitmentService recruitment) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("olustur", "Yeni 10 kişilik tim ilanı açar")]
    public async Task OlusturAsync(
        [Summary("tim_adi", "Tim adı")] string timAdi,
        [Summary("aciklama", "Kısa açıklama")] string? aciklama = null)
    {
        var user = Context.User;
        var created = await recruitment.CreateAsync(user.Id, user.Username, timAdi, aciklama).ConfigureAwait(false);
        await RespondAsync(EmbedTextFormatter.FormatRecruitment(created)).ConfigureAwait(false);
    }

    [SlashCommand("katil", "Açık bir tim ilanına katılır")]
    public async Task KatilAsync(
        [Summary("id", "İlan kimliği")] string id)
    {
        var (ok, updated, error) = await recruitment.JoinAsync(id, Context.User.Id).ConfigureAwait(false);
        if (!ok || updated is null)
        {
            await RespondAsync(error ?? "Katılım başarısız.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var msg = EmbedTextFormatter.FormatRecruitment(updated);
        if (updated.IsFull)
            msg += "\n✅ Tim tamamlandı (10/10)! Lobide buluşun.";

        await RespondAsync(msg).ConfigureAwait(false);
    }

    [SlashCommand("liste", "Açık tim toplama ilanlarını listeler")]
    public async Task ListeAsync()
    {
        var open = await recruitment.ListOpenAsync().ConfigureAwait(false);
        if (open.Count == 0)
        {
            await RespondAsync("Açık tim ilanı yok. `/tim-ara olustur` ile başlat.").ConfigureAwait(false);
            return;
        }

        var text = string.Join("\n\n", open.Select(EmbedTextFormatter.FormatRecruitment));
        await RespondAsync(text).ConfigureAwait(false);
    }
}
