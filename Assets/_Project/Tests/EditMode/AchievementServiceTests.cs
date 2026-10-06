using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class AchievementServiceTests
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

        private static List<AchievementDefinition> Defs() => new List<AchievementDefinition>
        {
            new AchievementDefinition { id = "k1", metric = "kills", target = 1 },
            new AchievementDefinition { id = "k5", metric = "kills", target = 5 },
            new AchievementDefinition { id = "w1", metric = "wins", target = 1 },
            new AchievementDefinition { id = "surv", metric = "survival", target = 600 },
            new AchievementDefinition { id = "bad", metric = "", target = 3 },
        };

        private static MatchResult Result(bool win, int kills, float surv) =>
            new MatchResult(win, win ? 1 : 20, 100, kills, 0, 0f, surv, 0f, null);

        [Test]
        public void InvalidDefinitionsIgnored()
        {
            Assert.AreEqual(4, new AchievementService(null, Defs()).Definitions.Count);
        }

        [Test]
        public void MatchUnlocksAndRaisesEventOnce()
        {
            var s = new AchievementService(new MemStore(), Defs());
            var fired = new List<string>();
            s.AchievementUnlocked += d => fired.Add(d.id);
            s.RecordMatch(Result(true, 2, 700f), 0);
            Assert.AreEqual(3, fired.Count);
            Assert.IsTrue(fired.Contains("k1") && fired.Contains("w1") && fired.Contains("surv"));
            s.RecordMatch(Result(true, 1, 10f), 0);
            Assert.AreEqual(3, fired.Count);
            Assert.AreEqual(3, s.GetProgress("kills"));
            Assert.IsFalse(s.IsUnlocked("k5"));
        }

        [Test]
        public void ProgressAccumulatesAndPersists()
        {
            var store = new MemStore();
            var s = new AchievementService(store, Defs());
            s.RecordMatch(Result(false, 3, 100f), 0);
            s.RecordMatch(Result(false, 2, 100f), 0);
            Assert.IsTrue(s.IsUnlocked("k5"));

            var s2 = new AchievementService(store, Defs());
            s2.Load();
            Assert.IsTrue(s2.IsUnlocked("k5"));
            Assert.AreEqual(5, s2.GetProgress("kills"));
        }

        [Test]
        public void SetMaxOnlyRaises()
        {
            var s = new AchievementService(null, Defs());
            s.SetMax("survival", 300);
            s.SetMax("survival", 100);
            Assert.AreEqual(300, s.GetProgress("survival"));
        }

        [Test]
        public void AddProgressIgnoresNonPositive()
        {
            var s = new AchievementService(null, Defs());
            s.AddProgress("kills", 0);
            s.AddProgress("kills", -3);
            Assert.AreEqual(0, s.GetProgress("kills"));
        }
    }
}
