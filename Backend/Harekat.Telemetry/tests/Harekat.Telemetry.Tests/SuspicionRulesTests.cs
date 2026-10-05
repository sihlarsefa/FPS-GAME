using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Application.Rules;
using Harekat.Telemetry.Application.Services;
using Harekat.Telemetry.Domain.Entities;
using Harekat.Telemetry.Domain.Enums;
using Harekat.Telemetry.Domain.ValueObjects;
using Harekat.Telemetry.Infrastructure.Rules;

namespace Harekat.Telemetry.Tests;

public class SuspicionRulesTests
{
    private static DetectionRulesSnapshot DefaultSnapshot()
    {
        var doc = JsonHotReloadRuleProvider.CreateDefaultDocument();
        return new DetectionRulesSnapshot(doc.Version, DateTimeOffset.UtcNow, doc.Rules);
    }

    [Fact]
    public void ImkansizIsabetOrani_TespitEdilir()
    {
        var events = new List<MatchEvent>();
        for (var i = 0; i < 25; i++)
            events.Add(Evt(MatchEventType.Shot));
        for (var i = 0; i < 24; i++)
            events.Add(Evt(MatchEventType.Hit));

        var findings = SuspicionAnalysisService.EvaluatePlayer("m1", "cheater", events, DefaultSnapshot());
        Assert.Contains(findings, f => f.Rule == SuspicionRuleKind.ImpossibleHitRate);
    }

    [Fact]
    public void YuksekKafaVurusu_TespitEdilir()
    {
        var events = Enumerable.Range(0, 15)
            .Select(_ => Evt(MatchEventType.Hit, hs: true))
            .ToList();

        var findings = SuspicionAnalysisService.EvaluatePlayer("m1", "hs", events, DefaultSnapshot());
        Assert.Contains(findings, f => f.Rule == SuspicionRuleKind.HighHeadshotRate);
    }

    [Fact]
    public void ImkansizHiz_TespitEdilir()
    {
        var t0 = DateTimeOffset.UtcNow;
        var events = new List<MatchEvent>
        {
            Evt(MatchEventType.PositionSample, x: 0, z: 0, ts: t0),
            Evt(MatchEventType.PositionSample, x: 100, z: 0, ts: t0.AddSeconds(1))
        };

        var findings = SuspicionAnalysisService.EvaluatePlayer("m1", "speed", events, DefaultSnapshot());
        Assert.Contains(findings, f => f.Rule == SuspicionRuleKind.ImpossibleSpeed);
    }

    [Fact]
    public void DuvarArkasiIsabet_TespitEdilir()
    {
        var events = Enumerable.Range(0, 10)
            .Select(_ => Evt(MatchEventType.Hit, wall: true))
            .ToList();

        var findings = SuspicionAnalysisService.EvaluatePlayer("m1", "wall", events, DefaultSnapshot());
        Assert.Contains(findings, f => f.Rule == SuspicionRuleKind.WallbangConsistency);
    }

    [Fact]
    public void TemizOyuncu_BulguYok()
    {
        var events = new List<MatchEvent>();
        for (var i = 0; i < 30; i++) events.Add(Evt(MatchEventType.Shot));
        for (var i = 0; i < 8; i++) events.Add(Evt(MatchEventType.Hit, hs: i % 4 == 0));

        var findings = SuspicionAnalysisService.EvaluatePlayer("m1", "legit", events, DefaultSnapshot());
        Assert.Empty(findings);
    }

    [Fact]
    public void DisabledRule_Atlanir()
    {
        var doc = JsonHotReloadRuleProvider.CreateDefaultDocument();
        foreach (var r in doc.Rules) r.Enabled = false;
        var snap = new DetectionRulesSnapshot("off", DateTimeOffset.UtcNow, doc.Rules);

        var events = Enumerable.Range(0, 15).Select(_ => Evt(MatchEventType.Hit, hs: true)).ToList();
        var findings = SuspicionAnalysisService.EvaluatePlayer("m1", "p", events, snap);
        Assert.Empty(findings);
    }

    private static MatchEvent Evt(
        MatchEventType type,
        bool hs = false,
        bool wall = false,
        float x = 0,
        float z = 0,
        DateTimeOffset? ts = null) => new()
    {
        Id = Guid.NewGuid(),
        MatchId = "m1",
        PlayerId = "p",
        EventType = type,
        Timestamp = ts ?? DateTimeOffset.UtcNow,
        Position = new WorldPosition(x, 1, z),
        IsHeadshot = hs,
        ThroughWall = wall,
        WeaponId = "ar_mpt76"
    };
}
