using Discord;
using Discord.Interactions;
using Harekat.DiscordBot.Formatting;
using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Modules;

[Group("turnuva", "Turnuva braketi ve sonuç yönetimi")]
[DefaultMemberPermissions(GuildPermission.ManageEvents)]
public sealed class TournamentModule(ITournamentService tournaments) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("olustur", "Takım listesinden braket oluşturur (virgülle ayır)")]
    public async Task OlusturAsync(
        [Summary("ad", "Turnuva adı")] string ad,
        [Summary("takimlar", "Takımlar, virgülle")] string takimlar)
    {
        var teams = takimlar.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        try
        {
            var t = tournaments.CreateBracket(ad, teams);
            await RespondAsync(EmbedTextFormatter.FormatTournamentBracket(t)).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            await RespondAsync(ex.Message, ephemeral: true).ConfigureAwait(false);
        }
    }

    [SlashCommand("goster", "Turnuva braketini gösterir")]
    public async Task GosterAsync(
        [Summary("id", "Turnuva kimliği")] string id)
    {
        var t = tournaments.Get(id);
        if (t is null)
        {
            await RespondAsync("Turnuva bulunamadı.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        await RespondAsync(EmbedTextFormatter.FormatTournamentBracket(t)).ConfigureAwait(false);
    }

    [SlashCommand("sonuc", "Maç sonucunu girer ve otomatik ilerletir")]
    public async Task SonucAsync(
        [Summary("turnuva_id", "Turnuva kimliği")] string turnuvaId,
        [Summary("mac_id", "Maç kimliği (örn. m1)")] string macId,
        [Summary("kazanan", "Kazanan takım adı")] string kazanan)
    {
        var result = tournaments.SubmitResult(turnuvaId, macId, kazanan);
        if (!result.Ok || result.Tournament is null)
        {
            await RespondAsync(result.Message, ephemeral: true).ConfigureAwait(false);
            return;
        }

        var body = $"{result.Message}\n\n{EmbedTextFormatter.FormatTournamentBracket(result.Tournament)}";
        await RespondAsync(body).ConfigureAwait(false);
    }

    [SlashCommand("liste", "Turnuvaları listeler")]
    public async Task ListeAsync()
    {
        var list = tournaments.List();
        if (list.Count == 0)
        {
            await RespondAsync("Kayıtlı turnuva yok.").ConfigureAwait(false);
            return;
        }

        var text = string.Join('\n', list.Select(t =>
            $"• `{t.TournamentId}` **{t.Name}** — {t.Status}" +
            (t.WinnerTeam is null ? "" : $" | Şampiyon: {t.WinnerTeam}")));
        await RespondAsync(text).ConfigureAwait(false);
    }
}
