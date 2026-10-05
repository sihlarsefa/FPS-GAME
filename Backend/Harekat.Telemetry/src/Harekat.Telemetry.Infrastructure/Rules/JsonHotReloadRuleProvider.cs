using System.Text.Json;
using System.Text.Json.Serialization;
using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Application.Rules;
using Harekat.Telemetry.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harekat.Telemetry.Infrastructure.Rules;

public sealed class RuleEngineOptions
{
    public string RulesFilePath { get; set; } = "rules/detection-rules.json";
    public bool EnableHotReload { get; set; } = true;
}

public sealed class JsonHotReloadRuleProvider : IRuleProvider, IHostedService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly RuleEngineOptions _options;
    private readonly ILogger<JsonHotReloadRuleProvider> _logger;
    private readonly object _gate = new();
    private DetectionRulesSnapshot _snapshot;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounceCts;

    public event Action? RulesChanged;

    public JsonHotReloadRuleProvider(IOptions<RuleEngineOptions> options, ILogger<JsonHotReloadRuleProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        _snapshot = LoadOrDefault();
    }

    public DetectionRulesSnapshot GetCurrent()
    {
        lock (_gate) return _snapshot;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableHotReload) return Task.CompletedTask;
        var path = ResolvePath(_options.RulesFilePath);
        var dir = Path.GetDirectoryName(path);
        var file = Path.GetFileName(path);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            _logger.LogWarning("Kural dizini bulunamadı: {Dir}", dir);
            return Task.CompletedTask;
        }

        _watcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnChanged;
        _watcher.EnableRaisingEvents = true;
        _logger.LogInformation("Kural hot-reload aktif: {Path}", path);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _watcher = null;
        return Task.CompletedTask;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(200, token).ConfigureAwait(false);
                Reload();
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    public void Reload()
    {
        var next = LoadOrDefault();
        lock (_gate) _snapshot = next;
        _logger.LogInformation("Kurallar yenilendi version={Version} count={Count}", next.Version, next.Rules.Count);
        RulesChanged?.Invoke();
    }

    private DetectionRulesSnapshot LoadOrDefault()
    {
        var path = ResolvePath(_options.RulesFilePath);
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var doc = JsonSerializer.Deserialize<DetectionRulesDocument>(json, JsonOptions)
                          ?? CreateDefaultDocument();
                return new DetectionRulesSnapshot(doc.Version, DateTimeOffset.UtcNow, doc.Rules);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kural dosyası okunamadı, varsayılanlar kullanılacak: {Path}", path);
        }

        var fallback = CreateDefaultDocument();
        return new DetectionRulesSnapshot(fallback.Version, DateTimeOffset.UtcNow, fallback.Rules);
    }

    public static DetectionRulesDocument CreateDefaultDocument() => new()
    {
        Version = "default-1",
        Rules =
        [
            new DetectionRuleDefinition
            {
                Id = "impossible_hit_rate",
                Enabled = true,
                Kind = SuspicionRuleKind.ImpossibleHitRate,
                ScoreWeight = 35,
                Parameters = new Dictionary<string, double> { ["minShots"] = 20, ["maxHitRate"] = 0.85 }
            },
            new DetectionRuleDefinition
            {
                Id = "high_headshot_rate",
                Enabled = true,
                Kind = SuspicionRuleKind.HighHeadshotRate,
                ScoreWeight = 30,
                Parameters = new Dictionary<string, double> { ["minHits"] = 10, ["maxHeadshotRate"] = 0.70 }
            },
            new DetectionRuleDefinition
            {
                Id = "impossible_speed",
                Enabled = true,
                Kind = SuspicionRuleKind.ImpossibleSpeed,
                ScoreWeight = 40,
                Parameters = new Dictionary<string, double> { ["maxSpeedMps"] = 18 }
            },
            new DetectionRuleDefinition
            {
                Id = "wallbang_consistency",
                Enabled = true,
                Kind = SuspicionRuleKind.WallbangConsistency,
                ScoreWeight = 35,
                Parameters = new Dictionary<string, double> { ["minWallHits"] = 5, ["minWallHitRatio"] = 0.40 }
            }
        ]
    };

    private static string ResolvePath(string relativeOrAbsolute)
    {
        if (Path.IsPathRooted(relativeOrAbsolute)) return relativeOrAbsolute;
        var candidates = new[]
        {
            Path.GetFullPath(relativeOrAbsolute),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativeOrAbsolute)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativeOrAbsolute)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", relativeOrAbsolute)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativeOrAbsolute))
        };
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounceCts?.Dispose();
    }
}
