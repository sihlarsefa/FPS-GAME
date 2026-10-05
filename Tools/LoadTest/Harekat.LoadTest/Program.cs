using Harekat.LoadTest.Distributed;
using Harekat.LoadTest.Metrics;
using Harekat.LoadTest.Options;
using Harekat.LoadTest.Reporting;
using Harekat.LoadTest.Scenarios;

var options = LoadTestOptionsLoader.FromArgs(args);
Directory.CreateDirectory(options.OutputDirectory);

Console.WriteLine($"HAREKÂT LoadTest | senaryo={options.Scenario} | base={options.BaseUrl} | dryRun={options.DryRun}");
Console.WriteLine($"runId={options.RunId} | out={options.OutputDirectory}");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("İptal isteniyor…");
};

RunSummary summary;
try
{
    summary = await RunScenarioAsync(options, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Koşu iptal edildi.");
    return 130;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"HATA: {ex.Message}");
    return 1;
}

var slo = SloEvaluator.Evaluate(summary, options.Slo);
var bottlenecks = BottleneckGuide.Analyze(summary, slo);

ConsoleReporter.Print(summary, slo);
BottleneckGuide.Print(bottlenecks);

var csvPath = CsvReporter.Write(summary, options.OutputDirectory);
var jsonPath = JsonRunStore.Save(summary, options.OutputDirectory);
Console.WriteLine($"CSV  → {csvPath}");
Console.WriteLine($"JSON → {jsonPath}");

if (options.GenerateHtml)
{
    RunSummary? previous = null;
    if (!string.IsNullOrWhiteSpace(options.CompareWithRunId))
    {
        previous = JsonRunStore.Load(options.OutputDirectory, options.CompareWithRunId);
        if (previous is null)
        {
            Console.WriteLine($"Uyarı: karşılaştırma koşusu bulunamadı: {options.CompareWithRunId}");
        }
    }

    var htmlPath = HtmlReporter.Write(summary, slo, bottlenecks, options.OutputDirectory, previous);
    Console.WriteLine($"HTML → {htmlPath}");
}

return slo.Passed ? 0 : 2;

static async Task<RunSummary> RunScenarioAsync(LoadTestOptions options, CancellationToken ct)
{
    return options.Scenario.ToLowerInvariant() switch
    {
        "player" or "default" or "full" => await RunPlayerAsync(options, ct),
        "ramp" => await new RampScenario(options).RunAsync(ct),
        "soak" => await new SoakScenario(options).RunAsync(ct),
        "spike" => await new SpikeScenario(options).RunAsync(ct),
        "distributed-coordinator" or "coordinator" => await new LoadCoordinator(options).RunAsync(ct),
        "distributed-worker" or "worker" => await new LoadWorker(options).RunAsync(ct),
        _ => throw new ArgumentException($"Bilinmeyen senaryo: {options.Scenario}")
    };
}

static async Task<RunSummary> RunPlayerAsync(LoadTestOptions options, CancellationToken ct)
{
    var n = Math.Max(options.SquadSize, options.VirtualPlayers - (options.VirtualPlayers % options.SquadSize));
    Console.WriteLine($"[player] {n} sanal oyuncu, concurrency={options.MaxConcurrency}, squadSize={options.SquadSize}");
    var metrics = new MetricsCollector();
    var runner = new ScenarioRunner(options, metrics);
    return await runner.RunPlayersAsync(n, "player", ct);
}
