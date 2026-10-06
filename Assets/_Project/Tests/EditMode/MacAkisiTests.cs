using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;

namespace Project.Tests.Match
{
    /// <summary>P4 maç akışı: bölge planı, ikmal sandığı, başlangıç kiti, arazi çıpaları. Docs/MAC_AKISI.md</summary>
    public sealed class MacAkisiTests
    {
        private const float MapHalf = 512f;

        private static List<T> Of<T>(MatchTestBus bus) => bus.Published.OfType<T>().ToList();

        // ---------------------------------------------------------------- Bölge planı

        [Test]
        public void DefaultZonePlan_IsMonotoneAndOutrunnable()
        {
            var phases = MatchConfig.DefaultZonePhases();
            var radius = 740f;
            var total = 0f;
            for (var i = 0; i < phases.Length; i++)
            {
                var p = phases[i];
                Assert.Less(p.TargetRadius, radius, "yarıçap azalmalı, faz " + i);
                var edgeSpeed = (radius - p.TargetRadius) / p.ShrinkSeconds;
                Assert.LessOrEqual(edgeSpeed, 5.5f, "çember kenarı koşudan yavaş olmalı, faz " + i);
                if (i > 0)
                {
                    Assert.Greater(p.DamagePerSecond, phases[i - 1].DamagePerSecond, "hasar artmalı");
                    Assert.LessOrEqual(p.WaitSeconds, phases[i - 1].WaitSeconds, "bekleme kısalmalı");
                }

                radius = p.TargetRadius;
                total += p.WaitSeconds + p.ShrinkSeconds;
            }

            Assert.AreEqual(0f, phases[phases.Length - 1].TargetRadius, 1e-4f);
            Assert.That(total, Is.InRange(600f, 800f), "toplam süre ~10-13 dk");
            Assert.LessOrEqual(phases[0].WaitSeconds, 120f, "ilk bekleme: ölü zaman sınırı");
        }

        [Test]
        public void DefaultAirdropConfig_FillsDeadTime()
        {
            var c = new MatchConfig();
            Assert.LessOrEqual(c.AirdropFirstSeconds, 90f, "ilk olay 90 sn içinde");
            Assert.LessOrEqual(c.AirdropIntervalSeconds, 120f);
            Assert.GreaterOrEqual(c.AirdropOpenDelaySeconds, 5f, "anında üçüncü taraf engeli");
            Assert.GreaterOrEqual(c.ArtilleryCooldownSeconds, 180f);
        }

        // ---------------------------------------------------------------- İkmal sandığı

        private static (AirdropService svc, MatchTestBus bus, ZoneService zone) CreateAirdrop(int seed, MatchConfig cfg = null)
        {
            cfg ??= new MatchConfig();
            var bus = new MatchTestBus();
            var zone = new ZoneService(cfg.ZonePhases, new SeededRandom(seed), bus, MapHalf);
            zone.Initialize(0f, 0f, 740f, 1f);
            zone.Start();
            var svc = new AirdropService(bus, zone, new SeededRandom(seed + 1), cfg);
            return (svc, bus, zone);
        }

        [Test]
        public void Airdrop_DoesNotRunBeforeInMatch()
        {
            var (svc, bus, _) = CreateAirdrop(1);
            svc.Tick(500f);
            Assert.AreEqual(0, Of<AirdropEvent>(bus).Count);
            bus.Publish(new MatchPhaseChangedEvent(MatchPhase.Insertion, MatchPhase.InMatch));
            Assert.IsTrue(svc.IsArmed);
        }

        [Test]
        public void Airdrop_StagesFireInOrderWithConfiguredTimings()
        {
            var cfg = new MatchConfig();
            var (svc, bus, _) = CreateAirdrop(2, cfg);
            bus.Publish(new MatchPhaseChangedEvent(MatchPhase.Insertion, MatchPhase.InMatch));

            svc.Tick(cfg.AirdropFirstSeconds - 1f);
            Assert.AreEqual(0, Of<AirdropEvent>(bus).Count);
            svc.Tick(1.01f);
            var ev = Of<AirdropEvent>(bus);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(AirdropStage.Announced, ev[0].Stage);
            Assert.AreEqual(cfg.AirdropDescentSeconds, ev[0].SecondsToNextStage, 1e-3f);

            svc.Tick(cfg.AirdropDescentSeconds);
            Assert.AreEqual(AirdropStage.Landed, Of<AirdropEvent>(bus).Last().Stage);
            Assert.AreEqual(1, svc.ActiveCount);
            svc.Tick(cfg.AirdropOpenDelaySeconds);
            Assert.AreEqual(AirdropStage.Opened, Of<AirdropEvent>(bus).Last().Stage);
            Assert.AreEqual(0, svc.ActiveCount);
        }

