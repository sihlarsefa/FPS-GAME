using Microsoft.Extensions.Options;

namespace Harekat.ServerManager.Services;

/// <summary>
/// Backend'den bekleyen maçları alır, port lease eder, claim eder, HAREKAT_Server.exe başlatır.
/// </summary>
public sealed class MatchSpawnWorker : BackgroundService
{
    private readonly BackendApiClient _api;
    private readonly PortPool _ports;
    private readonly MatchProcessRegistry _registry;
    private readonly ServerManagerOptions _options;
    private readonly ILogger<MatchSpawnWorker> _log;

    public MatchSpawnWorker(
        BackendApiClient api,
        PortPool ports,
        MatchProcessRegistry registry,
        IOptions<ServerManagerOptions> options,
        ILogger<MatchSpawnWorker> log)
    {
        _api = api;
        _ports = ports;
        _registry = registry;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation(
            "MatchSpawnWorker başladı — poll={Poll}s ports={Min}-{Max} exe={Exe}",
            _options.PollIntervalSeconds, _options.PortMin, _options.PortMax, _options.GameServerExe);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _registry.ReapAndRestartCrashed(entry =>
                    _registry.StartGameProcess(entry.Port, entry.MatchId, entry.ServerId));

                if (!_registry.IsOverResourceLimit())
                    await PollAndSpawnAsync(stoppingToken);
                else
                    _log.LogWarning("Kaynak limiti aşıldı — yeni maç spawn atlanıyor");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "MatchSpawnWorker döngü hatası");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)), stoppingToken);
        }
    }

    private async Task PollAndSpawnAsync(CancellationToken ct)
    {
        var pending = await _api.GetPendingAllocationsAsync(ct);
        if (pending.Count == 0)
            return;

        // WarmReadySlots kadar boş slot bırak (ani spike için)
        var reserve = Math.Max(0, _options.WarmReadySlots);
        foreach (var match in pending)
        {
            if (_ports.Available <= reserve)
            {
                _log.LogDebug("Port rezervi doldu (warm={Warm})", reserve);
                break;
            }

            if (_registry.IsOverResourceLimit())
                break;

            if (!_ports.TryLease(out var port))
                break;

            var claimed = await _api.ClaimMatchAsync(match.Id, port, ct);
            if (claimed is null)
            {
                _ports.Release(port);
                continue;
            }

            var serverId = claimed.ServerId;
            if (serverId == Guid.Empty)
            {
                var lookedUp = await _api.FindServerByEndpointAsync(_options.PublicHost, port, ct);
                serverId = lookedUp?.Id ?? Guid.Empty;
            }

            if (serverId == Guid.Empty)
            {
                _log.LogWarning("Claim sonrası ServerId yok match={MatchId} — süreç yine de başlatılıyor", match.Id);
                serverId = Guid.NewGuid();
            }

            var process = _registry.StartGameProcess(port, match.Id, serverId);
            if (process is null)
            {
                _ports.Release(port);
                try { await _api.ReleaseAsync(serverId, ct); } catch { /* ignore */ }
                continue;
            }

            var tracked = new TrackedMatchProcess
            {
                MatchId = match.Id,
                ServerId = serverId,
                Port = port,
                Process = process,
                Status = "Allocated",
                LogFile = null
            };
            _registry.TryAdd(tracked);
            _log.LogInformation(
                "Maç spawn edildi match={MatchId} server={ServerId} port={Port}",
                match.Id, serverId, port);
        }
    }
}
