namespace Harekat.ServerManager.Services;

/// <summary>
/// Periyodik log rotation (LogRetainDays).
/// </summary>
public sealed class LogRotationWorker : BackgroundService
{
    private readonly LogRotator _rotator;
    private readonly ILogger<LogRotationWorker> _log;

    public LogRotationWorker(LogRotator rotator, ILogger<LogRotationWorker> log)
    {
        _rotator = rotator;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("LogRotationWorker başladı");
        // İlk turda hemen temizle, sonra saatlik
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _rotator.Rotate();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Log rotation hatası");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
