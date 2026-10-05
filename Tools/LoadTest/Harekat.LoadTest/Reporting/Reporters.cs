using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harekat.LoadTest.Metrics;
using Harekat.LoadTest.Options;

namespace Harekat.LoadTest.Reporting;

public static class ConsoleReporter
{
    public static void Print(RunSummary summary, SloEvaluation? slo = null)
    {
        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════");
        Console.WriteLine($"  HAREKÂT Yük Testi Raporu  |  {summary.RunId}  |  {summary.Scenario}");
        Console.WriteLine("══════════════════════════════════════════════════════════");
        Console.WriteLine($"  Süre           : {summary.DurationSeconds:F2} s");
        Console.WriteLine($"  Toplam istek   : {summary.TotalRequests}");
        Console.WriteLine($"  Başarılı       : {summary.SuccessCount}");
        Console.WriteLine($"  Hatalı         : {summary.FailureCount}");
        Console.WriteLine($"  Hata oranı     : {summary.ErrorRate:P2}");
        Console.WriteLine($"  RPS            : {summary.RequestsPerSecond:F2}");
        Console.WriteLine("──────────────────────────────────────────────────────────");
        Console.WriteLine("  Genel gecikme (ms)");
        Console.WriteLine($"    p50={summary.Overall.P50Ms:F1}  p95={summary.Overall.P95Ms:F1}  p99={summary.Overall.P99Ms:F1}");
        Console.WriteLine($"    min={summary.Overall.MinMs:F1}  mean={summary.Overall.MeanMs:F1}  max={summary.Overall.MaxMs:F1}");
        Console.WriteLine("──────────────────────────────────────────────────────────");
        Console.WriteLine("  İşlem başına:");
        foreach (var op in summary.Operations.Values.OrderBy(o => o.Name))
        {
            Console.WriteLine(
                $"    {op.Name,-20} n={op.Total,6} err={op.ErrorRate,7:P2}  " +
                $"p50={op.Latency.P50Ms,6:F0} p95={op.Latency.P95Ms,6:F0} p99={op.Latency.P99Ms,6:F0}");
        }

        if (slo is not null)
        {
            Console.WriteLine("──────────────────────────────────────────────────────────");
            Console.WriteLine($"  SLO: {(slo.Passed ? "GEÇTİ ✓" : "KALDI ✗")}");
            foreach (var c in slo.Checks)
            {
                Console.WriteLine($"    {(c.Passed ? "[OK]" : "[X ]")} {c.Name}: {c.Actual:F2} (eşik {c.Threshold:F2}) — {c.Detail}");
            }
        }

        Console.WriteLine("══════════════════════════════════════════════════════════");
        Console.WriteLine();
    }
}

public static class CsvReporter
{
    public static string Write(RunSummary summary, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{summary.RunId}.csv");
        var sb = new StringBuilder();
        sb.AppendLine("run_id,scenario,operation,total,success,failure,error_rate,rps_share,p50_ms,p95_ms,p99_ms,min_ms,mean_ms,max_ms,duration_s,overall_rps,overall_error_rate");

        void Row(string op, long total, long success, long failure, double err, LatencyPercentiles lat)
        {
            sb.Append(CultureInfo.InvariantCulture,
                $"{summary.RunId},{summary.Scenario},{op},{total},{success},{failure},{err:F6},");
            sb.Append(CultureInfo.InvariantCulture,
                $"{(summary.DurationSeconds > 0 ? total / summary.DurationSeconds : 0):F4},");
            sb.Append(CultureInfo.InvariantCulture,
                $"{lat.P50Ms:F3},{lat.P95Ms:F3},{lat.P99Ms:F3},{lat.MinMs:F3},{lat.MeanMs:F3},{lat.MaxMs:F3},");
            sb.Append(CultureInfo.InvariantCulture,
                $"{summary.DurationSeconds:F3},{summary.RequestsPerSecond:F3},{summary.ErrorRate:F6}");
            sb.AppendLine();
        }

        Row("_overall", summary.TotalRequests, summary.SuccessCount, summary.FailureCount, summary.ErrorRate, summary.Overall);
        foreach (var op in summary.Operations.Values.OrderBy(o => o.Name))
        {
            Row(op.Name, op.Total, op.Success, op.Failure, op.ErrorRate, op.Latency);
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }
}

public static class JsonRunStore
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Save(RunSummary summary, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{summary.RunId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(summary, Opts), Encoding.UTF8);
        return path;
    }

