using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;

namespace Project.Tests.Match
{
    /// <summary>MatchService uç durumları (eşzamanlı ölüm, yaralı tim silinmesi) ve ZoneService çoklu tohum taraması.</summary>
    public sealed class CoverageMatchTests
    {
        private MatchTestBus _bus;
        private MatchService _match;

        [SetUp]
        public void SetUp()
        {
            _bus = new MatchTestBus();
            _match = new MatchService(MatchTestSetup.Config(3, 2), _bus);
            MatchTestSetup.Populate(_match, 3, 2, 0);
        }

        [Test]
        public void SameVictimDyingTwice_CountsOnce()
        {
            _bus.Kill(100);
            _bus.Kill(100);
            Assert.AreEqual(5, _match.AlivePlayerCount);
            Assert.AreEqual(6, _match.GetPlacement(new PlayerId(100)));
        }

        [Test]
        public void SimultaneousDeaths_LastTeamStanding_Wins()
        {
            MatchTestSetup.KillTeam(_bus, 1, 2);
            MatchTestSetup.KillTeam(_bus, 2, 2);
            Assert.IsTrue(_match.HasEnded);
            Assert.AreEqual(0, _match.WinnerTeam);
            Assert.AreEqual(MatchPhase.Ending, _match.CurrentPhase);
        }

        [Test]
        public void TeamPlacements_AreAssignedInEliminationOrder()
        {
            MatchTestSetup.KillTeam(_bus, 2, 2);
            MatchTestSetup.KillTeam(_bus, 1, 2);
            Assert.AreEqual(3, _match.GetTeamPlacement(2));
            Assert.AreEqual(2, _match.GetTeamPlacement(1));
            Assert.AreEqual(1, _match.GetTeamPlacement(0));
        }

        [Test]
        public void MatchEndedEvent_PublishedExactlyOnce()
        {
            MatchTestSetup.KillTeam(_bus, 1, 2);
            MatchTestSetup.KillTeam(_bus, 2, 2);
            _bus.Kill(100);
            _bus.Kill(101);
            Assert.AreEqual(1, _bus.Of<MatchEndedEvent>().Count);
        }

        [Test]
        public void DeathsAfterEnd_AreIgnored()
        {
            _match.ForceEnd(1);
            _bus.Kill(100);
            Assert.IsTrue(_match.IsAlive(new PlayerId(100)));
            Assert.AreEqual(1, _match.WinnerTeam);
        }

        [Test]
        public void ForceEnd_Twice_KeepsFirstWinner()
        {
            _match.ForceEnd(1);
            _match.ForceEnd(2);
            Assert.AreEqual(1, _match.WinnerTeam);
            Assert.AreEqual(1, _bus.Of<MatchEndedEvent>().Count);
        }

        [Test]
        public void ForceEnd_NegativeTeam_IsDraw()
        {
            _match.ForceEnd(-1);
            Assert.IsTrue(_match.HasEnded);
            Assert.AreEqual(-1, _match.WinnerTeam);
            Assert.IsFalse(_match.WinnerId.IsValid);
        }

        [Test]
        public void AllMembersDowned_TeamIsEliminated()
        {
            _bus.Publish(new DownedEvent(new PlayerId(0), new PlayerId(200), 0));
            Assert.IsTrue(_match.IsTeamAlive(0), "Biri yaralı, diğeri sağlam");
            _bus.Publish(new DownedEvent(new PlayerId(1), new PlayerId(200), 0));
            Assert.IsFalse(_match.IsTeamAlive(0));
            Assert.AreEqual(2, _match.AliveTeamCount);
        }

        [Test]
        public void DownedWipe_ThenRemainingTeam_EndsMatch()
        {
            _bus.Publish(new DownedEvent(new PlayerId(0), new PlayerId(200), 0));
            _bus.Publish(new DownedEvent(new PlayerId(1), new PlayerId(200), 0));
            _bus.Publish(new DownedEvent(new PlayerId(200), new PlayerId(100), 2));
            _bus.Publish(new DownedEvent(new PlayerId(201), new PlayerId(100), 2));
            Assert.IsTrue(_match.HasEnded);
            Assert.AreEqual(1, _match.WinnerTeam);
        }

        [Test]
        public void RevivedBeforeLastDowned_PreventsWipe()
        {
            _bus.Publish(new DownedEvent(new PlayerId(0), new PlayerId(200), 0));
            _bus.Publish(new RevivedEvent(new PlayerId(0), new PlayerId(1)));
            _bus.Publish(new DownedEvent(new PlayerId(1), new PlayerId(200), 0));
            Assert.IsTrue(_match.IsTeamAlive(0));
        }

