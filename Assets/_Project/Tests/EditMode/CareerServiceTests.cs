using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class CareerServiceTests
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

        private static MatchResult R(bool win, int place, int kills, int hs) =>
            new MatchResult(win, place, 100, kills, hs, 400f, 700f, 0.6f, null);

        [Test]
        public void ThirtyMedals() => Assert.AreEqual(30, CareerService.DefaultMedals().Count);

        [Test]
        public void LevelCurve()
        {
            Assert.AreEqual(1, CareerService.LevelForXp(0));
            Assert.AreEqual(2, CareerService.LevelForXp(200));
            Assert.AreEqual(1, CareerService.LevelForXp(199));
            Assert.AreEqual(CareerService.MaxLevel, CareerService.LevelForXp(int.MaxValue));
        }

        [Test]
        public void HistoryCapsAt20AndPersists()
        {
            var store = new MemStore();
            var s = new CareerService(store);
            for (var i = 0; i < 25; i++) s.RecordMatch(R(false, 50, 1, 0), null, null);
            Assert.AreEqual(20, s.History.Count);
            var s2 = new CareerService(store);
            Assert.AreEqual(20, s2.History.Count);
            Assert.AreEqual(s.Experience, s2.Experience);
            Assert.AreEqual(25, s2.GetMetric("matches"));
        }

        [Test]
        public void WeaponAndMapStats()
        {
            var store = new MemStore();
            var s = new CareerService(store, new[] { "ak" }, new[] { "Vadi" });
            var w = new Dictionary<string, WeaponMatchStats>
            { ["ak"] = new WeaponMatchStats { Shots = 10, Hits = 7, Kills = 2, Headshots = 1, LongestKill = 123.4f } };
            s.RecordMatch(R(true, 1, 2, 1), w, "Vadi");
            var s2 = new CareerService(store, new[] { "ak" }, new[] { "Vadi" });
            Assert.AreEqual(0.7f, s2.Weapons["ak"].Accuracy, 0.001f);
            Assert.AreEqual(123.4f, s2.Weapons["ak"].LongestKillMeters, 0.11f);
            Assert.AreEqual(1, s2.Maps["Vadi"].Wins);
            Assert.AreEqual(1, s2.Maps["Vadi"].BestPlacement);
        }

        [Test]
        public void MedalTiersFireAndGhost()
        {
            var s = new CareerService(new MemStore());
            var earned = new List<string>();
            s.MedalEarned += (d, t) => earned.Add(d.Id + t);
            s.RecordMatch(R(false, 5, 0, 0), null, null);
            Assert.IsTrue(earned.Contains("hayaletBronz"));
            s.AddProgress("heals", 10);
            Assert.IsTrue(earned.Contains("sihhiyeciBronz"));
            Assert.AreEqual(MedalTier.None, s.GetTier(s.Medals[0]));
        }

        [Test]
        public void SparklineMatchesHistory()
        {
            var s = new CareerService(null);
            s.RecordMatch(R(true, 1, 3, 1), null, null);
            s.RecordMatch(R(false, 100, 0, 0), null, null);
            Assert.AreEqual("█▁", s.PlacementSparkline());
        }
    }
}
