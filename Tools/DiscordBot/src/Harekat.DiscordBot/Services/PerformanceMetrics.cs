using System.Diagnostics;

namespace Harekat.DiscordBot.Services;

/// <summary>Basit performans ölçümü — komut ve servis süreleri.</summary>
public sealed class PerformanceMetrics
{
    private long _operationCount;
    private long _totalElapsedTicks;
    private long _errorCount;
    private readonly object _gate = new();
    private readonly Dictionary<string, OperationStats> _byName = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string operationName, TimeSpan elapsed, bool success = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        lock (_gate)
        {
            Interlocked.Increment(ref _operationCount);
            Interlocked.Add(ref _totalElapsedTicks, elapsed.Ticks);
            if (!success)
                Interlocked.Increment(ref _errorCount);

            if (!_byName.TryGetValue(operationName, out var stats))
            {
                stats = new OperationStats(operationName);
                _byName[operationName] = stats;
            }

            stats.Record(elapsed, success);
        }
    }

    public PerformanceSnapshot Snapshot()
    {
        lock (_gate)
        {
            var count = _operationCount;
            var avgMs = count == 0
                ? 0
                : TimeSpan.FromTicks(_totalElapsedTicks / count).TotalMilliseconds;

            return new PerformanceSnapshot(
                count,
                _errorCount,
                avgMs,
                _byName.Values.Select(s => s.Snapshot()).OrderByDescending(s => s.Count).ToArray());
        }
    }

    public async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> action)
    {
        var sw = Stopwatch.StartNew();
        var success = false;
        try
        {
            var result = await action().ConfigureAwait(false);
            success = true;
            return result;
        }
        finally
        {
            sw.Stop();
            Record(operationName, sw.Elapsed, success);
        }
    }

    public async Task MeasureAsync(string operationName, Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        var success = false;
        try
        {
            await action().ConfigureAwait(false);
            success = true;
        }
        finally
        {
            sw.Stop();
            Record(operationName, sw.Elapsed, success);
        }
    }

    private sealed class OperationStats(string name)
    {
        private long _count;
        private long _errors;
        private long _totalTicks;
        private long _maxTicks;

        public void Record(TimeSpan elapsed, bool success)
        {
            _count++;
            _totalTicks += elapsed.Ticks;
            if (elapsed.Ticks > _maxTicks)
                _maxTicks = elapsed.Ticks;
            if (!success)
                _errors++;
        }

        public OperationSnapshot Snapshot() =>
            new(
                name,
                _count,
                _errors,
                _count == 0 ? 0 : TimeSpan.FromTicks(_totalTicks / _count).TotalMilliseconds,
                TimeSpan.FromTicks(_maxTicks).TotalMilliseconds);
    }
}

public sealed record PerformanceSnapshot(
    long TotalOperations,
    long ErrorCount,
    double AverageMs,
    IReadOnlyList<OperationSnapshot> Operations);

public sealed record OperationSnapshot(
    string Name,
    long Count,
    long Errors,
    double AverageMs,
    double MaxMs);