        [Test]
        public void Revive_RestoresTeamAndClearsPlacement()
        {
            _bus.Kill(0);
            _bus.Kill(1);
            Assert.IsFalse(_match.IsTeamAlive(0));
            _match.Revive(new PlayerId(0));
            Assert.IsTrue(_match.IsTeamAlive(0));
            Assert.AreEqual(0, _match.GetPlacement(new PlayerId(0)));
            Assert.AreEqual(3, _match.AliveTeamCount);
        }

        [Test]
        public void Revive_AfterEnd_IsNoOp()
        {
            _bus.Kill(100);
            _match.ForceEnd(1);
            _match.Revive(new PlayerId(100));
            Assert.IsFalse(_match.IsAlive(new PlayerId(100)));
        }

        [Test]
        public void AutoEndDisabled_DoesNotEndOnLastTeam()
        {
            _match.AutoEndOnElimination = false;
            MatchTestSetup.KillTeam(_bus, 1, 2);
            MatchTestSetup.KillTeam(_bus, 2, 2);
            Assert.IsFalse(_match.HasEnded);
            Assert.AreEqual(1, _match.AliveTeamCount);
        }

        [Test]
        public void TeamEliminatedHandler_Throwing_StillEndsMatch()
        {
            _match.TeamEliminated += (t, p) => throw new InvalidOperationException("abone hatası");
            _bus.Kill(200);
            try { _bus.Kill(201); } catch (InvalidOperationException) { }
            try { MatchTestSetup.KillTeam(_bus, 1, 2); } catch (InvalidOperationException) { }
            Assert.IsTrue(_match.HasEnded);
        }

        [Test]
        public void UnknownVictim_IsIgnored()
        {
            _bus.Kill(9999);
            Assert.AreEqual(6, _match.AlivePlayerCount);
        }

        [Test]
        public void RegisterInvalidId_IsIgnored()
        {
            _match.RegisterCombatant(PlayerId.Invalid, "x", false, 0, TeamRole.Rifleman);
            Assert.AreEqual(6, _match.TotalPlayers);
        }

        [Test]
        public void ReRegister_MovesCombatantBetweenTeams()
        {
            _match.RegisterCombatant(new PlayerId(1), "Taşındı", false, 1, TeamRole.Rifleman);
            Assert.AreEqual(1, _match.GetAliveCountInTeam(0));
            Assert.AreEqual(3, _match.GetAliveCountInTeam(1));
            Assert.AreEqual(1, _match.GetTeam(new PlayerId(1)));
        }

        [Test]
        public void AreAllies_SameTeamOnly_AndNotWithUnknown()
        {
            Assert.IsTrue(_match.AreAllies(new PlayerId(100), new PlayerId(101)));
            Assert.IsFalse(_match.AreAllies(new PlayerId(100), new PlayerId(200)));
            Assert.IsFalse(_match.AreAllies(new PlayerId(100), new PlayerId(9999)));
        }

        [Test]
        public void GetDisplayName_FallsBackForUnknown()
        {
            Assert.AreEqual("Bilinmeyen", _match.GetDisplayName(PlayerId.Invalid));
            Assert.AreEqual("Asker 4242", _match.GetDisplayName(new PlayerId(4242)));
        }

        [Test]
        public void Tick_IgnoresNaNAndNegativeDelta()
        {
            _match.Begin();
            _match.Tick(float.NaN);
            _match.Tick(-3f);
            _match.Tick(float.PositiveInfinity);
            Assert.AreEqual(0f, _match.PhaseElapsedSeconds, 1e-6f);
            Assert.AreEqual(MatchPhase.PreMatch, _match.CurrentPhase);
        }

        [Test]
        public void PreMatch_HugeStep_OverflowsIntoInsertion()
        {
            _match.Begin();
            _match.Tick(7f); // ön süre 5 sn
            Assert.AreEqual(MatchPhase.Insertion, _match.CurrentPhase);
            Assert.AreEqual(2f, _match.PhaseElapsedSeconds, 1e-3f);
        }

        [Test]
        public void NotifyDropComplete_OnlyWorksDuringInsertion()
        {
            _match.NotifyDropComplete();
            Assert.AreEqual(MatchPhase.Lobby, _match.CurrentPhase);
            _match.Begin();
            _match.Tick(6f);
            _match.NotifyDropComplete();
            Assert.AreEqual(MatchPhase.InMatch, _match.CurrentPhase);
        }

        [Test]
        public void Begin_Twice_DoesNotRestart()
        {
            _match.Begin();
            _match.Tick(2f);
            _match.Begin();
            Assert.AreEqual(2f, _match.PhaseElapsedSeconds, 1e-4f);
        }

        [Test]
        public void MatchClock_StopsAfterEnd()
        {
            _match.Begin();
            _match.Tick(6f);
            _match.NotifyDropComplete();
            _match.Tick(10f);
            _match.ForceEnd(0);
            var t = _match.MatchElapsedSeconds;
            _match.Tick(50f);
            Assert.AreEqual(t, _match.MatchElapsedSeconds, 1e-4f);
        }

