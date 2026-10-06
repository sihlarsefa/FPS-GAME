using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class ChallengeRulesTests
    {
        private sealed class MemStore : ISettingsStore
        {
            public readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
            public int Saves;
            public bool HasKey(string key) => Ints.ContainsKey(key);
            public float GetFloat(string key, float fallback) => fallback;
            public int GetInt(string key, int fallback) => Ints.TryGetValue(key, out var v) ? v : fallback;
            public void SetFloat(string key, float value) { }
            public void SetInt(string key, int value) => Ints[key] = value;
            public void Save() => Saves++;
        }

        [Test]
        public void QuickFire_HeadshotDoublesAndFarIsWorthMore()
        {
            Assert.AreEqual(ChallengeRules.QuickFireHitScore(50f, false) * 2, ChallengeRules.QuickFireHitScore(50f, true));
            Assert.Greater(ChallengeRules.QuickFireHitScore(100f, false), ChallengeRules.QuickFireHitScore(25f, false));
        }

        [Test]
        public void Sniper_FarHitsWorthMore_HeadshotBonus()
        {
            Assert.Greater(ChallengeRules.SniperHitScore(500f, false), ChallengeRules.SniperHitScore(200f, false));
            Assert.Greater(ChallengeRules.SniperHitScore(300f, true), ChallengeRules.SniperHitScore(300f, false));
        }

        [Test]
        public void KillHouse_FriendlyPenaltyAndClamp()
        {
            var clean = ChallengeRules.KillHouseScore(6, 0, 40f, true);
            var dirty = ChallengeRules.KillHouseScore(6, 2, 40f, true);
            Assert.AreEqual(clean - 2 * ChallengeRules.KillHouseFriendlyPenalty, dirty);
            Assert.AreEqual(0, ChallengeRules.KillHouseScore(0, 5, 100f, false));
        }

        [Test]
        public void KillHouse_FasterIsBetter_NoBonusWhenNotCleared()
        {
            Assert.Greater(ChallengeRules.KillHouseScore(6, 0, 30f, true), ChallengeRules.KillHouseScore(6, 0, 90f, true));
            Assert.AreEqual(300, ChallengeRules.KillHouseScore(3, 0, 10f, false));
        }

        [Test]
        public void Grenade_RingsAreMonotonic()
        {
            Assert.AreEqual(100, ChallengeRules.GrenadeScore(0f));
            Assert.AreEqual(100, ChallengeRules.GrenadeScore(3f));
            Assert.AreEqual(60, ChallengeRules.GrenadeScore(5f));
            Assert.AreEqual(30, ChallengeRules.GrenadeScore(9f));
            Assert.AreEqual(10, ChallengeRules.GrenadeScore(14f));
            Assert.AreEqual(0, ChallengeRules.GrenadeScore(40f));
        }

        [Test]
        public void Rank_ThresholdsDescend()
        {
            foreach (ChallengeKind kind in System.Enum.GetValues(typeof(ChallengeKind)))
            {
                var t = ChallengeRules.RankThresholds(kind);
                for (var i = 1; i < t.Length; i++)
                    Assert.Less(t[i], t[i - 1]);
                Assert.AreEqual("S", ChallengeRules.Rank(kind, t[0]));
                Assert.AreEqual("D", ChallengeRules.Rank(kind, 0));
            }
        }

        [Test]
        public void QuickFireSchedule_DeterministicAndInRange()
        {
            var a = ChallengeRules.BuildQuickFireSchedule(7);
            var b = ChallengeRules.BuildQuickFireSchedule(7);
            Assert.AreEqual(a.Count, b.Count);
            Assert.Greater(a.Count, 20);
            for (var i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Distance, b[i].Distance);
                Assert.GreaterOrEqual(a[i].Distance, 25f);
                Assert.LessOrEqual(a[i].Distance, 100f);
                Assert.GreaterOrEqual(a[i].Lateral, -12f);
                Assert.LessOrEqual(a[i].Lateral, 12f);
                Assert.Less(a[i].SpawnTime, ChallengeRules.QuickFireDuration);
                if (i > 0)
                    Assert.Greater(a[i].SpawnTime, a[i - 1].SpawnTime);
            }
        }

        [Test]
        public void SniperLayout_HasAllDistances()
        {
            var layout = ChallengeRules.BuildSniperLayout(3);
            Assert.AreEqual(ChallengeRules.SniperDistances.Length * ChallengeRules.SniperTargetsPerDistance, layout.Count);
            Assert.AreEqual(500f, layout[layout.Count - 1].Distance);
        }

        [Test]
        public void RecordResult_KeepsBestAndCountsRuns()
        {
            var store = new MemStore();
            Assert.IsTrue(ChallengeRules.RecordResult(store, ChallengeKind.Sniper, 300));
            Assert.IsFalse(ChallengeRules.RecordResult(store, ChallengeKind.Sniper, 200));
            Assert.IsTrue(ChallengeRules.RecordResult(store, ChallengeKind.Sniper, 450));
            Assert.AreEqual(450, ChallengeRules.GetBest(store, ChallengeKind.Sniper));
            Assert.AreEqual(3, ChallengeRules.GetRuns(store, ChallengeKind.Sniper));
            Assert.AreEqual(0, ChallengeRules.GetBest(store, ChallengeKind.KillHouse));
            Assert.AreEqual(3, store.Saves);
            Assert.IsFalse(ChallengeRules.RecordResult(null, ChallengeKind.Sniper, 5));
        }

        [Test]
        public void PadIdsAndKeysAreUnique()
        {
            var ids = new HashSet<string>();
            foreach (ChallengeKind kind in System.Enum.GetValues(typeof(ChallengeKind)))
            {
                Assert.IsTrue(ids.Add(ChallengeRules.PadId(kind)));
                Assert.IsTrue(ids.Add(ChallengeRules.BestKey(kind)));
                Assert.IsNotEmpty(ChallengeRules.Name(kind));
            }
        }
    }
}
