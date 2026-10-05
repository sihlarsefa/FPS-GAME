using FluentAssertions;
using Harekat.DiscordBot.Domain;
using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Tests;

public class MatchAnnouncementServiceTests
{
    [Fact]
    public async Task Announce_publishes_formatted_message()
    {
        var sink = new InMemoryAnnouncementSink();
        var sut = new MatchAnnouncementService(sink, new PerformanceMetrics());
        var announcement = new MatchResultAnnouncement
        {
            MatchId = "m-1",
            WinningSquadName = "Bozkurtlar",
            WinningMembers = ["A", "B"],
            WinningTeamKills = 33,
            TeamCount = 6,
            MapName = "Kuzgun Vadisi"
        };

        await sut.AnnounceWinnerAsync(announcement);

        sink.Messages.Should().ContainSingle();
        sink.Messages[0].ChannelKey.Should().Be(MatchAnnouncementService.ChannelKey);
        sink.Messages[0].Content.Should().Contain("Bozkurtlar");
        sink.Messages[0].Content.Should().Contain("33");
    }

    [Fact]
    public async Task Announce_requires_squad_name()
    {
        var sut = new MatchAnnouncementService(new InMemoryAnnouncementSink(), new PerformanceMetrics());
        var bad = new MatchResultAnnouncement
        {
            MatchId = "x",
            WinningSquadName = " ",
            WinningMembers = [],
            TeamCount = 1
        };
        var act = async () => await sut.AnnounceWinnerAsync(bad);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
