using Harekat.Telemetry.Domain.Entities;
using Harekat.Telemetry.Domain.Enums;

namespace Harekat.Telemetry.Application.Abstractions;

public interface IEventStore
{
    Task AppendBatchAsync(IReadOnlyList<MatchEvent> events, CancellationToken ct = default);
    Task<IReadOnlyList<MatchEvent>> GetByMatchAsync(string matchId, CancellationToken ct = default);
    Task<IReadOnlyList<MatchEvent>> GetByPlayerAsync(string playerId, CancellationToken ct = default);
    Task<IReadOnlyList<MatchEvent>> GetAllAsync(CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}

public interface ISuspicionReportStore
{
    Task SaveAsync(PlayerSuspicionReport report, CancellationToken ct = default);
    Task<IReadOnlyList<PlayerSuspicionReport>> ListAsync(double? minScore = null, CancellationToken ct = default);
    Task<PlayerSuspicionReport?> GetAsync(string playerId, string matchId, CancellationToken ct = default);
}

public interface IEventStreamPublisher
{
    string BackendName { get; }
    Task PublishAsync(string streamKey, MatchEvent evt, CancellationToken ct = default);
    Task PublishBatchAsync(string streamKey, IReadOnlyList<MatchEvent> events, CancellationToken ct = default);
}

public interface IRuleProvider
{
    DetectionRulesSnapshot GetCurrent();
    event Action? RulesChanged;
}

public sealed record DetectionRulesSnapshot(
    string Version,
    DateTimeOffset LoadedAt,
    IReadOnlyList<Harekat.Telemetry.Application.Rules.DetectionRuleDefinition> Rules);

public interface IRiskScoreStore
{
    Task AppendAsync(RiskScoreSample sample, CancellationToken ct = default);
    Task<IReadOnlyList<RiskScoreSample>> GetSeriesAsync(string playerId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default);
    Task<double> GetLatestAsync(string playerId, CancellationToken ct = default);
}

public interface IReviewQueue
{
    Task EnqueueAsync(ReviewQueueItem item, CancellationToken ct = default);
    Task<IReadOnlyList<ReviewQueueItem>> ListAsync(ReviewQueueStatus? status = null, CancellationToken ct = default);
    Task<ReviewQueueItem?> UpdateStatusAsync(Guid id, ReviewQueueStatus status, string? notes, CancellationToken ct = default);
}

public interface IReplayStore
{
    Task SaveCompressedAsync(MatchReplay replay, CancellationToken ct = default);
    Task<MatchReplay?> LoadAsync(string matchId, CancellationToken ct = default);
    Task<long> GetCompressedSizeAsync(string matchId, CancellationToken ct = default);
}

public interface IHeatmapImageRenderer
{
    byte[] RenderPng(IReadOnlyList<HeatmapCell> cells, HeatmapRenderOptions options);
}

public sealed record HeatmapRenderOptions(
    int PixelSize = 512,
    bool Deaths = true,
    bool Landings = true,
    string SeasonId = "current");

public interface IPerformanceMetrics
{
    void RecordIngestion(int eventCount, TimeSpan elapsed);
    void RecordAnalysis(TimeSpan elapsed);
    PerformanceSnapshot Snapshot();
}

public sealed record PerformanceSnapshot(
    long TotalEventsIngested,
    long TotalBatches,
    double AvgIngestionMs,
    double AvgAnalysisMs,
    double P95IngestionMs,
    double P95AnalysisMs);
