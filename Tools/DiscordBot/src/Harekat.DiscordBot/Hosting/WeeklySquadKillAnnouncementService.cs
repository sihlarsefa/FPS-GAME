using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Hosting;

/// <summary>
/// Haftalık "En Çok Öldüren Tim" duyurusunu planlı yayınlar (Pazartesi 12:00 UTC).
/// </summary>
public sealed class WeeklySquadKillAnnouncementService(
    ILeaderboardService leaderboards,
    IAnnouncementSink announcements,
    ILogger<WeeklySquadKillAnnouncementService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Haftalık tim öldürme duyuru servisi aktif.");
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNextMondayNoonUtc(DateTimeOffset.UtcNow);
            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                await PublishAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Haftalık duyuru başarısız.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    public async Task PublishAsync(CancellationToken ct = default)
    {
        var season = await leaderboards.GetCurrentSeasonAsync(ct).ConfigureAwait(false);
        var top = await leaderboards.GetWeeklyTopKillerSquadsAsync(5, ct).ConfigureAwait(false);
        var lines = top.Select(e => $"#{e.Rank} **{e.SquadName}** — {e.WeeklyKills} öldürme");
        var text =
            $"📊 **Haftalık En Çok Öldüren Tim** ({season.Name})\n" +
            string.Join('\n', lines);
        await announcements.PublishAsync(MatchAnnouncementService.ChannelKey, text, ct).ConfigureAwait(false);
        logger.LogInformation("Haftalık tim öldürme duyurusu yayınlandı.");
    }

    internal static TimeSpan DelayUntilNextMondayNoonUtc(DateTimeOffset now)
    {
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, 12, 0, 0, TimeSpan.Zero)
            .AddDays(daysUntilMonday);
        if (candidate <= now)
            candidate = candidate.AddDays(7);
        return candidate - now;
    }
}
