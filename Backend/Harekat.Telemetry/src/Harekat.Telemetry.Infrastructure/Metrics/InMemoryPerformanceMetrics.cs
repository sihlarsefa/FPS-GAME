using System.Collections.Concurrent;
using Harekat.Telemetry.Application.Abstractions;

namespace Harekat.Telemetry.Infrastructure.Metrics;

public sealed class InMemoryPerformanceMetrics : IPerformanceMetrics
{
    private readonly ConcurrentQueue<double> _ingestionMs = new();
    private readonly ConcurrentQueue<double> _analysisMs = new();
    private long _events;
    private long _batches;

    public void RecordIngestion(int eventCount, TimeSpan elapsed)
    {
        Interlocked.Add(ref _events, eventCount);
        Interlocked.Increment(ref _batches);
        _ingestionMs.Enqueue(elapsed.TotalMilliseconds);
        Trim(_ingestionMs, 2000);
    }

    public void RecordAnalysis(TimeSpan elapsed)
    {
        _analysisMs.Enqueue(elapsed.TotalMilliseconds);
        Trim(_analysisMs, 2000);
    }

    public PerformanceSnapshot Snapshot()
    {
        var ing = _ingestionMs.ToArray();
        var ana = _analysisMs.ToArray();
        return new PerformanceSnapshot(
            Interlocked.Read(ref _events),
            Interlocked.Read(ref _batches),
            Avg(ing),
            Avg(ana),
            Percentile(ing, 0.95),
            Percentile(ana, 0.95));
    }

    private static void Trim(ConcurrentQueue<double> q, int max)
    {
        while (q.Count > max && q.TryDequeue(out _)) { }
    }

    private static double Avg(double[] values) => values.Length == 0 ? 0 : values.Average();

    private static double Percentile(double[] values, double p)
    {
        if (values.Length == 0) return 0;
        var sorted = values.OrderBy(x => x).ToArray();
        var idx = (int)Math.Clamp(Math.Ceiling(p * sorted.Length) - 1, 0, sorted.Length - 1);
        return Math.Round(sorted[idx], 3);
    }
}
