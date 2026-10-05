using Harekat.LoadTest.Metrics;
using Harekat.LoadTest.Options;
using Harekat.LoadTest.Reporting;

namespace Harekat.LoadTest.Tests;

public class LatencyStoreTests
{
    [Fact]
    public void Percentile_Empty_ReturnsZero()
    {
        Assert.Equal(0, LatencyStore.Percentile(Array.Empty<long>(), 0.5));
    }

    [Fact]
    public void Percentile_Single_ReturnsValue()
    {
        Assert.Equal(42, LatencyStore.Percentile(new long[] { 42 }, 0.99));
    }

    [Fact]
    public void Compute_P50P95P99_OnSortedSeries()
    {
        var store = new LatencyStore();
        for (long i = 1; i <= 100; i++)
        {
            store.Add(i);
        }

        var p = store.Compute();
        Assert.Equal(100, p.Count);
        Assert.Equal(50.5, p.P50Ms, 1);
        Assert.True(p.P95Ms >= 95 && p.P95Ms <= 96);
        Assert.True(p.P99Ms >= 99 && p.P99Ms <= 100);
        Assert.Equal(1, p.MinMs);
        Assert.Equal(100, p.MaxMs);
    }
}

public class MetricsCollectorTests
{
    [Fact]
    public void Record_TracksSuccessAndFailure()
    {
        var m = new MetricsCollector();
        m.MarkStart();
        m.Record(new TimedResult("login", 10, true, 200));
        m.Record(new TimedResult("login", 20, false, 500, "err"));
        m.MarkEnd();
        var s = m.BuildSummary("t1", "player", TimeSpan.FromSeconds(1));
        Assert.Equal(2, s.TotalRequests);
        Assert.Equal(1, s.SuccessCount);
        Assert.Equal(1, s.FailureCount);
        Assert.Equal(0.5, s.ErrorRate);
        Assert.True(s.Operations.ContainsKey("login"));
        Assert.Equal(15, s.Operations["login"].Latency.P50Ms);
    }

    [Fact]
    public void Merge_CombinesCollectors()
    {
        var a = new MetricsCollector();
        var b = new MetricsCollector();
        a.Record(new TimedResult("a", 5, true, 200));
        b.Record(new TimedResult("b", 15, true, 200));
        a.Merge(b);
        var s = a.BuildSummary("m", "x", TimeSpan.FromSeconds(2));
        Assert.Equal(2, s.TotalRequests);
        Assert.True(s.Operations.ContainsKey("a"));
        Assert.True(s.Operations.ContainsKey("b"));
    }
}

public class SloEvaluatorTests
{
    [Fact]
    public void Evaluate_PassesWhenWithinThresholds()
    {
        var summary = MakeSummary(p50: 40, p95: 100, p99: 200, err: 0.001, rps: 200);
        var slo = SloEvaluator.Evaluate(summary, new SloThresholds());
        Assert.True(slo.Passed);
        Assert.All(slo.Checks, c => Assert.True(c.Passed));
    }

    [Fact]
    public void Evaluate_FailsOnHighP95()
    {
        var summary = MakeSummary(p50: 40, p95: 900, p99: 200, err: 0.001, rps: 200);
        var slo = SloEvaluator.Evaluate(summary, new SloThresholds { P95MaxMs = 250 });
        Assert.False(slo.Passed);
        Assert.Contains(slo.Checks, c => c.Name == "p95_ms" && !c.Passed);
    }

    [Fact]
    public void Evaluate_FailsOnErrorRate()
    {
        var summary = MakeSummary(p50: 10, p95: 20, p99: 30, err: 0.1, rps: 200);
        var slo = SloEvaluator.Evaluate(summary, new SloThresholds { ErrorRateMax = 0.01 });
        Assert.False(slo.Passed);
    }

    private static RunSummary MakeSummary(double p50, double p95, double p99, double err, double rps)
    {
        var lat = new LatencyPercentiles(100, 1, 999, 50, p50, p95, p99);
        return new RunSummary(
            "run",
            "player",
            DateTime.UtcNow.AddSeconds(-10),
            DateTime.UtcNow,
            10,
            100,
            (long)(100 * (1 - err)),
            (long)(100 * err),
            err,
            rps,
            lat,
            new Dictionary<string, OperationSummary>
            {
                ["login"] = new("login", 100, 99, 1, 0.01, lat)
            });
    }
}

public class BottleneckGuideTests
{
    [Fact]
    public void Analyze_ReturnsTipsWhenSloFails()
    {
        var lat = new LatencyPercentiles(10, 1, 2000, 400, 100, 800, 1500);
        var summary = new RunSummary(
            "r", "player", DateTime.UtcNow, DateTime.UtcNow, 1, 10, 5, 5, 0.5, 10, lat,
            new Dictionary<string, OperationSummary>
            {
                ["matchmaking_queue"] = new("matchmaking_queue", 10, 5, 5, 0.5, lat)
            });
        var slo = SloEvaluator.Evaluate(summary, new SloThresholds());
        var tips = BottleneckGuide.Analyze(summary, slo);
        Assert.NotEmpty(tips);
        Assert.Contains(tips, t => t.Contains("matchmaking", StringComparison.OrdinalIgnoreCase) || t.Contains("SLO"));
    }
}

public class OptionsLoaderTests
{
    [Fact]
    public void FromArgs_ParsesPlayersAndScenario()
    {
        var opts = LoadTestOptionsLoader.FromArgs(new[] { "--players", "200", "--scenario", "ramp", "--dry-run" });
        Assert.Equal(200, opts.VirtualPlayers);
        Assert.Equal("ramp", opts.Scenario);
        Assert.True(opts.DryRun);
    }

    [Fact]
    public void FromJsonFile_LoadsDefaultProfile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Profiles", "default.json");
        if (!File.Exists(path))
        {
            // Test çıktısında Profiles kopyalanmamış olabilir — kaynak dizininden dene
            path = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "Harekat.LoadTest", "Profiles", "default.json"));
        }

        Assert.True(File.Exists(path), $"Profil yok: {path}");
        var opts = LoadTestOptionsLoader.FromJsonFile(path);
        Assert.Equal("player", opts.Scenario);
        Assert.Equal(10, opts.SquadSize);
    }
}

public class ReporterTests
{
    [Fact]
    public void CsvAndJsonAndHtml_WriteFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "harekat-loadtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var lat = new LatencyPercentiles(5, 1, 50, 20, 15, 40, 48);
            var summary = new RunSummary(
                "testrun",
                "player",
                DateTime.UtcNow.AddSeconds(-5),
                DateTime.UtcNow,
                5,
                5,
                5,
                0,
                0,
                1,
                lat,
                new Dictionary<string, OperationSummary>
                {
                    ["login"] = new("login", 5, 5, 0, 0, lat)
                });

            var slo = SloEvaluator.Evaluate(summary, new SloThresholds { MinRps = 0.1 });
            var tips = BottleneckGuide.Analyze(summary, slo);

            var csv = CsvReporter.Write(summary, dir);
            var json = JsonRunStore.Save(summary, dir);
            var html = HtmlReporter.Write(summary, slo, tips, dir);

            Assert.True(File.Exists(csv));
            Assert.True(File.Exists(json));
            Assert.True(File.Exists(html));
            Assert.Contains("p50_ms", File.ReadAllText(csv));
            Assert.Contains("GEÇTİ", File.ReadAllText(html));

            var loaded = JsonRunStore.Load(dir, "testrun");
            Assert.NotNull(loaded);
            Assert.Equal("testrun", loaded!.RunId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
