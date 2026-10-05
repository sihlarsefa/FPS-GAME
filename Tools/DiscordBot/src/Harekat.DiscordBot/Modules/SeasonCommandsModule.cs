using Discord.Interactions;
using Harekat.DiscordBot.Formatting;
using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Modules;

public sealed class SeasonCommandsModule(ILeaderboardService leaderboards) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("sezon", "Aktif sezon sıralamasını gösterir")]
    public async Task SezonAsync(
        [Summary("adet", "Kaç oyuncu (1-25)")] int adet = 10)
    {
        var season = await leaderboards.GetCurrentSeasonAsync().ConfigureAwait(false);
        var entries = await leaderboards.GetSeasonAsync(adet).ConfigureAwait(false);
        var title = $"{season.Name} Sıralaması";
        await RespondAsync(EmbedTextFormatter.FormatLeaderboard(entries, title)).ConfigureAwait(false);
    }

    [SlashCommand("haftalik-tim", "Haftalık en çok öldüren timleri gösterir")]
    public async Task HaftalikTimAsync(
        [Summary("adet", "Kaç tim (1-25)")] int adet = 5)
    {
        var entries = await leaderboards.GetWeeklyTopKillerSquadsAsync(adet).ConfigureAwait(false);
        await RespondAsync(EmbedTextFormatter.FormatWeeklySquadKills(entries)).ConfigureAwait(false);
    }
}
