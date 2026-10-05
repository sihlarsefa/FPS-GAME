using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    /// <summary>DamageCalculator, HealthService ve CombatService: zırh, vücut bölgesi, dost ateşi, tek seferlik ölüm.</summary>
    [TestFixture]
    public sealed class CombatTests
    {
        private const float Eps = 1e-3f;

        private sealed class RecordingBus : IEventBus
        {
            public readonly List<object> Events = new();

            public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent => Events.Add(gameEvent);
            public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent { }
            public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent { }

            public int Count<T>()
            {
                var n = 0;
                foreach (var e in Events)
                {
                    if (e is T)
                        n++;
                }

                return n;
            }

            public T Last<T>()
            {
                for (var i = Events.Count - 1; i >= 0; i--)
                {
                    if (Events[i] is T t)
                        return t;
                }

                throw new InvalidOperationException("Olay yok: " + typeof(T).Name);
            }
        }

        private sealed class Registry : IDamageableRegistry
        {
            private readonly Dictionary<int, IDamageable> _map = new();
            public void Register(IDamageable d) => _map[d.OwnerId.Value] = d;
            public void Unregister(IDamageable d) => _map.Remove(d.OwnerId.Value);
            public bool TryGet(PlayerId id, out IDamageable d) => _map.TryGetValue(id.Value, out d);
        }

        private sealed class Teams : ITeamRelations
        {
            private readonly Dictionary<int, int> _teamOf = new();
            public void Set(int id, int team) => _teamOf[id] = team;
            public int GetTeam(PlayerId id) => _teamOf.TryGetValue(id.Value, out var t) ? t : -1;

            public bool AreAllies(PlayerId a, PlayerId b)
            {
                var ta = GetTeam(a);
                return ta >= 0 && ta == GetTeam(b);
            }

            public string GetTeamName(int team) => "Tim " + team;
            public TeamRole GetRole(PlayerId id) => TeamRole.Rifleman;
        }

        /// <summary>Kask/yelek taşıyan sahte savaşan (HealthService sarmalar).</summary>
        private sealed class Soldier : IDamageable, IHealthReadModel, IArmored, IArmorProvider
        {
            public Soldier(int id, IEventBus bus, float health = 100f)
            {
                Health = new HealthService(new PlayerId(id), health, bus);
            }

            public HealthService Health { get; }
            public ArmorPiece Helmet { get; set; }
            public ArmorPiece Vest { get; set; }
            public DamageInfo LastDamage { get; private set; }
            public int DamageCalls { get; private set; }

            public PlayerId OwnerId => Health.OwnerId;
            public bool IsAlive => Health.IsAlive;
            public HealthState State => Health.State;
            public IArmorProvider Armor => this;

            public void ApplyDamage(DamageInfo damage)
            {
                DamageCalls++;
                LastDamage = damage;
                Health.ApplyDamage(damage);
            }

            public ArmorPiece GetArmorFor(BodyPart part)
            {
                switch (part)
                {
                    case BodyPart.Head: return Helmet;
                    case BodyPart.Torso: return Vest;
                    default: return null;
                }
            }
        }

        private RecordingBus _bus;
        private Registry _registry;
        private Teams _teams;

        [SetUp]
        public void SetUp()
        {
            _bus = new RecordingBus();
            _registry = new Registry();
            _teams = new Teams();
        }

        private Soldier Spawn(int id, int team, float health = 100f)
        {
            var s = new Soldier(id, _bus, health);
            _registry.Register(s);
            _teams.Set(id, team);
            return s;
        }

        private static ArmorPiece Armor(float reduction, float durability = 100f) =>
            new("armor_test", 2, 100f, reduction, durability);

        private static readonly Float3 Muzzle = new(1f, 2f, 3f);

        // ---------------------------------------------------------------- DamageCalculator

        [Test]
        public void Calculator_BodyPartMultipliers()
        {
            var jng = WeaponCatalog.Get(WeaponIds.Jng90);
            Assert.AreEqual(2.5f, DamageCalculator.BodyPartMultiplier(jng, BodyPart.Head), Eps);
            Assert.AreEqual(1f, DamageCalculator.BodyPartMultiplier(jng, BodyPart.Torso), Eps);
            Assert.AreEqual(jng.LimbMultiplier, DamageCalculator.BodyPartMultiplier(jng, BodyPart.Arm), Eps);
            Assert.AreEqual(jng.LimbMultiplier, DamageCalculator.BodyPartMultiplier(jng, BodyPart.Leg), Eps);
            Assert.AreEqual(2f, DamageCalculator.BodyPartMultiplier(null, BodyPart.Head), Eps);
        }

        [Test]
        public void Calculator_DistanceFalloff()
        {
            var w = WeaponCatalog.Get(WeaponIds.Mpt55);
            Assert.AreEqual(1f, DamageCalculator.DistanceFactor(w, 0f), Eps);
            Assert.AreEqual(1f, DamageCalculator.DistanceFactor(w, w.FalloffStart), Eps);
            Assert.AreEqual(w.MinDamageFactor, DamageCalculator.DistanceFactor(w, w.FalloffEnd), Eps);
            Assert.AreEqual(w.MinDamageFactor, DamageCalculator.DistanceFactor(w, 5000f), Eps);
            var mid = (w.FalloffStart + w.FalloffEnd) * 0.5f;
            Assert.AreEqual((1f + w.MinDamageFactor) * 0.5f, DamageCalculator.DistanceFactor(w, mid), Eps);
            Assert.AreEqual(1f, DamageCalculator.DistanceFactor(null, 999f), Eps);
        }

        [Test]
        public void Calculator_ArmorReducesAndWears()
        {
            var vest = Armor(0.4f);
            var r = DamageCalculator.ApplyArmor(50f, vest);
            Assert.AreEqual(30f, r.Damage, Eps);
            Assert.AreEqual(20f, r.ArmorAbsorbed, Eps);
            Assert.AreEqual(80f, vest.Durability, Eps);

            var half = DamageCalculator.ApplyArmor(50f, Armor(0.4f), 0.5f);
            Assert.AreEqual(40f, half.Damage, Eps);

            var broken = Armor(0.4f, 0f);
            var rb = DamageCalculator.ApplyArmor(50f, broken);
            Assert.AreEqual(50f, rb.Damage, Eps);
            Assert.AreEqual(0f, rb.ArmorAbsorbed, Eps);

            var none = DamageCalculator.ApplyArmor(50f, null);
            Assert.AreEqual(50f, none.Damage, Eps);
            Assert.AreEqual(0f, DamageCalculator.ApplyArmor(-5f, vest).Damage, Eps);
        }

        [Test]
        public void Calculator_BulletDamageCombinesFactors()
        {
            var w = WeaponCatalog.Get(WeaponIds.Mpt76); // 36, kafa x2
            Assert.AreEqual(36f, DamageCalculator.ComputeBulletDamage(w, BodyPart.Torso, 10f, null).Damage, Eps);
            Assert.AreEqual(72f, DamageCalculator.ComputeBulletDamage(w, BodyPart.Head, 10f, null).Damage, Eps);
            Assert.AreEqual(36f * 0.8f, DamageCalculator.ComputeBulletDamage(w, BodyPart.Leg, 10f, null).Damage, Eps);
            Assert.AreEqual(36f * w.MinDamageFactor, DamageCalculator.ComputeBulletDamage(w, BodyPart.Torso, 2000f, null).Damage, Eps);

            var helmet = Armor(0.5f);
            var r = DamageCalculator.ComputeBulletDamage(w, BodyPart.Head, 10f, helmet);
            Assert.AreEqual(36f, r.Damage, Eps);
            Assert.AreEqual(36f, r.ArmorAbsorbed, Eps);
            Assert.AreEqual(0f, DamageCalculator.ComputeBulletDamage(null, BodyPart.Head, 10f, null).Damage, Eps);
        }

        [Test]
        public void Calculator_SniperHeadshotOneTaps()
        {
            var jng = WeaponCatalog.Get(WeaponIds.Jng90);
            Assert.GreaterOrEqual(DamageCalculator.ComputeBulletDamage(jng, BodyPart.Head, 150f, null).Damage, 100f);
        }

        [Test]
        public void Calculator_ExplosionFalloff()
        {
            Assert.AreEqual(100f, DamageCalculator.ComputeExplosionDamage(100f, 8f, 0f), Eps);
            Assert.AreEqual(0f, DamageCalculator.ComputeExplosionDamage(100f, 8f, 8f), Eps);
            Assert.AreEqual(0f, DamageCalculator.ComputeExplosionDamage(100f, 8f, 20f), Eps);
            Assert.AreEqual(0f, DamageCalculator.ComputeExplosionDamage(100f, 0f, 0f), Eps);
            var near = DamageCalculator.ComputeExplosionDamage(100f, 8f, 3f);
            var far = DamageCalculator.ComputeExplosionDamage(100f, 8f, 6f);
            Assert.Greater(near, far);
            Assert.Greater(far, 0f);
            Assert.Less(near, 100f);
        }

        [TestCase(0f, 0f)]
        [TestCase(11.9f, 0f)]
        [TestCase(12f, 0f)]
        [TestCase(20f, 60f)]
        [TestCase(-20f, 60f)]
        [TestCase(30f, 135f)]
        public void Calculator_FallDamage(float speed, float expected)
        {
            Assert.AreEqual(expected, DamageCalculator.ComputeFallDamage(speed), Eps);
        }

        // ---------------------------------------------------------------- HealthService

        [Test]
        public void Health_DamagePublishesFullEvent()
        {
            var h = new HealthService(new PlayerId(3), 100f, _bus);
            var info = new DamageInfo(30f, new PlayerId(9), WeaponIds.G3, BodyPart.Arm, Muzzle, true);
            h.ApplyDamage(info);

            Assert.AreEqual(70f, h.Current, Eps);
            Assert.AreEqual(1, _bus.Count<PlayerDamagedEvent>());
            var e = _bus.Last<PlayerDamagedEvent>();
            Assert.AreEqual(new PlayerId(3), e.VictimId);
            Assert.AreEqual(new PlayerId(9), e.AttackerId);
            Assert.AreEqual(30f, e.DamageAmount, Eps);
            Assert.AreEqual(70f, e.RemainingHealth, Eps);
            Assert.AreEqual(BodyPart.Arm, e.BodyPart);
            Assert.AreEqual(WeaponIds.G3, e.WeaponId);
            Assert.IsTrue(e.HasSourcePosition);
            Assert.AreEqual(Muzzle, e.SourcePosition);
            Assert.AreEqual(0, _bus.Count<PlayerDiedEvent>());
        }

        [Test]
        public void Health_DeathPublishedExactlyOnce()
        {
            var h = new HealthService(new PlayerId(3), 100f, _bus);
            var damagedCalls = 0;
            var diedCalls = 0;
            var order = new List<string>();
            h.Damaged += _ =>
            {
                damagedCalls++;
                order.Add("damaged");
            };
            h.Died += d =>
            {
                diedCalls++;
                order.Add("died");
                Assert.AreEqual(40f, d.Amount, Eps, "Died olayı uygulanan (kırpılmış) hasarı taşır");
            };

            h.ApplyDamage(new DamageInfo(60f, new PlayerId(5), WeaponIds.Mpt76, false));
            h.ApplyDamage(new DamageInfo(500f, new PlayerId(6), WeaponIds.Jng90, BodyPart.Head, Muzzle, true));
            h.ApplyDamage(new DamageInfo(50f, new PlayerId(7), WeaponIds.G3, false));
            h.ApplyDamage(new DamageInfo(50f, new PlayerId(7), WeaponIds.G3, false));

            Assert.IsFalse(h.IsAlive);
            Assert.AreEqual(0f, h.Current, Eps);
            Assert.AreEqual(1, _bus.Count<PlayerDiedEvent>());
            Assert.AreEqual(2, _bus.Count<PlayerDamagedEvent>());
            Assert.AreEqual(2, damagedCalls);
            Assert.AreEqual(1, diedCalls);
            Assert.AreEqual("died", order[order.Count - 1]);
            Assert.AreEqual("damaged", order[order.Count - 2]);

            var died = _bus.Last<PlayerDiedEvent>();
            Assert.AreEqual(new PlayerId(3), died.VictimId);
            Assert.AreEqual(new PlayerId(6), died.KillerId);
            Assert.AreEqual(WeaponIds.Jng90, died.WeaponId);
            Assert.IsTrue(died.IsHeadshot);

            var lethal = _bus.Last<PlayerDamagedEvent>();
            Assert.AreEqual(40f, lethal.DamageAmount, Eps);
            Assert.AreEqual(0f, lethal.RemainingHealth, Eps);
            Assert.IsTrue(lethal.IsHeadshot);
        }

        [Test]
        public void Health_IgnoresInvalidDamage()
        {
            var h = new HealthService(new PlayerId(1), 100f, _bus);
            h.ApplyDamage(new DamageInfo(0f, PlayerId.Invalid, null));
            h.ApplyDamage(new DamageInfo(-10f, PlayerId.Invalid, null));
            h.ApplyDamage(new DamageInfo(float.NaN, PlayerId.Invalid, null));
            Assert.AreEqual(100f, h.Current, Eps);
            Assert.AreEqual(0, _bus.Events.Count);
        }

        [Test]
        public void Health_HealAndCaps()
        {
            var h = new HealthService(new PlayerId(1), 100f, null);
            h.ApplyDamage(new DamageInfo(80f, PlayerId.Invalid, DamageSourceIds.Zone));
            Assert.AreEqual(20f, h.Current, Eps);

            h.HealCapped(100f, 75f);
            Assert.AreEqual(75f, h.Current, Eps);
            h.HealCapped(10f, 75f);
            Assert.AreEqual(75f, h.Current, Eps, "Sınırın üstüne çıkmaz");
            h.HealCapped(10f, 500f);
            Assert.AreEqual(85f, h.Current, Eps);
            h.Heal(100f);
            Assert.AreEqual(100f, h.Current, Eps);
            h.Heal(-5f);
            Assert.AreEqual(100f, h.Current, Eps);
        }

        [Test]
        public void Health_DeadCannotHealButResetRevives()
        {
            var h = new HealthService(new PlayerId(1), 100f, _bus);
            h.ApplyDamage(new DamageInfo(150f, PlayerId.Invalid, DamageSourceIds.Fall));
            Assert.IsFalse(h.IsAlive);
            h.Heal(50f);
            Assert.AreEqual(0f, h.Current, Eps);
            Assert.IsFalse(h.IsAlive);

            h.ResetToFull();
            Assert.IsTrue(h.IsAlive);
            Assert.AreEqual(100f, h.Current, Eps);

            h.ApplyDamage(new DamageInfo(100f, PlayerId.Invalid, DamageSourceIds.Fall));
            Assert.AreEqual(2, _bus.Count<PlayerDiedEvent>(), "Yeniden doğduktan sonra yeni ölüm yayınlanır");
        }

        // ---------------------------------------------------------------- CombatService

        [Test]
        public void Combat_BulletHitAppliesBodyPartArmorAndConfirms()
        {
            var combat = new CombatService(_bus, _registry, _teams);
            Spawn(1, 0);
            var victim = Spawn(2, 1);
            victim.Vest = Armor(0.5f);

            var weapon = WeaponCatalog.Get(WeaponIds.Mpt76);
            var outcome = combat.ApplyBulletHit(new PlayerId(1), weapon, new PlayerId(2), BodyPart.Torso, 10f, Muzzle);

            Assert.IsTrue(outcome.Applied);
            Assert.AreEqual(18f, outcome.Damage, Eps);
            Assert.IsTrue(outcome.ArmorAbsorbed);
            Assert.IsFalse(outcome.Killed);
            Assert.AreEqual(82f, victim.Health.Current, Eps);
            Assert.AreEqual(82f, victim.Vest.Durability, Eps);

            Assert.AreEqual(BodyPart.Torso, victim.LastDamage.BodyPart);
            Assert.AreEqual(Muzzle, victim.LastDamage.SourcePosition);
            Assert.IsTrue(victim.LastDamage.HasSourcePosition);
            Assert.AreEqual(new PlayerId(1), victim.LastDamage.AttackerId);
            Assert.AreEqual(WeaponIds.Mpt76, victim.LastDamage.SourceWeaponId);

            var hit = _bus.Last<HitConfirmedEvent>();
            Assert.AreEqual(new PlayerId(1), hit.AttackerId);
            Assert.AreEqual(new PlayerId(2), hit.VictimId);
            Assert.AreEqual(18f, hit.Damage, Eps);
            Assert.IsTrue(hit.ArmorAbsorbed);
            Assert.IsFalse(hit.IsKill);
        }

        [Test]
        public void Combat_LimbsIgnoreArmor()
        {
            var combat = new CombatService(_bus, _registry, _teams);
            var victim = Spawn(2, 1);
            victim.Vest = Armor(0.5f);
            victim.Helmet = Armor(0.5f);
            var outcome = combat.ApplyBulletHit(new PlayerId(1), WeaponCatalog.Get(WeaponIds.Mpt76), new PlayerId(2),
                BodyPart.Leg, 10f, Muzzle);
            Assert.AreEqual(36f * 0.8f, outcome.Damage, Eps);
            Assert.IsFalse(outcome.ArmorAbsorbed);
            Assert.AreEqual(100f, victim.Vest.Durability, Eps);
        }

        [Test]
        public void Combat_HeadshotKillConfirmed()
        {
            var combat = new CombatService(_bus, _registry, _teams);
            Spawn(1, 0);
            var victim = Spawn(2, 1);

            var outcome = combat.ApplyBulletHit(new PlayerId(1), WeaponCatalog.Get(WeaponIds.Jng90), new PlayerId(2),
                BodyPart.Head, 120f, Muzzle);

            Assert.IsTrue(outcome.Applied);
            Assert.IsTrue(outcome.Killed);
            Assert.IsTrue(outcome.Headshot);
            Assert.AreEqual(100f, outcome.Damage, Eps, "Raporlanan hasar kalan canla sınırlı");
            Assert.IsFalse(victim.IsAlive);

            var hit = _bus.Last<HitConfirmedEvent>();
            Assert.IsTrue(hit.IsKill);
            Assert.IsTrue(hit.IsHeadshot);

            var died = _bus.Last<PlayerDiedEvent>();
            Assert.AreEqual(new PlayerId(1), died.KillerId);
            Assert.AreEqual(WeaponIds.Jng90, died.WeaponId);
            Assert.IsTrue(died.IsHeadshot);

            // Ölüye ikinci isabet uygulanmaz.
            var again = combat.ApplyBulletHit(new PlayerId(1), WeaponCatalog.Get(WeaponIds.Jng90), new PlayerId(2),
                BodyPart.Head, 120f, Muzzle);
            Assert.IsFalse(again.Applied);
            Assert.AreEqual(1, _bus.Count<PlayerDiedEvent>());
            Assert.AreEqual(1, _bus.Count<HitConfirmedEvent>());
        }

        [Test]
        public void Combat_FriendlyFireBlockedBetweenAllies()
        {
            var combat = new CombatService(_bus, _registry, _teams, friendlyFire: false);
            Spawn(1, 0);
            var ally = Spawn(2, 0);
            var enemy = Spawn(3, 1);
            var weapon = WeaponCatalog.Get(WeaponIds.G3);

            var allyHit = combat.ApplyBulletHit(new PlayerId(1), weapon, new PlayerId(2), BodyPart.Torso, 5f, Muzzle);
            Assert.IsFalse(allyHit.Applied);
            Assert.AreEqual(100f, ally.Health.Current, Eps);
            Assert.AreEqual(0, ally.DamageCalls);
            Assert.IsFalse(combat.CanDamage(new PlayerId(1), new PlayerId(2)));

            Assert.IsFalse(combat.ApplyMeleeHit(new PlayerId(1), new PlayerId(2), BodyPart.Torso, Muzzle).Applied);
            Assert.IsFalse(combat.ApplyExplosion(new PlayerId(1), new PlayerId(2), DamageSourceIds.FragGrenade, 80f, Muzzle).Applied);

            var enemyHit = combat.ApplyBulletHit(new PlayerId(1), weapon, new PlayerId(3), BodyPart.Torso, 5f, Muzzle);
            Assert.IsTrue(enemyHit.Applied);
            Assert.AreEqual(62f, enemy.Health.Current, Eps);
            Assert.AreEqual(1, _bus.Count<HitConfirmedEvent>());
        }

        [Test]
        public void Combat_FriendlyFireEnabledOrNoTeamsAllowsDamage()
        {
            Spawn(1, 0);
            var ally = Spawn(2, 0);
            var weapon = WeaponCatalog.Get(WeaponIds.G3);

            var ffOn = new CombatService(_bus, _registry, _teams, friendlyFire: true);
            Assert.IsTrue(ffOn.ApplyBulletHit(new PlayerId(1), weapon, new PlayerId(2), BodyPart.Torso, 5f, Muzzle).Applied);
            Assert.AreEqual(62f, ally.Health.Current, Eps);

            var noTeams = new CombatService(_bus, _registry);
            Assert.IsTrue(noTeams.ApplyBulletHit(new PlayerId(1), weapon, new PlayerId(2), BodyPart.Torso, 5f, Muzzle).Applied);
            Assert.AreEqual(24f, ally.Health.Current, Eps);
        }

        [Test]
        public void Combat_SelfExplosionDamagesWithoutHitConfirm()
        {
            var combat = new CombatService(_bus, _registry, _teams);
            var self = Spawn(1, 0);
            var outcome = combat.ApplyExplosion(new PlayerId(1), new PlayerId(1), DamageSourceIds.FragGrenade, 40f, Muzzle);
            Assert.IsTrue(outcome.Applied);
            Assert.AreEqual(60f, self.Health.Current, Eps);
            Assert.AreEqual(0, _bus.Count<HitConfirmedEvent>());
        }

        [Test]
        public void Combat_ExplosionHalvesVestAndIgnoresHelmet()
        {
            var combat = new CombatService(_bus, _registry, _teams);
            var victim = Spawn(2, 1);
            victim.Vest = Armor(0.4f);
            victim.Helmet = Armor(0.9f);

            var outcome = combat.ApplyExplosion(new PlayerId(1), new PlayerId(2), DamageSourceIds.FragGrenade, 50f, Muzzle);

            // Yelek %40 → patlamada %20 → 40 hasar.
            Assert.AreEqual(40f, outcome.Damage, Eps);
            Assert.IsTrue(outcome.ArmorAbsorbed);
            Assert.AreEqual(60f, victim.Health.Current, Eps);
            Assert.AreEqual(100f, victim.Helmet.Durability, Eps, "Kask etkilenmez");
            Assert.AreEqual(90f, victim.Vest.Durability, Eps);
            Assert.AreEqual(BodyPart.Torso, victim.LastDamage.BodyPart);
            Assert.AreEqual(DamageSourceIds.FragGrenade, victim.LastDamage.SourceWeaponId);

            var defaultSource = combat.ApplyExplosion(new PlayerId(1), new PlayerId(2), null, 10f, Muzzle);
            Assert.IsTrue(defaultSource.Applied);
            Assert.AreEqual(DamageSourceIds.FragGrenade, victim.LastDamage.SourceWeaponId);
        }

        [Test]
        public void Combat_EnvironmentalIgnoresArmorAndHasNoAttacker()
        {
            var combat = new CombatService(_bus, _registry, _teams);
            var victim = Spawn(2, 1);
            victim.Vest = Armor(0.9f);

            var outcome = combat.ApplyEnvironmentalDamage(new PlayerId(2), DamageSourceIds.Zone, 30f);
            Assert.IsTrue(outcome.Applied);
            Assert.AreEqual(70f, victim.Health.Current, Eps);
            Assert.AreEqual(100f, victim.Vest.Durability, Eps);
            Assert.IsFalse(victim.LastDamage.AttackerId.IsValid);
            Assert.AreEqual(0, _bus.Count<HitConfirmedEvent>());

            combat.ApplyEnvironmentalDamage(new PlayerId(2), DamageSourceIds.Zone, 100f);
            var died = _bus.Last<PlayerDiedEvent>();
            Assert.IsFalse(died.KillerId.IsValid);
            Assert.AreEqual(DamageSourceIds.Zone, died.WeaponId);
        }

        [Test]
        public void Combat_MeleeFistDamage()
        {
            var combat = new CombatService(_bus, _registry, _teams);
            var victim = Spawn(2, 1);
            victim.Vest = Armor(0.5f);

            var body = combat.ApplyMeleeHit(new PlayerId(1), new PlayerId(2), BodyPart.Torso, Muzzle);
            Assert.AreEqual(DamageCalculator.FistDamage, body.Damage, Eps);
            var head = combat.ApplyMeleeHit(new PlayerId(1), new PlayerId(2), BodyPart.Head, Muzzle);
            Assert.AreEqual(DamageCalculator.FistDamage * DamageCalculator.FistHeadMultiplier, head.Damage, Eps);
            Assert.AreEqual(DamageSourceIds.Fists, victim.LastDamage.SourceWeaponId);
        }

        [Test]
        public void Combat_MissingOrInvalidTargetsAreSafe()
        {
            var combat = new CombatService(_bus, _registry, _teams);
            var weapon = WeaponCatalog.Get(WeaponIds.G3);
            Assert.IsFalse(combat.ApplyBulletHit(new PlayerId(1), weapon, new PlayerId(42), BodyPart.Torso, 5f, Muzzle).Applied);
            Assert.IsFalse(combat.ApplyBulletHit(new PlayerId(1), weapon, PlayerId.Invalid, BodyPart.Torso, 5f, Muzzle).Applied);
            Assert.IsFalse(combat.ApplyBulletHit(new PlayerId(1), null, new PlayerId(42), BodyPart.Torso, 5f, Muzzle).Applied);
            Assert.IsFalse(combat.ApplyEnvironmentalDamage(new PlayerId(42), DamageSourceIds.Zone, 5f).Applied);

            var noRegistry = new CombatService(null, null);
            Assert.IsFalse(noRegistry.ApplyBulletHit(new PlayerId(1), weapon, new PlayerId(2), BodyPart.Torso, 5f, Muzzle).Applied);
            Assert.AreEqual(0, _bus.Events.Count);
        }

        private sealed class FixedScanner : IHitScanner
        {
            public HitScanResult Result;
            public int Calls;

            public HitScanResult Scan(HitScanRequest request)
            {
                Calls++;
                return Result;
            }
        }

        [Test]
        public void Combat_LegacyTryFireConsumesAmmoScansAndDamages()
        {
            var combat = new CombatService(_bus, _registry, _teams);
            Spawn(1, 0);
            var victim = Spawn(2, 1);
            var weapon = new WeaponRuntimeService(WeaponCatalog.Get(WeaponIds.Mpt76), _bus) { OwnerId = new PlayerId(1) };
            var scanner = new FixedScanner { Result = new HitScanResult(true, new PlayerId(2), true, 0f, 0f, 10f) };
            var request = new HitScanRequest(0f, 0f, 0f, 0f, 0f, 1f, 500f);

            Assert.IsTrue(combat.TryFire(new PlayerId(1), weapon, scanner, request));
            Assert.AreEqual(19, weapon.CurrentAmmo);
            Assert.AreEqual(1, scanner.Calls);
            Assert.AreEqual(1, _bus.Count<WeaponFiredEvent>());
            Assert.AreEqual(28f, victim.Health.Current, Eps);
            Assert.AreEqual(BodyPart.Head, victim.LastDamage.BodyPart);

            // Soğuma sırasında atış yok, tarama yok.
            Assert.IsFalse(combat.TryFire(new PlayerId(1), weapon, scanner, request));
            Assert.AreEqual(1, scanner.Calls);
            Assert.IsFalse(combat.TryFire(new PlayerId(1), null, scanner, request));
        }
    }
}
