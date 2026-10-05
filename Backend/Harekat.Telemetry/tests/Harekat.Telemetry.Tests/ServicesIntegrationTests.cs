using Harekat.Telemetry.Application.Contracts;
using Harekat.Telemetry.Application.Services;
using Harekat.Telemetry.Domain.Catalogs;
using Harekat.Telemetry.Domain.Entities;
using Harekat.Telemetry.Domain.Enums;
using Harekat.Telemetry.Domain.ValueObjects;
using Harekat.Telemetry.Infrastructure.Heatmap;
using Harekat.Telemetry.Infrastructure.Metrics;
using Harekat.Telemetry.Infrastructure.Replay;
using Harekat.Telemetry.Infrastructure.Rules;
using Harekat.Telemetry.Infrastructure.Storage;
using Harekat.Telemetry.Infrastructure.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Harekat.Telemetry.Tests;

public class ServicesIntegrationTests
{
    private static (EventIngestionService Ingestion, SuspicionAnalysisService Analysis, HeatmapService Heatmap,
        WeaponBalanceService Weapons, RiskScoreQueryService Risk, ReplayService Replay, InMemoryEventStore Store)
        CreateStack()
    {
        var store = new InMemoryEventStore();
        var reports = new InMemorySuspicionReportStore();
        var riskStore = new InMemoryRiskScoreStore();
        var queue = new InMemoryReviewQueue();
        var rules = new JsonHotReloadRuleProvider(
            Options.Create(new RuleEngineOptions { EnableHotReload = false, RulesFilePath = "missing.json" }),
            NullLogger<JsonHotReloadRuleProvider>.Instance);
        var metrics = new InMemoryPerformanceMetrics();
        var stream = new EventStreamPublisher(new InMemoryStreamBackend(), NullLogger<EventStreamPublisher>.Instance);
        var analysis = new SuspicionAnalysisService(store, reports, rules, riskStore, queue, metrics, NullLogger<SuspicionAnalysisService>.Instance);
        var ingestion = new EventIngestionService(store, stream, analysis, metrics, NullLogger<EventIngestionService>.Instance);
        var heatmap = new HeatmapService(store, new HeatmapPngRenderer());
        var weapons = new WeaponBalanceService(store);
        var risk = new RiskScoreQueryService(riskStore, queue);
        var replay = new ReplayService(new CompressedReplayStore());
        return (ingestion, analysis, heatmap, weapons, risk, replay, store);
    }

    [Fact]
    public async Task BatchIngest_VeRapor()
    {
        var s = CreateStack();
        var events = new List<MatchEventDto>();
        for (var i = 0; i < 25; i++)
            events.Add(new MatchEventDto { PlayerId = "suspect", EventType = MatchEventType.Shot, WeaponId = WeaponIds.Mpt76 });
        for (var i = 0; i < 23; i++)
            events.Add(new MatchEventDto { PlayerId = "suspect", EventType = MatchEventType.Hit, IsHeadshot = true, ThroughWall = true, WeaponId = WeaponIds.Mpt76 });

        var result = await s.Ingestion.IngestAsync(new EventBatchRequest { MatchId = "match-1", Events = events });
        Assert.Equal(48, result.Accepted);
        Assert.Equal(0, result.Rejected);
        Assert.Contains(result.SuspicionSummaries, x => x.PlayerId == "suspect" && x.IsSuspect);

        var reports = await s.Analysis.ListReportsAsync(50);
        Assert.NotEmpty(reports);

        var queue = await s.Risk.ListQueueAsync();
        Assert.Contains(queue, q => q.PlayerId == "suspect");

        var series = await s.Risk.GetSeriesAsync("suspect");
        Assert.NotEmpty(series.Points);
    }

    [Fact]
    public async Task Heatmap_10mGrid_VePng()
    {
        var s = CreateStack();
        await s.Ingestion.IngestAsync(new EventBatchRequest
        {
            MatchId = "hm",
            Events =
            [
                new MatchEventDto { PlayerId = "a", EventType = MatchEventType.Death, X = 0, Y = 1, Z = 0 },
                new MatchEventDto { PlayerId = "b", EventType = MatchEventType.Landing, X = 5, Y = 1, Z = 5 },
                new MatchEventDto { PlayerId = "c", EventType = MatchEventType.Death, X = 0, Y = 1, Z = 0 }
            ]
        });

        var hm = await s.Heatmap.GetHeatmapAsync("hm");
        Assert.Equal(10f, hm.CellSizeMeters);
        Assert.True(hm.Cells.Sum(c => c.Deaths) >= 2);
        Assert.True(hm.Cells.Sum(c => c.Landings) >= 1);

        var png = await s.Heatmap.RenderPngAsync("hm");
        Assert.True(png.Length > 100);
        Assert.Equal(0x89, png[0]);
        Assert.Equal((byte)'P', png[1]);
        Assert.Equal((byte)'N', png[2]);
        Assert.Equal((byte)'G', png[3]);
    }