        [Test]
        public void DeathTime_RecordsMatchClock()
        {
            _match.Begin();
            _match.Tick(5f);
            _match.NotifyDropComplete();
            _match.Tick(12f);
            _bus.Kill(201);
            Assert.AreEqual(12f, _match.GetDeathTime(new PlayerId(201)), 0.01f);
            Assert.AreEqual(-1f, _match.GetDeathTime(new PlayerId(200)), 1e-6f);
        }

        [Test]
        public void Dispose_UnsubscribesFromBus()
        {
            _match.Dispose();
            Assert.AreEqual(0, _bus.HandlerCount<PlayerDiedEvent>());
            _match.Dispose();
        }

        [Test]
        public void GetAliveTeams_ListsOnlyLiveTeams()
        {
            MatchTestSetup.KillTeam(_bus, 1, 2);
            var list = new List<int>();
            _match.GetAliveTeams(list);
            Assert.AreEqual(2, list.Count);
            Assert.IsFalse(list.Contains(1));
        }
    }

    /// <summary>ZoneService: birçok tohumda sınır/içerme değişmezleri.</summary>
    public sealed class CoverageZoneTests
    {
        private const float MapHalf = 512f;

        private static ZoneService Create(int seed, ZonePhase[] phases, out MatchTestBus bus)
        {
            bus = new MatchTestBus();
            var z = new ZoneService(phases, new SeededRandom(seed), bus, MapHalf);
            z.Initialize(0f, 0f, 740f, 0.5f);
            return z;
        }

