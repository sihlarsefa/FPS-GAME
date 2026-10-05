using Microsoft.Extensions.Options;

namespace Harekat.ServerManager.Services;

/// <summary>
/// LogDirectory altındaki eski log dosyalarını siler (LogRetainDays).
/// </summary>
public sealed class LogRotator
{
    private readonly ServerManagerOptions _options;
    private readonly ILogger<LogRotator> _log;

    public LogRotator(IOptions<ServerManagerOptions> options, ILogger<LogRotator> log)
    {
        _options = options.Value;
        _log = log;
    }

    public int Rotate()
    {
        var dir = _options.LogDirectory;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return 0;

        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, _options.LogRetainDays));
        var deleted = 0;

        foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
        {
            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTimeUtc < cutoff)
                {
                    info.Delete();
                    deleted++;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Log silinemedi: {File}", file);
            }
        }

        if (deleted > 0)
            _log.LogInformation("Log rotation: {Count} dosya silindi (retain {Days} gün)", deleted, _options.LogRetainDays);

        return deleted;
    }
}
