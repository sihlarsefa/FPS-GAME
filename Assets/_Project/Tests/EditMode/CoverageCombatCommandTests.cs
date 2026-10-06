using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Tests.Match;

namespace Project.Tests.EditMode
{
    /// <summary>CombatService dost ateşi/zırh aşınması, ChainOfCommandService devirleri, ArtilleryService zamanlaması.</summary>
    [TestFixture]
    public sealed class CoverageCombatTests
    {
        private sealed class Registry : IDamageableRegistry
        {
            private readonly Dictionary<int, IDamageable> _map = new();
            public void Register(IDamageable d) => _map[d.OwnerId.Value] = d;
            public void Unregister(IDamageable d) => _map.Remove(d.OwnerId.Value);
            public bool TryGet(PlayerId id, out IDamageable d) => _map.TryGetValue(id.Value, out d);
        }

        private sealed class Teams : ITeamRelations
        {
            public readonly Dictionary<int, int> Of = new();
            public int GetTeam(PlayerId id) => Of.TryGetValue(id.Value, out var t) ? t : -1;
            public bool AreAllies(PlayerId a, PlayerId b) { var t = GetTeam(a); return t >= 0 && t == GetTeam(b); }
            public string GetTeamName(int team) => "T" + team;
            public TeamRole GetRole(PlayerId id) => TeamRole.Rifleman;
        }

        private sealed class Soldier : IDamageable, IHealthReadModel, IArmored, IArmorProvider
        {
            public Soldier(int id, IEventBus bus, float hp = 100f) { Health = new HealthService(new PlayerId(id), hp, bus); }
            public HealthService Health { get; }
            public ArmorPiece Helmet, Vest;
            public PlayerId OwnerId => Health.OwnerId;
            public bool IsAlive => Health.IsAlive;
            public HealthState State => Health.State;
            public IArmorProvider Armor => this;
            public void ApplyDamage(DamageInfo d) => Health.ApplyDamage(d);
            public ArmorPiece GetArmorFor(BodyPart p) => p == BodyPart.Head ? Helmet : p == BodyPart.Torso ? Vest : null;
        }

        private MatchTestBus _bus;
        private Registry _reg;
        private Teams _teams;
        private CombatService _combat;
        private static readonly Float3 Src = new(0f, 1f, 0f);

        [SetUp]
        public void SetUp()
        {
            _bus = new MatchTestBus();
            _reg = new Registry();
            _teams = new Teams();
            _combat = new CombatService(_bus, _reg, _teams);
        }

        private Soldier Spawn(int id, int team, float hp = 100f)
        {
            var s = new Soldier(id, _bus, hp);
            _reg.Register(s);
            _teams.Of[id] = team;
            return s;
        }

        private static ArmorPiece Armor(float red, float dur = 100f) => new("a", 1, 100f, red, dur);

        [Test]
        public void FriendlyFire_Off_BlocksBullet_Melee_Explosion()
        {
            Spawn(1, 0); var v = Spawn(2, 0);
            var w = WeaponCatalog.Get(WeaponIds.Mpt76);
            Assert.IsFalse(_combat.ApplyBulletHit(new PlayerId(1), w, new PlayerId(2), BodyPart.Torso, 5f, Src).Applied);
            Assert.IsFalse(_combat.ApplyMeleeHit(new PlayerId(1), new PlayerId(2), BodyPart.Torso, Src).Applied);
            Assert.IsFalse(_combat.ApplyExplosion(new PlayerId(1), new PlayerId(2), null, 50f, Src).Applied);
            Assert.AreEqual(100f, v.Health.Current, 1e-4f);
        }

        [Test]
        public void FriendlyFire_On_Allows()
        {
            Spawn(1, 0); var v = Spawn(2, 0);
            _combat.FriendlyFire = true;
            Assert.IsTrue(_combat.CanDamage(new PlayerId(1), new PlayerId(2)));
            Assert.IsTrue(_combat.ApplyExplosion(new PlayerId(1), new PlayerId(2), null, 50f, Src).Applied);
            Assert.Less(v.Health.Current, 100f);
        }

