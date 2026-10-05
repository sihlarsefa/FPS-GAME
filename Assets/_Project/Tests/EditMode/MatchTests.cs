using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Tests.Match
{
    /// <summary>Testler için kayıt tutan, yayın sırasında abonelik değişimine dayanıklı olay yolu.</summary>
    internal sealed class MatchTestBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();
        public readonly List<object> Published = new();

        public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
        {
            Published.Add(gameEvent);
            if (!_handlers.TryGetValue(typeof(TEvent), out var list))
                return;

            var snapshot = list.ToArray();
            for (var i = 0; i < snapshot.Length; i++)
                ((Action<TEvent>)snapshot[i]).Invoke(gameEvent);
        }

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                list = new List<Delegate>();
                _handlers[typeof(TEvent)] = list;
            }

            list.Add(handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var list))
                list.Remove(handler);
        }

        public int HandlerCount<TEvent>() where TEvent : IGameEvent =>
            _handlers.TryGetValue(typeof(TEvent), out var list) ? list.Count : 0;

        public List<TEvent> Of<TEvent>() where TEvent : IGameEvent
        {
            var result = new List<TEvent>();
            foreach (var e in Published)
            {
                if (e is TEvent typed)
                    result.Add(typed);
            }

            return result;
        }

        public void Kill(int victim, int killer = -1, string weaponId = null, bool headshot = false) =>
            Publish(new PlayerDiedEvent(new PlayerId(victim), new PlayerId(killer), weaponId, headshot));
    }

    internal static class MatchTestSetup
    {
        /// <summary>teamCount × teamSize savaşan kaydeder. Kimlik = tim*100 + slot; slot 0 komutan.</summary>
        public static void Populate(MatchService match, int teamCount, int teamSize, int localId = -1)
        {
            for (var t = 0; t < teamCount; t++)
            {
                for (var s = 0; s < teamSize; s++)
                {
                    var id = t * 100 + s;
                    var role = s == 0 ? TeamRole.Leader : TeamRole.Rifleman;
                    match.RegisterCombatant(new PlayerId(id), (s == 0 ? "Yzb. " : "Uzm.Çvş. ") + "Asker" + id, id == localId, t, role);
                }
            }
        }

        public static void KillTeam(MatchTestBus bus, int team, int teamSize, int killer = -1)
        {
            for (var s = 0; s < teamSize; s++)
                bus.Kill(team * 100 + s, killer);
        }

        public static MatchConfig Config(int teams = 4, int size = 3, float preMatch = 5f)
        {
            var config = new MatchConfig().WithTeams(teams, size);
            config.PreMatchDurationSeconds = preMatch;
            return config;
        }
    }

    public sealed class MatchTests
    {
        private MatchTestBus _bus;
        private MatchService _match;

        [SetUp]
        public void SetUp()
        {
            _bus = new MatchTestBus();
            _match = new MatchService(MatchTestSetup.Config(), _bus);
        }

        [Test]
        public void NewMatch_StartsInLobby_WithNoWinner()
        {
            Assert.AreEqual(MatchPhase.Lobby, _match.CurrentPhase);
            Assert.AreEqual(-1, _match.WinnerTeam);
            Assert.IsFalse(_match.WinnerId.IsValid);
            Assert.IsFalse(_match.LocalPlayerId.IsValid);
            Assert.AreEqual(-1, _match.LocalTeam);
            Assert.AreEqual(0, _match.TotalPlayers);
            Assert.AreEqual(4, _match.TeamCount, "Kayıt yokken yapılandırmadaki tim sayısı");
            Assert.AreEqual(0f, _match.MatchElapsedSeconds, 1e-6f);
            Assert.AreEqual(1, _bus.HandlerCount<PlayerDiedEvent>());
        }

        [Test]
        public void Begin_GoesLobbyToPreMatch_AndPublishesPhaseChange()
        {
            _match.Begin();

            Assert.AreEqual(MatchPhase.PreMatch, _match.CurrentPhase);
            var changes = _bus.Of<MatchPhaseChangedEvent>();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(MatchPhase.Lobby, changes[0].PreviousPhase);
            Assert.AreEqual(MatchPhase.PreMatch, changes[0].CurrentPhase);

            _match.Begin();
            Assert.AreEqual(1, _bus.Of<MatchPhaseChangedEvent>().Count, "Begin ikinci kez çağrılınca geçiş yok");
        }

        [Test]
        public void Tick_PreMatchCountdown_ThenInsertion_WithOverflowCarried()
        {
            _match.Begin();
            _match.Tick(4.9f);

            Assert.AreEqual(MatchPhase.PreMatch, _match.CurrentPhase);
            Assert.AreEqual(0.1f, _match.PhaseRemainingSeconds, 1e-3f);
            Assert.AreEqual(0f, _match.MatchElapsedSeconds, 1e-6f, "Maç saati Insertion'dan önce işlemez");

            _match.Tick(0.3f);

            Assert.AreEqual(MatchPhase.Insertion, _match.CurrentPhase);
            Assert.AreEqual(0.2f, _match.PhaseElapsedSeconds, 1e-3f);
            Assert.AreEqual(0.2f, _match.MatchElapsedSeconds, 1e-3f);
            Assert.AreEqual(0f, _match.PhaseRemainingSeconds, 1e-6f);

            _match.Tick(10f);
            Assert.AreEqual(MatchPhase.Insertion, _match.CurrentPhase, "Insertion yalnızca NotifyDropComplete ile biter");
            Assert.AreEqual(10.2f, _match.MatchElapsedSeconds, 1e-3f);
        }

        [Test]
        public void Tick_IgnoresNonPositiveAndNaN()
        {
            _match.Begin();
            _match.Tick(0f);
            _match.Tick(-3f);
            _match.Tick(float.NaN);

            Assert.AreEqual(MatchPhase.PreMatch, _match.CurrentPhase);
            Assert.AreEqual(0f, _match.PhaseElapsedSeconds, 1e-6f);
        }

        [Test]
        public void NotifyDropComplete_OnlyFromInsertion()
        {
            _match.NotifyDropComplete();
            Assert.AreEqual(MatchPhase.Lobby, _match.CurrentPhase);

            _match.Begin();
            _match.NotifyDropComplete();
            Assert.AreEqual(MatchPhase.PreMatch, _match.CurrentPhase);

            _match.Tick(5f);
            Assert.AreEqual(MatchPhase.Insertion, _match.CurrentPhase);
            _match.Tick(2f);
            _match.NotifyDropComplete();

            Assert.AreEqual(MatchPhase.InMatch, _match.CurrentPhase);
            Assert.AreEqual(0f, _match.PhaseElapsedSeconds, 1e-6f);
            Assert.AreEqual(2f, _match.MatchElapsedSeconds, 1e-3f, "Maç saati Insertion'dan beri sayar");

            _match.Tick(3f);
            Assert.AreEqual(5f, _match.MatchElapsedSeconds, 1e-3f);
        }

        [Test]
        public void EveryTransition_PublishesPhaseChangedEvent()
        {
            MatchTestSetup.Populate(_match, 2, 1);
            _match.Begin();
            _match.Tick(6f);
            _match.NotifyDropComplete();
            _bus.Kill(0, 100);

            var changes = _bus.Of<MatchPhaseChangedEvent>();
            Assert.AreEqual(4, changes.Count);
            Assert.AreEqual(MatchPhase.PreMatch, changes[0].CurrentPhase);
            Assert.AreEqual(MatchPhase.Insertion, changes[1].CurrentPhase);
            Assert.AreEqual(MatchPhase.InMatch, changes[2].CurrentPhase);
            Assert.AreEqual(MatchPhase.Ending, changes[3].CurrentPhase);
            Assert.AreEqual(MatchPhase.InMatch, changes[3].PreviousPhase);
        }

        [Test]
        public void TransitionTo_SamePhase_DoesNothing()
        {
            _match.TransitionTo(MatchPhase.Lobby);
            Assert.AreEqual(0, _bus.Of<MatchPhaseChangedEvent>().Count);
        }

        [Test]
        public void Register_TracksTeamsPlayersAndNames()
        {
            MatchTestSetup.Populate(_match, 4, 3, localId: 201);

            Assert.AreEqual(12, _match.TotalPlayers);
            Assert.AreEqual(12, _match.AlivePlayerCount);
            Assert.AreEqual(12, _match.AliveIds.Count);
            Assert.AreEqual(4, _match.TeamCount);
            Assert.AreEqual(4, _match.AliveTeamCount);
            Assert.AreEqual(3, _match.GetAliveCountInTeam(2));
            Assert.IsTrue(_match.IsTeamAlive(3));
            Assert.IsFalse(_match.IsTeamAlive(7));

            Assert.AreEqual(2, _match.GetTeam(new PlayerId(201)));
            Assert.AreEqual(-1, _match.GetTeam(new PlayerId(999)));
            Assert.AreEqual(TeamRole.Leader, _match.GetRole(new PlayerId(300)));
            Assert.AreEqual(TeamRole.Rifleman, _match.GetRole(new PlayerId(301)));

            Assert.AreEqual("Yzb. Asker100", _match.GetDisplayName(new PlayerId(100)));
            Assert.AreEqual("Uzm.Çvş. Asker101", _match.GetDisplayName(new PlayerId(101)));
            Assert.AreEqual("Asker 999", _match.GetDisplayName(new PlayerId(999)));
            Assert.IsFalse(string.IsNullOrEmpty(_match.GetDisplayName(PlayerId.Invalid)));

            Assert.IsTrue(_match.IsLocalPlayer(new PlayerId(201)));
            Assert.IsFalse(_match.IsLocalPlayer(new PlayerId(200)));
            Assert.AreEqual(new PlayerId(201), _match.LocalPlayerId);
            Assert.AreEqual(2, _match.LocalTeam);

            var members = _match.GetTeamMembers(1);
            Assert.AreEqual(3, members.Count);
            Assert.AreEqual(new PlayerId(100), members[0]);
            Assert.AreEqual(new PlayerId(102), members[2]);
            Assert.AreEqual(0, _match.GetTeamMembers(6).Count);
            Assert.AreEqual(0, _match.GetTeamMembers(-1).Count);
        }

        [Test]
        public void TeamNames_AreTurkishTimNames()
        {
            var expected = new[]
            {
                "Kartal Timi", "Bozkurt Timi", "Şimşek Timi", "Yıldırım Timi",
                "Kılıç Timi", "Kaplan Timi", "Pars Timi", "Atmaca Timi"
            };

            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], _match.GetTeamName(i));

            Assert.AreEqual("9. Tim", _match.GetTeamName(8));
            Assert.IsFalse(string.IsNullOrEmpty(_match.GetTeamName(-1)));
        }

        [Test]
        public void AreAllies_SameTeamOnly()
        {
            MatchTestSetup.Populate(_match, 3, 2);

            Assert.IsTrue(_match.AreAllies(new PlayerId(100), new PlayerId(101)));
            Assert.IsTrue(_match.AreAllies(new PlayerId(100), new PlayerId(100)));
            Assert.IsFalse(_match.AreAllies(new PlayerId(100), new PlayerId(200)));
            Assert.IsFalse(_match.AreAllies(new PlayerId(100), new PlayerId(555)));
            Assert.IsFalse(_match.AreAllies(PlayerId.Invalid, PlayerId.Invalid));
        }

        [Test]
        public void Death_IndividualPlacementIsAliveCountAtDeath()
        {
            MatchTestSetup.Populate(_match, 4, 3);

            _bus.Kill(1, 200);
            _bus.Kill(2, 200);

            Assert.AreEqual(12, _match.GetPlacement(new PlayerId(1)));
            Assert.AreEqual(11, _match.GetPlacement(new PlayerId(2)));
            Assert.AreEqual(0, _match.GetPlacement(new PlayerId(0)), "Hayattaysa 0");
            Assert.AreEqual(10, _match.AlivePlayerCount);
            Assert.IsFalse(_match.IsAlive(new PlayerId(1)));
            Assert.IsTrue(_match.IsAlive(new PlayerId(0)));
            Assert.IsFalse(Contains(_match.AliveIds, new PlayerId(1)));
            Assert.AreEqual(1, _match.GetAliveCountInTeam(0));
            Assert.IsTrue(_match.IsTeamAlive(0));
        }

        [Test]
        public void Death_UnknownAndDuplicateAreIgnored()
        {
            MatchTestSetup.Populate(_match, 2, 2);

            _bus.Kill(777, 0);
            _bus.Kill(-1, 0);
            Assert.AreEqual(4, _match.AlivePlayerCount);

            _bus.Kill(1, 100);
            _bus.Kill(1, 100);
            _match.RegisterPlayerDeath(new PlayerId(1));

            Assert.AreEqual(3, _match.AlivePlayerCount);
            Assert.AreEqual(4, _match.GetPlacement(new PlayerId(1)));
            Assert.AreEqual(1, _match.GetAliveCountInTeam(0));
        }

        [Test]
        public void TeamElimination_OrderGivesTeamPlacements_AndLastTeamWins()
        {
            MatchTestSetup.Populate(_match, 4, 3, localId: 101);
            var eliminated = new List<int>();
            _match.TeamEliminated += (team, placement) => eliminated.Add(team * 10 + placement);
            _match.Begin();
            _match.Tick(5f);
            _match.NotifyDropComplete();

            MatchTestSetup.KillTeam(_bus, 2, 3, killer: 100);
            Assert.AreEqual(4, _match.GetTeamPlacement(2));
            Assert.AreEqual(3, _match.AliveTeamCount);
            Assert.IsFalse(_match.IsTeamAlive(2));
            Assert.AreEqual(0, _match.GetTeamPlacement(1), "Hayattaki tim için 0");

            MatchTestSetup.KillTeam(_bus, 0, 3, killer: 300);
            Assert.AreEqual(3, _match.GetTeamPlacement(0));
            Assert.AreEqual(2, _match.AliveTeamCount);
            Assert.AreEqual(MatchPhase.InMatch, _match.CurrentPhase);
            Assert.AreEqual(0, _bus.Of<MatchEndedEvent>().Count);

            // Kazanan timden bir kayıp (yerel oyuncu hayatta kalır).
            _bus.Kill(100, 300);

            MatchTestSetup.KillTeam(_bus, 3, 3, killer: 101);
            Assert.AreEqual(2, _match.GetTeamPlacement(3));
            Assert.AreEqual(1, _match.AliveTeamCount);

            Assert.AreEqual(MatchPhase.Ending, _match.CurrentPhase);
            Assert.AreEqual(1, _match.WinnerTeam);
            Assert.AreEqual(1, _match.GetTeamPlacement(1));
            Assert.AreEqual(new PlayerId(101), _match.WinnerId, "Hayattaki yerel oyuncu kazananı temsil eder");

            var ended = _bus.Of<MatchEndedEvent>();
            Assert.AreEqual(1, ended.Count);
            Assert.AreEqual(1, ended[0].WinnerTeam);
            Assert.AreEqual(new PlayerId(101), ended[0].WinnerId);

            Assert.AreEqual(3, eliminated.Count);
            Assert.AreEqual(24, eliminated[0]);
            Assert.AreEqual(3, eliminated[1]);
            Assert.AreEqual(32, eliminated[2]);
        }

        [Test]
        public void MatchEnd_WinnerRepresentativePrefersLeaderWhenNoLocal()
        {
            MatchTestSetup.Populate(_match, 2, 3);

            MatchTestSetup.KillTeam(_bus, 1, 3, killer: 1);

            Assert.AreEqual(0, _match.WinnerTeam);
            Assert.AreEqual(new PlayerId(0), _match.WinnerId);
        }

        [Test]
        public void MatchEnd_WinnerRepresentativeFallsBackToFirstAlive()
        {
            MatchTestSetup.Populate(_match, 2, 3);
            _bus.Kill(0, 100);

            MatchTestSetup.KillTeam(_bus, 1, 3, killer: 2);

            Assert.AreEqual(0, _match.WinnerTeam);
            Assert.AreEqual(new PlayerId(1), _match.WinnerId);
            Assert.IsTrue(_match.IsAlive(_match.WinnerId));
        }

        [Test]
        public void MatchEnd_StateFreezesAfterEnding()
        {
            MatchTestSetup.Populate(_match, 2, 2);
            _match.Begin();
            _match.Tick(5f);
            _match.NotifyDropComplete();
            _match.Tick(10f);

            MatchTestSetup.KillTeam(_bus, 0, 2, killer: 100);
            var elapsed = _match.MatchElapsedSeconds;

            _bus.Kill(100, -1);
            _match.Tick(30f);

            Assert.IsTrue(_match.HasEnded);
            Assert.IsTrue(_match.IsAlive(new PlayerId(100)), "Bitişten sonraki ölümler yok sayılır");
            Assert.AreEqual(1, _bus.Of<MatchEndedEvent>().Count);
            Assert.AreEqual(elapsed, _match.MatchElapsedSeconds, 1e-6f, "Maç saati Ending'de durur");
            Assert.AreEqual(10f, elapsed, 1e-3f);
        }

        [Test]
        public void MatchEnd_PublishesEndedBeforeEndingPhase()
        {
            MatchTestSetup.Populate(_match, 2, 1);
            var order = new List<string>();
            _bus.Subscribe<MatchEndedEvent>(_ => order.Add("ended"));
            _bus.Subscribe<MatchPhaseChangedEvent>(e => order.Add("phase:" + e.CurrentPhase));

            _bus.Kill(0, 100);

            Assert.AreEqual(2, order.Count);
            Assert.AreEqual("ended", order[0]);
            Assert.AreEqual("phase:Ending", order[1]);
        }

        [Test]
        public void SingleTeamMatch_EndsOnlyWhenWholeTeamDies_WithNoWinner()
        {
            MatchTestSetup.Populate(_match, 1, 2);

            _bus.Kill(0);
            Assert.AreEqual(0, _bus.Of<MatchEndedEvent>().Count);

            _bus.Kill(1);
            var ended = _bus.Of<MatchEndedEvent>();
            Assert.AreEqual(1, ended.Count);
            Assert.AreEqual(-1, ended[0].WinnerTeam);
            Assert.IsFalse(ended[0].WinnerId.IsValid);
            Assert.AreEqual(1, _match.GetTeamPlacement(0));
        }

        [Test]
        public void DuplicateRegistration_UpdatesWithoutDoubleCounting()
        {
            MatchTestSetup.Populate(_match, 2, 2);

            _match.RegisterCombatant(new PlayerId(1), "Asb.Kd.Çvş. Mehmet", true, 0, TeamRole.Medic);

            Assert.AreEqual(4, _match.TotalPlayers);
            Assert.AreEqual(2, _match.GetAliveCountInTeam(0));
            Assert.AreEqual("Asb.Kd.Çvş. Mehmet", _match.GetDisplayName(new PlayerId(1)));
            Assert.AreEqual(TeamRole.Medic, _match.GetRole(new PlayerId(1)));
            Assert.AreEqual(new PlayerId(1), _match.LocalPlayerId);

            // Tim değişikliği sayımları taşır.
            _match.RegisterCombatant(new PlayerId(1), null, true, 1, TeamRole.Medic);
            Assert.AreEqual(1, _match.GetAliveCountInTeam(0));
            Assert.AreEqual(3, _match.GetAliveCountInTeam(1));
            Assert.AreEqual(3, _match.GetTeamMembers(1).Count);
            Assert.AreEqual("Asb.Kd.Çvş. Mehmet", _match.GetDisplayName(new PlayerId(1)), "Boş ad mevcut adı silmez");
            Assert.AreEqual(1, _match.LocalTeam);
        }

        [Test]
        public void LocalPlayer_SecondLocalReplacesFirst()
        {
            _match.RegisterCombatant(new PlayerId(1), "A", true, 0, TeamRole.Leader);
            _match.RegisterCombatant(new PlayerId(2), "B", true, 1, TeamRole.Leader);

            Assert.IsFalse(_match.IsLocalPlayer(new PlayerId(1)));
            Assert.IsTrue(_match.IsLocalPlayer(new PlayerId(2)));
            Assert.AreEqual(1, _match.LocalTeam);
        }

        [Test]
        public void InvalidId_IsNotRegistered()
        {
            _match.RegisterCombatant(PlayerId.Invalid, "X", false, 0, TeamRole.Rifleman);
            Assert.AreEqual(0, _match.TotalPlayers);
        }

        [Test]
        public void NegativeTeam_GetsItsOwnTeam()
        {
            MatchTestSetup.Populate(_match, 2, 1);
            _match.RegisterCombatant(new PlayerId(50), "Yalnız", false, -1, TeamRole.Rifleman);

            var team = _match.GetTeam(new PlayerId(50));
            Assert.GreaterOrEqual(team, 4);
            Assert.AreEqual(3, _match.AliveTeamCount);
            Assert.IsFalse(_match.AreAllies(new PlayerId(50), new PlayerId(0)));
        }

        [Test]
        public void Dispose_UnsubscribesFromDeaths()
        {
            MatchTestSetup.Populate(_match, 2, 2);
            _match.Dispose();
            _match.Dispose();

            Assert.AreEqual(0, _bus.HandlerCount<PlayerDiedEvent>());
            _bus.Kill(0, 100);
            Assert.IsTrue(_match.IsAlive(new PlayerId(0)));
        }

        [Test]
        public void NullConfigAndBus_AreTolerated()
        {
            var match = new MatchService(null, null);
            Assert.IsNotNull(match.Config);
            match.RegisterCombatant(new PlayerId(1), "A", false, 0, TeamRole.Leader);
            match.RegisterCombatant(new PlayerId(2), "B", false, 1, TeamRole.Leader);
            match.Begin();
            match.Tick(100f);
            match.RegisterPlayerDeath(new PlayerId(1));

            Assert.AreEqual(1, match.WinnerTeam);
            Assert.AreEqual(MatchPhase.Ending, match.CurrentPhase);
            Assert.DoesNotThrow(match.Dispose);
        }

        [Test]
        public void FullSquadBattleRoyale_EightTeamsOfTen()
        {
            var match = new MatchService(MatchTestSetup.Config(8, 10), _bus);
            MatchTestSetup.Populate(match, 8, 10, localId: 0);
            Assert.AreEqual(80, match.TotalPlayers);

            // Timleri 7,6,...,1 sırasıyla ele; tim 0 kazanır.
            for (var team = 7; team >= 1; team--)
            {
                MatchTestSetup.KillTeam(_bus, team, 10, killer: 5);
                Assert.AreEqual(team + 1, match.GetTeamPlacement(team));
            }

            Assert.AreEqual(0, match.WinnerTeam);
            Assert.AreEqual(1, match.GetTeamPlacement(0));
            Assert.AreEqual(80, match.GetPlacement(new PlayerId(700)));
            Assert.AreEqual(11, match.GetPlacement(new PlayerId(109)));
            Assert.AreEqual(10, match.AlivePlayerCount);
            match.Dispose();
        }

        [Test]
        public void ThrowingTeamEliminatedSubscriber_StillEndsMatch()
        {
            MatchTestSetup.Populate(_match, 2, 1);
            _match.TeamEliminated += (_, _) => throw new InvalidOperationException("sunum hatası");

            Assert.Throws<InvalidOperationException>(() => _bus.Kill(100, 0));

            Assert.AreEqual(MatchPhase.Ending, _match.CurrentPhase);
            Assert.AreEqual(0, _match.WinnerTeam);
            Assert.AreEqual(1, _bus.Of<MatchEndedEvent>().Count);
        }

        [Test]
        public void GetAliveTeams_ListsSurvivingTeamsInOrder()
        {
            MatchTestSetup.Populate(_match, 4, 2, localId: 300);
            MatchTestSetup.KillTeam(_bus, 1, 2, killer: 0);

            var alive = new List<int>();
            _match.GetAliveTeams(alive);
            Assert.AreEqual(3, alive.Count);
            Assert.AreEqual(0, alive[0]);
            Assert.AreEqual(2, alive[1]);
            Assert.AreEqual(3, alive[2]);
            Assert.DoesNotThrow(() => _match.GetAliveTeams(null));

            Assert.IsTrue(_match.IsLocalTeam(3));
            Assert.IsFalse(_match.IsLocalTeam(0));
            Assert.IsFalse(_match.IsLocalTeam(-1));
        }

        [Test]
        public void GetAliveTeamMembers_SkipsDead()
        {
            MatchTestSetup.Populate(_match, 2, 4);
            _bus.Kill(1, 100);
            _bus.Kill(3, 100);

            var members = new List<PlayerId>();
            _match.GetAliveTeamMembers(0, members);
            Assert.AreEqual(2, members.Count);
            Assert.AreEqual(new PlayerId(0), members[0]);
            Assert.AreEqual(new PlayerId(2), members[1]);
            Assert.AreEqual(4, _match.GetTeamMembers(0).Count, "Üye listesi ölüleri de içerir");
        }

        [Test]
        public void DeathTime_RecordedOnMatchClock()
        {
            MatchTestSetup.Populate(_match, 2, 2);
            _match.Begin();
            _match.Tick(5f);
            _match.NotifyDropComplete();
            _match.Tick(12.5f);
            _bus.Kill(1, 100);

            Assert.AreEqual(12.5f, _match.GetDeathTime(new PlayerId(1)), 1e-3f);
            Assert.AreEqual(-1f, _match.GetDeathTime(new PlayerId(0)), 1e-6f, "Hayattaysa -1");
            Assert.AreEqual(-1f, _match.GetDeathTime(new PlayerId(999)), 1e-6f);
        }

        [Test]
        public void EliminationOrderFromMixedDeaths_GivesCorrectTeamPlacements()
        {
            // Ölümler timler arasında karışık sırayla gelir; tim sıralaması son üyenin ölüm anına göre belirlenir.
            MatchTestSetup.Populate(_match, 4, 2);
            _bus.Kill(0, 100);      // tim 0: 1 kaldı
            _bus.Kill(200, 100);    // tim 2: 1 kaldı
            _bus.Kill(201, 300);    // tim 2 elendi → #4
            _bus.Kill(300, 101);    // tim 3: 1 kaldı
            _bus.Kill(1, 301);      // tim 0 elendi → #3
            _bus.Kill(301, 100);    // tim 3 elendi → #2, tim 1 kazanır

            Assert.AreEqual(4, _match.GetTeamPlacement(2));
            Assert.AreEqual(3, _match.GetTeamPlacement(0));
            Assert.AreEqual(2, _match.GetTeamPlacement(3));
            Assert.AreEqual(1, _match.GetTeamPlacement(1));
            Assert.AreEqual(1, _match.WinnerTeam);
            Assert.AreEqual(new PlayerId(100), _match.WinnerId, "Kazanan timin komutanı temsil eder");
            Assert.AreEqual(3, _match.GetPlacement(new PlayerId(301)));
            Assert.AreEqual(8, _match.GetPlacement(new PlayerId(0)));
        }

        private static bool Contains(IReadOnlyCollection<PlayerId> ids, PlayerId id)
        {
            foreach (var candidate in ids)
            {
                if (candidate == id)
                    return true;
            }

            return false;
        }
    }

    public sealed class DamageableRegistryTests
    {
        private sealed class FakeTarget : IDamageable
        {
            public FakeTarget(int id) => OwnerId = new PlayerId(id);
            public PlayerId OwnerId { get; set; }
            public bool IsAlive => true;
            public void ApplyDamage(DamageInfo damage) { }
        }

        [Test]
        public void RegisterTryGetAndAll()
        {
            var registry = new DamageableRegistry();
            var a = new FakeTarget(1);
            var b = new FakeTarget(2);
            registry.Register(a);
            registry.Register(b);
            registry.Register(null);
            registry.Register(new FakeTarget(-1));

            Assert.AreEqual(2, registry.All.Count);
            Assert.IsTrue(registry.TryGet(new PlayerId(1), out var found));
            Assert.AreSame(a, found);
            Assert.IsFalse(registry.TryGet(PlayerId.Invalid, out var none));
            Assert.IsNull(none);

            registry.Unregister(a);
            Assert.IsFalse(registry.TryGet(new PlayerId(1), out _));
            Assert.AreEqual(1, registry.All.Count);
            Assert.DoesNotThrow(() => registry.Unregister(null));
        }

        [Test]
        public void UnregisterStaleInstance_KeepsNewerRegistration()
        {
            var registry = new DamageableRegistry();
            var old = new FakeTarget(5);
            var fresh = new FakeTarget(5);
            registry.Register(old);
            registry.Register(fresh);

            registry.Unregister(old);

            Assert.IsTrue(registry.TryGet(new PlayerId(5), out var found));
            Assert.AreSame(fresh, found);
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void SwapRemove_KeepsLookupsConsistent_AndIndexedIterationCoversAll()
        {
            var registry = new DamageableRegistry();
            var targets = new List<FakeTarget>();
            for (var i = 0; i < 6; i++)
            {
                var t = new FakeTarget(10 + i);
                targets.Add(t);
                registry.Register(t);
            }

            registry.Unregister(targets[0]);                  // ortadan silme: son eleman yerine taşınır
            Assert.IsTrue(registry.Unregister(new PlayerId(13)));
            Assert.IsFalse(registry.Unregister(new PlayerId(13)));
            Assert.IsFalse(registry.Unregister(PlayerId.Invalid));

            Assert.AreEqual(4, registry.Count);
            Assert.AreEqual(4, registry.All.Count);
            foreach (var id in new[] { 11, 12, 14, 15 })
            {
                Assert.IsTrue(registry.TryGet(new PlayerId(id), out var found), "kimlik " + id);
                Assert.AreEqual(id, found.OwnerId.Value);
                Assert.IsTrue(registry.Contains(new PlayerId(id)));
            }

            Assert.IsFalse(registry.Contains(new PlayerId(10)));
            var sum = 0;
            for (var i = 0; i < registry.Count; i++)
                sum += registry[i].OwnerId.Value;
            Assert.AreEqual(11 + 12 + 14 + 15, sum);

            var copy = new List<IDamageable>();
            registry.CopyTo(copy);
            Assert.AreEqual(4, copy.Count);

            registry.Clear();
            Assert.AreEqual(0, registry.Count);
            Assert.IsFalse(registry.TryGet(new PlayerId(11), out _));
        }

        [Test]
        public void OwnerIdChangedAfterRegister_DoesNotCorruptIndex()
        {
            var registry = new DamageableRegistry();
            var a = new FakeTarget(1);
            var b = new FakeTarget(2);
            var c = new FakeTarget(3);
            registry.Register(a);
            registry.Register(b);
            registry.Register(c);

            c.OwnerId = new PlayerId(99);                       // kayıttan sonra kimlik değişti
            Assert.IsTrue(registry.Unregister(new PlayerId(1))); // c, a'nın yerine taşınır

            Assert.IsTrue(registry.TryGet(new PlayerId(3), out var found));
            Assert.AreSame(c, found);
            Assert.IsTrue(registry.TryGet(new PlayerId(2), out found));
            Assert.AreSame(b, found);
            Assert.AreEqual(2, registry.Count);
        }
    }
}