    [Fact]
    public async Task SilahDengesi_KdMesafeTtk()
    {
        var s = CreateStack();
        await s.Ingestion.IngestAsync(new EventBatchRequest
        {
            MatchId = "wb",
            Events =
            [
                new MatchEventDto { PlayerId = "a", EventType = MatchEventType.Shot, WeaponId = WeaponIds.Jng90 },
                new MatchEventDto { PlayerId = "a", EventType = MatchEventType.Kill, WeaponId = WeaponIds.Jng90, DistanceMeters = 120, TimeToKillMs = 400 },
                new MatchEventDto { PlayerId = "b", EventType = MatchEventType.Death, WeaponId = WeaponIds.Jng90, X = 10, Y = 1, Z = 10 },
                new MatchEventDto { PlayerId = "a", EventType = MatchEventType.Kill, WeaponId = WeaponIds.Sar9, DistanceMeters = 15, TimeToKillMs = 900 }
            ]
        });

        var report = await s.Weapons.GetReportAsync("wb");
        var jng = Assert.Single(report.Weapons, w => w.WeaponId == WeaponIds.Jng90 && w.Kills > 0);
        Assert.Equal(1, jng.Kills);
        Assert.Equal(120, jng.AverageKillDistance);
        Assert.Equal(400, jng.AverageTtkMs);
        Assert.Equal(3, jng.TtkPercentiles.Count);
    }

    [Fact]
    public async Task Replay_SikistirilmisSaklama()
    {
        var s = CreateStack();
        var frames = Enumerable.Range(0, 100).Select(i => new ReplayFrameDto
        {
            Sequence = i,
            TimeSeconds = i * 0.05f,
            PlayerId = "p1",
            X = i,
            Y = 1,
            Z = i,
            WeaponId = WeaponIds.Mpt55
        }).ToList();

        var meta = await s.Replay.SaveAsync(new ReplaySaveRequest { MatchId = "r1", Frames = frames });
        Assert.Equal("harekat-replay-v1", meta.FormatVersion);
        Assert.Equal(100, meta.FrameCount);
        Assert.True(meta.CompressedBytes > 0);

        var loaded = await s.Replay.LoadAsync("r1");
        Assert.NotNull(loaded);
        Assert.Equal(100, loaded!.Frames.Count);
    }

    [Fact]
    public async Task GecersizWeapon_Reddedilir()
    {
        var s = CreateStack();
        var result = await s.Ingestion.IngestAsync(new EventBatchRequest
        {
            MatchId = "bad",
            Events =
            [
                new MatchEventDto { PlayerId = "a", EventType = MatchEventType.Shot, WeaponId = "ak47" },
                new MatchEventDto { PlayerId = "a", EventType = MatchEventType.Shot, WeaponId = WeaponIds.Tp9 }
            ]
        });
        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.Rejected);
        Assert.Contains(result.Errors, e => e.Contains("WeaponId"));
    }

    [Fact]
    public async Task BosBatch_Hata()
    {
        var s = CreateStack();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            s.Ingestion.IngestAsync(new EventBatchRequest { MatchId = "x", Events = [] }));
    }

    [Fact]
    public async Task ReviewQueue_StatusGuncelle()
    {
        var s = CreateStack();
        var events = new List<MatchEventDto>();
        for (var i = 0; i < 25; i++) events.Add(new MatchEventDto { PlayerId = "s", EventType = MatchEventType.Shot, WeaponId = WeaponIds.G3 });
        for (var i = 0; i < 23; i++) events.Add(new MatchEventDto { PlayerId = "s", EventType = MatchEventType.Hit, IsHeadshot = true, ThroughWall = true, WeaponId = WeaponIds.G3 });
        await s.Ingestion.IngestAsync(new EventBatchRequest { MatchId = "rq", Events = events });

        var item = (await s.Risk.ListQueueAsync()).First();
        var updated = await s.Risk.UpdateQueueAsync(item.Id, new UpdateReviewStatusRequest
        {
            Status = ReviewQueueStatus.Sanctioned,
            Notes = "aimbot"
        });
        Assert.NotNull(updated);
        Assert.Equal("Sanctioned", updated!.Status);
    }
}