        [Test]
        public void SelfDamage_Explosion_IsAllowedEvenWithoutFriendlyFire()
        {
            var s = Spawn(1, 0);
            Assert.IsTrue(_combat.ApplyExplosion(new PlayerId(1), new PlayerId(1), null, 30f, Src).Applied);
            Assert.AreEqual(70f, s.Health.Current, 1e-3f);
        }

        [Test]
        public void NoTeams_MeansEveryoneIsEnemy()
        {
            _combat.Teams = null;
            Spawn(1, 0); Spawn(2, 0);
            Assert.IsTrue(_combat.CanDamage(new PlayerId(1), new PlayerId(2)));
        }

        [Test]
        public void EnemyTeam_CanBeDamaged()
        {
            Spawn(1, 0); Spawn(2, 1);
            Assert.IsTrue(_combat.CanDamage(new PlayerId(1), new PlayerId(2)));
        }

        [Test]
        public void Bullet_NullWeapon_IsNone()
        {
            Spawn(1, 0); Spawn(2, 1);
            Assert.IsFalse(_combat.ApplyBulletHit(new PlayerId(1), null, new PlayerId(2), BodyPart.Torso, 1f, Src).Applied);
        }

        [Test]
        public void DeadTarget_CannotBeHitAgain()
        {
            Spawn(1, 0); var v = Spawn(2, 1, 10f);
            var w = WeaponCatalog.Get(WeaponIds.Mpt76);
            var first = _combat.ApplyBulletHit(new PlayerId(1), w, new PlayerId(2), BodyPart.Head, 5f, Src);
            Assert.IsTrue(first.Killed);
            Assert.IsFalse(_combat.ApplyBulletHit(new PlayerId(1), w, new PlayerId(2), BodyPart.Head, 5f, Src).Applied);
            Assert.IsFalse(v.IsAlive);
        }

        [Test]
        public void UnknownVictim_IsNone()
        {
            Spawn(1, 0);
            Assert.IsFalse(_combat.ApplyEnvironmentalDamage(new PlayerId(77), "zone", 5f).Applied);
            Assert.IsFalse(_combat.ApplyEnvironmentalDamage(PlayerId.Invalid, "zone", 5f).Applied);
        }

        [Test]
        public void Environmental_IgnoresArmor_AndHasNoAttacker()
        {
            Spawn(1, 0);
            var v = Spawn(2, 1);
            v.Vest = Armor(0.9f);
            var o = _combat.ApplyEnvironmentalDamage(new PlayerId(2), "zone", 10f);
            Assert.AreEqual(10f, o.Damage, 1e-3f);
            Assert.AreEqual(100f, v.Vest.Durability, 1e-4f);
            Assert.AreEqual(0, _bus.Of<HitConfirmedEvent>().Count, "çevresel hasar vuruş onayı üretmez");
        }

        [Test]
        public void Environmental_NaNOrNonPositive_IsNone()
        {
            Spawn(2, 1);
            Assert.IsFalse(_combat.ApplyEnvironmentalDamage(new PlayerId(2), "z", float.NaN).Applied);
            Assert.IsFalse(_combat.ApplyEnvironmentalDamage(new PlayerId(2), "z", 0f).Applied);
            Assert.IsFalse(_combat.ApplyEnvironmentalDamage(new PlayerId(2), "z", -3f).Applied);
        }

        [Test]
        public void Explosion_NaNOrNonPositive_IsNone()
        {
            Spawn(1, 0); Spawn(2, 1);
            Assert.IsFalse(_combat.ApplyExplosion(new PlayerId(1), new PlayerId(2), null, float.NaN, Src).Applied);
            Assert.IsFalse(_combat.ApplyExplosion(new PlayerId(1), new PlayerId(2), null, 0f, Src).Applied);
        }

        [Test]
        public void Explosion_VestHalfEffective_HelmetIgnored()
        {
            Spawn(1, 0);
            var v = Spawn(2, 1);
            v.Helmet = Armor(0.5f);
            v.Vest = Armor(0.4f);
            var o = _combat.ApplyExplosion(new PlayerId(1), new PlayerId(2), null, 50f, Src);
            Assert.AreEqual(40f, o.Damage, 1e-3f);
            Assert.AreEqual(100f, v.Helmet.Durability, 1e-4f);
            Assert.Less(v.Vest.Durability, 100f);
        }

