using Harekat.Telemetry.Application.Contracts;
using Harekat.Telemetry.Application.Services;
using Harekat.Telemetry.Domain.Enums;
using Harekat.Telemetry.Infrastructure.Heatmap;
using Harekat.Telemetry.Infrastructure.Metrics;
using Harekat.Telemetry.Infrastructure.Rules;
using Harekat.Telemetry.Infrastructure.Storage;
using Harekat.Telemetry.Infrastructure.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Harekat.Telemetry.Tests;

public class EdgeCaseTests
{
    [Fact]
    public async Task HaritaDisiOlum_Reddedilir()
    {
        var ingestion = CreateIngestion();
        var result = await ingestion.IngestAsync(new EventBatchRequest
        {
            MatchId = "oob",
            Events =
            [
                new MatchEventDto { PlayerId = "a", EventType = MatchEventType.Death, X = 9999, Y = 1, Z = 0 }
            ]
        });
        Assert.Equal(0, result.Accepted);
        Assert.Equal(1, result.Rejected);
    }

    [Fact]
    public async Task EksikKonumBileseni_Reddedilir()
    {
        var ingestion = CreateIngestion();
        var result = await ingestion.IngestAsync(new EventBatchRequest
        {
            MatchId = "pos",
            Events =
            [
                new MatchEventDto { PlayerId = "a", EventType = MatchEventType.Shot, X = 1 }
            ]
        });
        Assert.Equal(1, result.Rejected);
    }

    [Fact]
    public async Task MaxBatchAsimi_Hata()
    {
        var ingestion = CreateIngestion();
        var events = Enumerable.Range(0, EventIngestionService.MaxBatchSize + 1)
            .Select(i => new MatchEventDto { PlayerId = "a", EventType = MatchEventType.Shot })
            .ToList();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ingestion.IngestAsync(new EventBatchRequest { MatchId = "big", Events = events }));
    }

    [Fact]
    public async Task PlayerIdBos_Reddedilir()
    {
        var ingestion = CreateIngestion();
        var result = await ingestion.IngestAsync(new EventBatchRequest
        {
            MatchId = "pid",
            Events = [new MatchEventDto { PlayerId = "  ", EventType = MatchEventType.Shot }]
        });
        Assert.Equal(1, result.Rejected);
    }

    [Fact]
    public void HeatmapBuildCells_SadeceDeathLanding()
    {
        var cells = HeatmapService.BuildCells(
        [
            new Domain.Entities.MatchEvent
            {
                Id = Guid.NewGuid(), MatchId = "m", PlayerId = "p",
                EventType = MatchEventType.Shot,
                Timestamp = DateTimeOffset.UtcNow,
                Position = new Domain.ValueObjects.WorldPosition(0, 1, 0)
            },
            new Domain.Entities.MatchEvent
            {
                Id = Guid.NewGuid(), MatchId = "m", PlayerId = "p",
                EventType = MatchEventType.Death,
                Timestamp = DateTimeOffset.UtcNow,
                Position = new Domain.ValueObjects.WorldPosition(0, 1, 0)
            }
        ], 10f);
        Assert.Single(cells);
        Assert.Equal(1, cells[0].DeathCount);
        Assert.Equal(0, cells[0].LandingCount);
    }

    private static EventIngestionService CreateIngestion()
    {
        var store = new InMemoryEventStore();
        var reports = new InMemorySuspicionReportStore();
        var risk = new InMemoryRiskScoreStore();
        var queue = new InMemoryReviewQueue();
        var rules = new JsonHotReloadRuleProvider(
            Options.Create(new RuleEngineOptions { EnableHotReload = false }),
            NullLogger<JsonHotReloadRuleProvider>.Instance);
        var metrics = new InMemoryPerformanceMetrics();
        var analysis = new SuspicionAnalysisService(store, reports, rules, risk, queue, metrics, NullLogger<SuspicionAnalysisService>.Instance);
        return new EventIngestionService(
            store,
            new EventStreamPublisher(new InMemoryStreamBackend(), NullLogger<EventStreamPublisher>.Instance),
            analysis,
            metrics,
            NullLogger<EventIngestionService>.Instance);
    }
}
