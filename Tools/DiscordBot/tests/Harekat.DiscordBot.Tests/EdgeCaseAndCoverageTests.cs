using FluentAssertions;
using Harekat.DiscordBot.Domain;
using Harekat.DiscordBot.Formatting;
using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Tests;

public class EdgeCaseAndCoverageTests
{
    [Fact]
    public void DataStore_find_squad_and_server_upsert()
    {
        var store = new InMemoryGameDataStore();
        store.SeedDemoData();
        store.FindSquadByName("bozkurtlar")!.LeaderName.Should().Be("KomutanYilmaz");
        store.FindSquadByName("yok").Should().BeNull();
        store.FindPlayerById("missing").Should().BeNull();

        store.UpsertServer(new GameServerStatus
        {
            ServerId = "eu-ist-01",
            Region = "İstanbul",
            Name = "Updated",
            PlayerCount = 1,
            MaxPlayers = 60,
            IsOnline = true,
            PingMs = 10
        });
        store.GetServers().Should().Contain(s => s.Name == "Updated");
    }

    [Fact]
    public void Season_contains_and_set()
    {
        var store = new InMemoryGameDataStore();
        var season = store.GetCurrentSeason();
        season.Contains(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        season.Contains(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeFalse();

        store.SetCurrentSeason(new SeasonInfo
        {
            SeasonId = "S2",
            Name = "Sezon 2",
            StartsAt = DateTimeOffset.UtcNow,
            EndsAt = DateTimeOffset.UtcNow.AddMonths(3),
            IsActive = true
        });
        store.GetCurrentSeason().SeasonId.Should().Be("S2");
    }

    [Fact]
    public async Task Recruitment_list_and_missing_join()
    {
        var sut = new SquadRecruitmentService(new InMemoryGameDataStore(), new PerformanceMetrics());
        await sut.CreateAsync(1, "A", "Tim", null);
        (await sut.ListOpenAsync()).Should().NotBeEmpty();
        var (ok, _, err) = await sut.JoinAsync("yok", 2);
        ok.Should().BeFalse();
        err.Should().Contain("bulunamadı");
        (await sut.JoinAsync(" ", 2)).Ok.Should().BeFalse();
    }

    [Fact]
    public void Tournament_duplicate_result_and_missing()
    {
        var sut = new TournamentService(new InMemoryGameDataStore(), new PerformanceMetrics());
        var t = sut.CreateBracket("K", ["A", "B"]);
        var m = t.Matches.Single(x => x.Round == 1);
        sut.SubmitResult(t.TournamentId, m.MatchId, "A").Ok.Should().BeTrue();
        sut.SubmitResult(t.TournamentId, m.MatchId, "A").Ok.Should().BeFalse();
        sut.SubmitResult("yok", "m1", "A").Ok.Should().BeFalse();
        sut.Get("yok").Should().BeNull();
        sut.List().Should().ContainSingle();
    }

    [Fact]
    public void Format_tournament_and_recruitment_and_metrics()
    {
        var store = new InMemoryGameDataStore();
        var tournament = new TournamentService(store, new PerformanceMetrics()).CreateBracket("Kupası", ["A", "B", "C", "D"]);
        EmbedTextFormatter.FormatTournamentBracket(tournament).Should().Contain("Kupası");

        var rec = new SquadRecruitment
        {
            RecruitmentId = "rec-1",
            CreatorDiscordId = 1,
            CreatorDisplayName = "L",
            SquadName = "T",
            Description = "not"
        };
        rec.MemberDiscordIds.Add(1);
        EmbedTextFormatter.FormatRecruitment(rec).Should().Contain("tim-ara");

        var metrics = new PerformanceMetrics();
        metrics.Record("x", TimeSpan.FromMilliseconds(1));
        EmbedTextFormatter.FormatMetrics(metrics.Snapshot()).Should().Contain("Bot Performans");
    }

    [Fact]
    public async Task MeasureAsync_records_success_and_failure()
    {
        var m = new PerformanceMetrics();
        var v = await m.MeasureAsync("ok", async () =>
        {
            await Task.Yield();
            return 7;
        });
        v.Should().Be(7);

        var act = async () => await m.MeasureAsync<int>("fail", async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("boom");
        });
        await act.Should().ThrowAsync<InvalidOperationException>();

        var snap = m.Snapshot();
        snap.TotalOperations.Should().Be(2);
        snap.ErrorCount.Should().Be(1);
    }

    [Fact]
    public void Announcement_format_defaults_map()
    {
        var sut = new MatchAnnouncementService(new InMemoryAnnouncementSink(), new PerformanceMetrics());
        var text = sut.FormatAnnouncement(new MatchResultAnnouncement
        {
            MatchId = "m",
            WinningSquadName = "S",
            WinningMembers = [],
            TeamCount = 2
        });
        text.Should().Contain("Kuzgun Vadisi");
    }

    [Fact]
    public async Task Weekly_publish_writes_announcement()
    {
        var store = new InMemoryGameDataStore();
        store.SeedDemoData();
        var sink = new InMemoryAnnouncementSink();
        var leaderboards = new LeaderboardService(store, new PerformanceMetrics());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<Hosting.WeeklySquadKillAnnouncementService>();
        var svc = new Hosting.WeeklySquadKillAnnouncementService(leaderboards, sink, logger);
        await svc.PublishAsync();
        sink.Messages.Should().ContainSingle(m => m.Content.Contains("Haftalık"));
    }

    [Fact]
    public void RankSync_non_promotion_message()
    {
        var sut = new RankSyncService(new RecordingRoleSyncGateway());
        var msg = sut.FormatPromotionMessage(new RankPromotionResult(
            "X", MilitaryRank.Er, MilitaryRank.Er, 10, false));
        msg.Should().Contain("değişmedi");
    }

    [Fact]
    public async Task Profile_promote_missing_player()
    {
        var sut = new ProfileService(new InMemoryGameDataStore(), new PerformanceMetrics());
        (await sut.ApplyXpAndPromoteAsync("yok", 10)).Should().BeNull();
    }
}