        [Test]
        public void ArmorWear_AccumulatesAcrossHits_ThenBreaks()
        {
            Spawn(1, 0);
            var v = Spawn(2, 1, 100000f);
            v.Vest = Armor(0.5f, 30f);
            var w = WeaponCatalog.Get(WeaponIds.Mpt76);
            for (var i = 0; i < 20 && !v.Vest.IsBroken; i++)
                _combat.ApplyBulletHit(new PlayerId(1), w, new PlayerId(2), BodyPart.Torso, 5f, Src);
            Assert.IsTrue(v.Vest.IsBroken);
            var after = _combat.ApplyBulletHit(new PlayerId(1), w, new PlayerId(2), BodyPart.Torso, 5f, Src);
            Assert.IsFalse(after.ArmorAbsorbed);
            Assert.AreEqual(36f, after.Damage, 0.01f);
        }

        [Test]
        public void ArmorAbsorbed_FlagSetOnlyWhenArmorHelped()
        {
            Spawn(1, 0);
            var v = Spawn(2, 1);
            var w = WeaponCatalog.Get(WeaponIds.Mpt76);
            Assert.IsFalse(_combat.ApplyBulletHit(new PlayerId(1), w, new PlayerId(2), BodyPart.Torso, 5f, Src).ArmorAbsorbed);
            v.Vest = Armor(0.3f);
            Assert.IsTrue(_combat.ApplyBulletHit(new PlayerId(1), w, new PlayerId(2), BodyPart.Torso, 5f, Src).ArmorAbsorbed);
            Assert.IsFalse(_combat.ApplyBulletHit(new PlayerId(1), w, new PlayerId(2), BodyPart.Leg, 5f, Src).ArmorAbsorbed);
        }

        [Test]
        public void Helmet_ProtectsHeadOnly()
        {
            Spawn(1, 0);
            var v = Spawn(2, 1);
            v.Helmet = Armor(0.5f);
            var w = WeaponCatalog.Get(WeaponIds.Mpt76);
            var head = _combat.ApplyBulletHit(new PlayerId(1), w, new PlayerId(2), BodyPart.Head, 5f, Src);
            // P2: Sv.1 kask 7.62'ye karşı tam etkin değil (zırh sınıfı vs kalibre); eski beklenti 36 geçersiz.
            Assert.AreEqual(72f * (1f - 0.5f * PenetrationRules.ArmorEffectiveness(AmmoType.Mm762, 1)), head.Damage, 0.01f);
            var torso = _combat.ApplyBulletHit(new PlayerId(1), w, new PlayerId(2), BodyPart.Torso, 5f, Src);
            Assert.AreEqual(36f, torso.Damage, 0.01f);
        }

        [Test]
        public void HitConfirmed_PublishedWithHeadshotAndKill()
        {
            Spawn(1, 0); Spawn(2, 1, 20f);
            var w = WeaponCatalog.Get(WeaponIds.Mpt76);
            _combat.ApplyBulletHit(new PlayerId(1), w, new PlayerId(2), BodyPart.Head, 5f, Src);
            var hits = _bus.Of<HitConfirmedEvent>();
            Assert.AreEqual(1, hits.Count);
            Assert.IsTrue(hits[0].IsHeadshot);
            Assert.IsTrue(hits[0].IsKill);
        }

        [Test]
        public void Damage_ReportedIsActuallyDealt_NotOverkill()
        {
            Spawn(1, 0); Spawn(2, 1, 10f);
            var w = WeaponCatalog.Get(WeaponIds.Mpt76);
            var o = _combat.ApplyBulletHit(new PlayerId(1), w, new PlayerId(2), BodyPart.Torso, 5f, Src);
            Assert.AreEqual(10f, o.Damage, 1e-3f);
        }

