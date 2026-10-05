using System.Net;
using System.Text;
using Microsoft.Extensions.Options;

namespace Harekat.ServerManager.Services;

/// <summary>
/// Basit Prometheus text exposition: HTTP GET /metrics (MetricsPort, varsayılan 9183).
/// </summary>
public sealed class MetricsHttpWorker : BackgroundService
{
    private readonly MatchProcessRegistry _registry;
    private readonly PortPool _ports;
    private readonly BackendApiClient _api;
    private readonly ServerManagerOptions _options;
    private readonly ILogger<MetricsHttpWorker> _log;
    private HttpListener? _listener;

    public MetricsHttpWorker(
        MatchProcessRegistry registry,
        PortPool ports,
        BackendApiClient api,
        IOptions<ServerManagerOptions> options,
        ILogger<MetricsHttpWorker> log)
    {
        _registry = registry;
        _ports = ports;
        _api = api;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = _options.MetricsPort;
        var prefix = $"http://+:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            // macOS / yetkisiz ortamda + bağlanamayabilir — localhost dene
            _log.LogWarning(ex, "Metrics bind {Prefix} başarısız, 127.0.0.1 deneniyor", prefix);
            try { _listener.Close(); } catch { /* ignore */ }
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                _listener.Start();
            }
            catch (Exception ex2)
            {
                _log.LogError(ex2, "MetricsHttpWorker başlatılamadı — atlanıyor");
                return;
            }
        }

        _log.LogInformation("MetricsHttpWorker dinliyor :{Port}/metrics", port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var ctxTask = _listener.GetContextAsync();
                var completed = await Task.WhenAny(ctxTask, Task.Delay(Timeout.Infinite, stoppingToken));
                if (completed != ctxTask)
                    break;

                var ctx = await ctxTask;
                _ = Task.Run(() => HandleAsync(ctx, stoppingToken), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // normal shutdown
        }
        catch (HttpListenerException) when (stoppingToken.IsCancellationRequested)
        {
            // normal
        }
        finally
        {
            try { _listener.Stop(); } catch { /* ignore */ }
            try { _listener.Close(); } catch { /* ignore */ }
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            if (!string.Equals(path, "/metrics", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(path, "/metrics/", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
                return;
            }

            var body = await BuildMetricsAsync(ct);
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, ct);
            ctx.Response.Close();
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "metrics istek hatası");
            try { ctx.Response.Abort(); } catch { /* ignore */ }
        }
    }

    private async Task<string> BuildMetricsAsync(CancellationToken ct)
    {
        var sb = new StringBuilder(1024);
        var agg = _registry.AggregateResources();
        var queueDepth = 0;
        try { queueDepth = await _api.GetQueueDepthAsync(ct); } catch { /* ignore */ }

        sb.AppendLine("# HELP harekat_sm_active_matches Active game server processes");
        sb.AppendLine("# TYPE harekat_sm_active_matches gauge");
        sb.Append("harekat_sm_active_matches ").Append(_registry.ActiveCount).AppendLine();

        sb.AppendLine("# HELP harekat_sm_ports_available Free UDP ports in pool");
        sb.AppendLine("# TYPE harekat_sm_ports_available gauge");
        sb.Append("harekat_sm_ports_available ").Append(_ports.Available).AppendLine();

        sb.AppendLine("# HELP harekat_sm_ports_leased Leased UDP ports");
        sb.AppendLine("# TYPE harekat_sm_ports_leased gauge");
        sb.Append("harekat_sm_ports_leased ").Append(_ports.Leased).AppendLine();

        sb.AppendLine("# HELP harekat_sm_cpu_percent Aggregate game-process CPU percent");
        sb.AppendLine("# TYPE harekat_sm_cpu_percent gauge");
        sb.Append("harekat_sm_cpu_percent ").Append(agg.CpuPercent.ToString("F2")).AppendLine();

        sb.AppendLine("# HELP harekat_sm_ram_mb Aggregate game-process working set MB");
        sb.AppendLine("# TYPE harekat_sm_ram_mb gauge");
        sb.Append("harekat_sm_ram_mb ").Append(agg.WorkingSetMb).AppendLine();

        sb.AppendLine("# HELP harekat_sm_queue_depth Matchmaking queue depth from backend");
        sb.AppendLine("# TYPE harekat_sm_queue_depth gauge");
        sb.Append("harekat_sm_queue_depth ").Append(queueDepth).AppendLine();

        return sb.ToString();
    }

    public override void Dispose()
    {
        try { _listener?.Close(); } catch { /* ignore */ }
        base.Dispose();
    }
}
