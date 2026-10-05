using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Application.Contracts;
using Harekat.Telemetry.Application.Rules;
using Harekat.Telemetry.Domain.Catalogs;
using Harekat.Telemetry.Domain.Constants;
using Harekat.Telemetry.Domain.Entities;
using Harekat.Telemetry.Domain.Enums;
using Harekat.Telemetry.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harekat.Telemetry.Application.Services;

public sealed class EventIngestionService
{
    public const int MaxBatchSize = 5000;
    private const string StreamKey = "harekat:match-events";

    private readonly IEventStore _store;
    private readonly IEventStreamPublisher _stream;
    private readonly SuspicionAnalysisService _analysis;
    private readonly IPerformanceMetrics _metrics;
    private readonly ILogger<EventIngestionService> _logger;

    public EventIngestionService(
        IEventStore store,
        IEventStreamPublisher stream,
        SuspicionAnalysisService analysis,
        IPerformanceMetrics metrics,
        ILogger<EventIngestionService> logger)
    {
        _store = store;
        _stream = stream;
        _analysis = analysis;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<EventBatchResponse> IngestAsync(EventBatchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.MatchId))
            throw new ArgumentException("MatchId zorunludur.", nameof(request));

        if (request.Events is null || request.Events.Count == 0)
            throw new ArgumentException("Events boş olamaz.", nameof(request));

        if (request.Events.Count > MaxBatchSize)
            throw new ArgumentException($"Batch boyutu {MaxBatchSize} olaydan fazla olamaz.", nameof(request));

        var accepted = new List<MatchEvent>(request.Events.Count);
        for (var i = 0; i < request.Events.Count; i++)
        {
            var dto = request.Events[i];
            if (!TryMap(request.MatchId, dto, i, out var evt, out var err))
            {
                errors.Add(err!);
                continue;
            }
            accepted.Add(evt!);
        }

        if (accepted.Count > 0)
        {
            await _store.AppendBatchAsync(accepted, ct).ConfigureAwait(false);
            await _stream.PublishBatchAsync(StreamKey, accepted, ct).ConfigureAwait(false);
        }

        sw.Stop();
        _metrics.RecordIngestion(accepted.Count, sw.Elapsed);

        var summaries = await _analysis.AnalyzeMatchAsync(request.MatchId, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Batch alındı match={MatchId} accepted={Accepted} rejected={Rejected} stream={Stream}",
            request.MatchId, accepted.Count, errors.Count, _stream.BackendName);

        return new EventBatchResponse
        {
            MatchId = request.MatchId,
            Accepted = accepted.Count,
            Rejected = errors.Count,
            Errors = errors,
            SuspicionSummaries = summaries
        };
    }

    private static bool TryMap(string matchId, MatchEventDto dto, int index, out MatchEvent? evt, out string? error)
    {
        evt = null;
        error = null;

        if (dto is null)
        {
            error = $"[{index}] olay null";
            return false;
        }

        if (string.IsNullOrWhiteSpace(dto.PlayerId))
        {
            error = $"[{index}] PlayerId zorunlu";
            return false;
        }

        if (!Enum.IsDefined(dto.EventType))
        {
            error = $"[{index}] geçersiz EventType";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(dto.WeaponId) && !WeaponIds.IsKnown(dto.WeaponId))
        {
            error = $"[{index}] bilinmeyen WeaponId: {dto.WeaponId}";
            return false;
        }

        WorldPosition? pos = null;
        if (dto.X is not null || dto.Y is not null || dto.Z is not null)
        {
            if (dto.X is null || dto.Y is null || dto.Z is null)
            {
                error = $"[{index}] konum X/Y/Z birlikte verilmeli";
                return false;
            }

            if (!MapBounds.Contains(dto.X.Value, dto.Z.Value) &&
                dto.EventType is MatchEventType.Death or MatchEventType.Landing)
            {
                error = $"[{index}] konum harita dışında ({dto.X},{dto.Z})";
                return false;
            }

            pos = new WorldPosition(dto.X.Value, dto.Y.Value, dto.Z.Value);
        }

        WorldPosition? target = null;
        if (dto.TargetX is not null || dto.TargetY is not null || dto.TargetZ is not null)
        {
            if (dto.TargetX is null || dto.TargetY is null || dto.TargetZ is null)
            {
                error = $"[{index}] hedef konum TargetX/Y/Z birlikte verilmeli";
                return false;
            }
            target = new WorldPosition(dto.TargetX.Value, dto.TargetY.Value, dto.TargetZ.Value);
        }

        evt = new MatchEvent
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            PlayerId = dto.PlayerId.Trim(),
            EventType = dto.EventType,
            Timestamp = dto.Timestamp ?? DateTimeOffset.UtcNow,
            Position = pos,
            TargetPosition = target,
            TargetPlayerId = dto.TargetPlayerId,
            WeaponId = dto.WeaponId,
            IsHeadshot = dto.IsHeadshot,
            ThroughWall = dto.ThroughWall,
            Damage = dto.Damage,
            TimeToKillMs = dto.TimeToKillMs,
            DistanceMeters = dto.DistanceMeters
        };
        return true;
    }
}