        [Test]
        public void Melee_HeadDealsMoreThanTorso()
        {
            Spawn(1, 0); Spawn(2, 1, 1000f);
            var torso = _combat.ApplyMeleeHit(new PlayerId(1), new PlayerId(2), BodyPart.Torso, Src);
            var head = _combat.ApplyMeleeHit(new PlayerId(1), new PlayerId(2), BodyPart.Head, Src);
            Assert.AreEqual(DamageCalculator.FistDamage, torso.Damage, 1e-3f);
            Assert.AreEqual(DamageCalculator.FistDamage * DamageCalculator.FistHeadMultiplier, head.Damage, 1e-3f);
        }

        [Test]
        public void Calculator_ArmorEffectiveness_ZeroMeansNoReduction()
        {
            var a = Armor(0.5f);
            var r = DamageCalculator.ApplyArmor(40f, a, 0f);
            Assert.AreEqual(40f, r.Damage, 1e-3f);
        }
    }

    [TestFixture]
    public sealed class CoverageChainOfCommandTests
    {
        private MatchTestBus _bus;
        private ChainOfCommandService _chain;
        private static PlayerId P(int i) => new(i);

        [SetUp]
        public void SetUp()
        {
            _bus = new MatchTestBus();
            _chain = new ChainOfCommandService(_bus);
        }

        [Test]
        public void Commander_IsHighestRank()
        {
            _chain.Register(P(1), 0, MilitaryRank.Er);
            _chain.Register(P(2), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(3), 0, MilitaryRank.Cavus);
            Assert.AreEqual(P(2), _chain.GetCommander(0));
            Assert.AreEqual(P(3), _chain.GetDeputy(0));
        }

        [Test]
        public void EqualRank_EarlierRegistrationOutranks()
        {
            _chain.Register(P(5), 0, MilitaryRank.Onbasi);
            _chain.Register(P(6), 0, MilitaryRank.Onbasi);
            Assert.AreEqual(P(5), _chain.GetCommander(0));
            Assert.AreEqual(P(6), _chain.GetDeputy(0));
        }