        [Test]
        public void Airdrop_HugeTick_StillOrdersAndCapsCount()
        {
            var cfg = new MatchConfig { AirdropCount = 3, AirdropMinZoneRadius = 0f };
            var (svc, bus, _) = CreateAirdrop(3, cfg);
            bus.Publish(new MatchPhaseChangedEvent(MatchPhase.Insertion, MatchPhase.InMatch));
            svc.Tick(5000f);

            var ev = Of<AirdropEvent>(bus);
            Assert.AreEqual(9, ev.Count, "3 sandık x 3 aşama");
            foreach (var id in new[] { 0, 1, 2 })
            {
                var stages = ev.Where(e => e.Id == id).Select(e => e.Stage).ToArray();
                CollectionAssert.AreEqual(new[] { AirdropStage.Announced, AirdropStage.Landed, AirdropStage.Opened }, stages);
            }
        }

        [Test]
        public void Airdrop_PositionInsideNextZoneRingAndSeparated()
        {
            for (var seed = 0; seed < 60; seed++)
            {
                var cfg = new MatchConfig { AirdropMinZoneRadius = 0f };
                var (svc, bus, zone) = CreateAirdrop(seed, cfg);
                bus.Publish(new MatchPhaseChangedEvent(MatchPhase.Insertion, MatchPhase.InMatch));
                // İlk iki duyuru için bölgeyi ilerletmeden (aynı sonraki çember) test.
                svc.Tick(cfg.AirdropFirstSeconds + 0.01f);
                var next = zone.NextZone;
                var d = svc.Drops[0];
                var dx = d.Position.X - next.CenterX;
                var dz = d.Position.Z - next.CenterZ;
                var dist = (float)Math.Sqrt(dx * dx + dz * dz);
                Assert.LessOrEqual(dist, next.Radius * AirdropService.RingMax + 1f, "sonraki çemberin içinde, tohum " + seed);
                Assert.LessOrEqual(Math.Abs(d.Position.X), MapHalf * 0.85f + 0.01f);
                Assert.LessOrEqual(Math.Abs(d.Position.Z), MapHalf * 0.85f + 0.01f);
            }
        }

        [Test]
        public void Airdrop_Cancelled_WhenNextCircleTooSmall()
        {
            var cfg = new MatchConfig { AirdropCount = 4, AirdropFirstSeconds = 10f, AirdropIntervalSeconds = 10f, AirdropMinZoneRadius = 1000f };
            var (svc, bus, _) = CreateAirdrop(5, cfg);
            bus.Publish(new MatchPhaseChangedEvent(MatchPhase.Insertion, MatchPhase.InMatch));
            svc.Tick(200f);
            Assert.AreEqual(0, Of<AirdropEvent>(bus).Count);
        }

