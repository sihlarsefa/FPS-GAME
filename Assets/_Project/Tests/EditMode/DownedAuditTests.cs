using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Tests.Match;
#if UNITY_EDITOR
using Project.Infrastructure.Events;
using Project.Presentation.Bootstrap;
#endif

namespace Project.Tests.EditMode
{
    /// <summary>Yaralı (DBNO) × araç/İHA/topçu/maç/istatistik/başarım etkileşim denetimi (saf mantık).</summary>
    [TestFixture]
    public sealed class DownedAuditTests
    {
        private static PlayerId P(int i) => new PlayerId(i);

        // ------------------------------------------------------------------ DownedRules

        [Test]
        public void Equipment_AndBoarding_BlockedWhileDownedOrDead()
        {
            Assert.IsTrue(DownedRules.CanUseEquipment(true, false));
            Assert.IsFalse(DownedRules.CanUseEquipment(true, true), "yaralı İHA/topçu/emir kullanamaz");
            Assert.IsFalse(DownedRules.CanUseEquipment(false, false));
            Assert.IsTrue(DownedRules.CanBoardVehicle(true, false));
            Assert.IsFalse(DownedRules.CanBoardVehicle(true, true), "yaralı araca binemez");
            Assert.IsFalse(DownedRules.CanBoardVehicle(false, false));
        }

        [Test]
        public void DownedDriver_IsEjected_OnlyWhileAliveAndDowned()
        {
            Assert.IsTrue(DownedRules.ShouldEjectFromVehicle(true, true));
            Assert.IsFalse(DownedRules.ShouldEjectFromVehicle(true, false));
            Assert.IsFalse(DownedRules.ShouldEjectFromVehicle(false, true), "ölü sürücüyü ölüm akışı indirir");
        }

        [Test]
        public void Passenger_Unboarded_WhenDeadDownedOrSlotLost()
        {
            Assert.IsFalse(DownedRules.ShouldUnboardPassenger(false, false, false));
            Assert.IsTrue(DownedRules.ShouldUnboardPassenger(true, false, false));
            Assert.IsTrue(DownedRules.ShouldUnboardPassenger(false, true, false));
            Assert.IsTrue(DownedRules.ShouldUnboardPassenger(false, false, true));
        }

        [Test]
        public void TeamWiped_NeedsDownedMembersAndNoHealthy()
        {
            Assert.IsTrue(DownedRules.IsTeamWiped(0, 2));
            Assert.IsFalse(DownedRules.IsTeamWiped(1, 2));
            Assert.IsFalse(DownedRules.IsTeamWiped(0, 0), "kimse yoksa öldürülecek yaralı da yok");
        }

        // ------------------------------------------------------------------ ReviveService

        [Test]
        public void AttackerOf_ReturnsDowner_UntilRemoved()
        {
            var s = new ReviveService();
            Assert.IsTrue(s.TryDown(P(1), P(9), 0, 1));
            Assert.AreEqual(P(9), s.AttackerOf(P(1)));
            Assert.IsFalse(s.AttackerOf(P(2)).IsValid);
            s.Remove(P(1));
            Assert.IsFalse(s.AttackerOf(P(1)).IsValid);
        }

        // ------------------------------------------------------------------ Maç / istatistik

        private static (MatchTestBus bus, MatchService match, MatchStatsService stats) Setup()
        {
            var bus = new MatchTestBus();
            var match = new MatchService(MatchTestSetup.Config(3, 3), bus);
            MatchTestSetup.Populate(match, 3, 3, localId: 0);
            var stats = new MatchStatsService(bus, match, match);
            match.Begin();
            match.Tick(5f);
            match.NotifyDropComplete();
            return (bus, match, stats);
        }

        [Test]
        public void DownedThenBledOut_CountsKillOnce_AndDownAloneDoesNotKill()
        {
            var (bus, match, stats) = Setup();
            bus.Publish(new DownedEvent(P(100), P(0), 1));

            Assert.AreEqual(0, stats.Get(P(0)).Kills, "yaralı düşürmek leş değildir");
            Assert.IsTrue(match.IsAlive(P(100)));

            bus.Kill(100, 0, "bleedout");
            bus.Kill(100, 0, "bleedout"); // yinelenen olay
            Assert.AreEqual(1, stats.Get(P(0)).Kills);
            Assert.IsFalse(match.IsAlive(P(100)));
        }

