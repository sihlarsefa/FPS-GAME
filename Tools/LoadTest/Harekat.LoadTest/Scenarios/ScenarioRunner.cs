using System.Diagnostics;
using Harekat.LoadTest.Metrics;
using Harekat.LoadTest.Options;

namespace Harekat.LoadTest.Scenarios;

/// <summary>
/// Eşzamanlı sanal oyuncu koşucusu — SemaphoreSlim ile concurrency sınırı.
/// </summary>
public sealed class ScenarioRunner
{
    private readonly LoadTestOptions _options;
    private readonly MetricsCollector _metrics;

    public ScenarioRunner(LoadTestOptions options, MetricsCollector metrics)
    {
        _options = options;
        _metrics = metrics;
    }

    public async Task<RunSummary> RunPlayersAsync(int playerCount, string scenarioName, CancellationToken ct)
    {
        SquadJoinRegistry.Reset();
        _metrics.MarkStart();
        var sw = Stopwatch.StartNew();

        if (_options.WarmupSeconds > 0)
        {
            Console.WriteLine($"[warmup] {_options.WarmupSeconds}s…");
            await Task.Delay(TimeSpan.FromSeconds(_options.WarmupSeconds), ct);
        }

        using var gate = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);
        var scenario = new VirtualPlayerScenario(_options, _metrics);
        var tasks = new List<Task>(playerCount);
        var completed = 0;

        for (var i = 0; i < playerCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            await gate.WaitAsync(ct);
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await scenario.RunPlayerAsync(index, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _metrics.Record(new TimedResult("player_scenario", 0, false, 0, ex.Message));
                }
                finally
                {
                    var done = Interlocked.Increment(ref completed);
                    if (done % Math.Max(1, playerCount / 20) == 0 || done == playerCount)
                    {
                        Console.WriteLine($"  ilerleme: {done}/{playerCount}");
                    }

                    gate.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);
        sw.Stop();
        _metrics.MarkEnd();
        return _metrics.BuildSummary(_options.RunId, scenarioName, sw.Elapsed);
    }
}

/// <summary>Kademeli artış (ramp) — 10k hedefe adım adım.</summary>
public sealed class RampScenario
{
    private readonly LoadTestOptions _options;

    public RampScenario(LoadTestOptions options) => _options = options;

    public async Task<RunSummary> RunAsync(CancellationToken ct)
    {
        var aggregate = new MetricsCollector();
        aggregate.MarkStart();
        var totalSw = Stopwatch.StartNew();
        var steps = Math.Max(1, _options.RampSteps);
        var target = _options.RampTargetPlayers;

        Console.WriteLine($"[ramp] hedef={target}, adım={steps}, adım süresi={_options.RampStepDurationSeconds}s");

        for (var step = 1; step <= steps; step++)
        {
            ct.ThrowIfCancellationRequested();
            var players = (int)Math.Ceiling(target * (step / (double)steps));
            players = Math.Max(_options.SquadSize, players - (players % _options.SquadSize));
            Console.WriteLine($"[ramp] adım {step}/{steps} → {players} oyuncu");

            var stepMetrics = new MetricsCollector();
            var stepOptions = CloneWithPlayers(_options, players);
            stepOptions.PlayerIdOffset = _options.PlayerIdOffset + step * 100_000;
            var runner = new ScenarioRunner(stepOptions, stepMetrics);
            _ = await runner.RunPlayersAsync(players, $"ramp-step-{step}", ct);
            aggregate.Merge(stepMetrics);

            if (step < steps && _options.RampStepDurationSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.RampStepDurationSeconds), ct);
            }
        }

        totalSw.Stop();
        aggregate.MarkEnd();
        return aggregate.BuildSummary(_options.RunId, "ramp", totalSw.Elapsed);
    }

    private static LoadTestOptions CloneWithPlayers(LoadTestOptions src, int players)
    {
        return new LoadTestOptions
        {
            BaseUrl = src.BaseUrl,
            ServerKey = src.ServerKey,
            Region = src.Region,
            Scenario = src.Scenario,
            VirtualPlayers = players,
            SquadSize = src.SquadSize,
            MaxConcurrency = src.MaxConcurrency,
            ThinkTimeMs = src.ThinkTimeMs,
            TimeoutSeconds = src.TimeoutSeconds,
            Password = src.Password,
            UsernamePrefix = src.UsernamePrefix,
            DryRun = src.DryRun,
            OutputDirectory = src.OutputDirectory,
            RunId = src.RunId,
            Slo = src.Slo
        };
    }
}