        [Test]
        public void Airdrop_Deterministic_ForSameSeed()
        {
            Float3 Run()
            {
                var (svc, bus, _) = CreateAirdrop(77);
                bus.Publish(new MatchPhaseChangedEvent(MatchPhase.Insertion, MatchPhase.InMatch));
                svc.Tick(100f);
                return svc.Drops[0].Position;
            }

            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void Airdrop_DisabledWhenCountZero_AndStopsAfterMatchEnds()
        {
            var cfg = new MatchConfig { AirdropCount = 0 };
            var (svc, bus, _) = CreateAirdrop(8, cfg);
            bus.Publish(new MatchPhaseChangedEvent(MatchPhase.Insertion, MatchPhase.InMatch));
            svc.Tick(1000f);
            Assert.AreEqual(0, Of<AirdropEvent>(bus).Count);

            var (svc2, bus2, _) = CreateAirdrop(8);
            bus2.Publish(new MatchPhaseChangedEvent(MatchPhase.Insertion, MatchPhase.InMatch));
            bus2.Publish(new MatchPhaseChangedEvent(MatchPhase.InMatch, MatchPhase.Ending));
            svc2.Tick(1000f);
            Assert.AreEqual(0, Of<AirdropEvent>(bus2).Count);
        }

        // ---------------------------------------------------------------- Yağma

        [Test]
        public void LootChances_FollowLocationTiers()
        {
            var s = new LootSpawnService();
            Assert.Less(s.SpawnChance(LootTier.Low), s.SpawnChance(LootTier.Medium));
            Assert.Less(s.SpawnChance(LootTier.Medium), s.SpawnChance(LootTier.High));
            Assert.Less(s.SpawnChance(LootTier.High), s.SpawnChance(LootTier.Military));
            Assert.Less(s.SpawnChance(LootTier.Low), 0.45f, "tarla yağması seyrek");
        }

        [Test]
        public void SupplyCrate_AlwaysHighTierGuaranteedContents()
        {
            var s = new LootSpawnService();
            var high = new HashSet<string>
                { WeaponIds.Mpt76, WeaponIds.Knt76, WeaponIds.Sar762Mt, WeaponIds.Pmt76, WeaponIds.Jng90, WeaponIds.Mg3 };
            for (var seed = 0; seed < 100; seed++)
            {
                var items = new List<LootItemData>();
                s.RollSupplyCrate(new SeededRandom(seed), items);
                Assert.IsTrue(items.All(i => i.IsValid), "geçersiz eşya yok");
                Assert.AreEqual(1, items.Count(i => i.Category == ItemCategory.Weapon && high.Contains(i.ItemId)), "tek yüksek silah");
                Assert.IsTrue(items.Any(i => i.ItemId == ItemIds.Vest3));
                Assert.IsTrue(items.Any(i => i.ItemId == ItemIds.Helmet3));
                Assert.IsTrue(items.Any(i => i.ItemId == ItemIds.MedKit));
                Assert.GreaterOrEqual(items.Count(i => i.Category == ItemCategory.Ammunition), LootSpawnService.AmmoStacksPerWeapon);
            }
        }

        [Test]
        public void StartKit_Guaranteed_SidearmAmmoBandages()
        {
            var s = new LootSpawnService();
            for (var seed = 0; seed < 50; seed++)
            {
                var items = new List<LootItemData>();
                s.RollStartKit(new SeededRandom(seed), items, false);
                Assert.AreEqual(1, items.Count(i => i.Category == ItemCategory.Weapon));
                Assert.AreEqual(2, items.Count(i => i.ItemId == ItemIds.Bandage));
                Assert.AreEqual(LootSpawnService.AmmoStacksPerWeapon, items.Count(i => i.Category == ItemCategory.Ammunition));

                var withPrimary = new List<LootItemData>();
                s.RollStartKit(new SeededRandom(seed), withPrimary, true);
                Assert.AreEqual(2, withPrimary.Count(i => i.Category == ItemCategory.Weapon));
                Assert.IsTrue(withPrimary.All(i => i.IsValid));
            }
        }

        [Test]
        public void StartKit_NullOutput_DoesNotThrow()
        {
            var s = new LootSpawnService();
            Assert.DoesNotThrow(() => s.RollStartKit(null, null, true));
            Assert.DoesNotThrow(() => s.RollSupplyCrate(null, null));
        }

        // ---------------------------------------------------------------- Arazi çıpaları

        [Test]
        public void ZoneAnchors_PullFinalCirclesTowardAnchor_AndKeepInvariants()
        {
            var anchor = new ZoneAnchor(-200f, 150f, 1f);
            double sumAnchored = 0, sumPlain = 0;
            var n = 0;
            for (var seed = 0; seed < 80; seed++)
            {
                var bus = new MatchTestBus();
                var anchored = new ZoneService(MatchConfig.DefaultZonePhases(), new SeededRandom(seed), bus, MapHalf);
                anchored.Initialize(0f, 0f, 740f, 1f);
                anchored.SetAnchors(new[] { anchor });
                anchored.Start();
                var plain = new ZoneService(MatchConfig.DefaultZonePhases(), new SeededRandom(seed), new MatchTestBus(), MapHalf);
                plain.Initialize(0f, 0f, 740f, 1f);
                plain.Start();

                var prev = anchored.CurrentZone;
                for (var step = 0; step < 3000 && anchored.Stage != ZoneStage.Finished; step++)
                {
                    var phase = anchored.PhaseIndex;
                    var next = anchored.NextZone;
                    var d = Math.Sqrt((next.CenterX - prev.CenterX) * (next.CenterX - prev.CenterX) + (next.CenterZ - prev.CenterZ) * (next.CenterZ - prev.CenterZ));
                    Assert.LessOrEqual(d + next.Radius, prev.Radius + 0.05f, "tamamen içinde, tohum " + seed);
                    Assert.LessOrEqual(Math.Abs(next.CenterX), MapHalf * 0.8f + 0.01f);
                    Assert.LessOrEqual(Math.Abs(next.CenterZ), MapHalf * 0.8f + 0.01f);
                    anchored.Tick(1f);
                    plain.Tick(1f);
                    if (anchored.PhaseIndex != phase) prev = anchored.CurrentZone;
                }

                anchored.Tick(1e5f);
                plain.Tick(1e5f);
            }

            // Final öncesi (hedef 100 m) çemberin çıpaya ortalama uzaklığı çıpalı planda daha küçük olmalı.
            for (var seed = 0; seed < 80; seed++)
            {
                var a = new ZoneService(MatchConfig.DefaultZonePhases(), new SeededRandom(seed), new MatchTestBus(), MapHalf);
                a.Initialize(0f, 0f, 740f, 1f);
                a.SetAnchors(new[] { anchor });
                a.Start();
                var p = new ZoneService(MatchConfig.DefaultZonePhases(), new SeededRandom(seed), new MatchTestBus(), MapHalf);
                p.Initialize(0f, 0f, 740f, 1f);
                p.Start();
                while (a.PhaseIndex < 3 && a.Stage != ZoneStage.Finished) { a.Tick(1f); p.Tick(1f); }
                var na = a.NextZone; var np = p.NextZone;
                sumAnchored += Math.Sqrt((na.CenterX - anchor.X) * (na.CenterX - anchor.X) + (na.CenterZ - anchor.Z) * (na.CenterZ - anchor.Z));
                sumPlain += Math.Sqrt((np.CenterX - anchor.X) * (np.CenterX - anchor.X) + (np.CenterZ - anchor.Z) * (np.CenterZ - anchor.Z));
                n++;
            }

            Assert.Less(sumAnchored / n, sumPlain / n, "çıpa final çemberini çekmeli");
        }

        [Test]
        public void ZoneAnchors_EarlyPhasesUnaffected_AndEmptyIsLegacy()
        {
            var a = new ZoneService(MatchConfig.DefaultZonePhases(), new SeededRandom(5), new MatchTestBus(), MapHalf);
            a.Initialize(0f, 0f, 740f, 1f);
            a.SetAnchors(new[] { new ZoneAnchor(300f, 300f, 1f) });
            a.Start();
            var b = new ZoneService(MatchConfig.DefaultZonePhases(), new SeededRandom(5), new MatchTestBus(), MapHalf);
            b.Initialize(0f, 0f, 740f, 1f);
            b.SetAnchors(null);
            b.Start();
            Assert.AreEqual(0, b.AnchorCount);
            Assert.AreEqual(b.NextZone.CenterX, a.NextZone.CenterX, 1e-4f, "faz 0 (r=400) çıpadan etkilenmez");
            Assert.AreEqual(b.NextZone.CenterZ, a.NextZone.CenterZ, 1e-4f);
        }

        [Test]
        public void ZoneAnchors_InvalidEntriesIgnored()
        {
            var z = new ZoneService(MatchConfig.DefaultZonePhases(), new SeededRandom(1), new MatchTestBus(), MapHalf);
            z.SetAnchors(new[] { new ZoneAnchor(float.NaN, 0f, 1f), new ZoneAnchor(1f, 1f, 0f), new ZoneAnchor(1f, 1f, 2f) });
            Assert.AreEqual(1, z.AnchorCount);
        }
    }
}
