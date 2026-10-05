using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;

namespace Project.Tests.EditMode.Sim
{
    public sealed class RankCatalogTests
    {
        [TestCase(MilitaryRank.Er, "Er", "Er")]
        [TestCase(MilitaryRank.Onbasi, "Onbaşı", "Onb.")]
        [TestCase(MilitaryRank.Cavus, "Çavuş", "Çvş.")]
        [TestCase(MilitaryRank.SozlesmeliEr, "Sözleşmeli Er", "Sözl.Er")]
        [TestCase(MilitaryRank.UzmanOnbasi, "Uzman Onbaşı", "Uzm.Onb.")]
        [TestCase(MilitaryRank.UzmanCavus, "Uzman Çavuş", "Uzm.Çvş.")]
        [TestCase(MilitaryRank.AstsubayCavus, "Astsubay Çavuş", "Astsb.Çvş.")]
        [TestCase(MilitaryRank.AstsubayKidemliCavus, "Astsubay Kıdemli Çavuş", "Astsb.Kd.Çvş.")]
        [TestCase(MilitaryRank.AstsubayUstcavus, "Astsubay Üstçavuş", "Astsb.Üçvş.")]
        [TestCase(MilitaryRank.AstsubayKidemliUstcavus, "Astsubay Kıdemli Üstçavuş", "Astsb.Kd.Üçvş.")]
        [TestCase(MilitaryRank.AstsubayBascavus, "Astsubay Başçavuş", "Astsb.Bçvş.")]
        [TestCase(MilitaryRank.AstsubayKidemliBascavus, "Astsubay Kıdemli Başçavuş", "Astsb.Kd.Bçvş.")]
        [TestCase(MilitaryRank.Astegmen, "Asteğmen", "Asteğmen")]
        [TestCase(MilitaryRank.Tegmen, "Teğmen", "Teğmen")]
        [TestCase(MilitaryRank.Ustegmen, "Üsteğmen", "Üsteğmen")]
        [TestCase(MilitaryRank.Yuzbasi, "Yüzbaşı", "Yzb.")]
        [TestCase(MilitaryRank.Binbasi, "Binbaşı", "Bnb.")]
        [TestCase(MilitaryRank.Yarbay, "Yarbay", "Yb.")]
        [TestCase(MilitaryRank.Albay, "Albay", "Alb.")]
        public void Names_AreTurkish(MilitaryRank rank, string fullName, string shortName)
        {
            Assert.AreEqual(fullName, RankCatalog.GetName(rank));
            Assert.AreEqual(shortName, RankCatalog.GetShortName(rank));
        }

        [TestCase(MilitaryRank.Er, "Er/Erbaş")]
        [TestCase(MilitaryRank.Onbasi, "Er/Erbaş")]
        [TestCase(MilitaryRank.Cavus, "Er/Erbaş")]
        [TestCase(MilitaryRank.SozlesmeliEr, "Er/Erbaş")]
        [TestCase(MilitaryRank.UzmanOnbasi, "Uzman Erbaş")]
        [TestCase(MilitaryRank.UzmanCavus, "Uzman Erbaş")]
        [TestCase(MilitaryRank.AstsubayCavus, "Astsubay")]
        [TestCase(MilitaryRank.AstsubayKidemliBascavus, "Astsubay")]
        [TestCase(MilitaryRank.Astegmen, "Subay")]
        [TestCase(MilitaryRank.Yuzbasi, "Subay")]
        [TestCase(MilitaryRank.Albay, "Subay")]
        public void Category_MatchesTsk(MilitaryRank rank, string category)
        {
            Assert.AreEqual(category, RankCatalog.GetCategory(rank));
        }

        [Test]
        public void AllRanks_HaveNamesAndAreOrdered()
        {
            Assert.AreEqual(19, RankCatalog.All.Count);
            for (var i = 0; i < RankCatalog.All.Count; i++)
            {
                var rank = RankCatalog.All[i];
                Assert.AreEqual(i, (int)rank);
                Assert.IsFalse(string.IsNullOrEmpty(RankCatalog.GetName(rank)));
                Assert.IsFalse(string.IsNullOrEmpty(RankCatalog.GetShortName(rank)));
            }

            Assert.IsTrue(RankCatalog.IsOfficer(MilitaryRank.Yuzbasi));
            Assert.IsFalse(RankCatalog.IsOfficer(MilitaryRank.AstsubayKidemliBascavus));
            Assert.IsTrue(RankCatalog.IsNonCommissionedOfficer(MilitaryRank.AstsubayCavus));
            Assert.Greater(RankCatalog.CompareSeniority(MilitaryRank.Yuzbasi, MilitaryRank.Ustegmen), 0);
            Assert.Less(RankCatalog.CompareSeniority(MilitaryRank.Er, MilitaryRank.Onbasi), 0);
        }

