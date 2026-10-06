using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class DailyMissionsTests
    {
        private sealed class MemStore : ISettingsStore
        {
            public readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
            public bool HasKey(string key) => Ints.ContainsKey(key);
            public float GetFloat(string key, float fallback) => fallback;
            public int GetInt(string key, int fallback) => Ints.TryGetValue(key, out var v) ? v : fallback;
            public void SetFloat(string key, float value) { }
            public void SetInt(string key, int value) => Ints[key] = value;
            public void Save() { }
        }

        [Test]
        public void PoolHasTwelveTemplates() => Assert.AreEqual(12, DailyMissions.DefaultPool.Count);

        [Test]
        public void PickIsDeterministicAndDistinct()
        {
            var a = DailyMissions.PickIndices(20261006, 12);
            var b = DailyMissions.PickIndices(20261006, 12);
            Assert.AreEqual(3, a.Length);
            CollectionAssert.AreEqual(a, b);
            Assert.AreEqual(3, new HashSet<int>(a).Count);
        }

        [Test]
        public void DifferentDaysVary()
        {
            var distinct = new HashSet<string>();
            for (var d = 1; d <= 28; d++)
                distinct.Add(string.Join(",", DailyMissions.PickIndices(20261000 + d, 12)));
            Assert.Greater(distinct.Count, 10);
        }

        [Test]
        public void DayNumberFormat() => Assert.AreEqual(20261006, DailyMissions.DayNumber(new DateTime(2026, 10, 6)));

        [Test]
        public void TodayHasThreeMissions()
        {
            var dm = new DailyMissions(new MemStore(), () => 20261006);
            Assert.AreEqual(3, dm.Today.Count);
        }

        [Test]
        public void RecordAdvancesAndCompletes()
        {
            var dm = new DailyMissions(new MemStore(), () => 20261006);
            var m = dm.Today[0];
            DailyMission done = null;
            dm.Completed += x => done = x;
            dm.Record(m.Template.Metric, m.Target);
            Assert.IsTrue(m.Done);
            Assert.AreSame(m, done);
            Assert.AreEqual(m.Tp, dm.TotalTp);
            Assert.AreEqual(m.Keys, dm.TotalKeys);
        }

        [Test]
        public void RewardGrantedOnlyOnce()
        {
            var dm = new DailyMissions(new MemStore(), () => 20261006);
            var m = dm.Today[0];
            dm.Record(m.Template.Metric, m.Target);
            dm.Record(m.Template.Metric, m.Target);
            Assert.AreEqual(m.Tp, dm.TotalTp);
        }

        [Test]
        public void ProgressPersistsSameDay()
        {
            var store = new MemStore();
            var a = new DailyMissions(store, () => 20261006);
            var m = a.Today[0];
            a.Record(m.Template.Metric, 1);
            var b = new DailyMissions(store, () => 20261006);
            Assert.AreEqual(Math.Min(1, m.Target), b.Today[0].Progress);
        }

        [Test]
        public void NewDayResetsProgressKeepsTotals()
        {
            var store = new MemStore();
            var day = 20261006;
            var dm = new DailyMissions(store, () => day);
            var m = dm.Today[0];
            dm.Record(m.Template.Metric, m.Target);
            var tp = dm.TotalTp;
            day = 20261007;
            Assert.IsFalse(dm.Today[0].Done);
            Assert.AreEqual(0, dm.Today[0].Progress);
            Assert.AreEqual(tp, dm.TotalTp);
        }

        [Test]
        public void ClosestToCompletePicksHighestRatioIncomplete()
        {
            var dm = new DailyMissions(new MemStore(), () => 20261006);
            var t = dm.Today;
            dm.Record(t[1].Template.Metric, Math.Max(1, t[1].Target - 1));
            var best = dm.ClosestToComplete();
            Assert.IsNotNull(best);
            Assert.IsFalse(best.Done);
            foreach (var m in t) if (!m.Done) Assert.GreaterOrEqual(best.Ratio, m.Ratio);
        }
    }
}
