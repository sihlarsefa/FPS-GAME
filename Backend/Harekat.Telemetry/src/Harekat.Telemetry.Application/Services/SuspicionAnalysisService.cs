using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Application.Contracts;
using Harekat.Telemetry.Application.Rules;
using Harekat.Telemetry.Domain.Entities;
using Harekat.Telemetry.Domain.Enums;
using Harekat.Telemetry.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harekat.Telemetry.Application.Services;

public sealed class SuspicionAnalysisService
{
    private readonly IEventStore _store;
    private readonly ISuspicionReportStore _reports;
    private readonly IRuleProvider _rules;
    private readonly IRiskScoreStore _risk;
    private readonly IReviewQueue _queue;
    private readonly IPerformanceMetrics _metrics;
    private readonly ILogger<SuspicionAnalysisService> _logger;

    public SuspicionAnalysisService(
        IEventStore store,
        ISuspicionReportStore reports,
        IRuleProvider rules,
        IRiskScoreStore risk,
        IReviewQueue queue,
        IPerformanceMetrics metrics,
        ILogger<SuspicionAnalysisService> logger)
    {
        _store = store;
        _reports = reports;
        _rules = rules;
        _risk = risk;
        _queue = queue;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlayerSuspicionSummaryDto>> AnalyzeMatchAsync(string matchId, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var events = await _store.GetByMatchAsync(matchId, ct).ConfigureAwait(false);
        var byPlayer = events.GroupBy(e => e.PlayerId, StringComparer.Ordinal);
        var snapshot = _rules.GetCurrent();
        var summaries = new List<PlayerSuspicionSummaryDto>();

        foreach (var group in byPlayer)
        {
            var findings = EvaluatePlayer(matchId, group.Key, group.ToList(), snapshot);
            var score = findings.Sum(f => f.ScoreContribution);
            var report = new PlayerSuspicionReport
            {
                PlayerId = group.Key,
                MatchId = matchId,
                TotalScore = Math.Min(100, score),
                GeneratedAt = DateTimeOffset.UtcNow,
                Findings = findings
            };

            await _reports.SaveAsync(report, ct).ConfigureAwait(false);
            await _risk.AppendAsync(new RiskScoreSample
            {
                PlayerId = group.Key,
                Timestamp = DateTimeOffset.UtcNow,
                Score = report.TotalScore,
                MatchId = matchId,
                Reason = findings.Count == 0 ? "clean" : string.Join(",", findings.Select(f => f.RuleId))
            }, ct).ConfigureAwait(false);

            if (report.IsSuspect)
            {
                await _queue.EnqueueAsync(new ReviewQueueItem
                {
                    Id = Guid.NewGuid(),
                    PlayerId = group.Key,
                    MatchId = matchId,
                    RiskScore = report.TotalScore,
                    EnqueuedAt = DateTimeOffset.UtcNow
                }, ct).ConfigureAwait(false);
            }

            summaries.Add(new PlayerSuspicionSummaryDto
            {
                PlayerId = group.Key,
                Score = report.TotalScore,
                IsSuspect = report.IsSuspect,
                FindingCount = findings.Count
            });
        }

        sw.Stop();
        _metrics.RecordAnalysis(sw.Elapsed);
        _logger.LogDebug("Analiz tamam match={MatchId} players={Count} ms={Ms}", matchId, summaries.Count, sw.ElapsedMilliseconds);
        return summaries.OrderByDescending(s => s.Score).ToList();
    }

    public async Task<IReadOnlyList<SuspicionReportDto>> ListReportsAsync(double? minScore = null, CancellationToken ct = default)
    {
        var reports = await _reports.ListAsync(minScore, ct).ConfigureAwait(false);
        return reports.Select(Map).ToList();
    }

    public static List<SuspicionFinding> EvaluatePlayer(
        string matchId,
        string playerId,
        IReadOnlyList<MatchEvent> events,
        DetectionRulesSnapshot snapshot)
    {
        var findings = new List<SuspicionFinding>();
        foreach (var rule in snapshot.Rules.Where(r => r.Enabled))
        {
            switch (rule.Kind)
            {
                case SuspicionRuleKind.ImpossibleHitRate:
                    MaybeAdd(findings, CheckHitRate(matchId, playerId, events, rule));
                    break;
                case SuspicionRuleKind.HighHeadshotRate:
                    MaybeAdd(findings, CheckHeadshot(matchId, playerId, events, rule));
                    break;
                case SuspicionRuleKind.ImpossibleSpeed:
                    MaybeAdd(findings, CheckSpeed(matchId, playerId, events, rule));
                    break;
                case SuspicionRuleKind.WallbangConsistency:
                    MaybeAdd(findings, CheckWallbang(matchId, playerId, events, rule));
                    break;
            }
        }
        return findings;
    }

    private static void MaybeAdd(List<SuspicionFinding> list, SuspicionFinding? finding)
    {
        if (finding is not null) list.Add(finding);
    }

    private static SuspicionFinding? CheckHitRate(string matchId, string playerId, IReadOnlyList<MatchEvent> events, DetectionRuleDefinition rule)
    {
        var minShots = (int)Get(rule, "minShots", 20);
        var maxHitRate = Get(rule, "maxHitRate", 0.85);
        var shots = events.Count(e => e.EventType == MatchEventType.Shot);
        var hits = events.Count(e => e.EventType is MatchEventType.Hit or MatchEventType.Kill);
        if (shots < minShots) return null;
        var rate = (double)hits / shots;
        if (rate < maxHitRate) return null;

        return new SuspicionFinding
        {
            Rule = SuspicionRuleKind.ImpossibleHitRate,
            RuleId = rule.Id,
            PlayerId = playerId,
            MatchId = matchId,
            ScoreContribution = rule.ScoreWeight,
            Detail = $"İmkânsız isabet oranı: {rate:P1} ({hits}/{shots})",
            DetectedAt = DateTimeOffset.UtcNow,
            Metrics = new Dictionary<string, double> { ["hitRate"] = rate, ["shots"] = shots, ["hits"] = hits }
        };
    }

    private static SuspicionFinding? CheckHeadshot(string matchId, string playerId, IReadOnlyList<MatchEvent> events, DetectionRuleDefinition rule)
    {
        var minHits = (int)Get(rule, "minHits", 10);
        var maxHs = Get(rule, "maxHeadshotRate", 0.70);
        var hits = events.Where(e => e.EventType is MatchEventType.Hit or MatchEventType.Kill).ToList();
        if (hits.Count < minHits) return null;
        var hs = hits.Count(h => h.IsHeadshot);
        var rate = (double)hs / hits.Count;
        if (rate < maxHs) return null;

        return new SuspicionFinding
        {
            Rule = SuspicionRuleKind.HighHeadshotRate,
            RuleId = rule.Id,
            PlayerId = playerId,
            MatchId = matchId,
            ScoreContribution = rule.ScoreWeight,
            Detail = $"Çok yüksek kafa vuruşu oranı: {rate:P1} ({hs}/{hits.Count})",
            DetectedAt = DateTimeOffset.UtcNow,
            Metrics = new Dictionary<string, double> { ["headshotRate"] = rate, ["hits"] = hits.Count, ["headshots"] = hs }
        };
    }

    private static SuspicionFinding? CheckSpeed(string matchId, string playerId, IReadOnlyList<MatchEvent> events, DetectionRuleDefinition rule)
    {
        var maxSpeed = Get(rule, "maxSpeedMps", 18);
        var samples = events
            .Where(e => e.Position is not null && e.EventType is MatchEventType.PositionSample or MatchEventType.Shot or MatchEventType.Hit or MatchEventType.Kill or MatchEventType.Death)
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (samples.Count < 2) return null;

        double peak = 0;
        for (var i = 1; i < samples.Count; i++)
        {
            var a = samples[i - 1];
            var b = samples[i];
            var dt = (b.Timestamp - a.Timestamp).TotalSeconds;
            if (dt <= 0.01) continue;
            var dist = a.Position!.Value.HorizontalDistanceTo(b.Position!.Value);
            var speed = dist / dt;
            if (speed > peak) peak = speed;
        }

        if (peak < maxSpeed) return null;

        return new SuspicionFinding
        {
            Rule = SuspicionRuleKind.ImpossibleSpeed,
            RuleId = rule.Id,
            PlayerId = playerId,
            MatchId = matchId,
            ScoreContribution = rule.ScoreWeight,
            Detail = $"Fiziksel olarak imkânsız hız: {peak:F1} m/s (eşik {maxSpeed})",
            DetectedAt = DateTimeOffset.UtcNow,
            Metrics = new Dictionary<string, double> { ["peakSpeedMps"] = peak, ["maxAllowed"] = maxSpeed }
        };
    }

    private static SuspicionFinding? CheckWallbang(string matchId, string playerId, IReadOnlyList<MatchEvent> events, DetectionRuleDefinition rule)
    {
        var minHits = (int)Get(rule, "minWallHits", 5);
        var minRatio = Get(rule, "minWallHitRatio", 0.40);
        var hits = events.Where(e => e.EventType is MatchEventType.Hit or MatchEventType.Kill).ToList();
        if (hits.Count < minHits) return null;
        var wall = hits.Count(h => h.ThroughWall);
        if (wall < minHits) return null;
        var ratio = (double)wall / hits.Count;
        if (ratio < minRatio) return null;

        return new SuspicionFinding
        {
            Rule = SuspicionRuleKind.WallbangConsistency,
            RuleId = rule.Id,
            PlayerId = playerId,
            MatchId = matchId,
            ScoreContribution = rule.ScoreWeight,
            Detail = $"Duvar arkası tutarlı isabet: {ratio:P1} ({wall}/{hits.Count})",
            DetectedAt = DateTimeOffset.UtcNow,
            Metrics = new Dictionary<string, double> { ["wallHitRatio"] = ratio, ["wallHits"] = wall, ["hits"] = hits.Count }
        };
    }

    private static double Get(DetectionRuleDefinition rule, string key, double fallback) =>
        rule.Parameters.TryGetValue(key, out var v) ? v : fallback;

    private static SuspicionReportDto Map(PlayerSuspicionReport r) => new()
    {
        PlayerId = r.PlayerId,
        MatchId = r.MatchId,
        TotalScore = r.TotalScore,
        GeneratedAt = r.GeneratedAt,
        Findings = r.Findings.Select(f => new SuspicionFindingDto
        {
            RuleId = f.RuleId,
            Rule = f.Rule.ToString(),
            ScoreContribution = f.ScoreContribution,
            Detail = f.Detail,
            Metrics = f.Metrics
        }).ToList()
    };
}