        [Test]
        public void OutOfRangeRank_IsHandled()
        {
            Assert.AreEqual("Er", RankCatalog.GetShortName((MilitaryRank)(-4)));
            Assert.AreEqual("Albay", RankCatalog.GetName((MilitaryRank)99));
            Assert.DoesNotThrow(() => RankCatalog.GetCategory((MilitaryRank)50));
        }

        [Test]
        public void ExperienceThresholds_StrictlyIncreasing()
        {
            Assert.AreEqual(0, RankCatalog.RequiredExperience(MilitaryRank.Er));
            for (var i = 1; i < RankCatalog.All.Count; i++)
            {
                Assert.Greater(RankCatalog.RequiredExperience(RankCatalog.All[i]), RankCatalog.RequiredExperience(RankCatalog.All[i - 1]));
            }
        }

        [Test]
        public void RankForExperience_MatchesThresholds()
        {
            foreach (var rank in RankCatalog.All)
            {
                var xp = RankCatalog.RequiredExperience(rank);
                Assert.AreEqual(rank, RankCatalog.RankForExperience(xp));
                if (rank != MilitaryRank.Er)
                    Assert.AreEqual((MilitaryRank)((int)rank - 1), RankCatalog.RankForExperience(xp - 1));
            }

            Assert.AreEqual(MilitaryRank.Er, RankCatalog.RankForExperience(-500));
            Assert.AreEqual(MilitaryRank.Albay, RankCatalog.RankForExperience(int.MaxValue));
        }

        [Test]
        public void XpAliases_MatchLongNames()
        {
            foreach (var rank in RankCatalog.All)
            {
                var xp = RankCatalog.XpForRank(rank);
                Assert.AreEqual(RankCatalog.RequiredExperience(rank), xp);
                Assert.AreEqual(RankCatalog.RankForExperience(xp), RankCatalog.RankForXp(xp));
                Assert.AreEqual(RankCatalog.RankForExperience(xp + 7), RankCatalog.RankForXp(xp + 7));
            }
        }

        [Test]
        public void YuzbasiIsReachableWithReasonableCareer()
        {
            var xp = RankCatalog.RequiredExperience(MilitaryRank.Yuzbasi);
            Assert.Greater(xp, 20000);
            Assert.Less(xp, 120000);
        }

        [Test]
        public void ProgressAndRemainingToNextRank()
        {
            var onbasi = RankCatalog.RequiredExperience(MilitaryRank.Onbasi);
            Assert.AreEqual(0f, RankCatalog.ProgressToNextRank(0), 0.0001f);
            Assert.AreEqual(0.5f, RankCatalog.ProgressToNextRank(onbasi / 2), 0.01f);
            Assert.AreEqual(onbasi, RankCatalog.ExperienceToNextRank(0));
            Assert.AreEqual(1f, RankCatalog.ProgressToNextRank(10000000), 0.0001f);
            Assert.AreEqual(0, RankCatalog.ExperienceToNextRank(10000000));
            Assert.IsTrue(RankCatalog.TryGetNextRank(MilitaryRank.Ustegmen, out var next));
            Assert.AreEqual(MilitaryRank.Yuzbasi, next);
            Assert.IsFalse(RankCatalog.TryGetNextRank(MilitaryRank.Albay, out _));
        }

