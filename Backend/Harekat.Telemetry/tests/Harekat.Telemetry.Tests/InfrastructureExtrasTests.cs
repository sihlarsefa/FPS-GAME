using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Domain.Entities;
using Harekat.Telemetry.Domain.Enums;
using Harekat.Telemetry.Domain.ValueObjects;
using Harekat.Telemetry.Infrastructure.Heatmap;
using Harekat.Telemetry.Infrastructure.Metrics;
using Harekat.Telemetry.Infrastructure.Replay;
using Harekat.Telemetry.Infrastructure.Rules;
using Harekat.Telemetry.Infrastructure.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Harekat.Telemetry.Tests;

public class InfrastructureExtrasTests
{
    [Fact]
    public void StreamBackends_Isimler()
    {
        Assert.Equal("InMemory", new InMemoryStreamBackend().Name);
        Assert.Equal("RedisStreams", new RedisStreamsBackend().Name);
        Assert.Equal("Kafka", new KafkaStreamBackend().Name);
    }

    [Fact]
    public async Task StreamPublisher_BatchYazar()
    {
        var backend = new InMemoryStreamBackend();
        var pub = new EventStreamPublisher(backend, NullLogger<EventStreamPublisher>.Instance);
        var events = Enumerable.Range(0, 5).Select(i => new MatchEvent
        {
            Id = Guid.NewGuid(),
            MatchId = "m",
            PlayerId = "p",
            EventType = MatchEventType.Shot,
            Timestamp = DateTimeOffset.UtcNow
        }).ToList();

        await pub.PublishBatchAsync("test-stream", events);
        Assert.Equal(5, backend.Count("test-stream"));
        Assert.Equal("InMemory", pub.BackendName);
    }

    [Fact]
    public void PngEncoder_GecerliImza()
    {
        var rgba = new byte[4 * 4 * 4];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = 255; rgba[i + 3] = 255;
        }
        var png = HeatmapPngRenderer.EncodeRgbaPng(rgba, 4, 4);
        Assert.Equal([137, 80, 78, 71, 13, 10, 26, 10], png.Take(8).ToArray());
    }

    [Fact]
    public void HeatmapRenderer_BosHucreler()
    {
        var png = new HeatmapPngRenderer().RenderPng([], new HeatmapRenderOptions(128));
        Assert.True(png.Length > 50);
    }

    [Fact]
    public void RuleProvider_VarsayilanYukler()
    {
        var provider = new JsonHotReloadRuleProvider(
            Options.Create(new RuleEngineOptions { EnableHotReload = false, RulesFilePath = "yok.json" }),
            NullLogger<JsonHotReloadRuleProvider>.Instance);
        var snap = provider.GetCurrent();
        Assert.Equal(4, snap.Rules.Count);
        Assert.Contains(snap.Rules, r => r.Id == "impossible_hit_rate");
    }

    [Fact]
    public void RuleProvider_DosyadanYukler()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "rules", "detection-rules.json"));
        if (!File.Exists(path))
            path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "rules", "detection-rules.json"));
        Assert.True(File.Exists(path), $"rules missing at {path}");

        var provider = new JsonHotReloadRuleProvider(
            Options.Create(new RuleEngineOptions { EnableHotReload = false, RulesFilePath = path }),
            NullLogger<JsonHotReloadRuleProvider>.Instance);
        Assert.Equal("1.0", provider.GetCurrent().Version);
    }

    [Fact]
    public void PerformanceMetrics_Snapshot()
    {
        var m = new InMemoryPerformanceMetrics();
        m.RecordIngestion(10, TimeSpan.FromMilliseconds(5));
        m.RecordIngestion(20, TimeSpan.FromMilliseconds(15));
        m.RecordAnalysis(TimeSpan.FromMilliseconds(8));
        var snap = m.Snapshot();
        Assert.Equal(30, snap.TotalEventsIngested);
        Assert.Equal(2, snap.TotalBatches);
        Assert.True(snap.AvgIngestionMs > 0);
        Assert.True(snap.P95IngestionMs >= snap.AvgIngestionMs || snap.P95IngestionMs > 0);
    }

    [Fact]
    public void ReplayCompress_BoyutKucultur()
    {
        var raw = System.Text.Encoding.UTF8.GetBytes(new string('x', 5000));
        var compressed = CompressedReplayStore.Compress(raw);
        Assert.True(compressed.Length < raw.Length);
    }

    [Fact]
    public void WeaponPercentiles_BosVeDolu()
    {
        Assert.Equal([0, 0], WeaponBalanceService_PercentileProxy([], 0.5, 0.9));
        var p = WeaponBalanceService_PercentileProxy([10, 20, 30, 40, 50], 0.5, 0.99);
        Assert.Equal(2, p.Count);
        Assert.True(p[0] <= p[1]);
    }

    // erişim için ince sarmalayıcı
    private static IReadOnlyList<double> WeaponBalanceService_PercentileProxy(IReadOnlyList<double> s, params double[] ps) =>
        Harekat.Telemetry.Application.Services.WeaponBalanceService.Percentiles(s, ps);
}
