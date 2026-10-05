using System.Text.Json.Serialization;

namespace Harekat.LoadTest.Options;

/// <summary>
/// Yük testi yapılandırması — CLI argümanları veya profil JSON'undan yüklenir.
/// </summary>
public sealed class LoadTestOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5080";
    public string ServerKey { get; set; } = "dev-server-key";
    public string Region { get; set; } = "tr-ist";

    /// <summary>Senaryo: player | ramp | soak | spike | distributed-coordinator | distributed-worker</summary>
    public string Scenario { get; set; } = "player";

    public int VirtualPlayers { get; set; } = 100;
    public int SquadSize { get; set; } = 10;
    public int MaxConcurrency { get; set; } = 200;
    public int ThinkTimeMs { get; set; } = 50;
    public int TimeoutSeconds { get; set; } = 30;
    public int WarmupSeconds { get; set; } = 0;

    public int RampTargetPlayers { get; set; } = 10_000;
    public int RampSteps { get; set; } = 10;
    public int RampStepDurationSeconds { get; set; } = 30;

    public int SoakDurationMinutes { get; set; } = 120;
    public int SoakPlayers { get; set; } = 2_000;

    public int SpikeBaselinePlayers { get; set; } = 500;
    public int SpikePeakPlayers { get; set; } = 10_000;
    public int SpikePeakSeconds { get; set; } = 60;
    public int SpikeBaselineSeconds { get; set; } = 120;

    public string CoordinatorUrl { get; set; } = "http://localhost:9090";
    public int CoordinatorPort { get; set; } = 9090;
    public string WorkerId { get; set; } = Environment.MachineName;
    public int PlayerIdOffset { get; set; }
    public int ExpectedWorkers { get; set; } = 1;

    public string OutputDirectory { get; set; } = "reports";
    public string RunId { get; set; } = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    public string? CompareWithRunId { get; set; }
    public bool GenerateHtml { get; set; } = true;
    public bool DryRun { get; set; }

    public SloThresholds Slo { get; set; } = new();

    public string Password { get; set; } = "LoadTest!123";
    public string UsernamePrefix { get; set; } = "vp";
}

public sealed class SloThresholds
{
    public double P50MaxMs { get; set; } = 100;
    public double P95MaxMs { get; set; } = 250;
    public double P99MaxMs { get; set; } = 500;
    public double ErrorRateMax { get; set; } = 0.01;
    public double MinRps { get; set; } = 50;
}

public static class LoadTestOptionsLoader
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static LoadTestOptions FromArgs(string[] args)
    {
        string? profilePath = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--profile" or "-p" && i + 1 < args.Length)
            {
                profilePath = args[++i];
            }
            else if (args[i] is "--help" or "-h")
            {
                PrintHelp();
                Environment.Exit(0);
            }
        }

        var options = profilePath is not null ? FromJsonFile(profilePath) : new LoadTestOptions();
        ApplyCliOverrides(options, args);
        options.BaseUrl = options.BaseUrl.TrimEnd('/');
        return options;
    }

    public static LoadTestOptions FromJsonFile(string path)
    {
        var resolved = ResolveProfilePath(path);
        var json = File.ReadAllText(resolved);
        return System.Text.Json.JsonSerializer.Deserialize<LoadTestOptions>(json, JsonOptions)
               ?? new LoadTestOptions();
    }

    private static string ResolveProfilePath(string path)
    {
        if (File.Exists(path))
        {
            return path;
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Profiles", path),
            Path.Combine(AppContext.BaseDirectory, path),
            Path.Combine(AppContext.BaseDirectory, "Profiles", path + ".json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Profiles", path),
            Path.Combine(Directory.GetCurrentDirectory(), "Profiles", path + ".json"),
            Path.Combine(Directory.GetCurrentDirectory(), path)
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                return c;
            }
        }

        throw new FileNotFoundException($"Profil bulunamadı: {path}");
    }

    private static void ApplyCliOverrides(LoadTestOptions options, string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Eksik değer: {a}");

            switch (a)
            {
                case "--profile":
                case "-p":
                    _ = Next();
                    break;
                case "--base-url": options.BaseUrl = Next().TrimEnd('/'); break;
                case "--server-key": options.ServerKey = Next(); break;
                case "--region": options.Region = Next(); break;
                case "--scenario":
                case "-s": options.Scenario = Next(); break;
                case "--players":
                case "-n": options.VirtualPlayers = int.Parse(Next()); break;
                case "--concurrency":
                case "-c": options.MaxConcurrency = int.Parse(Next()); break;
                case "--think-ms": options.ThinkTimeMs = int.Parse(Next()); break;
                case "--ramp-target": options.RampTargetPlayers = int.Parse(Next()); break;
                case "--ramp-steps": options.RampSteps = int.Parse(Next()); break;
                case "--soak-minutes": options.SoakDurationMinutes = int.Parse(Next()); break;
                case "--soak-players": options.SoakPlayers = int.Parse(Next()); break;
                case "--spike-peak": options.SpikePeakPlayers = int.Parse(Next()); break;
                case "--coordinator-url": options.CoordinatorUrl = Next().TrimEnd('/'); break;
                case "--coordinator-port": options.CoordinatorPort = int.Parse(Next()); break;
                case "--worker-id": options.WorkerId = Next(); break;
                case "--player-offset": options.PlayerIdOffset = int.Parse(Next()); break;
                case "--expected-workers": options.ExpectedWorkers = int.Parse(Next()); break;
                case "--out": options.OutputDirectory = Next(); break;
                case "--run-id": options.RunId = Next(); break;
                case "--compare": options.CompareWithRunId = Next(); break;
                case "--no-html": options.GenerateHtml = false; break;
                case "--dry-run": options.DryRun = true; break;
                case "--slo-p95": options.Slo.P95MaxMs = double.Parse(Next()); break;
                case "--slo-p99": options.Slo.P99MaxMs = double.Parse(Next()); break;
                case "--slo-error": options.Slo.ErrorRateMax = double.Parse(Next()); break;
                case "--slo-rps": options.Slo.MinRps = double.Parse(Next()); break;
            }
        }
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
            HAREKÂT Yük Testi Aracı

            Kullanım:
              dotnet run --project Harekat.LoadTest -- [seçenekler]

            Seçenekler:
              --profile, -p <ad|yol>     Profil JSON (Profiles/ altında)
              --base-url <url>           API adresi (varsayılan http://localhost:5080)
              --server-key <key>         Maç sonucu için server key
              --scenario, -s <ad>        player|ramp|soak|spike|distributed-coordinator|distributed-worker
              --players, -n <n>          Sanal oyuncu sayısı
              --concurrency, -c <n>      Maks eşzamanlı istek
              --ramp-target <n>          Ramp hedefi (10k)
              --soak-minutes <n>         Soak süresi (120)
              --spike-peak <n>           Spike zirvesi
              --coordinator-url <url>    Dağıtık worker için koordinatör
              --coordinator-port <n>     Koordinatör dinleme portu
              --expected-workers <n>     Beklenen worker sayısı
              --out <dir>                Rapor dizini
              --run-id <id>              Koşu kimliği
              --compare <run-id>         Önceki koşu ile HTML karşılaştırma
              --slo-p95 / --slo-p99 / --slo-error / --slo-rps
              --dry-run                  Gerçek HTTP yok (metrik simülasyonu)
              --help, -h
            """);
    }
}