        [Test]
        public void RankForTeamSlot_FollowsTeamStructure()
        {
            for (var seed = 1; seed <= 50; seed++)
            {
                var random = new SeededRandom(seed);
                var leader = RankCatalog.RankForTeamSlot(0, random);
                Assert.IsTrue(leader == MilitaryRank.Yuzbasi || leader == MilitaryRank.Ustegmen, leader.ToString());

                var deputy = RankCatalog.RankForTeamSlot(1, random);
                Assert.IsTrue(deputy == MilitaryRank.AstsubayKidemliCavus || deputy == MilitaryRank.AstsubayUstcavus, deputy.ToString());
                Assert.AreEqual("Astsubay", RankCatalog.GetCategory(deputy));

                Assert.AreEqual(MilitaryRank.UzmanCavus, RankCatalog.RankForTeamSlot(2, random));
                Assert.AreEqual(MilitaryRank.UzmanCavus, RankCatalog.RankForTeamSlot(3, random));

                for (var slot = 4; slot <= 5; slot++)
                {
                    var r = RankCatalog.RankForTeamSlot(slot, random);
                    Assert.IsTrue(r == MilitaryRank.UzmanOnbasi || r == MilitaryRank.SozlesmeliEr, r.ToString());
                }

                for (var slot = 6; slot <= 12; slot++)
                {
                    var r = RankCatalog.RankForTeamSlot(slot, random);
                    Assert.IsTrue(r == MilitaryRank.SozlesmeliEr || r == MilitaryRank.Er || r == MilitaryRank.Onbasi || r == MilitaryRank.Cavus, r.ToString());
                }
            }
        }

        [Test]
        public void RankForTeamSlot_IsNonIncreasingBySlotGroup()
        {
            // Gruplar: {0} komutan, {1} yardımcı, {2,3} uzman çavuşlar, {4,5} uzman onbaşı/sözleşmeli er, {6..9} piyade.
            // Her grubun en kıdemlisi, önceki grubun en kıdemsizinden kıdemli olamaz.
            var groups = new[] { new[] { 0 }, new[] { 1 }, new[] { 2, 3 }, new[] { 4, 5 }, new[] { 6, 7, 8, 9 } };
            for (var seed = 1; seed <= 100; seed++)
            {
                var random = new SeededRandom(seed);
                var previousMin = (int)MilitaryRank.Albay;
                foreach (var group in groups)
                {
                    var min = int.MaxValue;
                    var max = int.MinValue;
                    foreach (var slot in group)
                    {
                        var rank = (int)RankCatalog.RankForTeamSlot(slot, random);
                        min = Math.Min(min, rank);
                        max = Math.Max(max, rank);
                    }

                    Assert.LessOrEqual(max, previousMin, $"seed {seed} group starting {group[0]}");
                    previousMin = min;
                }
            }
        }

        [Test]
        public void RankForTeamSlot_WorksWithoutRandom()
        {
            Assert.AreEqual(MilitaryRank.Yuzbasi, RankCatalog.RankForTeamSlot(0, null));
            Assert.AreEqual(MilitaryRank.Er, RankCatalog.RankForTeamSlot(-1, null));
            Assert.DoesNotThrow(() => RankCatalog.RankForTeamSlot(9, null));
        }

        [Test]
        public void RankForTeamSlot_ProducesVariety()
        {
            var leaders = new HashSet<MilitaryRank>();
            var riflemen = new HashSet<MilitaryRank>();
            var random = new SeededRandom(17);
            for (var i = 0; i < 200; i++)
            {
                leaders.Add(RankCatalog.RankForTeamSlot(0, random));
                riflemen.Add(RankCatalog.RankForTeamSlot(7, random));
            }

            Assert.AreEqual(2, leaders.Count);
            Assert.AreEqual(4, riflemen.Count);
        }

        [Test]
        public void FormatName_UsesAbbreviation()
        {
            Assert.AreEqual("Yzb. Kartal", RankCatalog.FormatName(MilitaryRank.Yuzbasi, "Kartal"));
            Assert.AreEqual("Astsb.Kd.Çvş. Bozkurt", RankCatalog.FormatName(MilitaryRank.AstsubayKidemliCavus, " Bozkurt "));
            Assert.AreEqual("Uzm.Çvş. Atmaca", RankCatalog.FormatName(MilitaryRank.UzmanCavus, "Atmaca"));
            Assert.AreEqual("Er Şahin", RankCatalog.FormatName(MilitaryRank.Er, "Şahin"));
            Assert.AreEqual("Yüzbaşı", RankCatalog.FormatName(MilitaryRank.Yuzbasi, null));
            Assert.AreEqual("Teğmen", RankCatalog.FormatName(MilitaryRank.Tegmen, "  "));
            Assert.AreEqual("Yüzbaşı Kartal", RankCatalog.FormatFullName(MilitaryRank.Yuzbasi, "Kartal"));
        }
    }

