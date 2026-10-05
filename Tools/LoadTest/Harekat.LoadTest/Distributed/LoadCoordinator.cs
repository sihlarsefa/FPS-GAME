using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harekat.LoadTest.Metrics;
using Harekat.LoadTest.Options;
using Harekat.LoadTest.Reporting;
using Harekat.LoadTest.Scenarios;

namespace Harekat.LoadTest.Distributed;

/// <summary>
/// Dağıtık yük koordinasyonu: worker'lar kayıt olur, oyuncu dilimi alır, sonuçları merkeze gönderir.
/// </summary>
public sealed class LoadCoordinator
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly LoadTestOptions _options;
    private readonly ConcurrentDictionary<string, WorkerRegistration> _workers = new();
    private readonly ConcurrentDictionary<string, RunSummary> _results = new();
    private readonly TaskCompletionSource _allRegistered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _allResults = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _nextOffset;

    public LoadCoordinator(LoadTestOptions options) => _options = options;

    public async Task<RunSummary> RunAsync(CancellationToken ct)
    {
        using var listener = new HttpListener();
        var prefix = $"http://+:{_options.CoordinatorPort}/";
        try
        {
            listener.Prefixes.Add(prefix);
            listener.Start();
        }
        catch (HttpListenerException)
        {
            // macOS'ta + için izin gerekebilir — localhost'a düş
            prefix = $"http://127.0.0.1:{_options.CoordinatorPort}/";
            listener.Prefixes.Clear();
            listener.Prefixes.Add(prefix);
            listener.Start();
        }

        Console.WriteLine($"[coordinator] dinleniyor: {prefix}");
        Console.WriteLine($"[coordinator] beklenen worker: {_options.ExpectedWorkers}");

        var listenTask = ListenLoopAsync(listener, ct);

        // Worker'ların kaydolmasını bekle (timeout 10 dk)
        var regTimeout = Task.Delay(TimeSpan.FromMinutes(10), ct);
        var finished = await Task.WhenAny(_allRegistered.Task, regTimeout);
        if (finished != _allRegistered.Task)
        {
            throw new TimeoutException("Worker kayıt zaman aşımı.");
        }

        Console.WriteLine("[coordinator] tüm worker'lar kayıtlı — sonuç bekleniyor…");

        var resultTimeout = Task.Delay(TimeSpan.FromHours(3), ct);
        finished = await Task.WhenAny(_allResults.Task, resultTimeout);
        if (finished != _allResults.Task)
        {
            Console.WriteLine("[coordinator] uyarı: tüm sonuçlar gelmedi, kısmi birleştirme.");
        }

        listener.Stop();
        try { await listenTask; } catch { /* ignore */ }

        return Aggregate();
    }

    private async Task ListenLoopAsync(HttpListener listener, CancellationToken ct)
    {
        while (listener.IsListening && !ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await listener.GetContextAsync().WaitAsync(ct);
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(ctx), ct);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath.TrimEnd('/') ?? "";
            if (ctx.Request.HttpMethod == "GET" && path is "/health" or "")
            {
                await WriteJsonAsync(ctx, 200, new { status = "ok", workers = _workers.Count, expected = _options.ExpectedWorkers });
                return;
            }

            if (ctx.Request.HttpMethod == "POST" && path == "/register")
            {
                using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                var body = await reader.ReadToEndAsync();
                var req = JsonSerializer.Deserialize<WorkerRegistrationRequest>(body, JsonOpts)
                          ?? throw new InvalidOperationException("geçersiz kayıt");

                var playersPerWorker = Math.Max(
                    _options.SquadSize,
                    (_options.VirtualPlayers + _options.ExpectedWorkers - 1) / _options.ExpectedWorkers);
                playersPerWorker -= playersPerWorker % _options.SquadSize;

                var offset = Interlocked.Add(ref _nextOffset, playersPerWorker) - playersPerWorker;
                var reg = new WorkerRegistration(req.WorkerId, offset, playersPerWorker, DateTime.UtcNow);
                _workers[req.WorkerId] = reg;

                Console.WriteLine($"[coordinator] kayıt: {req.WorkerId} offset={offset} n={playersPerWorker}");

                if (_workers.Count >= _options.ExpectedWorkers)
                {
                    _allRegistered.TrySetResult();
                }

                await WriteJsonAsync(ctx, 200, new WorkerAssignment(reg.WorkerId, reg.PlayerOffset, reg.PlayerCount, _options.BaseUrl, _options.RunId));
                return;
            }

            if (ctx.Request.HttpMethod == "POST" && path == "/results")
            {
                using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                var body = await reader.ReadToEndAsync();
                var summary = JsonSerializer.Deserialize<RunSummary>(body, JsonOpts)
                              ?? throw new InvalidOperationException("geçersiz sonuç");

                var workerId = ctx.Request.Headers["X-Worker-Id"] ?? summary.RunId;
                _results[workerId] = summary;
                Console.WriteLine($"[coordinator] sonuç: {workerId} req={summary.TotalRequests} rps={summary.RequestsPerSecond:F1}");

                if (_results.Count >= _options.ExpectedWorkers)
                {
                    _allResults.TrySetResult();
                }

                await WriteJsonAsync(ctx, 200, new { accepted = true });
                return;
            }

            await WriteJsonAsync(ctx, 404, new { error = "not found" });
        }
        catch (Exception ex)
        {
            try { await WriteJsonAsync(ctx, 500, new { error = ex.Message }); } catch { /* ignore */ }
        }
    }

    private RunSummary Aggregate()
    {
        var merged = new MetricsCollector();
        merged.MarkStart();
        double duration = 0;
        foreach (var s in _results.Values)
        {
            duration = Math.Max(duration, s.DurationSeconds);
            // RunSummary'den tekrar örnek yok — özet alanlarından sentetik kayıt
            foreach (var op in s.Operations.Values)
            {
                for (var i = 0; i < op.Success; i++)
                {
                    merged.Record(new TimedResult(op.Name, (long)op.Latency.P50Ms, true, 200));
                }

                for (var i = 0; i < op.Failure; i++)
                {
                    merged.Record(new TimedResult(op.Name, (long)op.Latency.P50Ms, false, 500));
                }
            }
        }

        merged.MarkEnd();
        var wall = TimeSpan.FromSeconds(Math.Max(duration, 0.001));
        return merged.BuildSummary(_options.RunId, "distributed", wall);
    }

    private static async Task WriteJsonAsync(HttpListenerContext ctx, int status, object payload)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOpts));
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }
}

