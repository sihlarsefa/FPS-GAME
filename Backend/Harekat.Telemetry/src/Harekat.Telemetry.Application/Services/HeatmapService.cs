using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Application.Contracts;
using Harekat.Telemetry.Domain.Constants;
using Harekat.Telemetry.Domain.Entities;
using Harekat.Telemetry.Domain.Enums;

namespace Harekat.Telemetry.Application.Services;

public sealed class HeatmapService
{
    private readonly IEventStore _store;
    private readonly IHeatmapImageRenderer _renderer;

    public HeatmapService(IEventStore store, IHeatmapImageRenderer renderer)
    {
        _store = store;
        _renderer = renderer;
    }

    public async Task<HeatmapResponse> GetHeatmapAsync(string matchId, float cellSize = MapBounds.HeatmapGridMeters, CancellationToken ct = default)
    {
        if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
        var events = await _store.GetByMatchAsync(matchId, ct).ConfigureAwait(false);
        var cells = BuildCells(events, cellSize);
        return new HeatmapResponse
        {
            MatchId = matchId,
            CellSizeMeters = cellSize,
            GridWidth = MapBounds.GridDimension(cellSize),
            Cells = cells.Select(c => new HeatmapCellDto
            {
                GridX = c.GridX,
                GridZ = c.GridZ,
                Deaths = c.DeathCount,
                Landings = c.LandingCount
            }).OrderBy(c => c.GridZ).ThenBy(c => c.GridX).ToList()
        };
    }

    public async Task<byte[]> RenderPngAsync(
        string? matchId,
        bool deaths = true,
        bool landings = true,
        string seasonId = "current",
        int pixelSize = 512,
        CancellationToken ct = default)
    {
        IReadOnlyList<MatchEvent> events = matchId is null
            ? await _store.GetAllAsync(ct).ConfigureAwait(false)
            : await _store.GetByMatchAsync(matchId, ct).ConfigureAwait(false);

        var cells = BuildCells(events, MapBounds.HeatmapGridMeters);
        return _renderer.RenderPng(cells, new HeatmapRenderOptions(pixelSize, deaths, landings, seasonId));
    }

    public static IReadOnlyList<HeatmapCell> BuildCells(IEnumerable<MatchEvent> events, float cellSize)
    {
        var map = new Dictionary<(int, int), (int Deaths, int Landings)>();
        foreach (var e in events)
        {
            if (e.Position is null) continue;
            if (e.EventType is not (MatchEventType.Death or MatchEventType.Landing)) continue;
            var (gx, gz) = MapBounds.ToGrid(e.Position.Value.X, e.Position.Value.Z, cellSize);
            map.TryGetValue((gx, gz), out var cur);
            if (e.EventType == MatchEventType.Death)
                map[(gx, gz)] = (cur.Deaths + 1, cur.Landings);
            else
                map[(gx, gz)] = (cur.Deaths, cur.Landings + 1);
        }

        return map.Select(kv => new HeatmapCell
        {
            GridX = kv.Key.Item1,
            GridZ = kv.Key.Item2,
            DeathCount = kv.Value.Deaths,
            LandingCount = kv.Value.Landings
        }).ToList();
    }
}