    public sealed class NameRosterTests
    {
        [Test]
        public void Names_AtLeast80AndUnique()
        {
            var names = NameRoster.Names;
            Assert.GreaterOrEqual(names.Count, 80);
            var set = new HashSet<string>();
            foreach (var name in names)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(name));
                Assert.AreEqual(name.Trim(), name);
                Assert.IsTrue(set.Add(name), "tekrar eden ad: " + name);
            }
        }

        [Test]
        public void Names_ContainClassicTurkishCallsigns()
        {
            var names = new List<string>(NameRoster.Names);
            foreach (var expected in new[] { "Kartal", "Bozkurt", "Atmaca", "Şahin", "Yılmaz", "Demir", "Çelik", "Kaya", "Aydın", "Öztürk" })
                Assert.Contains(expected, names);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(10)]
        [TestCase(40)]
        [TestCase(60)]
        [TestCase(250)]
        public void CreateUnique_ReturnsDistinctNames(int count)
        {
            var list = NameRoster.CreateUnique(count, new SeededRandom(count + 1));
            Assert.AreEqual(count, list.Count);
            Assert.AreEqual(count, new HashSet<string>(list).Count);
        }

        [Test]
        public void CreateUnique_HandlesNegativeAndNullRandom()
        {
            Assert.AreEqual(0, NameRoster.CreateUnique(-5, new SeededRandom(1)).Count);
            var list = NameRoster.CreateUnique(12, null);
            Assert.AreEqual(12, list.Count);
            Assert.AreEqual(12, new HashSet<string>(list).Count);
            Assert.IsFalse(string.IsNullOrEmpty(NameRoster.Pick(null)));
        }

        [Test]
        public void CreateUnique_IsDeterministicAndShuffled()
        {
            var a = NameRoster.CreateUnique(20, new SeededRandom(8));
            var b = NameRoster.CreateUnique(20, new SeededRandom(8));
            var c = NameRoster.CreateUnique(20, new SeededRandom(9));
            var differs = false;
            for (var i = 0; i < 20; i++)
            {
                Assert.AreEqual(a[i], b[i]);
                differs |= a[i] != c[i];
            }

            Assert.IsTrue(differs);
        }
    }

    public sealed class ChainOfCommandTests
    {
        private SimRecordingEventBus _bus;
        private ChainOfCommandService _chain;

        [SetUp]
        public void SetUp()
        {
            _bus = new SimRecordingEventBus();
            _chain = new ChainOfCommandService(_bus);
        }

        private static PlayerId P(int value) => new(value);

        [Test]
        public void Chain_OrderedByRankThenRegistration()
        {
            _chain.Register(P(1), 0, MilitaryRank.UzmanCavus);
            _chain.Register(P(2), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(3), 0, MilitaryRank.UzmanCavus);
            _chain.Register(P(4), 0, MilitaryRank.AstsubayKidemliCavus);
            _chain.Register(P(5), 0, MilitaryRank.Er);

            var chain = _chain.GetChain(0);
            Assert.AreEqual(5, chain.Count);
            Assert.AreEqual(P(2), chain[0]);
            Assert.AreEqual(P(4), chain[1]);
            Assert.AreEqual(P(1), chain[2]);
            Assert.AreEqual(P(3), chain[3]);
            Assert.AreEqual(P(5), chain[4]);
            Assert.AreEqual(P(2), _chain.GetCommander(0));
            Assert.IsTrue(_chain.IsCommander(P(2)));
            Assert.IsFalse(_chain.IsCommander(P(4)));
            Assert.AreEqual(1, _chain.GetChainIndex(P(4)));
            Assert.AreEqual(0, _bus.Published.Count, "kayıt olay yayınlamamalı");
        }

        [Test]
        public void TeamsAreIndependent()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 1, MilitaryRank.Ustegmen);
            _chain.Register(P(3), 1, MilitaryRank.Er);
            Assert.AreEqual(P(1), _chain.GetCommander(0));
            Assert.AreEqual(P(2), _chain.GetCommander(1));
            Assert.AreEqual(1, _chain.GetChain(0).Count);
            Assert.AreEqual(2, _chain.GetChain(1).Count);
            Assert.AreEqual(0, _chain.GetChain(7).Count);
            Assert.AreEqual(PlayerId.Invalid, _chain.GetCommander(7));
            Assert.AreEqual(1, _chain.GetTeam(P(3)));
            Assert.AreEqual(-1, _chain.GetTeam(P(99)));
        }

        [Test]
        public void GetRank_ReturnsRegisteredOrEr()
        {
            _chain.Register(P(1), 0, MilitaryRank.AstsubayUstcavus);
            Assert.AreEqual(MilitaryRank.AstsubayUstcavus, _chain.GetRank(P(1)));
            Assert.AreEqual(MilitaryRank.Er, _chain.GetRank(P(42)));
        }

        [Test]
        public void CommanderDeath_TransfersCommand()
        {
            _chain.Register(P(10), 2, MilitaryRank.Yuzbasi);
            _chain.Register(P(11), 2, MilitaryRank.AstsubayKidemliCavus);
            _chain.Register(P(12), 2, MilitaryRank.UzmanCavus);

            _bus.Publish(new PlayerDiedEvent(P(10), P(99)));

            Assert.AreEqual(P(11), _chain.GetCommander(2));
            Assert.IsTrue(_chain.IsCommander(P(11)));
            Assert.IsFalse(_chain.IsCommander(P(10)));
            Assert.IsFalse(_chain.IsAlive(P(10)));
            Assert.AreEqual(2, _chain.GetChain(2).Count);
            Assert.AreEqual(-1, _chain.GetChainIndex(P(10)));

            var transfers = _bus.OfType<CommandTransferredEvent>();
            Assert.AreEqual(1, transfers.Count);
            Assert.AreEqual(2, transfers[0].Team);
            Assert.AreEqual(P(10), transfers[0].PreviousCommanderId);
            Assert.AreEqual(P(11), transfers[0].NewCommanderId);
        }

        [Test]
        public void SubordinateDeath_DoesNotTransfer()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 0, MilitaryRank.UzmanCavus);
            _bus.Publish(new PlayerDiedEvent(P(2), PlayerId.Invalid));
            Assert.AreEqual(P(1), _chain.GetCommander(0));
            Assert.AreEqual(0, _bus.OfType<CommandTransferredEvent>().Count);
            Assert.AreEqual(1, _chain.GetChain(0).Count);
        }

        [Test]
        public void SuccessiveDeaths_WalkDownTheChain()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 0, MilitaryRank.AstsubayUstcavus);
            _chain.Register(P(3), 0, MilitaryRank.UzmanCavus);

            _bus.Publish(new PlayerDiedEvent(P(1), P(50)));
            _bus.Publish(new PlayerDiedEvent(P(1), P(50)));
            _bus.Publish(new PlayerDiedEvent(P(2), P(50)));
            Assert.AreEqual(P(3), _chain.GetCommander(0));

            var transfers = _bus.OfType<CommandTransferredEvent>();
            Assert.AreEqual(2, transfers.Count, "aynı ölüm iki kez sayılmamalı");
            Assert.AreEqual(P(3), transfers[1].NewCommanderId);

            _bus.Publish(new PlayerDiedEvent(P(3), P(50)));
            Assert.AreEqual(PlayerId.Invalid, _chain.GetCommander(0));
            Assert.AreEqual(0, _chain.GetChain(0).Count);
            Assert.AreEqual(2, _bus.OfType<CommandTransferredEvent>().Count, "halef yoksa devir yok");
        }

        [Test]
        public void UnknownVictim_IsIgnored()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            Assert.DoesNotThrow(() => _bus.Publish(new PlayerDiedEvent(P(77), P(1))));
            Assert.AreEqual(P(1), _chain.GetCommander(0));
        }

        [Test]
        public void ReRegister_UpdatesTeamAndRank()
        {
            _chain.Register(P(1), 0, MilitaryRank.Er);
            _chain.Register(P(2), 0, MilitaryRank.Cavus);
            _chain.Register(P(1), 1, MilitaryRank.Yuzbasi);
            Assert.AreEqual(1, _chain.GetChain(0).Count);
            Assert.AreEqual(P(2), _chain.GetCommander(0));
            Assert.AreEqual(P(1), _chain.GetCommander(1));
            Assert.AreEqual(MilitaryRank.Yuzbasi, _chain.GetRank(P(1)));
            Assert.AreEqual(2, _chain.RegisteredCount);
        }

        [Test]
        public void ReviveAndUnregister()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 0, MilitaryRank.Er);
            _chain.MarkDead(P(1));
            Assert.AreEqual(P(2), _chain.GetCommander(0));
            _chain.Revive(P(1));
            Assert.AreEqual(P(1), _chain.GetCommander(0));
            _chain.Unregister(P(1));
            Assert.IsFalse(_chain.IsRegistered(P(1)));
            Assert.AreEqual(P(2), _chain.GetCommander(0));
            _chain.Clear();
            Assert.AreEqual(0, _chain.RegisteredCount);
        }

        [Test]
        public void LocalEventAlsoRaised()
        {
            CommandTransferredEvent? received = null;
            _chain.CommandTransferred += e => received = e;
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 0, MilitaryRank.Er);
            _chain.MarkDead(P(1));
            Assert.IsTrue(received.HasValue);
            Assert.AreEqual(P(2), received.Value.NewCommanderId);
        }

        [Test]
        public void Dispose_UnsubscribesAndIsIdempotent()
        {
            Assert.AreEqual(1, _bus.SubscriberCount<PlayerDiedEvent>());
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 0, MilitaryRank.Er);
            _chain.Dispose();
            _chain.Dispose();
            Assert.AreEqual(0, _bus.SubscriberCount<PlayerDiedEvent>());
            _bus.Publish(new PlayerDiedEvent(P(1), P(2)));
            Assert.AreEqual(P(1), _chain.GetCommander(0));
        }

        [Test]
        public void NullEventBus_IsTolerated()
        {
            var chain = new ChainOfCommandService(null);
            chain.Register(new PlayerId(1), 0, MilitaryRank.Yuzbasi);
            chain.Register(new PlayerId(2), 0, MilitaryRank.Er);
            Assert.DoesNotThrow(() => chain.MarkDead(new PlayerId(1)));
            Assert.AreEqual(new PlayerId(2), chain.GetCommander(0));
            Assert.DoesNotThrow(() => chain.Dispose());
        }

        [Test]
        public void InvalidId_IsNotRegistered()
        {
            _chain.Register(PlayerId.Invalid, 0, MilitaryRank.Yuzbasi);
            Assert.AreEqual(0, _chain.RegisteredCount);
        }

        [Test]
        public void FullTeamFromSlots_CommanderIsSlotZero()
        {
            var random = new SeededRandom(4);
            for (var slot = 0; slot < 10; slot++)
                _chain.Register(P(100 + slot), 0, RankCatalog.RankForTeamSlot(slot, random));

            var chain = _chain.GetChain(0);
            Assert.AreEqual(10, chain.Count);
            for (var i = 0; i < 6; i++)
                Assert.AreEqual(P(100 + i), chain[i], "komutan, yardımcı ve uzmanlar slot sırasında olmalı");

            var riflemen = new HashSet<PlayerId>();
            for (var i = 6; i < 10; i++)
                riflemen.Add(chain[i]);
            for (var i = 6; i < 10; i++)
                Assert.IsTrue(riflemen.Contains(P(100 + i)));
            for (var i = 7; i < 10; i++)
                Assert.LessOrEqual((int)_chain.GetRank(chain[i]), (int)_chain.GetRank(chain[i - 1]));
        }

        [Test]
        public void Deputy_AndCounts_FollowDeaths()
        {
            Assert.AreEqual(PlayerId.Invalid, _chain.GetDeputy(0));
            Assert.AreEqual(0, _chain.GetAliveCount(0));
            Assert.AreEqual(0, _chain.GetMemberCount(0));

            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            Assert.AreEqual(PlayerId.Invalid, _chain.GetDeputy(0), "tek kişilik timde yardımcı yok");

            _chain.Register(P(2), 0, MilitaryRank.AstsubayKidemliCavus);
            _chain.Register(P(3), 0, MilitaryRank.UzmanCavus);
            Assert.AreEqual(P(2), _chain.GetDeputy(0));
            Assert.AreEqual(3, _chain.GetAliveCount(0));

            _bus.Publish(new PlayerDiedEvent(P(1), P(99)));
            Assert.AreEqual(P(2), _chain.GetCommander(0));
            Assert.AreEqual(P(3), _chain.GetDeputy(0));
            Assert.AreEqual(2, _chain.GetAliveCount(0));
            Assert.AreEqual(3, _chain.GetMemberCount(0));
        }
    }
}
