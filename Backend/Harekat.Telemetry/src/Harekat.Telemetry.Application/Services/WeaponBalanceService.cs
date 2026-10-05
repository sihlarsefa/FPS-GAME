using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Application.Contracts;
using Harekat.Telemetry.Domain.Catalogs;
using Harekat.Telemetry.Domain.Entities;
using Harekat.Telemetry.Domain.Enums;

namespace Harekat.Telemetry.Application.Services;

public sealed class WeaponBalanceService
{
    private readonly IEventStore _store;

    public WeaponBalanceService(IEventStore store) => _store = store;

    public async Task<WeaponBalanceReportDto> GetReportAsync(string? matchId = null, CancellationToken ct = default)
    {
        var events = matchId is null
            ? await _store.GetAllAsync(ct).ConfigureAwait(false)
            : await _store.GetByMatchAsync(matchId, ct).ConfigureAwait(false);

        var stats = Analyze(events);
        return new WeaponBalanceReportDto
        {
            Scope = matchId ?? "global",
            Weapons = stats.Select(s => new WeaponBalanceItemDto
            {
                WeaponId = s.WeaponId,
                Kills = s.Kills,
                Deaths = s.Deaths,
                Kd = Math.Round(s.Kd, 3),
                AverageKillDistance = Math.Round(s.AverageKillDistance, 2),
                AverageTtkMs = Math.Round(s.AverageTtkMs, 1),
                HitRate = Math.Round(s.HitRate, 4),
                Shots = s.Shots,
                Hits = s.Hits,
                TtkPercentiles = Percentiles(s.TtkSamplesMs, 0.5, 0.95, 0.99)
            }).OrderByDescending(w => w.Kills).ToList()
        };
    }

    public static IReadOnlyList<WeaponBalanceStats> Analyze(IEnumerable<MatchEvent> events)
    {
        var bags = WeaponIds.All.ToDictionary(
            id => id,
            _ => new MutableStats(),
            StringComparer.Ordinal);

        foreach (var e in events)
        {
            if (string.IsNullOrWhiteSpace(e.WeaponId) || !bags.TryGetValue(e.WeaponId, out var bag))
                continue;

            switch (e.EventType)
            {
                case MatchEventType.Shot:
                    bag.Shots++;
                    break;
                case MatchEventType.Hit:
                    bag.Hits++;
                    break;
                case MatchEventType.Kill:
                    bag.Kills++;
                    bag.Hits++;
                    if (e.DistanceMeters is float d) bag.Distances.Add(d);
                    if (e.TimeToKillMs is float ttk) bag.Ttks.Add(ttk);
                    break;
                case MatchEventType.Death:
                    bag.Deaths++;
                    break;
            }
        }

        return bags.Select(kv => new WeaponBalanceStats
        {
            WeaponId = kv.Key,
            Kills = kv.Value.Kills,
            Deaths = kv.Value.Deaths,
            Shots = kv.Value.Shots,
            Hits = kv.Value.Hits,
            AverageKillDistance = kv.Value.Distances.Count == 0 ? 0 : kv.Value.Distances.Average(),
            AverageTtkMs = kv.Value.Ttks.Count == 0 ? 0 : kv.Value.Ttks.Average(),
            TtkSamplesMs = kv.Value.Ttks
        }).ToList();
    }

    public static IReadOnlyList<double> Percentiles(IReadOnlyList<double> samples, params double[] ps)
    {
        if (samples.Count == 0) return ps.Select(_ => 0d).ToList();
        var sorted = samples.OrderBy(x => x).ToArray();
        var result = new List<double>(ps.Length);
        foreach (var p in ps)
        {
            var idx = (int)Math.Clamp(Math.Ceiling(p * sorted.Length) - 1, 0, sorted.Length - 1);
            result.Add(Math.Round(sorted[idx], 1));
        }
        return result;
    }

    private sealed class MutableStats
    {
        public int Kills;
        public int Deaths;
        public int Shots;
        public int Hits;
        public List<double> Distances { get; } = [];
        public List<double> Ttks { get; } = [];
    }
}
