using Discord.Interactions;
using Harekat.DiscordBot.Formatting;
using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Modules;

public sealed class CoreCommandsModule(
    ILeaderboardService leaderboards,
    IProfileService profiles,
    ISquadBoardService squads,
    IServerStatusService servers) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("siralama", "Genel oyuncu sıralamasını gösterir")]
    public async Task SiralamaAsync(
        [Summary("adet", "Kaç oyuncu listelensin (1-25)")] int adet = 10)
    {
        var entries = await leaderboards.GetOverallAsync(adet).ConfigureAwait(false);
        await RespondAsync(EmbedTextFormatter.FormatLeaderboard(entries, "HAREKÂT Sıralama")).ConfigureAwait(false);
    }

    [SlashCommand("profil", "Oyuncu rütbe ve istatistiklerini gösterir")]
    public async Task ProfilAsync(
        [Summary("isim", "Oyuncu adı")] string isim)
    {
        var profile = await profiles.GetByNameAsync(isim).ConfigureAwait(false);
        if (profile is null)
        {
            await RespondAsync($"Oyuncu bulunamadı: `{isim}`", ephemeral: true).ConfigureAwait(false);
            return;
        }

        await RespondAsync(EmbedTextFormatter.FormatProfile(profile)).ConfigureAwait(false);
    }

    [SlashCommand("tim", "Boş koltuğu olan tim ilanlarını listeler")]
    public async Task TimAsync()
    {
        var list = await squads.GetLookingForMembersAsync().ConfigureAwait(false);
        await RespondAsync(EmbedTextFormatter.FormatSquadBoard(list)).ConfigureAwait(false);
    }

    [SlashCommand("sunucu", "Dedicated sunucu durumunu gösterir")]
    public async Task SunucuAsync()
    {
        var summary = await servers.GetSummaryAsync().ConfigureAwait(false);
        await RespondAsync(EmbedTextFormatter.FormatServers(summary)).ConfigureAwait(false);
    }
}