        [Test]
        public void TeamWipe_ThroughDownedEvents_NoDoubleCountOfKillsOrPlacement()
        {
            var (bus, match, stats) = Setup();
            bus.Publish(new DownedEvent(P(100), P(0), 1));
            bus.Publish(new DownedEvent(P(101), P(0), 1));
            Assert.AreEqual(3, match.GetAliveCountInTeam(1), "yaralılar yaşıyor: tim henüz elenmedi");
            Assert.IsTrue(match.IsAlive(P(100)));

            // Son sağ üye yaralı düşünce takım elenir (MatchService), sonra Combatant'lar ölür.
            bus.Publish(new DownedEvent(P(102), P(0), 1));
            Assert.IsFalse(match.IsAlive(P(100)));
            Assert.IsFalse(match.IsAlive(P(101)));
            Assert.IsFalse(match.IsAlive(P(102)));
            var placement = match.GetPlacement(P(100));

            bus.Kill(100, 0, "bleedout");
            bus.Kill(101, 0, "bleedout");
            bus.Kill(102, 0, "bleedout");

            Assert.AreEqual(3, stats.Get(P(0)).Kills);
            Assert.AreEqual(3, stats.GetTeamKills(0));
            Assert.AreEqual(placement, match.GetPlacement(P(100)), "ölüm olayı sıralamayı değiştirmez");
            Assert.AreEqual(6, match.AlivePlayerCount, "iki tim (6 asker) kaldı");
            Assert.AreEqual(0, match.GetAliveCountInTeam(1));
        }

        [Test]
        public void HealthyAllyDeath_WithDownedTeammate_EventuallyEliminatesTeamOnce()
        {
            var (bus, match, stats) = Setup();
            bus.Publish(new DownedEvent(P(100), P(0), 1));
            bus.Kill(101, 0, "ar_mpt76"); // sağ müttefik doğrudan ölür (yaralı arkadaşı varken)
            bus.Kill(102, 0, "ar_mpt76");
            Assert.IsTrue(match.IsAlive(P(100)), "yaralı hâlâ kayıtta yaşıyor");

            // Combatant.OnHealthDied → KillTeamIfWiped: yaralı, onu düşüren saldırgana yazılarak ölür.
            bus.Kill(100, 0, "bleedout");
            Assert.IsFalse(match.IsAlive(P(100)));
            Assert.AreEqual(3, stats.Get(P(0)).Kills);
            Assert.AreEqual(3, stats.GetTeamKills(0));
        }

        [Test]
        public void RevivedEvent_ClearsMatchDowned_SoLaterDownDoesNotWipeTeam()
        {
            var (bus, match, _) = Setup();
            bus.Publish(new DownedEvent(P(100), P(0), 1));
            bus.Publish(new RevivedEvent(P(100), PlayerId.Invalid)); // kaldıransız sıfırlama (Combatant.Revive)
            bus.Publish(new DownedEvent(P(101), P(0), 1));

            Assert.IsTrue(match.IsAlive(P(100)));
            Assert.IsTrue(match.IsAlive(P(101)));
            Assert.IsTrue(match.IsAlive(P(102)), "102 sağlam: takım elenmemeli");
        }

#if UNITY_EDITOR
        // ------------------------------------------------------------------ Başarım

        [Test]
        public void ReviveAchievement_CountsLocalRevivesOnly()
        {
            var bus = new EventBus();
            var svc = new AchievementService(null, new List<AchievementDefinition>
            {
                new AchievementDefinition { id = "r", metric = "revives", target = 99 },
            });
            var me = P(1);
            using (new AchievementTracker(bus, svc, me, 0, () => 0f))
            {
                bus.Publish(new RevivedEvent(P(2), me));                 // sayılır
                bus.Publish(new RevivedEvent(P(3), P(4)));               // başkası
                bus.Publish(new RevivedEvent(me, P(2)));                 // kendisi kaldırıldı
                bus.Publish(new RevivedEvent(P(5), PlayerId.Invalid));   // kaldıransız sıfırlama
            }

            Assert.AreEqual(1, svc.GetProgress("revives"));
        }
#endif
    }
}
