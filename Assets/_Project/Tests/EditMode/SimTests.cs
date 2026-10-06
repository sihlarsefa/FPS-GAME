using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode.Sim
{
    /// <summary>Testler için kayıt tutan basit olay veri yolu.</summary>
    internal sealed class SimRecordingEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();
        public readonly List<object> Published = new();

        public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
        {
            Published.Add(gameEvent);
            if (!_handlers.TryGetValue(typeof(TEvent), out var list))
                return;

            foreach (var handler in list.ToArray())
                ((Action<TEvent>)handler).Invoke(gameEvent);
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

        public int SubscriberCount<TEvent>() => _handlers.TryGetValue(typeof(TEvent), out var list) ? list.Count : 0;

        public List<TEvent> OfType<TEvent>()
        {
            var result = new List<TEvent>();
            foreach (var e in Published)
            {
                if (e is TEvent typed)
                    result.Add(typed);
            }

            return result;
        }
    }

    /// <summary>Bellek içi ayar mağazası.</summary>
    internal sealed class SimMemoryStore : ISettingsStore
    {
        public readonly Dictionary<string, float> Floats = new();
        public readonly Dictionary<string, int> Ints = new();
        public int SaveCount;

        public bool HasKey(string key) => Floats.ContainsKey(key) || Ints.ContainsKey(key);
        public float GetFloat(string key, float fallback) => Floats.TryGetValue(key, out var v) ? v : fallback;
        public int GetInt(string key, int fallback) => Ints.TryGetValue(key, out var v) ? v : fallback;
        public void SetFloat(string key, float value) => Floats[key] = value;
        public void SetInt(string key, int value) => Ints[key] = value;
        public void Save() => SaveCount++;
    }

    public sealed class SimRandomAndClockTests
    {
        [Test]
        public void SeededRandom_SameSeed_ProducesSameSequence()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            for (var i = 0; i < 50; i++)
            {
                Assert.AreEqual(a.Next(0, 1000), b.Next(0, 1000));
                Assert.AreEqual(a.NextFloat(), b.NextFloat());
            }
        }

        [Test]
        public void SeededRandom_RangesAreRespected()
        {
            var r = new SeededRandom(7);
            for (var i = 0; i < 500; i++)
            {
                var n = r.Next(3, 9);
                Assert.GreaterOrEqual(n, 3);
                Assert.Less(n, 9);
                var f = r.Range(-2f, 5f);
                Assert.GreaterOrEqual(f, -2f);
                Assert.LessOrEqual(f, 5f);
            }

            Assert.AreEqual(4, r.Next(4, 4));
            Assert.AreEqual(4, r.Next(4, 1));
        }

        [Test]
        public void SimulationClock_AdvanceAccumulatesTicks()
        {
            var clock = new SimulationClock(30f);
            Assert.AreEqual(0, clock.Advance(0.01f));
            Assert.AreEqual(3, clock.Advance(0.095f));
            Assert.AreEqual(3u, clock.CurrentTick);
            Assert.AreEqual(0.1f, clock.ElapsedSeconds, 0.0001f);
            Assert.Greater(clock.Alpha, 0f);
            Assert.AreEqual(0, clock.Advance(-1f));
            Assert.AreEqual(0, clock.Advance(float.NaN));
        }

        [Test]
        public void SimulationClock_CapsTicksPerAdvance()
        {
            var clock = new SimulationClock(30f);
            Assert.AreEqual(5, clock.Advance(10f));
            Assert.LessOrEqual(clock.Alpha, 1f);
            clock.Reset();
            Assert.AreEqual(0u, clock.CurrentTick);
            Assert.AreEqual(0f, clock.ElapsedSeconds, 0.0001f);
        }

        [Test]
        public void SimulationClock_InvalidRateFallsBackTo30Hz()
        {
            Assert.AreEqual(1f / 30f, new SimulationClock(0f).TickInterval, 0.0001f);
            Assert.AreEqual(1f / 30f, new SimulationClock(float.NaN).TickInterval, 0.0001f);
            Assert.AreEqual(1f / 60f, new SimulationClock(60f).TickInterval, 0.0001f);
        }
    }

    public sealed class SimPlaneRouteTests
    {
        [Test]
        public void CreateRandom_EndpointsOutsideMapAtAltitude()
        {
            const float half = 512f;
            for (var seed = 1; seed <= 60; seed++)
            {
                var route = PlaneRouteService.CreateRandom(new SeededRandom(seed), half, 300f, 40f);
                Assert.AreEqual(300f, route.Start.Y, 0.001f);
                Assert.AreEqual(300f, route.End.Y, 0.001f);
                Assert.AreEqual(40f, route.Speed, 0.001f);
                // Harita dışında; kenar geçişine uzaklık aşağıda (~350 m) ayrıca doğrulanır.
                Assert.Greater(Math.Max(Math.Abs(route.Start.X), Math.Abs(route.Start.Z)), half + 100f, "seed " + seed);
                Assert.Greater(Math.Max(Math.Abs(route.End.X), Math.Abs(route.End.Z)), half + 100f, "seed " + seed);
                Assert.IsTrue(route.TryGetTimeOverMap(half, out var enterT, out _));
                var edgeIn = route.PositionAt(enterT);
                Assert.AreEqual(PlaneRouteService.OutsideMargin, Float3.Distance(route.Start, edgeIn), 15f, "seed " + seed);
                Assert.IsTrue(route.TryGetTimeOverMap(half, out var enter, out var exit), "route must cross map, seed " + seed);
                Assert.Greater(exit, enter);
            }
        }

        [Test]
        public void CreateRandom_LinePassesNearCenter()
        {
            const float half = 512f;
            for (var seed = 1; seed <= 60; seed++)
            {
                var route = PlaneRouteService.CreateRandom(new SeededRandom(seed), half, 200f, 38f);
                var dir = route.Direction.Flat.Normalized;
                var toCenter = Float3.Zero - route.Start.Flat;
                var along = Float3.Dot(toCenter, dir);
                var closest = route.Start.Flat + dir * along;
                Assert.LessOrEqual(closest.Flat.Magnitude, half * 0.45f + 0.5f, "seed " + seed);
            }
        }

        [Test]
        public void CreateRandom_HandlesNullRandomAndBadInput()
        {
            var route = PlaneRouteService.CreateRandom(null, 0f, 150f, -5f);
            Assert.Greater(route.Speed, 0f);
            Assert.Greater(route.Length, 1000f);
        }

        [Test]
        public void CreateForInsertion_FliesFromStartToLandingZoneAtAltitude()
        {
            var plan = new TeamInsertion(0, InsertionMethod.Helicopter, new Float3(-560f, 0f, 10f), new Float3(-300f, 0f, 40f));
            var route = PlaneRouteService.CreateForInsertion(plan, 90f, 30f);
            Assert.AreEqual(-560f, route.Start.X, 0.001f);
            Assert.AreEqual(90f, route.Start.Y, 0.001f);
            Assert.AreEqual(-300f, route.End.X, 0.001f);
            Assert.AreEqual(40f, route.End.Z, 0.001f);
            Assert.AreEqual(90f, route.End.Y, 0.001f);
            Assert.AreEqual(30f, route.Speed, 0.001f);
        }
    }

    public sealed class SimSkydiveTests
    {
        [Test]
        public void StartsInTransport_AndDoesNotMove()
        {
            var sim = new SkydiveSimulator();
            Assert.AreEqual(DropState.InTransport, sim.State);
            var d = sim.Step(0.1f, 1f, 0f, 0f, 500f);
            Assert.AreEqual(0f, d.Magnitude, 0.0001f);
        }

        [Test]
        public void Freefall_ReachesTerminalVerticalSpeed()
        {
            var sim = new SkydiveSimulator();
            sim.BeginFreefall(Float3.Zero);
            Assert.AreEqual(DropState.Freefall, sim.State);
            for (var i = 0; i < 200; i++)
                sim.Step(0.05f, 0f, 0f, 0f, 2000f);
            Assert.AreEqual(-48f, sim.Velocity.Y, 0.01f);
            Assert.AreEqual(0f, sim.Velocity.Flat.Magnitude, 0.01f);
        }

        [Test]
        public void Freefall_DiveForwardIsFasterAndHorizontalCapped()
        {
            var sim = new SkydiveSimulator();
            sim.BeginFreefall(Float3.Zero);
            for (var i = 0; i < 300; i++)
                sim.Step(0.05f, 1f, 1f, 90f, 5000f);
            Assert.AreEqual(-62f, sim.Velocity.Y, 0.01f);
            Assert.LessOrEqual(sim.Velocity.Flat.Magnitude, 30.01f);
            Assert.Greater(sim.Velocity.Flat.Magnitude, 25f);
        }

        [Test]
        public void Freefall_ForwardFollowsHeading()
        {
            var sim = new SkydiveSimulator();
            sim.BeginFreefall(Float3.Zero);
            for (var i = 0; i < 200; i++)
                sim.Step(0.05f, 1f, 0f, 90f, 5000f);
            Assert.Greater(sim.Velocity.X, 25f, "yaw 90 = +X");
            Assert.AreEqual(0f, sim.Velocity.Z, 0.05f);

            var sim2 = new SkydiveSimulator();
            sim2.BeginFreefall(Float3.Zero);
            for (var i = 0; i < 200; i++)
                sim2.Step(0.05f, 0f, 1f, 0f, 5000f);
            Assert.Greater(sim2.Velocity.X, 15f, "yaw 0, right = +X");
        }

        [Test]
        public void Parachute_AutoOpensBelowThreshold()
        {
            var sim = new SkydiveSimulator();
            sim.BeginFreefall(Float3.Zero);
            sim.Step(0.05f, 0f, 0f, 0f, SkydiveSimulator.AutoOpenHeight + 5f);
            Assert.AreEqual(DropState.Freefall, sim.State);
            sim.Step(0.05f, 0f, 0f, 0f, SkydiveSimulator.AutoOpenHeight - 1f);
            Assert.AreEqual(DropState.Parachute, sim.State);
        }

        [TestCase(0f, -6f)]
        [TestCase(1f, -8f)]
        [TestCase(-1f, -4.5f)]
        public void Parachute_VerticalSpeedDependsOnSteer(float forward, float expected)
        {
            var sim = new SkydiveSimulator();
            sim.BeginFreefall(new Float3(0f, -48f, 0f));
            sim.RequestOpenParachute();
            Assert.AreEqual(DropState.Parachute, sim.State);
            for (var i = 0; i < 200; i++)
                sim.Step(0.05f, forward, 0f, 0f, 1000f);
            Assert.AreEqual(expected, sim.Velocity.Y, 0.01f);
            Assert.LessOrEqual(sim.Velocity.Flat.Magnitude, 13.01f);
        }

        [Test]
        public void Parachute_HorizontalSpeedCapped()
        {
            var sim = new SkydiveSimulator();
            sim.BeginFreefall(new Float3(30f, -48f, 0f));
            sim.RequestOpenParachute();
            for (var i = 0; i < 400; i++)
                sim.Step(0.05f, 1f, 1f, 45f, 1000f);
            Assert.LessOrEqual(sim.Velocity.Flat.Magnitude, 13.01f);
            Assert.Greater(sim.Velocity.Flat.Magnitude, 10f);
        }

        [Test]
        public void Landing_ClampsToGroundAndSetsLanded()
        {
            var sim = new SkydiveSimulator();
            sim.BeginFreefall(Float3.Zero);
            sim.RequestOpenParachute();
            var d = sim.Step(0.05f, 0f, 0f, 0f, 1f);
            Assert.AreEqual(DropState.Landed, sim.State);
            Assert.AreEqual(-1f, d.Y, 0.0001f);
            Assert.AreEqual(0f, sim.Step(0.05f, 1f, 0f, 0f, 0f).Magnitude, 0.0001f);
        }

        [Test]
        public void FullDrop_FromHighAltitude_LandsSafely()
        {
            var sim = new SkydiveSimulator();
            var states = new List<DropState>();
            sim.StateChanged += s => states.Add(s);
            sim.BeginFreefall(new Float3(0f, 0f, 38f));
            var height = 900f;
            var time = 0f;
            var maxDescent = 0f;
            while (sim.State != DropState.Landed && time < 600f)
            {
                var d = sim.Step(1f / 30f, 0.3f, 0f, 0f, height);
                height += d.Y;
                time += 1f / 30f;
                maxDescent = Math.Max(maxDescent, -sim.Velocity.Y);
                Assert.GreaterOrEqual(height, -0.001f);
            }

            Assert.AreEqual(DropState.Landed, sim.State);
            Assert.Less(time, 120f);
            Assert.Less(sim.LandingSpeed, 10f, "paraşüt açılmış olmalı");
            Assert.Contains(DropState.Freefall, states);
            Assert.Contains(DropState.Parachute, states);
            Assert.Contains(DropState.Landed, states);
            Assert.LessOrEqual(maxDescent, 62.01f);
        }

        [Test]
        public void Step_IgnoresInvalidInput()
        {
            var sim = new SkydiveSimulator();
            sim.BeginFreefall(new Float3(float.NaN, 0f, 0f));
            Assert.AreEqual(0f, sim.Velocity.Magnitude, 0.0001f);
            Assert.AreEqual(0f, sim.Step(0f, 1f, 1f, 0f, 500f).Magnitude, 0.0001f);
            var d = sim.Step(0.1f, float.NaN, float.NaN, float.NaN, float.NaN);
            Assert.IsFalse(float.IsNaN(d.X) || float.IsNaN(d.Y) || float.IsNaN(d.Z));
            sim.Reset();
            Assert.AreEqual(DropState.InTransport, sim.State);
        }
    }

    public sealed class SimInsertionPlannerTests
    {
        [Test]
        public void Plan_ReturnsOneEntryPerTeamWithPlayerMethod()
        {
            var plans = InsertionPlanner.Plan(4, 512f, new SeededRandom(3), InsertionMethod.ArmoredVehicle);
            Assert.AreEqual(4, plans.Length);
            for (var i = 0; i < plans.Length; i++)
                Assert.AreEqual(i, plans[i].Team);
            Assert.AreEqual(InsertionMethod.ArmoredVehicle, plans[0].Method);
        }

        [Test]
        public void Plan_ZeroTeams_ReturnsEmpty()
        {
            Assert.AreEqual(0, InsertionPlanner.Plan(0, 512f, new SeededRandom(1), InsertionMethod.Helicopter).Length);
            Assert.AreEqual(0, InsertionPlanner.Plan(-3, 512f, new SeededRandom(1), InsertionMethod.Helicopter).Length);
        }

        [Test]
        public void Plan_LandingZonesInsideLimitsAndInwardFromEdge()
        {
            const float half = 512f;
            for (var seed = 1; seed <= 40; seed++)
            {
                for (var teams = 2; teams <= 6; teams++)
                {
                    var plans = InsertionPlanner.Plan(teams, half, new SeededRandom(seed), InsertionMethod.Helicopter);
                    foreach (var p in plans)
                    {
                        Assert.Less(Math.Abs(p.LandingZone.X), 400f);
                        Assert.Less(Math.Abs(p.LandingZone.Z), 400f);
                        var edgeGap = half - Math.Max(Math.Abs(p.LandingZone.X), Math.Abs(p.LandingZone.Z));
                        Assert.GreaterOrEqual(edgeGap, 100f, "LZ kenardan içeride olmalı");
                        var travel = Float3.DistanceXZ(p.Start, p.LandingZone);
                        Assert.GreaterOrEqual(travel, 120f);
                        Assert.LessOrEqual(travel, 420f);
                    }
                }
            }
        }

        [Test]
        public void Plan_StartsOnEdge_HelicopterOutsideVehicleInside()
        {
            const float half = 512f;
            for (var seed = 1; seed <= 30; seed++)
            {
                var plans = InsertionPlanner.Plan(5, half, new SeededRandom(seed), InsertionMethod.Helicopter);
                foreach (var p in plans)
                {
                    var edge = Math.Max(Math.Abs(p.Start.X), Math.Abs(p.Start.Z));
                    if (p.Method == InsertionMethod.Helicopter)
                        Assert.Greater(edge, half);
                    else
                        Assert.Less(edge, half);
                    Assert.Greater(edge, half - 60f);
                    Assert.Less(edge, half + 100f);
                }
            }
        }

        [Test]
        public void Plan_TeamsSpreadEvenlyByAngle()
        {
            for (var seed = 1; seed <= 40; seed++)
            {
                const int teams = 4;
                var plans = InsertionPlanner.Plan(teams, 512f, new SeededRandom(seed), InsertionMethod.Helicopter);
                var angles = new List<double>();
                foreach (var p in plans)
                    angles.Add(Math.Atan2(p.Start.X, p.Start.Z));
                angles.Sort();
                var minGap = double.MaxValue;
                for (var i = 0; i < angles.Count; i++)
                {
                    var next = i + 1 < angles.Count ? angles[i + 1] : angles[0] + Math.PI * 2.0;
                    minGap = Math.Min(minGap, next - angles[i]);
                }

                Assert.GreaterOrEqual(minGap, Math.PI * 2.0 / teams * 0.45, "seed " + seed);

                var minLz = double.MaxValue;
                for (var i = 0; i < plans.Length; i++)
                for (var j = i + 1; j < plans.Length; j++)
                    minLz = Math.Min(minLz, Float3.DistanceXZ(plans[i].LandingZone, plans[j].LandingZone));
                Assert.Greater(minLz, 150.0, "seed " + seed);
            }
        }

        [Test]
        public void Plan_OtherTeamsMixMethods()
        {
            var plans = InsertionPlanner.Plan(6, 512f, new SeededRandom(11), InsertionMethod.Helicopter);
            var heli = 0;
            var armored = 0;
            foreach (var p in plans)
            {
                if (p.Method == InsertionMethod.Helicopter)
                    heli++;
                else
                    armored++;
            }

            Assert.Greater(heli, 0);
            Assert.Greater(armored, 0);
        }

        [Test]
        public void Plan_IsDeterministicForSeed()
        {
            var a = InsertionPlanner.Plan(4, 512f, new SeededRandom(99), InsertionMethod.Helicopter);
            var b = InsertionPlanner.Plan(4, 512f, new SeededRandom(99), InsertionMethod.Helicopter);
            for (var i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].Start, b[i].Start);
                Assert.AreEqual(a[i].LandingZone, b[i].LandingZone);
                Assert.AreEqual(a[i].Method, b[i].Method);
            }
        }

        [Test]
        public void Plan_SmallMapAndNullRandomStayInside()
        {
            var plans = InsertionPlanner.Plan(3, 120f, null, InsertionMethod.ArmoredVehicle);
            Assert.AreEqual(3, plans.Length);
            foreach (var p in plans)
            {
                Assert.Less(Math.Abs(p.LandingZone.X), 120f);
                Assert.Less(Math.Abs(p.LandingZone.Z), 120f);
            }
        }
    }

    public sealed class SimArtilleryTests
    {
        private SimRecordingEventBus _bus;
        private ArtilleryService _artillery;

        [SetUp]
        public void SetUp()
        {
            _bus = new SimRecordingEventBus();
            _artillery = new ArtilleryService(_bus, new SeededRandom(5), 120f);
        }

        [Test]
        public void TryCall_StartsCooldownAndPublishesStart()
        {
            var target = new Float3(10f, 5f, -20f);
            Assert.IsTrue(_artillery.TryCall(1, new PlayerId(7), target));
            Assert.AreEqual(120f, _artillery.GetCooldownRemaining(1), 0.001f);
            Assert.AreEqual(0f, _artillery.GetCooldownRemaining(2), 0.001f);

            var events = _bus.OfType<ArtilleryStrikeEvent>();
            Assert.AreEqual(1, events.Count);
            Assert.IsFalse(events[0].IsImpact);
            Assert.AreEqual(1, events[0].Team);
            Assert.AreEqual(new PlayerId(7), events[0].CallerId);
            Assert.AreEqual(target, events[0].Target);
            Assert.AreEqual(ArtilleryService.ShellsPerStrike, _artillery.PendingShellCount);
        }

        [Test]
        public void TryCall_RejectedDuringCooldown_OtherTeamAllowed()
        {
            Assert.IsTrue(_artillery.TryCall(1, new PlayerId(1), Float3.Zero));
            Assert.IsFalse(_artillery.TryCall(1, new PlayerId(2), Float3.Zero));
            Assert.IsTrue(_artillery.TryCall(2, new PlayerId(3), Float3.Zero));
        }

        [Test]
        public void Cooldown_ExpiresWithTicks()
        {
            _artillery.TryCall(0, new PlayerId(1), Float3.Zero);
            for (var i = 0; i < 100; i++)
                _artillery.Tick(1f);
            Assert.AreEqual(20f, _artillery.GetCooldownRemaining(0), 0.01f);
            Assert.IsFalse(_artillery.IsReady(0));
            for (var i = 0; i < 21; i++)
                _artillery.Tick(1f);
            Assert.IsTrue(_artillery.IsReady(0));
            Assert.IsTrue(_artillery.TryCall(0, new PlayerId(1), Float3.Zero));
        }

        [Test]
        public void Impacts_ArriveAfterDelayWithinSpread()
        {
            var target = new Float3(100f, 12f, 50f);
            _artillery.TryCall(3, new PlayerId(9), target);

            var t = 0f;
            const float dt = 1f / 30f;
            while (t < ArtilleryService.DelaySeconds - 0.1f)
            {
                _artillery.Tick(dt);
                t += dt;
            }

            Assert.AreEqual(0, _artillery.DueImpacts.Count, "gecikmeden önce düşmemeli");

            var impacts = new List<ArtilleryImpact>();
            for (var i = 0; i < 30 * 15; i++)
            {
                _artillery.Tick(dt);
                impacts.AddRange(_artillery.DueImpacts);
                _artillery.DueImpacts.Clear();
            }

            Assert.AreEqual(ArtilleryService.ShellsPerStrike, impacts.Count);
            Assert.AreEqual(0, _artillery.PendingShellCount);
            foreach (var impact in impacts)
            {
                Assert.AreEqual(3, impact.Team);
                Assert.AreEqual(new PlayerId(9), impact.CallerId);
                Assert.LessOrEqual(Float3.DistanceXZ(impact.Position, target), ArtilleryService.SpreadRadius + 0.001f);
                Assert.AreEqual(target.Y, impact.Position.Y, 0.001f);
            }

            var impactEvents = 0;
            foreach (var e in _bus.OfType<ArtilleryStrikeEvent>())
            {
                if (e.IsImpact)
                    impactEvents++;
            }

            Assert.AreEqual(ArtilleryService.ShellsPerStrike, impactEvents);
        }

        [Test]
        public void Impacts_AreSpreadOut()
        {
            _artillery.TryCall(0, new PlayerId(1), Float3.Zero);
            for (var i = 0; i < 30; i++)
                _artillery.Tick(1f);
            Assert.AreEqual(ArtilleryService.ShellsPerStrike, _artillery.DueImpacts.Count);
            var distinct = new HashSet<Float3>();
            foreach (var impact in _artillery.DueImpacts)
                distinct.Add(impact.Position);
            Assert.Greater(distinct.Count, ArtilleryService.ShellsPerStrike / 2);
        }

        [Test]
        public void ZeroCooldown_AllowsRepeatedCalls_AndQueueIsCapped()
        {
            var artillery = new ArtilleryService(null, null, 0f);
            for (var i = 0; i < 60; i++)
                Assert.IsTrue(artillery.TryCall(0, new PlayerId(1), Float3.Zero));
            artillery.Tick(100f);
            Assert.LessOrEqual(artillery.DueImpacts.Count, ArtilleryService.MaxQueuedImpacts);
            artillery.Reset();
            Assert.AreEqual(0, artillery.DueImpacts.Count);
            Assert.AreEqual(0, artillery.PendingShellCount);
            Assert.AreEqual(0, artillery.ActiveStrikeCount);
        }

        [Test]
        public void ActiveStrike_TrackedUntilAfterLastImpact()
        {
            var target = new Float3(40f, 0f, -60f);
            Assert.IsFalse(_artillery.TryGetActiveStrike(2, out _));
            Assert.IsTrue(_artillery.TryCall(2, new PlayerId(4), target));
            Assert.AreEqual(1, _artillery.ActiveStrikeCount);
            Assert.IsTrue(_artillery.TryGetActiveStrike(2, out var active));
            Assert.AreEqual(target, active);
            Assert.IsFalse(_artillery.TryGetActiveStrike(1, out _));
            Assert.AreEqual(ArtilleryService.DelaySeconds, _artillery.GetSecondsUntilImpact(2), 0.001f);

            const float dt = 1f / 30f;
            var landedAll = false;
            for (var i = 0; i < 30 * 20; i++)
            {
                _artillery.Tick(dt);
                _artillery.DueImpacts.Clear();
                if (_artillery.PendingShellCount == 0 && !landedAll)
                {
                    landedAll = true;
                    Assert.AreEqual(0f, _artillery.GetSecondsUntilImpact(2), 0.001f);
                    Assert.IsTrue(_artillery.TryGetActiveStrike(2, out _), "son mermiden hemen sonra hâlâ etkin");
                }
            }

            Assert.IsTrue(landedAll);
            Assert.AreEqual(0, _artillery.ActiveStrikeCount);
            Assert.IsFalse(_artillery.TryGetActiveStrike(2, out _));
        }

        [Test]
        public void DangerZone_CoversSpreadPlusBlast_AndCanExcludeOwnTeam()
        {
            var target = new Float3(100f, 0f, 100f);
            Assert.IsFalse(_artillery.IsInDangerZone(target));
            _artillery.TryCall(1, new PlayerId(3), target);

            Assert.IsTrue(_artillery.IsInDangerZone(target));
            var edge = ArtilleryService.SpreadRadius + ArtilleryService.ShellRadius;
            Assert.IsTrue(_artillery.IsInDangerZone(new Float3(100f + edge - 0.5f, 30f, 100f)));
            Assert.IsFalse(_artillery.IsInDangerZone(new Float3(100f + edge + 0.5f, 0f, 100f)));
            Assert.IsTrue(_artillery.IsInDangerZone(new Float3(100f + edge + 0.5f, 0f, 100f), 5f));
            Assert.IsFalse(_artillery.IsInDangerZone(target, 0f, 1), "kendi timinin atışı hariç tutulabilmeli");
            Assert.IsTrue(_artillery.IsInDangerZone(target, 0f, 2));
        }

        [Test]
        public void TryCall_RejectsNonFiniteTarget_WithoutCooldown()
        {
            Assert.IsFalse(_artillery.TryCall(0, new PlayerId(1), new Float3(float.NaN, 0f, 0f)));
            Assert.IsFalse(_artillery.TryCall(0, new PlayerId(1), new Float3(0f, 0f, float.PositiveInfinity)));
            Assert.IsTrue(_artillery.IsReady(0));
            Assert.AreEqual(0, _artillery.PendingShellCount);
            Assert.AreEqual(0, _bus.OfType<ArtilleryStrikeEvent>().Count);
        }

        [Test]
        public void OverlappingStrikes_LargeTick_KeepsLandingOrder()
        {
            var bus = new SimRecordingEventBus();
            var artillery = new ArtilleryService(bus, new SeededRandom(11), 0f);
            artillery.TryCall(0, new PlayerId(1), new Float3(0f, 0f, 0f));      // ilk mermi t=6
            artillery.Tick(1f);
            artillery.TryCall(1, new PlayerId(2), new Float3(200f, 0f, 0f));    // ilk mermi t=7

            // Tek büyük adım: tüm mermiler aynı tick'te düşer; DueImpacts düşüş sırasında olmalı.
            artillery.Tick(30f);
            Assert.AreEqual(ArtilleryService.ShellsPerStrike * 2, artillery.DueImpacts.Count);
            Assert.AreEqual(0, artillery.DueImpacts[0].Team, "ilk düşen, önce çağrılan atışın ilk mermisi");
            Assert.AreEqual(0, artillery.DueImpacts[1].Team, "ikinci mermi (t<6.75) de ilk atıştan");

            var impactTeams = new List<int>();
            foreach (var e in bus.OfType<ArtilleryStrikeEvent>())
            {
                if (e.IsImpact)
                    impactTeams.Add(e.Team);
            }

            Assert.AreEqual(ArtilleryService.ShellsPerStrike * 2, impactTeams.Count);
            for (var i = 0; i < impactTeams.Count; i++)
                Assert.AreEqual(artillery.DueImpacts[i].Team, impactTeams[i], "olay sırası DueImpacts ile aynı olmalı");
        }
    }

    public sealed class SimSquadOrderTests
    {
        [Test]
        public void Issue_StoresOrderAndPublishes()
        {
            var bus = new SimRecordingEventBus();
            var orders = new SquadOrderService(bus);
            Assert.IsFalse(orders.TryGetOrder(0, out _, out _));
            Assert.AreEqual(0, orders.GetRevision(0));

            var target = new Float3(1f, 2f, 3f);
            orders.Issue(0, SquadOrder.Attack, target);
            Assert.IsTrue(orders.TryGetOrder(0, out var order, out var stored));
            Assert.AreEqual(SquadOrder.Attack, order);
            Assert.AreEqual(target, stored);
            Assert.AreEqual(1, orders.GetRevision(0));
            Assert.IsFalse(orders.HasOrder(1));

            var events = bus.OfType<SquadOrderIssuedEvent>();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(SquadOrder.Attack, events[0].Order);
            Assert.AreEqual(0, events[0].Team);
            Assert.AreEqual(target, events[0].Target);
        }

        [Test]
        public void Issue_OverridesPreviousOrder_PerTeam()
        {
            var orders = new SquadOrderService(null);
            orders.Issue(2, SquadOrder.HoldPosition, Float3.Zero);
            orders.Issue(2, SquadOrder.Regroup, new Float3(5f, 0f, 5f));
            orders.Issue(3, SquadOrder.Follow, Float3.Zero);
            Assert.IsTrue(orders.TryGetOrder(2, out var order, out _));
            Assert.AreEqual(SquadOrder.Regroup, order);
            Assert.AreEqual(2, orders.GetRevision(2));
            Assert.IsTrue(orders.TryGetOrder(3, out order, out _));
            Assert.AreEqual(SquadOrder.Follow, order);
        }

        [Test]
        public void ClearOrder_RemovesOrder()
        {
            var orders = new SquadOrderService(null);
            orders.Issue(0, SquadOrder.HoldPosition, Float3.Zero);
            orders.ClearOrder(0);
            Assert.IsFalse(orders.TryGetOrder(0, out _, out _));
            Assert.AreEqual(2, orders.GetRevision(0));
            orders.Issue(0, SquadOrder.Follow, Float3.Zero);
            orders.ClearAll();
            Assert.IsFalse(orders.HasOrder(0));
        }

        [Test]
        public void ClearAll_KeepsRevisionsMonotonic_AndPublishesNothing()
        {
            var bus = new SimRecordingEventBus();
            var orders = new SquadOrderService(bus);
            orders.Issue(0, SquadOrder.Attack, Float3.Zero);
            orders.Issue(1, SquadOrder.HoldPosition, Float3.Zero);
            var before0 = orders.GetRevision(0);
            var before1 = orders.GetRevision(1);

            orders.ClearAll();
            Assert.IsFalse(orders.HasOrder(0));
            Assert.IsFalse(orders.HasOrder(1));
            Assert.Greater(orders.GetRevision(0), before0);
            Assert.Greater(orders.GetRevision(1), before1);
            Assert.AreEqual(2, bus.OfType<SquadOrderIssuedEvent>().Count);

            var cleared0 = orders.GetRevision(0);
            orders.ClearAll();
            Assert.AreEqual(cleared0, orders.GetRevision(0), "zaten temiz tim için sayaç değişmemeli");

            orders.Issue(0, SquadOrder.Follow, Float3.Zero);
            Assert.Greater(orders.GetRevision(0), cleared0, "yeni emir eski sayaçla eşleşmemeli");
            Assert.IsTrue(orders.TryGetOrder(0, out var order, out _));
            Assert.AreEqual(SquadOrder.Follow, order);
        }
    }

    public sealed class SimSettingsTests
    {
        [Test]
        public void Defaults_AreValidWithoutLoad()
        {
            var service = new SettingsService(null);
            Assert.IsNotNull(service.Current);
            Assert.AreEqual(64f, service.Current.FieldOfView, 0.001f);
            Assert.AreEqual(4, service.Current.TeamCount);
            Assert.AreEqual(39, service.Current.BotCount);
        }

        [Test]
        public void Apply_ClampsValuesAndRaisesChanged()
        {
            var store = new SimMemoryStore();
            var service = new SettingsService(store);
            GameSettings received = null;
            service.Changed += s => received = s;

            var input = new GameSettings
            {
                FieldOfView = 200f,
                MouseSensitivity = -3f,
                AdsSensitivityMultiplier = float.NaN,
                MasterVolume = 4f,
                AmbientVolume = -1f,
                QualityLevel = 9,
                TeamCount = 40,
                Difficulty = (BotDifficulty)17,
                Insertion = (InsertionMethod)(-4),
                PlayerName = "   Çok Uzun Bir Komutan Adı Burada   "
            };
            service.Apply(input);

            var s = service.Current;
            Assert.AreSame(s, received);
            Assert.AreEqual(SettingsService.MaxFieldOfView, s.FieldOfView, 0.001f);
            Assert.AreEqual(SettingsService.MinMouseSensitivity, s.MouseSensitivity, 0.0001f);
            Assert.AreEqual(new GameSettings().AdsSensitivityMultiplier, s.AdsSensitivityMultiplier, 0.0001f);
            Assert.AreEqual(1f, s.MasterVolume, 0.0001f);
            Assert.AreEqual(0f, s.AmbientVolume, 0.0001f);
            Assert.AreEqual(3, s.QualityLevel);
            Assert.AreEqual(SettingsService.MaxTeamCount, s.TeamCount);
            Assert.AreEqual(SettingsService.MaxTeamCount * 10 - 1, s.BotCount);
            Assert.AreEqual(BotDifficulty.Hard, s.Difficulty);
            Assert.AreEqual(InsertionMethod.Helicopter, s.Insertion);
            Assert.LessOrEqual(s.PlayerName.Length, SettingsService.MaxPlayerNameLength);
            Assert.IsTrue(s.PlayerName.StartsWith("Çok"));
            Assert.AreEqual(200f, input.FieldOfView, 0.001f, "girdi değiştirilmemeli");
            Assert.Greater(store.SaveCount, 0);
        }

        [Test]
        public void ApplyThenLoad_RoundTripsThroughStore()
        {
            var store = new SimMemoryStore();
            var writer = new SettingsService(store);
            writer.Apply(new GameSettings
            {
                FieldOfView = 95f,
                MouseSensitivity = 0.3f,
                InvertY = true,
                ShowFps = true,
                Fullscreen = false,
                QualityLevel = 1,
                TeamCount = 5,
                Difficulty = BotDifficulty.Easy,
                Insertion = InsertionMethod.ArmoredVehicle,
                PlayerName = "Şahin Öztürk"
            });

            var reader = new SettingsService(store);
            var changed = 0;
            reader.Changed += _ => changed++;
            reader.Load();
            var s = reader.Current;
            Assert.AreEqual(1, changed);
            Assert.AreEqual(95f, s.FieldOfView, 0.001f);
            Assert.AreEqual(0.3f, s.MouseSensitivity, 0.0001f);
            Assert.IsTrue(s.InvertY);
            Assert.IsTrue(s.ShowFps);
            Assert.IsFalse(s.Fullscreen);
            Assert.AreEqual(1, s.QualityLevel);
            Assert.AreEqual(5, s.TeamCount);
            Assert.AreEqual(49, s.BotCount);
            Assert.AreEqual(BotDifficulty.Easy, s.Difficulty);
            Assert.AreEqual(InsertionMethod.ArmoredVehicle, s.Insertion);
            Assert.AreEqual("Şahin Öztürk", s.PlayerName);
        }

        [Test]
        public void Load_EmptyOrCorruptStore_UsesSafeValues()
        {
            var store = new SimMemoryStore();
            var service = new SettingsService(store);
            service.Load();
            Assert.AreEqual(new GameSettings().FieldOfView, service.Current.FieldOfView, 0.001f);
            Assert.AreEqual(SettingsService.DefaultPlayerName, service.Current.PlayerName);

            store.SetFloat(SettingsService.Keys.FieldOfView, float.NaN);
            store.SetInt(SettingsService.Keys.TeamCount, -5);
            store.SetInt(SettingsService.Keys.PlayerNameLength, 999);
            service.Load();
            Assert.AreEqual(new GameSettings().FieldOfView, service.Current.FieldOfView, 0.001f);
            Assert.AreEqual(SettingsService.MinTeamCount, service.Current.TeamCount);
            Assert.AreEqual(SettingsService.DefaultPlayerName, service.Current.PlayerName);
        }

        [Test]
        public void Apply_NullIsIgnored_AndNullStoreWorks()
        {
            var service = new SettingsService(null);
            var before = service.Current;
            service.Apply(null);
            Assert.AreSame(before, service.Current);
            Assert.DoesNotThrow(() => service.Load());
            Assert.DoesNotThrow(() => service.Apply(new GameSettings { FieldOfView = 70f }));
            Assert.AreEqual(70f, service.Current.FieldOfView, 0.001f);
        }

        [Test]
        public void Modify_AppliesClampedCopy_AndPersists()
        {
            var store = new SimMemoryStore();
            var service = new SettingsService(store);
            var before = service.Current;
            var changes = 0;
            service.Changed += _ => changes++;

            service.Modify(s =>
            {
                s.FieldOfView = 500f;
                s.TeamCount = 3;
            });

            Assert.AreEqual(64f, before.FieldOfView, 0.001f, "önceki örnek değişmemeli");
            Assert.AreEqual(SettingsService.MaxFieldOfView, service.Current.FieldOfView, 0.001f);
            Assert.AreEqual(3, service.Current.TeamCount);
            Assert.AreEqual(29, service.Current.BotCount);
            Assert.AreEqual(1, changes);
            Assert.Greater(store.SaveCount, 0);

            service.Modify(null);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void CreateMatchConfig_UsesTeamCountDifficultyAndInsertion()
        {
            var service = new SettingsService(null);
            service.Modify(s =>
            {
                s.TeamCount = 5;
                s.Difficulty = BotDifficulty.Hard;
                s.Insertion = InsertionMethod.ArmoredVehicle;
            });

            var config = service.CreateMatchConfig();
            Assert.AreEqual(5, config.TeamCount);
            Assert.AreEqual(10, config.TeamSize);
            Assert.AreEqual(50, config.MaxPlayers);
            Assert.AreEqual(49, config.BotCount);
            Assert.AreEqual(BotDifficulty.Hard, config.Difficulty);
            Assert.AreEqual(InsertionMethod.ArmoredVehicle, config.PlayerInsertion);
        }

        [TestCase("", "Komutan")]
        [TestCase("   ", "Komutan")]
        [TestCase("  Kartal  ", "Kartal")]
        public void SanitizeName_Cases(string input, string expected)
        {
            Assert.AreEqual(expected, SettingsService.SanitizeName(input));
        }
    }

    public sealed class SimCareerTests
    {
        private static MatchResult Result(bool win, int kills, int headshots, int teamPlacement, int teamCount, float damage = 100f, float survival = 300f) =>
            new(win, teamPlacement * 10, teamCount * 10, kills, headshots, damage, survival, 0.3f, null,
                teamPlacement, teamCount, "Mavi Tim", kills);

        [Test]
        public void ComputeExperience_FollowsFormula()
        {
            Assert.AreEqual(3 * 100 + 1 * 25 + (4 - 2) * 150, CareerStatsService.ComputeExperience(Result(false, 3, 1, 2, 4)));
            Assert.AreEqual(5 * 100 + 2 * 25 + (4 - 1) * 150 + 1000, CareerStatsService.ComputeExperience(Result(true, 5, 2, 1, 4)));
            Assert.AreEqual(0, CareerStatsService.ComputeExperience(Result(false, 0, 0, 4, 4)));
            Assert.AreEqual(0, CareerStatsService.ComputeExperience(Result(false, -3, -1, 9, 4)));
        }

        [Test]
        public void Record_UpdatesStatsAndRank()
        {
            var store = new SimMemoryStore();
            var career = new CareerStatsService(store);
            career.Load();
            Assert.AreEqual(MilitaryRank.Er, career.Current.Rank);

            MilitaryRank? promotedTo = null;
            career.Promoted += (from, to) => promotedTo = to;

            career.Record(Result(false, 2, 1, 3, 4, 250f, 400f));
            Assert.AreEqual(1, career.Current.Matches);
            Assert.AreEqual(0, career.Current.Wins);
            Assert.AreEqual(2, career.Current.Kills);
            Assert.AreEqual(1, career.Current.Headshots);
            Assert.AreEqual(3, career.Current.BestPlacement);
            Assert.AreEqual(250f, career.Current.TotalDamage, 0.01f);
            Assert.AreEqual(400f, career.Current.LongestSurvivalSeconds, 0.01f);
            Assert.AreEqual(375, career.Current.Experience);
            Assert.AreEqual(MilitaryRank.Er, career.Current.Rank);
            Assert.IsNull(promotedTo);

            career.Record(Result(true, 4, 0, 1, 4, 100f, 200f));
            Assert.AreEqual(2, career.Current.Matches);
            Assert.AreEqual(1, career.Current.Wins);
            Assert.AreEqual(1, career.Current.BestPlacement);
            Assert.AreEqual(400f, career.Current.LongestSurvivalSeconds, 0.01f);
            Assert.AreEqual(375 + 400 + 450 + 1000, career.Current.Experience);
            Assert.AreEqual(Project.Application.Catalogs.RankCatalog.RankForExperience(2225), career.Current.Rank);
            Assert.IsTrue(career.PromotedLastMatch);
            Assert.AreEqual(2225 - 375, career.LastExperienceGained);
            Assert.AreEqual(career.Current.Rank, promotedTo);
        }

        [Test]
        public void Record_PersistsAndLoadRestores()
        {
            var store = new SimMemoryStore();
            var career = new CareerStatsService(store);
            career.Load();
            for (var i = 0; i < 10; i++)
                career.Record(Result(i % 3 == 0, 6, 2, i % 3 == 0 ? 1 : 2, 4));

            var reloaded = new CareerStatsService(store);
            reloaded.Load();
            Assert.AreEqual(career.Current.Matches, reloaded.Current.Matches);
            Assert.AreEqual(career.Current.Wins, reloaded.Current.Wins);
            Assert.AreEqual(career.Current.Kills, reloaded.Current.Kills);
            Assert.AreEqual(career.Current.Experience, reloaded.Current.Experience);
            Assert.AreEqual(career.Current.Rank, reloaded.Current.Rank);
            Assert.AreEqual(1, reloaded.Current.BestPlacement);
            Assert.Greater((int)reloaded.Current.Rank, (int)MilitaryRank.Er);
        }

        [Test]
        public void NullStore_WorksInMemory()
        {
            var career = new CareerStatsService(null);
            Assert.DoesNotThrow(() => career.Load());
            Assert.DoesNotThrow(() => career.Record(Result(true, 1, 0, 1, 2)));
            Assert.AreEqual(1, career.Current.Matches);
            career.ResetCareer();
            Assert.AreEqual(0, career.Current.Matches);
        }
    }
}
