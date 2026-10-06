using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class TutorialServiceTests
    {
        private sealed class Bus : IEventBus
        {
            private readonly Dictionary<Type, Delegate> _h = new Dictionary<Type, Delegate>();
            public void Publish<T>(T e) where T : IGameEvent { if (_h.TryGetValue(typeof(T), out var d)) ((Action<T>)d)(e); }
            public void Subscribe<T>(Action<T> h) where T : IGameEvent { _h.TryGetValue(typeof(T), out var d); _h[typeof(T)] = (Action<T>)d + h; }
            public void Unsubscribe<T>(Action<T> h) where T : IGameEvent { if (_h.TryGetValue(typeof(T), out var d)) _h[typeof(T)] = (Action<T>)d - h; }
        }

        private static TutorialStepDef Step(string id, bool anyOf, float limit, params (string k, string q, int n)[] c)
        {
            var s = new TutorialStepDef { Id = id, AnyOf = anyOf, TimeLimitSeconds = limit, RewardXp = 10 };
            foreach (var x in c) s.Conditions.Add(new TutorialCondition { Key = x.k, Qualifier = x.q, Count = x.n });
            return s;
        }

        [Test]
        public void AllOf_RequiresEveryCondition()
        {
            var svc = new TutorialService(new[] { Step("a", false, 0, ("StanceChanged", "Crouch", 1), ("StanceChanged", "Stand", 1)) });
            svc.Start();
            svc.Signal("StanceChanged", "Crouch");
            Assert.IsFalse(svc.IsCompleted);
            svc.Signal("StanceChanged", "Stand");
            Assert.IsTrue(svc.IsCompleted);
            Assert.AreEqual(10, svc.TotalXp);
        }

        [Test]
        public void AnyOf_CompletesOnFirst()
        {
            var svc = new TutorialService(new[] { Step("a", true, 0, ("X", "", 1), ("Y", "", 1)), Step("b", true, 0, ("Z", "", 1)) });
            svc.Start();
            svc.Signal("Y");
            Assert.AreEqual(1, svc.CurrentIndex);
        }

        [Test]
        public void Count_IsHonoured_AndQualifierFilters()
        {
            var svc = new TutorialService(new[] { Step("a", false, 0, ("Hit", "", 3)) });
            svc.Start();
            svc.Signal("Hit"); svc.Signal("Other"); svc.Signal("Hit");
            Assert.IsFalse(svc.IsCompleted);
            svc.Signal("Hit");
            Assert.IsTrue(svc.IsCompleted);
        }

        [Test]
        public void Timeout_AdvancesWithoutReward()
        {
            var svc = new TutorialService(new[] { Step("a", false, 5, ("X", "", 1)), Step("b", false, 0, ("Y", "", 1)) });
            svc.Start();
            svc.Tick(5.1f);
            Assert.AreEqual(1, svc.CurrentIndex);
            Assert.AreEqual(0, svc.TotalXp);
            Assert.AreEqual(0, svc.CompletedSteps);
        }

        [Test]
        public void SkipStep_And_SkipAll()
        {
            var svc = new TutorialService(new[] { Step("a", false, 0, ("X", "", 1)), Step("b", false, 0, ("Y", "", 1)) });
            svc.Start();
            svc.SkipStep();
            Assert.AreEqual(1, svc.CurrentIndex);
            svc.SkipAll();
            Assert.IsTrue(svc.IsCompleted);
        }

        [Test]
        public void EventBus_DrivesSteps()
        {
            var bus = new Bus();
            var svc = new TutorialService(new[]
            {
                Step("fire", false, 0, ("WeaponFiredEvent", "", 1)),
                Step("order", false, 0, ("SquadOrderIssuedEvent", "HoldPosition", 1)),
                Step("heal", false, 0, ("ItemUsedEvent", "bandage:completed", 1)),
            }, bus);
            svc.Start();
            bus.Publish(new WeaponFiredEvent(new PlayerId(1), "ak", 10, Float3.Zero));
            Assert.AreEqual(1, svc.CurrentIndex);
            bus.Publish(new SquadOrderIssuedEvent(0, SquadOrder.Follow, Float3.Zero));
            Assert.AreEqual(1, svc.CurrentIndex);
            bus.Publish(new SquadOrderIssuedEvent(0, SquadOrder.HoldPosition, Float3.Zero));
            bus.Publish(new ItemUsedEvent(new PlayerId(1), "bandage", false, true));
            Assert.IsTrue(svc.IsCompleted);
            svc.Dispose();
        }

        [Test]
        public void EmptySteps_CompleteImmediately()
        {
            var svc = new TutorialService(new List<TutorialStepDef>());
            svc.Start();
            Assert.IsTrue(svc.IsCompleted);
        }
    }
}
