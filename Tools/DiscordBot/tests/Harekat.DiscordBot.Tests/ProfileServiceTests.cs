using FluentAssertions;
using Harekat.DiscordBot.Domain;
using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Tests;

public class ProfileServiceTests
{
    private readonly InMemoryGameDataStore _store = new();
    private readonly ProfileService _sut;

    public ProfileServiceTests()
    {
        _store.SeedDemoData();
        _sut = new ProfileService(_store, new PerformanceMetrics());
    }

    [Fact]
    public async Task GetByName_is_case_insensitive()
    {
        var p = await _sut.GetByNameAsync("komutanyilmaz");
        p.Should().NotBeNull();
        p!.Stats.Rank.Should().Be(MilitaryRank.Yuzbasi);
    }

    [Fact]
    public async Task GetByName_returns_null_for_missing()
    {
        (await _sut.GetByNameAsync("yok")).Should().BeNull();
        (await _sut.GetByNameAsync(" ")).Should().BeNull();
    }

    [Fact]
    public async Task ApplyXp_promotes_when_threshold_crossed()
    {
        var newbie = _store.FindPlayerByName("AcemiEr")!;
        var result = await _sut.ApplyXpAndPromoteAsync(newbie.PlayerId, 500);
        result.Should().NotBeNull();
        result!.Promoted.Should().BeTrue();
        result.PreviousRank.Should().Be(MilitaryRank.Er);
        result.NewRank.Should().Be(MilitaryRank.Onbasi);
    }

    [Fact]
    public async Task ApplyXp_rejects_negative()
    {
        var act = async () => await _sut.ApplyXpAndPromoteAsync("p-yeni", -1);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task LinkDiscord_sets_user_id()
    {
        await _sut.LinkDiscordAsync("AcemiEr", 42);
        var p = await _sut.GetByDiscordIdAsync(42);
        p!.DisplayName.Should().Be("AcemiEr");
    }

    [Fact]
    public async Task CareerStats_rates_handle_zero()
    {
        var stats = new CareerStats();
        stats.WinRate.Should().Be(0);
        stats.HeadshotRate.Should().Be(0);
        stats.Matches = 10;
        stats.Wins = 5;
        stats.Kills = 10;
        stats.Headshots = 4;
        stats.WinRate.Should().BeApproximately(0.5, 0.001);
        stats.HeadshotRate.Should().BeApproximately(0.4, 0.001);
    }
}
