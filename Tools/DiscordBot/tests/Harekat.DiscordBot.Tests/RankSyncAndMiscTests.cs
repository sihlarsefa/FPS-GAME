using FluentAssertions;
using Harekat.DiscordBot.Configuration;
using Harekat.DiscordBot.Domain;
using Harekat.DiscordBot.Formatting;
using Harekat.DiscordBot.Hosting;
using Harekat.DiscordBot.Services;
using Harekat.DiscordBot.Webhooks;
using Microsoft.AspNetCore.Http;

namespace Harekat.DiscordBot.Tests;

public class RankSyncAndMiscTests
{
    [Fact]
    public async Task RankSync_applies_role_via_gateway()
    {
        var gateway = new RecordingRoleSyncGateway();
        var sut = new RankSyncService(gateway);
        await sut.EnsureRolesAsync(10);
        await sut.SyncPlayerAsync(10, 99, MilitaryRank.Binbasi);
        gateway.EnsuredGuilds.Should().Contain(10UL);
        gateway.Applied.Should().ContainSingle(x => x.UserId == 99 && x.Rank == MilitaryRank.Binbasi);
    }

    [Fact]
    public void Promotion_message_includes_ranks()
    {
        var sut = new RankSyncService(new RecordingRoleSyncGateway());
        var msg = sut.FormatPromotionMessage(new RankPromotionResult(
            "Ali", MilitaryRank.Er, MilitaryRank.Onbasi, 500, true));
        msg.Should().Contain("Terfi");
        msg.Should().Contain("Onbaşı");
    }

    [Fact]
    public void Webhook_auth_checks_header()
    {
        var opts = new DiscordBotOptions { WebhookApiKey = "secret" };
        var ctx = new DefaultHttpContext();
        WebhookEndpoints.IsAuthorized(ctx.Request, opts).Should().BeFalse();
        ctx.Request.Headers["X-Api-Key"] = "secret";
        WebhookEndpoints.IsAuthorized(ctx.Request, opts).Should().BeTrue();
    }

    [Fact]
    public void Token_prefers_environment_variable()
    {
        var previous = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("DISCORD_BOT_TOKEN", "env-token");
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
            var token = DiscordBotHostedService.ResolveToken(config, new DiscordBotOptions { Token = "file-token" });
            token.Should().Be("env-token");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DISCORD_BOT_TOKEN", previous);
        }
    }

    [Fact]
    public void Weekly_delay_is_positive()
    {
        var now = new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero); // Monday
        var delay = WeeklySquadKillAnnouncementService.DelayUntilNextMondayNoonUtc(now);
        delay.Should().BePositive();
        var target = now + delay;
        target.DayOfWeek.Should().Be(DayOfWeek.Monday);
        target.Hour.Should().Be(12);
    }

    [Fact]
    public void Formatters_handle_empty()
    {
        EmbedTextFormatter.FormatLeaderboard([], "T").Should().Contain("Henüz");
        EmbedTextFormatter.FormatWeeklySquadKills([]).Should().Contain("Henüz");
        EmbedTextFormatter.FormatSquadBoard([]).Should().Contain("yok");
        EmbedTextFormatter.FormatModerationLog([]).Should().Contain("Kayıt yok");
    }

    [Fact]
    public async Task Format_profile_and_servers()
    {
        var store = new InMemoryGameDataStore();
        store.SeedDemoData();
        var profile = store.FindPlayerByName("KeskinNisanci")!;
        EmbedTextFormatter.FormatProfile(profile).Should().Contain("KeskinNisanci");

        var servers = new ServerStatusService(store, new PerformanceMetrics());
        var summary = await servers.GetSummaryAsync();
        EmbedTextFormatter.FormatServers(summary).Should().Contain("İstanbul");
    }

    [Fact]
    public void Performance_metrics_record()
    {
        var m = new PerformanceMetrics();
        m.Record("a", TimeSpan.FromMilliseconds(5));
        m.Record("a", TimeSpan.FromMilliseconds(15), success: false);
        var snap = m.Snapshot();
        snap.TotalOperations.Should().Be(2);
        snap.ErrorCount.Should().Be(1);
        snap.Operations.Should().ContainSingle(o => o.Name == "a" && o.Count == 2);
    }

    [Fact]
    public async Task Squad_board_lists_open_slots()
    {
        var store = new InMemoryGameDataStore();
        store.SeedDemoData();
        var board = new SquadBoardService(store);
        var open = await board.GetLookingForMembersAsync();
        open.Should().OnlyContain(s => !s.IsFull);
        open.Should().Contain(s => s.Name == "Kartal Tim");
    }

    [Fact]
    public async Task Server_summary_counts_online()
    {
        var store = new InMemoryGameDataStore();
        store.SeedDemoData();
        var summary = await new ServerStatusService(store, new PerformanceMetrics()).GetSummaryAsync();
        summary.OnlineServers.Should().Be(2);
        summary.TotalServers.Should().Be(3);
        summary.ActivePlayers.Should().Be(70);
    }
}