        [Test]
        public void CommanderDeath_TransfersToNextRank_AndPublishesOnce()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 0, MilitaryRank.Cavus);
            _bus.Kill(1);
            Assert.AreEqual(P(2), _chain.GetCommander(0));
            var events = _bus.Of<CommandTransferredEvent>();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(P(1), events[0].PreviousCommanderId);
            Assert.AreEqual(P(2), events[0].NewCommanderId);
        }

        [Test]
        public void NonCommanderDeath_NoTransfer()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 0, MilitaryRank.Cavus);
            _bus.Kill(2);
            Assert.AreEqual(0, _bus.Of<CommandTransferredEvent>().Count);
            Assert.AreEqual(P(1), _chain.GetCommander(0));
        }

        [Test]
        public void LastMemberDeath_NoTransferEvent_AndNoCommander()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _bus.Kill(1);
            Assert.AreEqual(0, _bus.Of<CommandTransferredEvent>().Count);
            Assert.IsFalse(_chain.GetCommander(0).IsValid);
            Assert.AreEqual(0, _chain.GetAliveCount(0));
        }

        [Test]
        public void ChainOfSuccession_ThreeDeaths()
        {
            _chain.Register(P(1), 0, MilitaryRank.Binbasi);
            _chain.Register(P(2), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(3), 0, MilitaryRank.Ustegmen);
            _chain.Register(P(4), 0, MilitaryRank.Er);
            _bus.Kill(1); _bus.Kill(2); _bus.Kill(3);
            Assert.AreEqual(P(4), _chain.GetCommander(0));
            Assert.AreEqual(3, _bus.Of<CommandTransferredEvent>().Count);
        }

        [Test]
        public void Revive_RestoresSeniority_WithoutEvent()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 0, MilitaryRank.Cavus);
            _chain.MarkDead(P(1));
            var before = _bus.Of<CommandTransferredEvent>().Count;
            _chain.Revive(P(1));
            Assert.AreEqual(P(1), _chain.GetCommander(0));
            Assert.AreEqual(before, _bus.Of<CommandTransferredEvent>().Count);
        }

        [Test]
        public void Teams_AreIndependent()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 1, MilitaryRank.Er);
            _bus.Kill(1);
            Assert.AreEqual(P(2), _chain.GetCommander(1));
            Assert.IsFalse(_chain.GetCommander(0).IsValid);
        }

        [Test]
        public void MarkDead_Twice_PublishesOneTransfer()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 0, MilitaryRank.Er);
            _chain.MarkDead(P(1));
            _chain.MarkDead(P(1));
            Assert.AreEqual(1, _bus.Of<CommandTransferredEvent>().Count);
        }

        [Test]
        public void Unregistered_Queries_AreSafe()
        {
            Assert.IsFalse(_chain.IsRegistered(P(9)));
            Assert.IsFalse(_chain.IsAlive(P(9)));
            Assert.AreEqual(-1, _chain.GetTeam(P(9)));
            Assert.AreEqual(-1, _chain.GetChainIndex(P(9)));
            Assert.AreEqual(MilitaryRank.Er, _chain.GetRank(P(9)));
            Assert.IsFalse(_chain.IsCommander(P(9)));
            Assert.AreEqual(0, _chain.GetChain(42).Count);
            _chain.MarkDead(P(9));
            _chain.Revive(P(9));
            _chain.Unregister(P(9));
        }

        [Test]
        public void Register_InvalidId_Ignored()
        {
            _chain.Register(PlayerId.Invalid, 0, MilitaryRank.Albay);
            Assert.AreEqual(0, _chain.RegisteredCount);
        }

        [Test]
        public void ReRegister_ChangesTeamAndRank()
        {
            _chain.Register(P(1), 0, MilitaryRank.Er);
            _chain.Register(P(1), 2, MilitaryRank.Albay);
            Assert.AreEqual(0, _chain.GetMemberCount(0));
            Assert.AreEqual(1, _chain.GetMemberCount(2));
            Assert.AreEqual(MilitaryRank.Albay, _chain.GetRank(P(1)));
            Assert.AreEqual(1, _chain.RegisteredCount);
        }

        [Test]
        public void ChainIndex_ReflectsOrder_AndDeadAreMinusOne()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 0, MilitaryRank.Cavus);
            _chain.Register(P(3), 0, MilitaryRank.Er);
            Assert.AreEqual(0, _chain.GetChainIndex(P(1)));
            Assert.AreEqual(2, _chain.GetChainIndex(P(3)));
            _chain.MarkDead(P(2));
            Assert.AreEqual(-1, _chain.GetChainIndex(P(2)));
            Assert.AreEqual(1, _chain.GetChainIndex(P(3)));
            Assert.AreEqual(3, _chain.GetMemberCount(0));
            Assert.AreEqual(2, _chain.GetAliveCount(0));
        }

        [Test]
        public void Unregister_RemovesFromChain()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 0, MilitaryRank.Er);
            _chain.Unregister(P(1));
            Assert.AreEqual(P(2), _chain.GetCommander(0));
            Assert.IsFalse(_chain.IsRegistered(P(1)));
        }

        [Test]
        public void Clear_ResetsEverything()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Clear();
            Assert.AreEqual(0, _chain.RegisteredCount);
            Assert.IsFalse(_chain.GetCommander(0).IsValid);
        }

        [Test]
        public void Dispose_StopsListeningToDeaths()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Dispose();
            Assert.AreEqual(0, _bus.HandlerCount<PlayerDiedEvent>());
            _bus.Kill(1);
            Assert.IsTrue(_chain.IsAlive(P(1)));
        }

        [Test]
        public void CommandTransferred_ManagedEventFires()
        {
            var seen = new List<CommandTransferredEvent>();
            _chain.CommandTransferred += seen.Add;
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 0, MilitaryRank.Er);
            _bus.Kill(1);
            Assert.AreEqual(1, seen.Count);
        }

        [Test]
        public void IsCommander_FalseForDeadFormerCommander()
        {
            _chain.Register(P(1), 0, MilitaryRank.Yuzbasi);
            _chain.Register(P(2), 0, MilitaryRank.Er);
            Assert.IsTrue(_chain.IsCommander(P(1)));
            _chain.MarkDead(P(1));
            Assert.IsFalse(_chain.IsCommander(P(1)));
            Assert.IsTrue(_chain.IsCommander(P(2)));
        }
    }

    [TestFixture]
    public sealed class CoverageArtilleryTests
    {
        private MatchTestBus _bus;
        private ArtilleryService _art;
        private static readonly Float3 Target = new(100f, 0f, -50f);

        [SetUp]
        public void SetUp()
        {
            _bus = new MatchTestBus();
            _art = new ArtilleryService(_bus, new SeededRandom(11), 60f);
        }

        private void Advance(float seconds, float step = 0.25f)
        {
            for (var t = 0f; t < seconds; t += step) _art.Tick(step);
        }

        [Test]
        public void Call_StartsCooldown_AndSecondCallRejected()
        {
            Assert.IsTrue(_art.TryCall(0, new PlayerId(1), Target));
            Assert.IsFalse(_art.IsReady(0));
            Assert.IsFalse(_art.TryCall(0, new PlayerId(1), Target));
            Assert.AreEqual(ArtilleryService.ShellsPerStrike, _art.PendingShellCount);
        }

        [Test]
        public void Cooldown_IsPerTeam()
        {
            _art.TryCall(0, new PlayerId(1), Target);
            Assert.IsTrue(_art.IsReady(1));
            Assert.IsTrue(_art.TryCall(1, new PlayerId(2), Target));
        }

        [Test]
        public void Cooldown_ExpiresAfterConfiguredSeconds()
        {
            _art.TryCall(0, new PlayerId(1), Target);
            Assert.AreEqual(60f, _art.GetCooldownRemaining(0), 1e-3f);
            Advance(59f);
            Assert.IsFalse(_art.IsReady(0));
            Advance(2f);
            Assert.IsTrue(_art.IsReady(0));
            Assert.AreEqual(0f, _art.GetCooldownRemaining(0), 1e-6f);
        }

        [Test]
        public void ZeroCooldown_AllowsImmediateRecall()
        {
            var a = new ArtilleryService(_bus, new SeededRandom(1), 0f);
            Assert.IsTrue(a.TryCall(0, new PlayerId(1), Target));
            Assert.IsTrue(a.TryCall(0, new PlayerId(1), Target));
            Assert.AreEqual(ArtilleryService.ShellsPerStrike * 2, a.PendingShellCount);
        }

        [Test]
        public void NaNOrInfiniteTarget_Rejected_WithoutCooldown()
        {
            Assert.IsFalse(_art.TryCall(0, new PlayerId(1), new Float3(float.NaN, 0f, 0f)));
            Assert.IsFalse(_art.TryCall(0, new PlayerId(1), new Float3(0f, float.PositiveInfinity, 0f)));
            Assert.IsTrue(_art.IsReady(0));
            Assert.AreEqual(0, _art.PendingShellCount);
        }

        [Test]
        public void NoShellLands_BeforeDelay()
        {
            _art.TryCall(0, new PlayerId(1), Target);
            Advance(ArtilleryService.DelaySeconds - 0.5f);
            Assert.AreEqual(0, _art.DueImpacts.Count);
        }

        [Test]
        public void AllShellsLand_Eventually_InsideSpread()
        {
            _art.TryCall(0, new PlayerId(1), Target);
            Advance(ArtilleryService.DelaySeconds + ArtilleryService.ShellsPerStrike * ArtilleryService.ShellIntervalMax + 1f);
            Assert.AreEqual(ArtilleryService.ShellsPerStrike, _art.DueImpacts.Count);
            Assert.AreEqual(0, _art.PendingShellCount);
            foreach (var impact in _art.DueImpacts)
            {
                var dx = impact.Position.X - Target.X;
                var dz = impact.Position.Z - Target.Z;
                Assert.LessOrEqual(Math.Sqrt(dx * dx + dz * dz), ArtilleryService.SpreadRadius + 0.01f);
                Assert.AreEqual(0, impact.Team);
                Assert.AreEqual(new PlayerId(1), impact.CallerId);
            }
        }

        [Test]
        public void StrikeEvents_OneCallPlusOnePerImpact()
        {
            _art.TryCall(0, new PlayerId(1), Target);
            Advance(30f);
            Assert.AreEqual(1 + ArtilleryService.ShellsPerStrike, _bus.Of<ArtilleryStrikeEvent>().Count);
        }

        [Test]
        public void StrikeIsActiveUntilLingerEnds()
        {
            _art.TryCall(0, new PlayerId(1), Target);
            Assert.IsTrue(_art.TryGetActiveStrike(0, out var t));
            Assert.AreEqual(Target.X, t.X, 1e-4f);
            Assert.IsFalse(_art.TryGetActiveStrike(1, out _));
            Advance(60f);
            Assert.IsFalse(_art.TryGetActiveStrike(0, out _));
            Assert.AreEqual(0, _art.ActiveStrikeCount);
        }

        [Test]
        public void DangerZone_CoversSpreadPlusShell_ExcludingOwnTeam()
        {
            _art.TryCall(0, new PlayerId(1), Target);
            var inside = new Float3(Target.X + ArtilleryService.SpreadRadius + ArtilleryService.ShellRadius - 1f, 0f, Target.Z);
            var outside = new Float3(Target.X + ArtilleryService.SpreadRadius + ArtilleryService.ShellRadius + 5f, 0f, Target.Z);
            Assert.IsTrue(_art.IsInDangerZone(inside));
            Assert.IsFalse(_art.IsInDangerZone(outside));
            Assert.IsTrue(_art.IsInDangerZone(outside, 10f));
            Assert.IsFalse(_art.IsInDangerZone(inside, 0f, 0), "kendi timin atışı sayılmaz");
            Assert.IsTrue(_art.IsInDangerZone(inside, 0f, 1));
        }

        [Test]
        public void DangerZone_IgnoresHeight()
        {
            _art.TryCall(0, new PlayerId(1), Target);
            Assert.IsTrue(_art.IsInDangerZone(new Float3(Target.X, 500f, Target.Z)));
        }

        [Test]
        public void Impacts_AreOrderedByLandTime_AcrossTeams()
        {
            var a = new ArtilleryService(_bus, new SeededRandom(2), 0f);
            a.TryCall(0, new PlayerId(1), Target);
            a.Tick(3f);
            a.TryCall(1, new PlayerId(2), Target);
            Advance2(a, 40f);
            Assert.AreEqual(ArtilleryService.ShellsPerStrike * 2, a.DueImpacts.Count);
            Assert.AreEqual(0, a.DueImpacts[0].Team);
        }

        private static void Advance2(ArtilleryService a, float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.25f) a.Tick(0.25f);
        }

        [Test]
        public void DueImpacts_QueueIsBounded()
        {
            var a = new ArtilleryService(_bus, new SeededRandom(3), 0f);
            for (var i = 0; i < 60; i++) a.TryCall(0, new PlayerId(1), Target);
            a.Tick(500f);
            Assert.AreEqual(ArtilleryService.MaxQueuedImpacts, a.DueImpacts.Count);
        }

        [Test]
        public void Tick_NaNOrNegative_DoesNotAdvanceClock()
        {
            _art.Tick(1f);
            var t = _art.ElapsedSeconds;
            _art.Tick(float.NaN);
            _art.Tick(-5f);
            _art.Tick(float.PositiveInfinity);
            Assert.AreEqual(t, _art.ElapsedSeconds, 1e-6f);
        }

        [Test]
        public void SameSeed_SameImpactPositions()
        {
            var a = new ArtilleryService(new MatchTestBus(), new SeededRandom(8), 0f);
            var b = new ArtilleryService(new MatchTestBus(), new SeededRandom(8), 0f);
            a.TryCall(0, new PlayerId(1), Target);
            b.TryCall(0, new PlayerId(1), Target);
            Advance2(a, 30f); Advance2(b, 30f);
            for (var i = 0; i < a.DueImpacts.Count; i++)
                Assert.AreEqual(a.DueImpacts[i].Position.X, b.DueImpacts[i].Position.X, 1e-5f);
        }
    }
}
