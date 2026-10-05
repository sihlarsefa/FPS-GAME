using Microsoft.Extensions.Options;

namespace Harekat.ServerManager.Services;

/// <summary>
/// İzlenen oyun sunucuları için periyodik backend heartbeat.
/// </summary>
public sealed class HeartbeatWorker : BackgroundService
{
    private readonly BackendApiClient _api;
    private readonly MatchProcessRegistry _registry;
    private readonly ServerManagerOptions _options;
    private readonly ILogger<HeartbeatWorker> _log;

    public HeartbeatWorker(
        BackendApiClient api,
        MatchProcessRegistry registry,
        IOptions<ServerManagerOptions> options,
        ILogger<HeartbeatWorker> log)
    {
        _api = api;
        _registry = registry;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("HeartbeatWorker başladı — interval={Sec}s", _options.HeartbeatIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var entry in _registry.All)
                {
                    if (entry.ServerId == Guid.Empty)
                        continue;

                    var status = entry.Process is { HasExited: false }
                        ? entry.Status
                        : "Offline";

                    await _api.HeartbeatAsync(entry.ServerId, entry.CurrentPlayers, status, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "HeartbeatWorker hatası");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(2, _options.HeartbeatIntervalSeconds)),
                stoppingToken);
        }
    }
}
