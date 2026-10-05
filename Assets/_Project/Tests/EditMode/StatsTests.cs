using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Tests.Match
{
    public sealed class StatsTests
    {
        private MatchTestBus _bus;
        private MatchService _match;
        private MatchStatsService _stats;

        [SetUp]
        public void SetUp()
        {
            _bus = new MatchTestBus();
            _match = new MatchService(MatchTestSetup.Config(3, 3), _bus);
            MatchTestSetup.Populate(_match, 3, 3, localId: 0);
            _stats = new MatchStatsService(_bus, _match, _match);
            _match.Begin();
            _match.Tick(5f);
            _match.NotifyDropComplete();
        }

        private void Fire(int shooter, int count = 1)
        {
            for (var i = 0; i < count; i++)
                _bus.Publish(new WeaponFiredEvent(new PlayerId(shooter), "ar_mpt76", 10, Float3.Zero));
        }

        private void Hit(int attacker, int victim, float damage, bool headshot = false, bool kill = false) =>
            _bus.Publish(new HitConfirmedEvent(new PlayerId(attacker), new PlayerId(victim), damage, headshot, kill, false));

        [Test]
        public void ShotsHitsAccuracyAndDamage()
        {
            Fire(0, 10);
            Hit(0, 100, 30f);
            Hit(0, 100, 45f, headshot: true);
            Hit(0, 200, 25f);

            var s = _stats.Get(new PlayerId(0));
            Assert.AreEqual(10, s.ShotsFired);
            Assert.AreEqual(3, s.ShotsHit);
            Assert.AreEqual(100f, s.DamageDealt, 1e-4f);
            Assert.AreEqual(1, s.Headshots);
            Assert.AreEqual(0.3f, s.Accuracy, 1e-4f);
        }

        [Test]
        public void Hits_CannotExceedShots_ForPelletsOrExplosions()
        {
            Fire(0, 1);
            for (var i = 0; i < 8; i++)
                Hit(0, 100, 10f);

            var s = _stats.Get(new PlayerId(0));
            Assert.AreEqual(1, s.ShotsHit);
            Assert.AreEqual(80f, s.DamageDealt, 1e-4f, "Hasarın tamamı sayılır");
            Assert.LessOrEqual(_stats.BuildResult(new PlayerId(0)).Accuracy, 1f);
        }

        [Test]
        public void AllyAndSelfDamage_NotCounted()
        {
            Fire(0, 4);
            Hit(0, 1, 50f);
            Hit(0, 0, 20f);
            Hit(-1, 0, 20f);

            var s = _stats.Get(new PlayerId(0));
            Assert.AreEqual(0f, s.DamageDealt, 1e-6f);
            Assert.AreEqual(0, s.ShotsHit);
        }

        [Test]
        public void Kills_TeamKills_AndKillerName()
        {
            _match.Tick(42f);
            _bus.Kill(100, 0, "ar_mpt76", true);
            _bus.Kill(101, 1);
            _bus.Kill(200, 0);
            _bus.Kill(2, 1);          // dost öldürme: leş sayılmaz
            _bus.Kill(201, 201);      // kendini öldürme: leş sayılmaz

            Assert.AreEqual(2, _stats.Get(new PlayerId(0)).Kills);
            Assert.AreEqual(1, _stats.Get(new PlayerId(1)).Kills);
            Assert.AreEqual(3, _stats.GetTeamKills(0));
            Assert.AreEqual(0, _stats.GetTeamKills(2));

            Assert.AreEqual("Yzb. Asker0", _stats.GetKillerName(new PlayerId(100)), "Rütbeli görünen ad");
            Assert.AreEqual("Uzm.Çvş. Asker1", _stats.GetKillerName(new PlayerId(2)));
            Assert.IsNull(_stats.GetKillerName(new PlayerId(0)));

            var victim = _stats.Get(new PlayerId(100));
            Assert.AreEqual(42f, victim.SurvivalSeconds, 1e-3f);
            Assert.AreEqual(9, victim.Placement);
        }

        [Test]
        public void EnvironmentalDeath_KillerNameIsSourceName()
        {
            _bus.Kill(100, -1, DamageSourceIds.Zone);
            var name = _stats.GetKillerName(new PlayerId(100));

            Assert.IsFalse(string.IsNullOrEmpty(name));
            Assert.AreEqual(WeaponCatalog.GetDisplayName(DamageSourceIds.Zone), name, "Türkçe kaynak adı");
            Assert.AreEqual(0, _stats.GetTeamKills(0));

            _bus.Kill(101, -1);
            Assert.IsFalse(string.IsNullOrEmpty(_stats.GetKillerName(new PlayerId(101))));
        }

        [Test]
        public void DuplicateDeath_IsIgnored()
        {
            _bus.Kill(100, 0);
            _bus.Kill(100, 0);

            Assert.AreEqual(1, _stats.Get(new PlayerId(0)).Kills);
            Assert.AreEqual(1, _stats.GetTeamKills(0));
        }

        [Test]
        public void BuildResult_WinnerTeam_AllFields()
        {
            Fire(0, 4);
            Hit(0, 100, 60f, headshot: true);
            _match.Tick(100f);

            MatchTestSetup.KillTeam(_bus, 1, 3, killer: 0);
            _bus.Kill(1, 200);           // kazanan timden şehit
            _match.Tick(50f);
            MatchTestSetup.KillTeam(_bus, 2, 3, killer: 2);

            Assert.AreEqual(MatchPhase.Ending, _match.CurrentPhase);

            var local = _stats.BuildResult(new PlayerId(0));
            Assert.IsTrue(local.IsWinner);
            Assert.AreEqual(1, local.Placement);
            Assert.AreEqual(9, local.TotalPlayers);
            Assert.AreEqual(3, local.Kills);
            Assert.AreEqual(1, local.Headshots);
            Assert.AreEqual(60f, local.DamageDealt, 1e-4f);
            Assert.AreEqual(150f, local.SurvivalSeconds, 1e-2f);
            Assert.AreEqual(0.25f, local.Accuracy, 1e-4f);
            Assert.IsNull(local.KillerName);
            Assert.AreEqual(1, local.TeamPlacement);
            Assert.AreEqual(3, local.TeamCount);
            Assert.AreEqual("Kartal Timi", local.TeamName);
            Assert.AreEqual(6, local.TeamKills);

            // Kazanan timin şehidi de kazanır ama bireysel sıralaması korunur.
            var fallen = _stats.BuildResult(new PlayerId(1));
            Assert.IsTrue(fallen.IsWinner);
            Assert.AreEqual(1, fallen.TeamPlacement);
            Assert.AreEqual(6, fallen.Placement);
            Assert.AreEqual(100f, fallen.SurvivalSeconds, 1e-2f);
            Assert.AreEqual("Yzb. Asker200", fallen.KillerName);
        }

        [Test]
        public void BuildResult_EliminatedTeam()
        {
            _match.Tick(30f);
            MatchTestSetup.KillTeam(_bus, 2, 3, killer: 100);

            var r = _stats.BuildResult(new PlayerId(202));
            Assert.IsFalse(r.IsWinner);
            Assert.AreEqual(7, r.Placement);
            Assert.AreEqual(3, r.TeamPlacement);
            Assert.AreEqual(3, r.TeamCount);
            Assert.AreEqual("Şimşek Timi", r.TeamName);
            Assert.AreEqual("Yzb. Asker100", r.KillerName);
            Assert.AreEqual(30f, r.SurvivalSeconds, 1e-2f);
            Assert.AreEqual(0, r.TeamKills);

            var bozkurt = _stats.BuildResult(new PlayerId(100));
            Assert.AreEqual(3, bozkurt.TeamKills);
            Assert.AreEqual("Bozkurt Timi", bozkurt.TeamName);
        }

        [Test]
        public void BuildResult_MidMatch_AliveUsesCurrentStanding()
        {
            _match.Tick(20f);
            _bus.Kill(200, 0);

            var r = _stats.BuildResult(new PlayerId(0));
            Assert.IsFalse(r.IsWinner);
            Assert.AreEqual(8, r.Placement);
            Assert.AreEqual(3, r.TeamPlacement);
            Assert.AreEqual(20f, r.SurvivalSeconds, 1e-2f);
        }

        [Test]
        public void SubscriptionOrder_DoesNotChangePlacement()
        {
            var bus = new MatchTestBus();
            var config = MatchTestSetup.Config(2, 2);
            // İstatistik servisi maçtan ÖNCE abone olsun.
            MatchService match = null;
            var directory = new LateDirectory();
            var stats = new MatchStatsService(bus, new LateMatch(() => match), directory);
            match = new MatchService(config, bus);
            directory.Inner = match;
            MatchTestSetup.Populate(match, 2, 2);

            bus.Kill(0, 100);

            Assert.AreEqual(4, stats.Get(new PlayerId(0)).Placement);
            Assert.AreEqual(4, stats.BuildResult(new PlayerId(0)).Placement);
            Assert.AreEqual(4, match.GetPlacement(new PlayerId(0)));
            stats.Dispose();
            match.Dispose();
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            _stats.Dispose();
            _stats.Dispose();

            Fire(0, 3);
            _bus.Kill(100, 0);

            Assert.AreEqual(0, _stats.Get(new PlayerId(0)).ShotsFired);
            Assert.AreEqual(0, _stats.Get(new PlayerId(0)).Kills);
        }

        [Test]
        public void NullDependencies_AreTolerated()
        {
            var stats = new MatchStatsService(null, null, null);
            Assert.IsNotNull(stats.Get(new PlayerId(3)));
            Assert.IsNotNull(stats.Get(PlayerId.Invalid));
            Assert.DoesNotThrow(() => stats.BuildResult(new PlayerId(3)));
            Assert.DoesNotThrow(stats.Dispose);
        }

        /// <summary>IMatchService'i tembel çözen sarmalayıcı (abone sırası testi için).</summary>
        private sealed class LateMatch : IMatchService
        {
            private readonly System.Func<MatchService> _get;
            public LateMatch(System.Func<MatchService> get) => _get = get;
            private MatchService M => _get();
            public MatchPhase CurrentPhase => M.CurrentPhase;
            public MatchConfig Config => M.Config;
            public int AlivePlayerCount => M.AlivePlayerCount;
            public int TotalPlayers => M.TotalPlayers;
            public int AliveTeamCount => M.AliveTeamCount;
            public int TeamCount => M.TeamCount;
            public PlayerId WinnerId => M.WinnerId;
            public int WinnerTeam => M.WinnerTeam;
            public float PhaseElapsedSeconds => M.PhaseElapsedSeconds;
            public float MatchElapsedSeconds => M.MatchElapsedSeconds;
            public IReadOnlyCollection<PlayerId> AliveIds => M.AliveIds;
            public void RegisterCombatant(PlayerId id, string displayName, bool isLocalPlayer, int team, TeamRole role) => M.RegisterCombatant(id, displayName, isLocalPlayer, team, role);
            public void TransitionTo(MatchPhase phase) => M.TransitionTo(phase);
            public void RegisterPlayerDeath(PlayerId playerId) => M.RegisterPlayerDeath(playerId);
            public bool IsAlive(PlayerId id) => M.IsAlive(id);
            public bool IsTeamAlive(int team) => M.IsTeamAlive(team);
            public int GetAliveCountInTeam(int team) => M.GetAliveCountInTeam(team);
            public int GetPlacement(PlayerId id) => M.GetPlacement(id);
            public int GetTeamPlacement(int team) => M.GetTeamPlacement(team);
        }

        private sealed class LateDirectory : ICombatantDirectory, ITeamRelations
        {
            public MatchService Inner;
            public string GetDisplayName(PlayerId id) => Inner.GetDisplayName(id);
            public bool IsLocalPlayer(PlayerId id) => Inner.IsLocalPlayer(id);
            public int GetTeam(PlayerId id) => Inner.GetTeam(id);
            public bool AreAllies(PlayerId a, PlayerId b) => Inner.AreAllies(a, b);
            public string GetTeamName(int team) => Inner.GetTeamName(team);
            public TeamRole GetRole(PlayerId id) => Inner.GetRole(id);
        }
    }

    public sealed class KillFeedTests
    {
        private MatchTestBus _bus;
        private MatchService _match;
        private KillFeedService _feed;

        [SetUp]
        public void SetUp()
        {
            _bus = new MatchTestBus();
            _match = new MatchService(MatchTestSetup.Config(3, 3), _bus);
            MatchTestSetup.Populate(_match, 3, 3, localId: 1);
            _feed = new KillFeedService(_bus, _match, 4);
        }

        [Test]
        public void Entry_HasRankedNames_WeaponAndHeadshot()
        {
            KillFeedEntry? added = null;
            _feed.EntryAdded += e => added = e;

            _bus.Kill(100, 0, "ar_mpt76", true);

            Assert.AreEqual(1, _feed.Entries.Count);
            var entry = _feed.Entries[0];
            Assert.AreEqual("Yzb. Asker0", entry.KillerName);
            Assert.AreEqual("Yzb. Asker100", entry.VictimName);
            Assert.IsFalse(string.IsNullOrEmpty(entry.WeaponName), "Katalog yoksa kimliğe düşer");
            Assert.IsTrue(entry.IsHeadshot);
            Assert.IsTrue(added.HasValue);
            Assert.AreEqual(entry.VictimName, added.Value.VictimName);
        }

        [Test]
        public void AllyFlags_RelativeToLocalTeam()
        {
            _bus.Kill(100, 0);   // dost (yerel değil) düşmanı öldürdü
            _bus.Kill(2, 200);   // düşman dostu öldürdü
            _bus.Kill(201, 1);   // yerel oyuncu öldürdü
            _bus.Kill(1, 101);   // yerel oyuncu öldü

            var e = _feed.Entries;
            Assert.IsTrue(e[0].KillerIsAlly);
            Assert.IsFalse(e[0].KillerIsLocal);
            Assert.IsFalse(e[0].VictimIsAlly);

            Assert.IsFalse(e[1].KillerIsAlly);
            Assert.IsTrue(e[1].VictimIsAlly);

            Assert.IsTrue(e[2].KillerIsLocal);
            Assert.IsTrue(e[2].KillerIsAlly);
            Assert.IsFalse(e[2].VictimIsAlly);

            Assert.IsTrue(e[3].VictimIsLocal);
            Assert.IsTrue(e[3].VictimIsAlly);
            Assert.IsFalse(e[3].KillerIsAlly);
        }

        [Test]
        public void Capacity_DropsOldest_NewestLast()
        {
            for (var i = 0; i < 3; i++)
            {
                _bus.Kill(100 + i, 0);
                _bus.Kill(200 + i, 0);
            }

            Assert.AreEqual(4, _feed.Entries.Count);
            Assert.AreEqual("Uzm.Çvş. Asker101", _feed.Entries[0].VictimName);
            Assert.AreEqual("Uzm.Çvş. Asker202", _feed.Entries[3].VictimName);
        }

        [Test]
        public void EnvironmentalDeath_EmptyKiller_SourceAsWeapon()
        {
            _bus.Kill(100, -1, "zone");
            _bus.Kill(101, -1);

            var zone = _feed.Entries[0];
            Assert.AreEqual(string.Empty, zone.KillerName);
            Assert.IsFalse(string.IsNullOrEmpty(zone.WeaponName));
            Assert.IsFalse(zone.KillerIsAlly);
            Assert.IsFalse(zone.KillerIsLocal);
            Assert.AreEqual("Çevre", _feed.Entries[1].WeaponName);
        }

        [Test]
        public void WithoutTeamRelations_LocalIsStillAlly()
        {
            var bus = new MatchTestBus();
            var feed = new KillFeedService(bus, new NamesOnly(), 0);
            Assert.AreEqual(KillFeedService.DefaultCapacity, feed.Capacity);

            bus.Kill(5, 7);
            Assert.IsTrue(feed.Entries[0].KillerIsLocal);
            Assert.IsTrue(feed.Entries[0].KillerIsAlly);
            Assert.IsFalse(feed.Entries[0].VictimIsAlly);
            Assert.AreEqual("N5", feed.Entries[0].VictimName);
        }

        [Test]
        public void NullDirectory_FallsBackToIds()
        {
            var bus = new MatchTestBus();
            var feed = new KillFeedService(bus, null);
            bus.Kill(5, 7, "bilinmeyen_silah");

            Assert.AreEqual("Asker 7", feed.Entries[0].KillerName);
            Assert.AreEqual("Asker 5", feed.Entries[0].VictimName);
            Assert.IsFalse(string.IsNullOrEmpty(feed.Entries[0].WeaponName));
        }

        [Test]
        public void WeaponAndEnvironmentNames_ComeFromCatalog()
        {
            _bus.Kill(100, 0, WeaponIds.Mpt76);
            _bus.Kill(101, -1, DamageSourceIds.Zone);
            _bus.Kill(102, 200, DamageSourceIds.Artillery);

            Assert.AreEqual(WeaponCatalog.GetDisplayName(WeaponIds.Mpt76), _feed.Entries[0].WeaponName);
            Assert.AreEqual(WeaponCatalog.GetDisplayName(DamageSourceIds.Zone), _feed.Entries[1].WeaponName);
            Assert.AreEqual(WeaponCatalog.GetDisplayName(DamageSourceIds.Artillery), _feed.Entries[2].WeaponName);
            Assert.AreEqual("Yzb. Asker200", _feed.Entries[2].KillerName, "Topçu ateşini çağıran öldüren sayılır");
        }

        [Test]
        public void TeamRelationsDirectory_LearnsLocalTeam_ForLaterEntries()
        {
            var bus = new MatchTestBus();
            var directory = new TeamsDirectory();
            var feed = new KillFeedService(bus, directory);

            bus.Kill(12, 21);   // yerel yok: hiçbir taraf dost işaretlenemez
            Assert.IsFalse(feed.Entries[0].KillerIsAlly);
            Assert.IsFalse(feed.Entries[0].VictimIsAlly);

            bus.Kill(22, 10);   // yerel oyuncu (10, tim 1) öldürdü → yerel tim öğrenildi
            bus.Kill(13, 23);   // yerel olmayan kayıt: 13 timdaşı (dost kurban), 23 düşman

            Assert.IsTrue(feed.Entries[1].KillerIsLocal);
            Assert.IsTrue(feed.Entries[2].VictimIsAlly);
            Assert.IsFalse(feed.Entries[2].KillerIsAlly);

            feed.LocalTeamOverride = 2;
            bus.Kill(14, 24);
            Assert.IsTrue(feed.Entries[3].KillerIsAlly, "Elle belirlenen tim önceliklidir");
            Assert.IsFalse(feed.Entries[3].VictimIsAlly);
        }

        [Test]
        public void ExternalAdd_RespectsCapacity_AndRaisesEvent()
        {
            var raised = 0;
            _feed.EntryAdded += _ => raised++;
            for (var i = 0; i < 6; i++)
                _feed.Add(new KillFeedEntry("A" + i, "B" + i, "MPT-76", false, false, false));

            Assert.AreEqual(4, _feed.Entries.Count);
            Assert.AreEqual("A2", _feed.Entries[0].KillerName);
            Assert.AreEqual(6, raised);

            _feed.Clear();
            Assert.IsEmpty(_feed.Entries);
        }

        [Test]
        public void Dispose_StopsListening()
        {
            _feed.Dispose();
            _bus.Kill(100, 0);
            Assert.IsEmpty(_feed.Entries);
            Assert.AreEqual(0, _bus.HandlerCount<PlayerDiedEvent>() - 1, "Yalnızca maç servisi abone kalır");
        }

        private sealed class NamesOnly : ICombatantDirectory
        {
            public string GetDisplayName(PlayerId id) => "N" + id.Value;
            public bool IsLocalPlayer(PlayerId id) => id.Value == 7;
        }

        /// <summary>MatchService olmayan dizin: tim = kimlik/10, yerel oyuncu 10.</summary>
        private sealed class TeamsDirectory : ICombatantDirectory, ITeamRelations
        {
            public string GetDisplayName(PlayerId id) => "Er " + id.Value;
            public bool IsLocalPlayer(PlayerId id) => id.Value == 10;
            public int GetTeam(PlayerId id) => id.IsValid ? id.Value / 10 : -1;
            public bool AreAllies(PlayerId a, PlayerId b) => GetTeam(a) >= 0 && GetTeam(a) == GetTeam(b);
            public string GetTeamName(int team) => team + ". Tim";
            public TeamRole GetRole(PlayerId id) => TeamRole.Rifleman;
        }
    }
}