/// <summary>2 saatlik soak — sabit yük altında uzun süre.</summary>
public sealed class SoakScenario
{
    private readonly LoadTestOptions _options;

    public SoakScenario(LoadTestOptions options) => _options = options;

    public async Task<RunSummary> RunAsync(CancellationToken ct)
    {
        var aggregate = new MetricsCollector();
        aggregate.MarkStart();
        var totalSw = Stopwatch.StartNew();
        var deadline = DateTime.UtcNow.AddMinutes(_options.SoakDurationMinutes);
        var wave = 0;
        var players = Math.Max(_options.SquadSize, _options.SoakPlayers - (_options.SoakPlayers % _options.SquadSize));

        Console.WriteLine($"[soak] oyuncu={players}, süre={_options.SoakDurationMinutes} dk");

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            wave++;
            var remaining = deadline - DateTime.UtcNow;
            Console.WriteLine($"[soak] dalga {wave}, kalan {remaining:hh\\:mm\\:ss}");

            var waveMetrics = new MetricsCollector();
            var waveOpts = new LoadTestOptions
            {
                BaseUrl = _options.BaseUrl,
                ServerKey = _options.ServerKey,
                Region = _options.Region,
                VirtualPlayers = players,
                SquadSize = _options.SquadSize,
                MaxConcurrency = _options.MaxConcurrency,
                ThinkTimeMs = _options.ThinkTimeMs,
                TimeoutSeconds = _options.TimeoutSeconds,
                Password = _options.Password,
                UsernamePrefix = _options.UsernamePrefix,
                DryRun = _options.DryRun,
                PlayerIdOffset = _options.PlayerIdOffset + wave * 1_000_000,
                RunId = _options.RunId,
                Slo = _options.Slo
            };

            var runner = new ScenarioRunner(waveOpts, waveMetrics);
            using var waveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            // Dalga, kalan süreden uzun olmasın
            if (remaining < TimeSpan.FromMinutes(5))
            {
                waveCts.CancelAfter(remaining);
            }

            try
            {
                _ = await runner.RunPlayersAsync(players, $"soak-wave-{wave}", waveCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Console.WriteLine("[soak] süre doldu, son dalga kesildi.");
            }

            aggregate.Merge(waveMetrics);

            if (DateTime.UtcNow >= deadline)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }

        totalSw.Stop();
        aggregate.MarkEnd();
        return aggregate.BuildSummary(_options.RunId, "soak", totalSw.Elapsed);
    }
}

/// <summary>Ani yük (spike): düşük taban → zirve → taban.</summary>
public sealed class SpikeScenario
{
    private readonly LoadTestOptions _options;

    public SpikeScenario(LoadTestOptions options) => _options = options;

    public async Task<RunSummary> RunAsync(CancellationToken ct)
    {
        var aggregate = new MetricsCollector();
        aggregate.MarkStart();
        var totalSw = Stopwatch.StartNew();

        async Task Phase(string name, int players, int holdSeconds, int offset)
        {
            players = Math.Max(_options.SquadSize, players - (players % _options.SquadSize));
            Console.WriteLine($"[spike] {name}: {players} oyuncu, {holdSeconds}s");
            var m = new MetricsCollector();
            var opts = new LoadTestOptions
            {
                BaseUrl = _options.BaseUrl,
                ServerKey = _options.ServerKey,
                Region = _options.Region,
                VirtualPlayers = players,
                SquadSize = _options.SquadSize,
                MaxConcurrency = Math.Max(_options.MaxConcurrency, players / 5),
                ThinkTimeMs = _options.ThinkTimeMs,
                TimeoutSeconds = _options.TimeoutSeconds,
                Password = _options.Password,
                UsernamePrefix = _options.UsernamePrefix,
                DryRun = _options.DryRun,
                PlayerIdOffset = offset,
                RunId = _options.RunId,
                Slo = _options.Slo
            };
            var runner = new ScenarioRunner(opts, m);
            _ = await runner.RunPlayersAsync(players, $"spike-{name}", ct);
            aggregate.Merge(m);
            if (holdSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(holdSeconds), ct);
            }
        }

        await Phase("baseline", _options.SpikeBaselinePlayers, _options.SpikeBaselineSeconds / 2, 0);
        await Phase("peak", _options.SpikePeakPlayers, _options.SpikePeakSeconds, 5_000_000);
        await Phase("recovery", _options.SpikeBaselinePlayers, _options.SpikeBaselineSeconds / 2, 9_000_000);

        totalSw.Stop();
        aggregate.MarkEnd();
        return aggregate.BuildSummary(_options.RunId, "spike", totalSw.Elapsed);
    }
}
