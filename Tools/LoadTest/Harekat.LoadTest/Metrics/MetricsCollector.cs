using System.Collections.Concurrent;
using System.Diagnostics;

namespace Harekat.LoadTest.Metrics;

public readonly record struct TimedResult(
    string Operation,
    long ElapsedMs,
    bool Success,
    int StatusCode,
    string? Error = null);

/// <summary>
/// Thread-safe gecikme örnekleri; yüzdelik hesaplama için.
/// </summary>
public sealed class LatencyStore
{
    private readonly List<long> _samples = new(capacity: 65_536);
    private readonly object _gate = new();

    public void Add(long elapsedMs)
    {
        lock (_gate)
        {
            _samples.Add(elapsedMs);
        }
    }

    public void AddRange(IEnumerable<long> values)
    {
        lock (_gate)
        {
            _samples.AddRange(values);
        }
    }

    public IReadOnlyList<long> Snapshot()
    {
        lock (_gate)
        {
            return _samples.ToArray();
        }
    }

    public int Count
    {
        get
        {
            lock (_gate) return _samples.Count;
        }
    }

    public LatencyPercentiles Compute()
    {
        long[] copy;
        lock (_gate)
        {
            if (_samples.Count == 0)
            {
                return LatencyPercentiles.Empty;
            }

            copy = _samples.ToArray();
        }

        Array.Sort(copy);
        return new LatencyPercentiles(
            Count: copy.Length,
            MinMs: copy[0],
            MaxMs: copy[^1],
            MeanMs: copy.Average(),
            P50Ms: Percentile(copy, 0.50),
            P95Ms: Percentile(copy, 0.95),
            P99Ms: Percentile(copy, 0.99));
    }

    internal static double Percentile(long[] sortedAscending, double p)
    {
        if (sortedAscending.Length == 0)
        {
            return 0;
        }

        if (sortedAscending.Length == 1)
        {
            return sortedAscending[0];
        }

        var rank = p * (sortedAscending.Length - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        if (lo == hi)
        {
            return sortedAscending[lo];
        }

        var weight = rank - lo;
        return sortedAscending[lo] * (1 - weight) + sortedAscending[hi] * weight;
    }
}

public readonly record struct LatencyPercentiles(
    int Count,
    double MinMs,
    double MaxMs,
    double MeanMs,
    double P50Ms,
    double P95Ms,
    double P99Ms)
{
    public static LatencyPercentiles Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);
}

public sealed class OperationMetrics
{
    private long _success;
    private long _failure;
    public LatencyStore Latency { get; } = new();

    public void Record(TimedResult result)
    {
        Latency.Add(result.ElapsedMs);
        if (result.Success)
        {
            Interlocked.Increment(ref _success);
        }
        else
        {
            Interlocked.Increment(ref _failure);
        }
    }

    public void AddSuccess(long count = 1) => Interlocked.Add(ref _success, count);
    public void AddFailure(long count = 1) => Interlocked.Add(ref _failure, count);

    public long SuccessCount => Interlocked.Read(ref _success);
    public long FailureCount => Interlocked.Read(ref _failure);
    public long TotalCount => SuccessCount + FailureCount;

    public double ErrorRate => TotalCount == 0 ? 0 : (double)FailureCount / TotalCount;
}

/// <summary>
/// Tüm işlemlerin metriklerini toplar; RPS ve özet üretir.
/// </summary>
public sealed class MetricsCollector
{
    private readonly ConcurrentDictionary<string, OperationMetrics> _ops = new(StringComparer.OrdinalIgnoreCase);
    private readonly LatencyStore _overall = new();
    private long _success;
    private long _failure;
    private long _startedTicks = Stopwatch.GetTimestamp();
    private long _endedTicks;

    public void MarkStart() => _startedTicks = Stopwatch.GetTimestamp();

    public void MarkEnd() => _endedTicks = Stopwatch.GetTimestamp();

    public void Record(TimedResult result)
    {
        var op = _ops.GetOrAdd(result.Operation, static _ => new OperationMetrics());
        op.Record(result);
        _overall.Add(result.ElapsedMs);
        if (result.Success)
        {
            Interlocked.Increment(ref _success);
        }
        else
        {
            Interlocked.Increment(ref _failure);
        }
    }

    public void Merge(MetricsCollector other)
    {
        foreach (var (name, metrics) in other._ops)
        {
            var dest = _ops.GetOrAdd(name, static _ => new OperationMetrics());
            foreach (var sample in metrics.Latency.Snapshot())
            {
                dest.Latency.Add(sample);
                _overall.Add(sample);
            }

            dest.AddSuccess(metrics.SuccessCount);
            dest.AddFailure(metrics.FailureCount);
            Interlocked.Add(ref _success, metrics.SuccessCount);
            Interlocked.Add(ref _failure, metrics.FailureCount);
        }
    }

    public RunSummary BuildSummary(string runId, string scenario, TimeSpan? wallClock = null)
    {
        if (_endedTicks == 0)
        {
            MarkEnd();
        }

        var elapsed = wallClock ?? Stopwatch.GetElapsedTime(_startedTicks, _endedTicks);
        if (elapsed <= TimeSpan.Zero)
        {
            elapsed = TimeSpan.FromMilliseconds(1);
        }

        var total = Interlocked.Read(ref _success) + Interlocked.Read(ref _failure);
        var overall = _overall.Compute();
        var perOp = _ops.ToDictionary(
            kv => kv.Key,
            kv =>
            {
                var lat = kv.Value.Latency.Compute();
                return new OperationSummary(
                    kv.Key,
                    kv.Value.TotalCount,
                    kv.Value.SuccessCount,
                    kv.Value.FailureCount,
                    kv.Value.ErrorRate,
                    lat);
            },
            StringComparer.OrdinalIgnoreCase);

        return new RunSummary(
            RunId: runId,
            Scenario: scenario,
            StartedUtc: DateTime.UtcNow - elapsed,
            EndedUtc: DateTime.UtcNow,
            DurationSeconds: elapsed.TotalSeconds,
            TotalRequests: total,
            SuccessCount: Interlocked.Read(ref _success),
            FailureCount: Interlocked.Read(ref _failure),
            ErrorRate: total == 0 ? 0 : (double)Interlocked.Read(ref _failure) / total,
            RequestsPerSecond: total / elapsed.TotalSeconds,
            Overall: overall,
            Operations: perOp);
    }
}

public sealed record OperationSummary(
    string Name,
    long Total,
    long Success,
    long Failure,
    double ErrorRate,
    LatencyPercentiles Latency);

public sealed record RunSummary(
    string RunId,
    string Scenario,
    DateTime StartedUtc,
    DateTime EndedUtc,
    double DurationSeconds,
    long TotalRequests,
    long SuccessCount,
    long FailureCount,
    double ErrorRate,
    double RequestsPerSecond,
    LatencyPercentiles Overall,
    IReadOnlyDictionary<string, OperationSummary> Operations);