        private static float Dist(float ax, float az, float bx, float bz) =>
            (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        [Test]
        public void ManySeeds_NextCircleAlwaysInsideCurrent()
        {
            for (var seed = 0; seed < 200; seed++)
            {
                var zone = Create(seed, MatchConfig.DefaultZonePhases(), out _);
                zone.Start();
                var prev = zone.CurrentZone;
                for (var step = 0; step < 4000 && zone.Stage != ZoneStage.Finished; step++)
                {
                    var before = zone.PhaseIndex;
                    var next = zone.NextZone;
                    Assert.IsTrue(Dist(next.CenterX, next.CenterZ, prev.CenterX, prev.CenterZ) + next.Radius <= prev.Radius + 0.05f,
                        "tohum " + seed + " faz " + before);
                    zone.Tick(1f);
                    if (zone.PhaseIndex != before) prev = zone.CurrentZone;
                }
            }
        }

        [Test]
        public void ManySeeds_CenterStaysWithinMapBounds()
        {
            for (var seed = 1000; seed < 1150; seed++)
            {
                var zone = Create(seed, MatchConfig.DefaultZonePhases(), out _);
                zone.Start();
                for (var i = 0; i < 4000 && zone.Stage != ZoneStage.Finished; i++)
                {
                    var n = zone.NextZone;
                    Assert.Less(Math.Abs(n.CenterX), MapHalf * 0.8f + 0.01f);
                    Assert.Less(Math.Abs(n.CenterZ), MapHalf * 0.8f + 0.01f);
                    zone.Tick(1f);
                }
            }
        }

        [Test]
        public void ManySeeds_RadiusNeverIncreases()
        {
            for (var seed = 0; seed < 100; seed++)
            {
                var zone = Create(seed * 7 + 3, MatchConfig.DefaultZonePhases(), out _);
                zone.Start();
                var last = zone.CurrentZone.Radius;
                for (var i = 0; i < 4000 && zone.Stage != ZoneStage.Finished; i++)
                {
                    zone.Tick(0.7f);
                    var r = zone.CurrentZone.Radius;
                    Assert.LessOrEqual(r, last + 1e-3f);
                    last = r;
                }
            }
        }

        [Test]
        public void SameSeed_IsDeterministic()
        {
            var a = Create(77, MatchConfig.DefaultZonePhases(), out _);
            var b = Create(77, MatchConfig.DefaultZonePhases(), out _);
            a.Start(); b.Start();
            for (var i = 0; i < 300; i++)
            {
                a.Tick(1.3f); b.Tick(1.3f);
                Assert.AreEqual(a.CurrentZone.CenterX, b.CurrentZone.CenterX, 1e-4f);
                Assert.AreEqual(a.NextZone.Radius, b.NextZone.Radius, 1e-4f);
            }
        }

        [Test]
        public void FinishedZone_EndsAtFinalRadius()
        {
            var zone = Create(5, MatchConfig.DefaultZonePhases(), out _);
            zone.Start();
            for (var i = 0; i < 20000 && zone.Stage != ZoneStage.Finished; i++) zone.Tick(1f);
            Assert.AreEqual(ZoneStage.Finished, zone.Stage);
            var phases = MatchConfig.DefaultZonePhases();
            Assert.AreEqual(phases[phases.Length - 1].TargetRadius, zone.CurrentZone.Radius, 0.05f);
        }

        [Test]
        public void HugeSingleTick_FinishesAllPhases()
        {
            var zone = Create(9, MatchConfig.DefaultZonePhases(), out _);
            zone.Start();
            zone.Tick(1e6f);
            Assert.AreEqual(ZoneStage.Finished, zone.Stage);
        }

        [Test]
        public void StageEvents_FirePerStageChange()
        {
            var phases = new[] { new ZonePhase(5f, 5f, 200f, 1f), new ZonePhase(5f, 5f, 50f, 2f) };
            var zone = Create(1, phases, out var bus);
            zone.Start();
            zone.Tick(100f);
            var events = bus.Of<ZoneStageChangedEvent>();
            // Waiting, Shrinking, Waiting, Shrinking, Finished
            Assert.AreEqual(5, events.Count);
        }

        [Test]
        public void NoPhases_StartFinishesImmediately()
        {
            var zone = Create(1, new ZonePhase[0], out _);
            zone.Start();
            Assert.AreEqual(ZoneStage.Finished, zone.Stage);
            Assert.AreEqual(740f, zone.CurrentZone.Radius, 1e-3f);
        }

        [Test]
        public void ZeroDurationPhases_DoNotHang()
        {
            var phases = new[] { new ZonePhase(0f, 0f, 100f, 1f), new ZonePhase(0f, 0f, 10f, 1f) };
            var zone = Create(3, phases, out _);
            zone.Start();
            zone.Tick(0.1f);
            zone.Tick(0.1f);
            Assert.AreEqual(ZoneStage.Finished, zone.Stage);
            Assert.AreEqual(10f, zone.CurrentZone.Radius, 1e-3f);
        }

        [Test]
        public void NaNTick_IsIgnored()
        {
            var zone = Create(3, MatchConfig.DefaultZonePhases(), out _);
            zone.Start();
            var remaining = zone.StageRemainingSeconds;
            zone.Tick(float.NaN);
            zone.Tick(-5f);
            Assert.AreEqual(remaining, zone.StageRemainingSeconds, 1e-4f);
        }

        [Test]
        public void BeforeStart_EverywhereSafe_NoDamage()
        {
            var zone = Create(3, MatchConfig.DefaultZonePhases(), out _);
            Assert.IsTrue(zone.IsInsideZone(9999f, 9999f));
            Assert.AreEqual(0f, zone.GetDamagePerSecond(9999f, 9999f), 1e-6f);
        }

        [Test]
        public void OutsideZone_TakesPhaseDamage()
        {
            var zone = Create(3, new[] { new ZonePhase(1f, 1f, 100f, 3f) }, out _);
            zone.Start();
            Assert.AreEqual(3f, zone.GetDamagePerSecond(5000f, 0f), 1e-4f);
            Assert.AreEqual(0f, zone.GetDamagePerSecond(0f, 0f), 1e-4f);
        }

        [Test]
        public void DistanceToSafeZone_ZeroInside_PositiveOutside()
        {
            var zone = Create(3, new[] { new ZonePhase(10f, 10f, 100f, 1f) }, out _);
            zone.Start();
            var n = zone.NextZone;
            Assert.AreEqual(0f, zone.DistanceToSafeZone(n.CenterX, n.CenterZ), 1e-4f);
            Assert.Greater(zone.DistanceToSafeZone(n.CenterX + n.Radius + 50f, n.CenterZ), 49f);
        }

        [Test]
        public void Initialize_SanitizesBadValues()
        {
            var zone = new ZoneService(MatchConfig.DefaultZonePhases(), new SeededRandom(1), new MatchTestBus(), MapHalf);
            zone.Initialize(float.NaN, float.PositiveInfinity, -5f, float.NaN);
            Assert.AreEqual(0f, zone.CurrentZone.CenterX, 1e-6f);
            Assert.AreEqual(0f, zone.CurrentZone.Radius, 1e-6f);
        }

        [Test]
        public void Shrink_Negative_ClampsToZero()
        {
            var zone = Create(3, MatchConfig.DefaultZonePhases(), out _);
            zone.Shrink(-10f);
            Assert.AreEqual(0f, zone.CurrentZone.Radius, 1e-6f);
            zone.Shrink(float.NaN);
            Assert.AreEqual(0f, zone.CurrentZone.Radius, 1e-6f);
        }

        [Test]
        public void StartTwice_DoesNotRestartPhase()
        {
            var zone = Create(3, MatchConfig.DefaultZonePhases(), out _);
            zone.Start();
            zone.Tick(5f);
            var remaining = zone.StageRemainingSeconds;
            zone.Start();
            Assert.AreEqual(remaining, zone.StageRemainingSeconds, 1e-4f);
        }
    }
}
