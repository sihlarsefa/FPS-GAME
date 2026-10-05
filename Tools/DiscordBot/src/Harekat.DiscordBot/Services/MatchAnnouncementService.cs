using Harekat.DiscordBot.Domain;

namespace Harekat.DiscordBot.Services;

public interface IAnnouncementSink
{
    Task PublishAsync(string channelKey, string content, CancellationToken ct = default);
}

public sealed class InMemoryAnnouncementSink : IAnnouncementSink
{
    private readonly List<(string ChannelKey, string Content, DateTimeOffset At)> _messages = [];

    public IReadOnlyList<(string ChannelKey, string Content, DateTimeOffset At)> Messages => _messages;

    public Task PublishAsync(string channelKey, string content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(channelKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        _messages.Add((channelKey, content, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }
}

public interface IMatchAnnouncementService
{
    Task AnnounceWinnerAsync(MatchResultAnnouncement announcement, CancellationToken ct = default);
    string FormatAnnouncement(MatchResultAnnouncement announcement);
}

public sealed class MatchAnnouncementService(
    IAnnouncementSink sink,
    PerformanceMetrics metrics) : IMatchAnnouncementService
{
    public const string ChannelKey = "announcements";

    public string FormatAnnouncement(MatchResultAnnouncement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        var members = announcement.WinningMembers.Count == 0
            ? "—"
            : string.Join(", ", announcement.WinningMembers);
        var map = string.IsNullOrWhiteSpace(announcement.MapName) ? "Kuzgun Vadisi" : announcement.MapName;

        return $"🏆 **Maç Sonuçlandı!** `{announcement.MatchId}`\n" +
               $"Kazanan tim: **{announcement.WinningSquadName}**\n" +
               $"Üyeler: {members}\n" +
               $"Takım öldürme: **{announcement.WinningTeamKills}** | Tim sayısı: {announcement.TeamCount}\n" +
               $"Harita: {map} | {announcement.EndedAt:u}";
    }

    public Task AnnounceWinnerAsync(MatchResultAnnouncement announcement, CancellationToken ct = default) =>
        metrics.MeasureAsync("announce.match_winner", async () =>
        {
            ArgumentNullException.ThrowIfNull(announcement);
            if (string.IsNullOrWhiteSpace(announcement.WinningSquadName))
                throw new ArgumentException("Kazanan tim adı zorunlu.", nameof(announcement));

            var text = FormatAnnouncement(announcement);
            await sink.PublishAsync(ChannelKey, text, ct).ConfigureAwait(false);
        });
}