    public static RunSummary? Load(string directory, string runId)
    {
        var path = Path.Combine(directory, $"{runId}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RunSummary>(File.ReadAllText(path), Opts);
    }
}

public sealed record SloCheck(string Name, double Actual, double Threshold, bool Passed, string Detail);

public sealed record SloEvaluation(bool Passed, IReadOnlyList<SloCheck> Checks);

public static class SloEvaluator
{
    public static SloEvaluation Evaluate(RunSummary summary, SloThresholds thresholds)
    {
        var checks = new List<SloCheck>
        {
            Check("p50_ms", summary.Overall.P50Ms, thresholds.P50MaxMs, lowerIsBetter: true),
            Check("p95_ms", summary.Overall.P95Ms, thresholds.P95MaxMs, lowerIsBetter: true),
            Check("p99_ms", summary.Overall.P99Ms, thresholds.P99MaxMs, lowerIsBetter: true),
            Check("error_rate", summary.ErrorRate, thresholds.ErrorRateMax, lowerIsBetter: true),
            Check("rps", summary.RequestsPerSecond, thresholds.MinRps, lowerIsBetter: false)
        };

        return new SloEvaluation(checks.All(c => c.Passed), checks);
    }

    private static SloCheck Check(string name, double actual, double threshold, bool lowerIsBetter)
    {
        var passed = lowerIsBetter ? actual <= threshold : actual >= threshold;
        var cmp = lowerIsBetter ? "≤" : "≥";
        return new SloCheck(name, actual, threshold, passed, $"{actual:F2} {cmp} {threshold:F2}");
    }
}

/// <summary>
/// Darboğaz analizi — yavaş/hatalı uç noktalara göre rehber üretir.
/// </summary>
public static class BottleneckGuide
{
    public static IReadOnlyList<string> Analyze(RunSummary summary, SloEvaluation slo)
    {
        var tips = new List<string>();

        if (summary.Overall.P95Ms > 250)
        {
            tips.Add("p95 yüksek: API/DB bağlantı havuzu, EF N+1 ve Redis kuyruk gecikmesini kontrol edin.");
        }

        if (summary.Overall.P99Ms > 500)
        {
            tips.Add("p99 kuyruk şişmesi: GC basıncı, kilitlenmeler veya yavaş disk (SQLite) olabilir; PostgreSQL + connection pooling deneyin.");
        }

        if (summary.ErrorRate > 0.01)
        {
            tips.Add("Hata oranı > %1: 429/503 için rate limit ve HPA; 5xx için uygulama logları ve /health.");
        }

        if (summary.RequestsPerSecond < 50 && summary.TotalRequests > 100)
        {
            tips.Add("RPS düşük: istemci concurrency artırın; sunucu tarafında CPU/thread pool doygunluğunu izleyin.");
        }

        foreach (var op in summary.Operations.Values.OrderByDescending(o => o.Latency.P95Ms))
        {
            if (op.Latency.P95Ms < summary.Overall.P95Ms * 1.2 && op.ErrorRate < 0.02)
            {
                continue;
            }

            tips.Add(op.Name switch
            {
                "register" or "login" => $"'{op.Name}' yavaş/hatalı: PBKDF2 iterasyonları ve kullanıcı unique index'ini gözden geçirin.",
                "squad_create" or "squad_join" => $"'{op.Name}': tim kilidi / concurrent join yarışları; optimistic concurrency veya Redis lock.",
                "matchmaking_queue" => "'matchmaking_queue': kuyruk pop/push atomikliği (Redis LIST/ZSET) ve bot doldurma maliyeti.",
                "match_result" => "'match_result': XP/rütbe güncellemesi transaction süresi; batch yazma veya outbox.",
                "players_me" => "'players_me': JWT doğrulama ve profil cache (Redis).",
                _ => $"'{op.Name}': p95={op.Latency.P95Ms:F0}ms err={op.ErrorRate:P1} — uç nokta özel profil alın."
            });
        }

        if (!slo.Passed)
        {
            tips.Add("SLO kaldı: başarısız eşikleri CSV/HTML raporda işaretli. Önce en yüksek p95'li işlemi optimize edin.");
        }

        if (tips.Count == 0)
        {
            tips.Add("Belirgin darboğaz yok; SLO geçti. Kapasite için ramp/soak/spike senaryolarını tekrarlayın.");
        }

        return tips;
    }

    public static void Print(IReadOnlyList<string> tips)
    {
        Console.WriteLine("── Darboğaz Rehberi ──────────────────────────────────────");
        for (var i = 0; i < tips.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {tips[i]}");
        }

        Console.WriteLine();
    }
}
