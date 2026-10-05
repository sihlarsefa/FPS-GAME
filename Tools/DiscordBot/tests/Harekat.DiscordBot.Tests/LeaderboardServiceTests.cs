using FluentAssertions;
using Harekat.DiscordBot.Domain;
using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Tests;

public class LeaderboardServiceTests
{
    private readonly InMemoryGameDataStore _store = new();
    private readonly LeaderboardService _sut;

    public LeaderboardServiceTests()
    {
        _store.SeedDemoData();
        _sut = new LeaderboardService(_store, new PerformanceMetrics());
    }

    [Fact]
    public async Task Overall_orders_by_experience()
    {
        var list = await _sut.GetOverallAsync(3);
        list.Should().HaveCount(3);
        list[0].DisplayName.Should().Be("KomutanYilmaz");
        list[0].Rank.Should().Be(1);
        list.Select(e => e.Score).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Season_orders_by_season_xp()
    {
        var list = await _sut.GetSeasonAsync(2);
        list[0].DisplayName.Should().Be("KeskinNisanci");
        list[0].Score.Should().Be(15_800);
    }

    [Fact]
    public async Task Weekly_squad_kills_orders_correctly()
    {
        var list = await _sut.GetWeeklyTopKillerSquadsAsync(3);
        list[0].SquadName.Should().Be("Bozkurtlar");
        list[0].WeeklyKills.Should().Be(120);
    }

    [Fact]
    public async Task Top_is_clamped()
    {
        var list = await _sut.GetOverallAsync(100);
        list.Count.Should().BeLessThanOrEqualTo(25);
        (await _sut.GetOverallAsync(0)).Count.Should().Be(1);
    }

    [Fact]
    public async Task Current_season_is_active()
    {
        var season = await _sut.GetCurrentSeasonAsync();
        season.IsActive.Should().BeTrue();
        season.SeasonId.Should().Be("S1");
    }
}
