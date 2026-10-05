using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Application.Contracts;
using Harekat.Telemetry.Domain.Entities;
using Harekat.Telemetry.Domain.Enums;
using Harekat.Telemetry.Domain.ValueObjects;

namespace Harekat.Telemetry.Application.Services;

public sealed class RiskScoreQueryService
{
    private readonly IRiskScoreStore _store;
    private readonly IReviewQueue _queue;

    public RiskScoreQueryService(IRiskScoreStore store, IReviewQueue queue)
    {
        _store = store;
        _queue = queue;
    }

    public async Task<RiskSeriesDto> GetSeriesAsync(string playerId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("playerId zorunlu", nameof(playerId));

        var points = await _store.GetSeriesAsync(playerId, from, to, ct).ConfigureAwait(false);
        return new RiskSeriesDto
        {
            PlayerId = playerId,
            Points = points.Select(p => new RiskPointDto
            {
                Timestamp = p.Timestamp,
                Score = p.Score,
                MatchId = p.MatchId,
                Reason = p.Reason
            }).ToList()
        };
    }

    public async Task<IReadOnlyList<ReviewQueueItemDto>> ListQueueAsync(ReviewQueueStatus? status = null, CancellationToken ct = default)
    {
        var items = await _queue.ListAsync(status, ct).ConfigureAwait(false);
        return items.Select(Map).ToList();
    }

    public async Task<ReviewQueueItemDto?> UpdateQueueAsync(Guid id, UpdateReviewStatusRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await _queue.UpdateStatusAsync(id, request.Status, request.Notes, ct).ConfigureAwait(false);
        return item is null ? null : Map(item);
    }

    private static ReviewQueueItemDto Map(ReviewQueueItem i) => new()
    {
        Id = i.Id,
        PlayerId = i.PlayerId,
        MatchId = i.MatchId,
        RiskScore = i.RiskScore,
        EnqueuedAt = i.EnqueuedAt,
        Status = i.Status.ToString(),
        Notes = i.Notes
    };
}

public sealed class ReplayService
{
    private readonly IReplayStore _store;

    public ReplayService(IReplayStore store) => _store = store;

    public async Task<ReplayMetaDto> SaveAsync(ReplaySaveRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.MatchId))
            throw new ArgumentException("MatchId zorunlu", nameof(request));
        if (request.Frames is null || request.Frames.Count == 0)
            throw new ArgumentException("Frames boş olamaz", nameof(request));

        var replay = new MatchReplay
        {
            MatchId = request.MatchId,
            MapId = string.IsNullOrWhiteSpace(request.MapId) ? "kuzgun_vadisi" : request.MapId,
            StartedAt = request.StartedAt ?? DateTimeOffset.UtcNow,
            Frames = request.Frames.Select(f => new ReplayFrame
            {
                Sequence = f.Sequence,
                TimeSeconds = f.TimeSeconds,
                PlayerId = f.PlayerId,
                Position = new WorldPosition(f.X, f.Y, f.Z),
                Yaw = f.Yaw,
                Pitch = f.Pitch,
                EventHint = f.EventHint,
                WeaponId = f.WeaponId
            }).ToList()
        };

        await _store.SaveCompressedAsync(replay, ct).ConfigureAwait(false);
        var size = await _store.GetCompressedSizeAsync(request.MatchId, ct).ConfigureAwait(false);
        return new ReplayMetaDto
        {
            MatchId = replay.MatchId,
            FormatVersion = replay.FormatVersion,
            FrameCount = replay.Frames.Count,
            CompressedBytes = size
        };
    }

    public async Task<MatchReplay?> LoadAsync(string matchId, CancellationToken ct = default) =>
        await _store.LoadAsync(matchId, ct).ConfigureAwait(false);
}
