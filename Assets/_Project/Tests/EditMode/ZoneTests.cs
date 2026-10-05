using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;

namespace Project.Tests.Match
{
    public sealed class ZoneTests
    {
        private const float MapHalf = 512f;
        private const float Epsilon = 1e-2f;

        private MatchTestBus _bus;

        [SetUp]
        public void SetUp()
        {
            _bus = new MatchTestBus();
        }

        private ZoneService CreateDefault(int seed, ZonePhase[] phases = null)
        {
            var zone = new ZoneService(phases ?? MatchConfig.DefaultZonePhases(), new SeededRandom(seed), _bus, MapHalf);
            zone.Initialize(0f, 0f, 740f, 0.5f);
            return zone;
        }

        private static ZonePhase[] SimplePhases() => new[]
        {
            new ZonePhase(10f, 20f, 300f, 1f),
            new ZonePhase(5f, 10f, 100f, 4f),
            new ZonePhase(5f, 10f, 0f, 9f)
        };

        private static float Distance(float ax, float az, float bx, float bz)
        {
            var dx = ax - bx;
            var dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static void AssertInside(ZoneState inner, ZoneState outer, string message)
        {
            var d = Distance(inner.CenterX, inner.CenterZ, outer.CenterX, outer.CenterZ);
            Assert.LessOrEqual(d + inner.Radius, outer.Radius + Epsilon, message);
        }

        [Test]
        public void BeforeStart_IdleAndHarmless()
        {
            var zone = CreateDefault(1);

            Assert.IsFalse(zone.IsActive);
            Assert.AreEqual(ZoneStage.Idle, zone.Stage);
            Assert.AreEqual(7, zone.PhaseCount);
            Assert.AreEqual(0f, zone.CurrentDamagePerSecond, 1e-6f);
            Assert.AreEqual(0f, zone.GetDamagePerSecond(5000f, 5000f), 1e-6f, "Start'tan önce hasar yok");
            Assert.IsTrue(zone.IsInsideZone(5000f, 5000f));
            Assert.AreEqual(0f, zone.StageRemainingSeconds, 1e-6f);
            Assert.AreEqual(740f, zone.CurrentZone.Radius, 1e-3f);

            zone.Tick(1000f);
            Assert.AreEqual(ZoneStage.Idle, zone.Stage, "Tick Start'tan önce ilerletmez");
            Assert.AreEqual(0, _bus.Of<ZoneStageChangedEvent>().Count);
        }

        [Test]
        public void Start_EntersWaitingOfPhaseZero_WithNextCirclePicked()
        {
            var zone = CreateDefault(3);
            zone.Start();

            Assert.IsTrue(zone.IsActive);
            Assert.AreEqual(ZoneStage.Waiting, zone.Stage);
            Assert.AreEqual(0, zone.PhaseIndex);
            Assert.AreEqual(150f, zone.StageDurationSeconds, 1e-4f);
            Assert.AreEqual(150f, zone.StageRemainingSeconds, 1e-4f);
            Assert.AreEqual(1f, zone.CurrentDamagePerSecond, 1e-6f);
            Assert.AreEqual(420f, zone.NextZone.Radius, 1e-3f);
            AssertInside(zone.NextZone, zone.CurrentZone, "Faz 0 hedefi başlangıç çemberinin içinde");

            var events = _bus.Of<ZoneStageChangedEvent>();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ZoneStage.Waiting, events[0].Stage);
            Assert.AreEqual(0, events[0].PhaseIndex);
            Assert.AreEqual(7, events[0].PhaseCount);
            Assert.AreEqual(150f, events[0].DurationSeconds, 1e-4f);

            zone.Start();
            Assert.AreEqual(1, _bus.Of<ZoneStageChangedEvent>().Count, "İkinci Start yok sayılır");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        [TestCase(1337)]
        [TestCase(98765)]
        public void Containment_EveryPhaseNestedAndCenterInBounds(int seed)
        {
            var zone = CreateDefault(seed);
            zone.Start();
            var phases = MatchConfig.DefaultZonePhases();
            var bound = MapHalf * 0.8f + 1e-3f;

            var visited = 0;
            var guard = 0;
            while (zone.Stage != ZoneStage.Finished && guard++ < 10000)
            {
                // Her fazın bekleme başında: hedef mevcut çemberin tamamen içinde ve merkez sınır içinde.
                var current = zone.CurrentZone;
                var next = zone.NextZone;
                var expectedRadius = Math.Min(phases[zone.PhaseIndex].TargetRadius, current.Radius);

                Assert.AreEqual(ZoneStage.Waiting, zone.Stage);
                Assert.AreEqual(expectedRadius, next.Radius, 1e-3f, "Hedef yarıçap");
                AssertInside(next, current, "seed " + seed + " faz " + zone.PhaseIndex);
                Assert.LessOrEqual(Math.Abs(next.CenterX), bound, "merkez X sınırda");
                Assert.LessOrEqual(Math.Abs(next.CenterZ), bound, "merkez Z sınırda");

                // Daralma boyunca hareketli çember faz başındaki çemberin içinde, hedefi de kapsıyor.
                var phaseStart = current;
                zone.Tick(zone.StageRemainingSeconds);
                Assert.AreEqual(ZoneStage.Shrinking, zone.Stage);
                var steps = 7;
                var stepSeconds = zone.StageDurationSeconds / steps;
                for (var i = 0; i < steps - 1; i++)
                {
                    zone.Tick(stepSeconds);
                    var moving = zone.CurrentZone;
                    AssertInside(moving, phaseStart, "daralan çember faz başı çemberinin içinde");
                    AssertInside(next, moving, "daralan çember hedefi kapsar");
                }

                zone.Tick(zone.StageRemainingSeconds + 1e-4f);
                visited++;
            }

            Assert.AreEqual(phases.Length, visited);
            Assert.AreEqual(ZoneStage.Finished, zone.Stage);
            Assert.AreEqual(0f, zone.CurrentZone.Radius, 1e-3f, "Son faz yarıçapı 0");
        }

        [TestCase(3, 150f, -200f)]
        [TestCase(17, -300f, 300f)]
        [TestCase(29, 380f, 380f)]
        [TestCase(54, -60f, 10f)]
        public void Containment_OffCenterStart_AllPhasesNestedWithRandomTicks(int seed, float startX, float startZ)
        {
            var zone = new ZoneService(MatchConfig.DefaultZonePhases(), new SeededRandom(seed), _bus, MapHalf);
            zone.Initialize(startX, startZ, 700f, 0.5f);
            zone.Start();

            var tickRng = new SeededRandom(seed * 31 + 7);
            var bound = MapHalf * 0.8f + 1e-3f;
            var phaseStart = zone.CurrentZone;
            var lastPhase = zone.PhaseIndex;
            var target = zone.NextZone;
            var guard = 0;

            while (zone.Stage != ZoneStage.Finished && guard++ < 200000)
            {
                zone.Tick(0.05f + tickRng.NextFloat() * 3f);

                if (zone.PhaseIndex != lastPhase && zone.Stage == ZoneStage.Waiting)
                {
                    // Yeni faz: önceki hedefe tam oturmuş olmalı, yeni hedef onun içinde.
                    Assert.AreEqual(target.Radius, zone.CurrentZone.Radius, 1e-3f, "faz sonu yarıçap");
                    phaseStart = zone.CurrentZone;
                    target = zone.NextZone;
                    lastPhase = zone.PhaseIndex;
                    AssertInside(target, phaseStart, "seed " + seed + " faz " + lastPhase);
                    Assert.LessOrEqual(Math.Abs(target.CenterX), bound);
                    Assert.LessOrEqual(Math.Abs(target.CenterZ), bound);
                }

                if (zone.Stage == ZoneStage.Shrinking)
                {
                    AssertInside(zone.CurrentZone, phaseStart, "daralan çember faz başının içinde");
                    AssertInside(target, zone.CurrentZone, "daralan çember hedefi kapsar");
                }
            }

            Assert.AreEqual(ZoneStage.Finished, zone.Stage);
            Assert.AreEqual(MatchConfig.DefaultZonePhases().Length - 1, zone.PhaseIndex);
        }

        [Test]
        public void FullDefaultPlan_PublishesOneEventPerStage()
        {
            var zone = CreateDefault(21);
            zone.Start();
            for (var i = 0; i < 2000 && zone.Stage != ZoneStage.Finished; i++)
                zone.Tick(1f);

            var events = _bus.Of<ZoneStageChangedEvent>();
            Assert.AreEqual(7 * 2 + 1, events.Count);
            Assert.AreEqual(ZoneStage.Finished, events[events.Count - 1].Stage);
            foreach (var e in events)
                Assert.AreEqual(7, e.PhaseCount);
        }

        [Test]
        public void BeforeStart_DistanceIsToInitialCircle()
        {
            var zone = CreateDefault(2);
            Assert.AreEqual(0f, zone.DistanceToSafeZone(100f, 100f), 1e-6f);
            Assert.AreEqual(60f, zone.DistanceToSafeZone(800f, 0f), 1e-2f);
            Assert.AreEqual(0f, zone.StageProgress01, 1e-6f);
        }

        [Test]
        public void Shrinking_InterpolatesLinearly()
        {
            var zone = CreateDefault(11, SimplePhases());
            zone.Start();
            var from = zone.CurrentZone;
            var to = zone.NextZone;

            zone.Tick(10f);
            Assert.AreEqual(ZoneStage.Shrinking, zone.Stage);
            Assert.AreEqual(from.Radius, zone.CurrentZone.Radius, 1e-3f, "Daralma başında yarıçap değişmez");

            zone.Tick(5f);  // %25
            Assert.AreEqual(from.Radius + (to.Radius - from.Radius) * 0.25f, zone.CurrentZone.Radius, 1e-2f);

            zone.Tick(5f);  // %50
            var mid = zone.CurrentZone;
            Assert.AreEqual((from.Radius + to.Radius) * 0.5f, mid.Radius, 1e-2f);
            Assert.AreEqual((from.CenterX + to.CenterX) * 0.5f, mid.CenterX, 1e-2f);
            Assert.AreEqual((from.CenterZ + to.CenterZ) * 0.5f, mid.CenterZ, 1e-2f);
            Assert.AreEqual(10f, zone.StageRemainingSeconds, 1e-3f);
            Assert.AreEqual(0.5f, zone.StageProgress01, 1e-3f);

            zone.Tick(10f);
            Assert.AreEqual(ZoneStage.Waiting, zone.Stage);
            Assert.AreEqual(1, zone.PhaseIndex);
            Assert.AreEqual(to.Radius, zone.CurrentZone.Radius, 1e-4f);
            Assert.AreEqual(to.CenterX, zone.CurrentZone.CenterX, 1e-4f);
            Assert.AreEqual(to.CenterZ, zone.CurrentZone.CenterZ, 1e-4f);
        }

        [Test]
        public void StageEvents_FollowWaitingShrinkingSequence_ThenFinished()
        {
            var zone = CreateDefault(5, SimplePhases());
            zone.Start();
            zone.Tick(10000f);

            var events = _bus.Of<ZoneStageChangedEvent>();
            Assert.AreEqual(3 * 2 + 1, events.Count);
            for (var phase = 0; phase < 3; phase++)
            {
                Assert.AreEqual(ZoneStage.Waiting, events[phase * 2].Stage);
                Assert.AreEqual(phase, events[phase * 2].PhaseIndex);
                Assert.AreEqual(ZoneStage.Shrinking, events[phase * 2 + 1].Stage);
                Assert.AreEqual(phase, events[phase * 2 + 1].PhaseIndex);
            }

            Assert.AreEqual(10f, events[0].DurationSeconds, 1e-4f, "Faz 0 bekleme süresi");
            Assert.AreEqual(20f, events[1].DurationSeconds, 1e-4f, "Faz 0 daralma süresi");
            Assert.AreEqual(ZoneStage.Finished, events[6].Stage);
            Assert.AreEqual(ZoneStage.Finished, zone.Stage);
            Assert.IsTrue(zone.IsActive, "Bitince de son çember dışında hasar sürer");
            Assert.AreEqual(9f, zone.CurrentDamagePerSecond, 1e-6f);
        }

        [Test]
        public void LargeTick_MatchesManySmallTicks()
        {
            var big = CreateDefault(77);
            var small = new ZoneService(MatchConfig.DefaultZonePhases(), new SeededRandom(77), new MatchTestBus(), MapHalf);
            small.Initialize(0f, 0f, 740f, 0.5f);
            big.Start();
            small.Start();

            big.Tick(400f);
            for (var i = 0; i < 4000; i++)
                small.Tick(0.1f);

            Assert.AreEqual(big.PhaseIndex, small.PhaseIndex);
            Assert.AreEqual(big.Stage, small.Stage);
            Assert.AreEqual(big.CurrentZone.Radius, small.CurrentZone.Radius, 0.5f);
            Assert.AreEqual(big.CurrentZone.CenterX, small.CurrentZone.CenterX, 0.5f);
            Assert.AreEqual(big.NextZone.CenterZ, small.NextZone.CenterZ, 1e-3f);
        }

        [Test]
        public void SameSeed_IsDeterministic_DifferentSeedDiffers()
        {
            var a = CreateDefault(123);
            var b = CreateDefault(123);
            var c = CreateDefault(124);
            a.Start();
            b.Start();
            c.Start();

            Assert.AreEqual(a.NextZone.CenterX, b.NextZone.CenterX, 1e-6f);
            Assert.AreEqual(a.NextZone.CenterZ, b.NextZone.CenterZ, 1e-6f);
            Assert.IsTrue(Math.Abs(a.NextZone.CenterX - c.NextZone.CenterX) > 1e-4f
                          || Math.Abs(a.NextZone.CenterZ - c.NextZone.CenterZ) > 1e-4f);
        }

        [Test]
        public void Damage_OnlyAfterStartAndOutside()
        {
            var zone = CreateDefault(9, SimplePhases());
            zone.Start();
            var next = zone.NextZone;

            Assert.AreEqual(0f, zone.GetDamagePerSecond(0f, 0f), 1e-6f);
            Assert.IsTrue(zone.IsInsideZone(0f, 0f));
            Assert.IsFalse(zone.IsInsideZone(900f, 0f));
            Assert.AreEqual(1f, zone.GetDamagePerSecond(900f, 0f), 1e-6f);

            // Faz 1'e geç: hasar artar.
            zone.Tick(30f);
            Assert.AreEqual(1, zone.PhaseIndex);
            Assert.AreEqual(4f, zone.CurrentDamagePerSecond, 1e-6f);
            Assert.AreEqual(4f, zone.CurrentZone.DamagePerSecond, 1e-6f);
            var outsideX = next.CenterX + next.Radius + 5f;
            Assert.AreEqual(4f, zone.GetDamagePerSecond(outsideX, next.CenterZ), 1e-6f);
            Assert.AreEqual(0f, zone.GetDamagePerSecond(next.CenterX, next.CenterZ), 1e-6f);
        }

        [Test]
        public void DistanceToSafeZone_MeasuresToNextCircle()
        {
            var zone = CreateDefault(4, SimplePhases());
            zone.Start();
            var next = zone.NextZone;

            Assert.AreEqual(0f, zone.DistanceToSafeZone(next.CenterX, next.CenterZ), 1e-6f);
            Assert.AreEqual(0f, zone.DistanceToSafeZone(next.CenterX + next.Radius * 0.5f, next.CenterZ), 1e-6f);
            Assert.AreEqual(40f, zone.DistanceToSafeZone(next.CenterX, next.CenterZ + next.Radius + 40f), 1e-2f);
        }

        [Test]
        public void OffMapInitialCircle_ContainmentTakesPriority()
        {
            var zone = new ZoneService(SimplePhases(), new SeededRandom(2), _bus, MapHalf);
            zone.Initialize(2000f, 2000f, 2100f, 1f);
            zone.Start();

            AssertInside(zone.NextZone, zone.CurrentZone, "Sınır sağlanamasa da yeni çember tamamen içeride");
            // Sınır kutusuna doğru çekilmiş olmalı.
            Assert.Less(zone.NextZone.CenterX, 2000f);
            Assert.Less(zone.NextZone.CenterZ, 2000f);
        }

        [Test]
        public void TargetLargerThanCurrent_IsClamped()
        {
            var phases = new[] { new ZonePhase(1f, 1f, 5000f, 2f) };
            var zone = new ZoneService(phases, new SeededRandom(3), _bus, MapHalf);
            zone.Initialize(10f, -20f, 300f, 1f);
            zone.Start();

            Assert.AreEqual(300f, zone.NextZone.Radius, 1e-4f);
            Assert.AreEqual(10f, zone.NextZone.CenterX, 1e-3f);
            Assert.AreEqual(-20f, zone.NextZone.CenterZ, 1e-3f);
        }

        [Test]
        public void EmptyPlan_StartFinishesImmediately_UsingBaseDamage()
        {
            var zone = new ZoneService(new ZonePhase[0], new SeededRandom(1), _bus, MapHalf);
            zone.Initialize(0f, 0f, 100f, 3f);
            zone.Start();

            Assert.AreEqual(ZoneStage.Finished, zone.Stage);
            Assert.AreEqual(3f, zone.GetDamagePerSecond(500f, 0f), 1e-6f);
            Assert.AreEqual(0f, zone.GetDamagePerSecond(0f, 0f), 1e-6f);
            Assert.DoesNotThrow(() => zone.Tick(5f));
        }

        [Test]
        public void ZeroDurationStages_AdvanceWithoutHanging()
        {
            var phases = new[]
            {
                new ZonePhase(0f, 0f, 200f, 1f),
                new ZonePhase(0f, 0f, 50f, 2f)
            };
            var zone = CreateDefault(8, phases);
            zone.Start();
            zone.Tick(0.016f);

            Assert.AreEqual(ZoneStage.Finished, zone.Stage);
            Assert.AreEqual(50f, zone.CurrentZone.Radius, 1e-3f);
        }

        [Test]
        public void ManualShrink_KeepsNextInside()
        {
            var zone = CreateDefault(6, SimplePhases());
            zone.Start();
            zone.Shrink(150f);

            Assert.AreEqual(150f, zone.CurrentZone.Radius, 1e-4f);
            AssertInside(zone.NextZone, zone.CurrentZone, "Elle daraltmadan sonra hedef içeride");

            zone.Tick(15f);  // daralmanın ortası (10 bekleme + 5/20)
            AssertInside(zone.NextZone, zone.CurrentZone, "Daralma sürerken de içeride");
        }

        [Test]
        public void NullDependencies_AreTolerated()
        {
            var zone = new ZoneService(null, null, null, -5f);
            Assert.AreEqual(7, zone.PhaseCount, "Null faz planı → varsayılan plan");
            Assert.AreEqual(512f, zone.MapHalfSize, 1e-6f);
            zone.Start();
            Assert.DoesNotThrow(() => zone.Tick(2000f));
            Assert.AreEqual(ZoneStage.Finished, zone.Stage);
        }

        [Test]
        public void Reinitialize_ResetsToIdle()
        {
            var zone = CreateDefault(10, SimplePhases());
            zone.Start();
            zone.Tick(12f);
            zone.Initialize(5f, 5f, 600f, 0f);

            Assert.AreEqual(ZoneStage.Idle, zone.Stage);
            Assert.IsFalse(zone.IsActive);
            Assert.AreEqual(600f, zone.CurrentZone.Radius, 1e-4f);
            Assert.AreEqual(0f, zone.GetDamagePerSecond(5000f, 0f), 1e-6f);
        }
    }
}
