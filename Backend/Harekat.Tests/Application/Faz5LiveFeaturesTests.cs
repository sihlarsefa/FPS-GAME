using FluentAssertions;
using Harekat.Application.Dtos;
using Harekat.Application.Services;
using Harekat.Domain.Entities;
using Harekat.Infrastructure.Repositories;

namespace Harekat.Tests.Application;

public class Faz5LiveFeaturesTests
{
    [Fact]
    public async Task AchievementSync_MergesProgress_AndUnlocks()
    {
        var store = new InMemoryStore();
        var players = new MemoryPlayerRepository(store);
        var uow = new MemoryUnitOfWork(store);
        var player = new Player { Username = "SyncAsker", Email = "sync@t.com", PasswordHash = "x" };
        player.Stats.Kills = 1;
        player.Stats.Matches = 1;
        store.Players[player.Id] = player;

        var ach = new AchievementService(players, uow);
        var result = await ach.SyncAsync(player.Id, new AchievementSyncRequest(
            Progress: new Dictionary<string, int> { ["kills"] = 25, ["damage"] = 12_000 }));

        result.NewlyUnlocked.Should().Contain("first_blood");
        result.NewlyUnlocked.Should().Contain("killer_25");
        result.NewlyUnlocked.Should().Contain("damage_10k");
        result.Achievements.Should().Contain(a => a.Id == "killer_25" && a.Unlocked);

        // İstemci sunucu kills'ini düşüremez (floor=25 zaten sync sonrası).
        var again = await ach.SyncAsync(player.Id, new AchievementSyncRequest(
            Progress: new Dictionary<string, int> { ["kills"] = 1 }));
        again.Achievements.First(a => a.Id == "killer_25").Progress.Should().BeGreaterThanOrEqualTo(25);
    }

    [Fact]
    public async Task Leaderboard_FiltersByMapAndModeProgress()
    {
        var store = new InMemoryStore();
        var players = new MemoryPlayerRepository(store);
        var seasons = new MemorySeasonRepository(store);
        var lb = new LeaderboardService(players, seasons);

        var a = new Player { Username = "KuzgunKing", Email = "a@t.com", PasswordHash = "x" };
        a.Stats.Experience = 100;
        a.AchievementProgress["kills_kuzgun"] = 40;
        a.AchievementProgress["wins_br"] = 3;
        store.Players[a.Id] = a;

        var b = new Player { Username = "AyazOnly", Email = "b@t.com", PasswordHash = "x" };
        b.Stats.Experience = 200;
        b.AchievementProgress["kills_ayaz"] = 99;
        b.AchievementProgress["wins_skirmish"] = 5;
        store.Players[b.Id] = b;

        var mapRows = await lb.GetAsync("kills", 10, mapId: "kuzgun");
        mapRows.Should().ContainSingle(r => r.Username == "KuzgunKing");
        mapRows.Should().NotContain(r => r.Username == "AyazOnly");
        mapRows[0].Value.Should().Be(40);

        var modeRows = await lb.GetAsync("wins", 10, mode: "skirmish");
        modeRows.Should().ContainSingle(r => r.Username == "AyazOnly");
        modeRows[0].Value.Should().Be(5);
    }

    [Fact]
    public void AntiCheatRules_DetectJng90Burst_Wallbang_KirpiSpeed()
    {
        AntiCheatRules.IsSuspiciousJng90Headshot("sr_jng90", 650f, 3).Should().BeTrue();
        AntiCheatRules.IsSuspiciousJng90Headshot("sr_jng90", 500f, 3).Should().BeFalse();
        AntiCheatRules.IsSuspiciousJng90Headshot("ar_mpt76", 700f, 5).Should().BeFalse();

        AntiCheatRules.IsInvalidWallbang("7.62", "wood", 0.2f).Should().BeFalse();
        AntiCheatRules.IsInvalidWallbang("7.62", "wood", 0.5f).Should().BeTrue();
        AntiCheatRules.IsInvalidWallbang("7.62", "concrete", 0.1f).Should().BeTrue();

        AntiCheatRules.IsKirpiOverspeed(90f).Should().BeFalse();
        AntiCheatRules.IsKirpiOverspeed(100f).Should().BeTrue();

        AntiCheatRules.Catalog.Should().HaveCountGreaterThanOrEqualTo(3);
        AntiCheatRules.Catalog.Select(r => r.Id).Should().Contain(new[]
        {
            "jng90_hs_600m", "wallbang_invalid", "kirpi_overspeed"
        });
    }
}
