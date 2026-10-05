using System.Collections.Concurrent;
using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Domain.Entities;

namespace Harekat.Telemetry.Infrastructure.Storage;

public sealed class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<(string MatchId, Guid Id), MatchEvent> _events = new();

    public Task AppendBatchAsync(IReadOnlyList<MatchEvent> events, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var e in events)
        {
            ct.ThrowIfCancellationRequested();
            _events[(e.MatchId, e.Id)] = e;
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MatchEvent>> GetByMatchAsync(string matchId, CancellationToken ct = default)
    {
        var list = _events.Values
            .Where(e => string.Equals(e.MatchId, matchId, StringComparison.Ordinal))
            .OrderBy(e => e.Timestamp)
            .ToList();
        return Task.FromResult<IReadOnlyList<MatchEvent>>(list);
    }

    public Task<IReadOnlyList<MatchEvent>> GetByPlayerAsync(string playerId, CancellationToken ct = default)
    {
        var list = _events.Values
            .Where(e => string.Equals(e.PlayerId, playerId, StringComparison.Ordinal))
            .OrderBy(e => e.Timestamp)
            .ToList();
        return Task.FromResult<IReadOnlyList<MatchEvent>>(list);
    }

    public Task<IReadOnlyList<MatchEvent>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MatchEvent>>(_events.Values.OrderBy(e => e.Timestamp).ToList());

    public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(_events.Count);
}

public sealed class InMemorySuspicionReportStore : ISuspicionReportStore
{
    private readonly ConcurrentDictionary<(string PlayerId, string MatchId), PlayerSuspicionReport> _reports = new();

    public Task SaveAsync(PlayerSuspicionReport report, CancellationToken ct = default)
    {
        _reports[(report.PlayerId, report.MatchId)] = report;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PlayerSuspicionReport>> ListAsync(double? minScore = null, CancellationToken ct = default)
    {
        IEnumerable<PlayerSuspicionReport> q = _reports.Values;
        if (minScore is double min)
            q = q.Where(r => r.TotalScore >= min);
        return Task.FromResult<IReadOnlyList<PlayerSuspicionReport>>(
            q.OrderByDescending(r => r.TotalScore).ToList());
    }

    public Task<PlayerSuspicionReport?> GetAsync(string playerId, string matchId, CancellationToken ct = default)
    {
        _reports.TryGetValue((playerId, matchId), out var r);
        return Task.FromResult(r);
    }
}

public sealed class InMemoryRiskScoreStore : IRiskScoreStore
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<RiskScoreSample>> _series = new(StringComparer.Ordinal);

    public Task AppendAsync(RiskScoreSample sample, CancellationToken ct = default)
    {
        var bag = _series.GetOrAdd(sample.PlayerId, _ => []);
        bag.Add(sample);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RiskScoreSample>> GetSeriesAsync(string playerId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default)
    {
        if (!_series.TryGetValue(playerId, out var bag))
            return Task.FromResult<IReadOnlyList<RiskScoreSample>>([]);

        IEnumerable<RiskScoreSample> q = bag;
        if (from is DateTimeOffset f) q = q.Where(s => s.Timestamp >= f);
        if (to is DateTimeOffset t) q = q.Where(s => s.Timestamp <= t);
        return Task.FromResult<IReadOnlyList<RiskScoreSample>>(q.OrderBy(s => s.Timestamp).ToList());
    }

    public async Task<double> GetLatestAsync(string playerId, CancellationToken ct = default)
    {
        var series = await GetSeriesAsync(playerId, ct: ct).ConfigureAwait(false);
        return series.Count == 0 ? 0 : series[^1].Score;
    }
}

public sealed class InMemoryReviewQueue : IReviewQueue
{
    private readonly ConcurrentDictionary<Guid, ReviewQueueItem> _items = new();

    public Task EnqueueAsync(ReviewQueueItem item, CancellationToken ct = default)
    {
        // Aynı oyuncu+maç için tek kayıt
        var existing = _items.Values.FirstOrDefault(i =>
            i.PlayerId == item.PlayerId && i.MatchId == item.MatchId &&
            i.Status == Domain.Enums.ReviewQueueStatus.Pending);
        if (existing is not null)
        {
            existing.RiskScore = Math.Max(existing.RiskScore, item.RiskScore);
            return Task.CompletedTask;
        }
        _items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReviewQueueItem>> ListAsync(Domain.Enums.ReviewQueueStatus? status = null, CancellationToken ct = default)
    {
        IEnumerable<ReviewQueueItem> q = _items.Values;
        if (status is { } s) q = q.Where(i => i.Status == s);
        return Task.FromResult<IReadOnlyList<ReviewQueueItem>>(
            q.OrderByDescending(i => i.RiskScore).ThenBy(i => i.EnqueuedAt).ToList());
    }

    public Task<ReviewQueueItem?> UpdateStatusAsync(Guid id, Domain.Enums.ReviewQueueStatus status, string? notes, CancellationToken ct = default)
    {
        if (!_items.TryGetValue(id, out var item))
            return Task.FromResult<ReviewQueueItem?>(null);
        item.Status = status;
        item.Notes = notes;
        return Task.FromResult<ReviewQueueItem?>(item);
    }
}