public sealed record WorkerRegistrationRequest(string WorkerId);
public sealed record WorkerRegistration(string WorkerId, int PlayerOffset, int PlayerCount, DateTime RegisteredUtc);
public sealed record WorkerAssignment(string WorkerId, int PlayerOffset, int PlayerCount, string BaseUrl, string RunId);

public sealed class LoadWorker
{
    private readonly LoadTestOptions _options;

    public LoadWorker(LoadTestOptions options) => _options = options;

    public async Task<RunSummary> RunAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var registerUrl = $"{_options.CoordinatorUrl.TrimEnd('/')}/register";
        Console.WriteLine($"[worker {_options.WorkerId}] koordinatöre kayıt: {registerUrl}");

        var regRes = await http.PostAsync(
            registerUrl,
            new StringContent(
                JsonSerializer.Serialize(new WorkerRegistrationRequest(_options.WorkerId)),
                Encoding.UTF8,
                "application/json"),
            ct);
        regRes.EnsureSuccessStatusCode();
        var assignment = JsonSerializer.Deserialize<WorkerAssignment>(
            await regRes.Content.ReadAsStringAsync(ct),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("boş atama");

        Console.WriteLine($"[worker] offset={assignment.PlayerOffset} count={assignment.PlayerCount}");

        var local = new LoadTestOptions
        {
            BaseUrl = string.IsNullOrWhiteSpace(assignment.BaseUrl) ? _options.BaseUrl : assignment.BaseUrl,
            ServerKey = _options.ServerKey,
            Region = _options.Region,
            VirtualPlayers = assignment.PlayerCount,
            SquadSize = _options.SquadSize,
            MaxConcurrency = _options.MaxConcurrency,
            ThinkTimeMs = _options.ThinkTimeMs,
            TimeoutSeconds = _options.TimeoutSeconds,
            Password = _options.Password,
            UsernamePrefix = _options.UsernamePrefix,
            DryRun = _options.DryRun,
            PlayerIdOffset = assignment.PlayerOffset,
            RunId = assignment.RunId,
            OutputDirectory = _options.OutputDirectory,
            Slo = _options.Slo
        };

        var metrics = new MetricsCollector();
        var runner = new ScenarioRunner(local, metrics);
        var summary = await runner.RunPlayersAsync(assignment.PlayerCount, "distributed-worker", ct);

        var resultUrl = $"{_options.CoordinatorUrl.TrimEnd('/')}/results";
        using var req = new HttpRequestMessage(HttpMethod.Post, resultUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(summary, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Encoding.UTF8,
                "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Worker-Id", _options.WorkerId);
        var post = await http.SendAsync(req, ct);
        post.EnsureSuccessStatusCode();
        Console.WriteLine("[worker] sonuçlar gönderildi.");
        return summary;
    }
}
