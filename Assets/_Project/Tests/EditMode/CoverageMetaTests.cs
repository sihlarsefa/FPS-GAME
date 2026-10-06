using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Localization;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class CoverageAchievementTests
    {
        private sealed class MemStore : ISettingsStore
        {
            public readonly Dictionary<string, int> Ints = new();
            public int Saves;
            public bool HasKey(string key) => Ints.ContainsKey(key);
            public float GetFloat(string key, float fallback) => fallback;
            public int GetInt(string key, int fallback) => Ints.TryGetValue(key, out var v) ? v : fallback;
            public void SetFloat(string key, float value) { }
            public void SetInt(string key, int value) => Ints[key] = value;
            public void Save() => Saves++;
        }

        private static AchievementDefinition D(string id, string metric, int target) =>
            new AchievementDefinition { id = id, metric = metric, target = target };

        private static MatchResult R(bool win, int place, int kills, int hs, float dmg, float surv) =>
            new MatchResult(win, place, 40, kills, hs, dmg, surv, 0f, null);

        private static AchievementService Make(MemStore store, params AchievementDefinition[] defs) =>
            new AchievementService(store, defs);

        [Test]
        public void DuplicateIds_KeepFirstOnly()
        {
            var s = Make(null, D("a", "kills", 1), D("a", "wins", 1));
            Assert.AreEqual(1, s.Definitions.Count);
            Assert.AreEqual("kills", s.Definitions[0].metric);
        }

        [Test]
        public void ZeroOrNegativeTarget_Ignored()
        {
            var s = Make(null, D("a", "kills", 0), D("b", "kills", -1), null);
            Assert.AreEqual(0, s.Definitions.Count);
        }

        [Test]
        public void NullDefinitionList_IsSafe()
        {
            var s = new AchievementService(null, null);
            s.AddProgress("kills", 5);
            Assert.AreEqual(0, s.Definitions.Count);
        }

        [Test]
        public void AddProgress_IgnoresNonPositiveAndEmptyMetric()
        {
            var s = Make(null, D("a", "kills", 5));
            s.AddProgress("kills", 0);
            s.AddProgress("kills", -3);
            s.AddProgress("", 3);
            s.AddProgress(null, 3);
            Assert.AreEqual(0, s.GetProgress("kills"));
        }

        [Test]
        public void AddProgress_Overflow_SaturatesAtIntMax()
        {
            var s = Make(null, D("a", "x", 5));
            s.AddProgress("x", int.MaxValue);
            s.AddProgress("x", 10);
            Assert.AreEqual(int.MaxValue, s.GetProgress("x"));
        }

        [Test]
        public void SetMax_OnlyRaises()
        {
            var s = Make(null, D("a", "rank", 10));
            s.SetMax("rank", 4);
            s.SetMax("rank", 2);
            Assert.AreEqual(4, s.GetProgress("rank"));
            s.SetMax("rank", 10);
            Assert.IsTrue(s.IsUnlocked("a"));
        }

        [Test]
        public void DisplayedProgress_ClampedToTarget_AndFullWhenUnlocked()
        {
            var def = D("a", "kills", 5);
            var s = Make(null, def);
            s.AddProgress("kills", 3);
            Assert.AreEqual(3, s.GetProgress(def));
            s.AddProgress("kills", 50);
            Assert.AreEqual(5, s.GetProgress(def));
            Assert.AreEqual(0, s.GetProgress((AchievementDefinition)null));
        }

        [Test]
        public void Persistence_RoundTripsThroughStore()
        {
            var store = new MemStore();
            var s1 = Make(store, D("k", "kills", 3));
            s1.AddProgress("kills", 2);
            var s2 = Make(store, D("k", "kills", 3));
            s2.Load();
            Assert.AreEqual(2, s2.GetProgress("kills"));
            s2.AddProgress("kills", 1);
            var s3 = Make(store, D("k", "kills", 3));
            s3.Load();
            Assert.IsTrue(s3.IsUnlocked("k"));
            Assert.AreEqual(1, s3.UnlockedCount);
        }

        [Test]
        public void Persist_SavesStore()
        {
            var store = new MemStore();
            var s = Make(store, D("k", "kills", 3));
            s.AddProgress("kills", 1);
            Assert.Greater(store.Saves, 0);
        }

        [Test]
        public void Load_CorruptNegativeProgress_ClampsToZero()
        {
            var store = new MemStore();
            store.Ints[AchievementService.ProgressKeyPrefix + "kills"] = -50;
            var s = Make(store, D("k", "kills", 3));
            s.Load();
            Assert.AreEqual(0, s.GetProgress("kills"));
        }

        [Test]
        public void Unlock_FiresOnce_AndThrowingSubscriberIsContained()
        {
            var s = Make(null, D("k", "kills", 1));
            var calls = 0;
            s.AchievementUnlocked += _ => { calls++; throw new System.Exception("abone"); };
            s.AddProgress("kills", 1);
            s.AddProgress("kills", 1);
            Assert.AreEqual(1, calls);
            Assert.IsTrue(s.IsUnlocked("k"));
        }

        [Test]
        public void Changed_FiresOnProgress()
        {
            var s = Make(null, D("k", "kills", 10));
            var n = 0;
            s.Changed += () => n++;
            s.AddProgress("kills", 1);
            Assert.AreEqual(1, n);
        }

        [Test]
        public void SharedMetric_UnlocksMultipleTiers()
        {
            var s = Make(null, D("t1", "kills", 1), D("t2", "kills", 3), D("t3", "kills", 10));
            s.AddProgress("kills", 3);
            Assert.AreEqual(2, s.UnlockedCount);
        }

        [Test]
        public void RecordMatch_UpdatesAllCounters()
        {
            var s = Make(null, D("m", "matches", 99), D("w", "wins", 99), D("k", "kills", 99), D("h", "headshots", 99),
                D("d", "damage", 9999), D("t", "top3", 99), D("s", "survival", 9999), D("r", "rank", 99));
            s.RecordMatch(R(true, 1, 4, 2, 350.9f, 612.4f), 7);
            Assert.AreEqual(1, s.GetProgress("matches"));
            Assert.AreEqual(1, s.GetProgress("wins"));
            Assert.AreEqual(4, s.GetProgress("kills"));
            Assert.AreEqual(2, s.GetProgress("headshots"));
            Assert.AreEqual(350, s.GetProgress("damage"));
            Assert.AreEqual(1, s.GetProgress("top3"));
            Assert.AreEqual(612, s.GetProgress("survival"));
            Assert.AreEqual(7, s.GetProgress("rank"));
        }

        [Test]
        public void RecordMatch_Loss_NoWinNoTop3_WhenPlacedLow()
        {
            var s = Make(null, D("w", "wins", 9), D("t", "top3", 9), D("m", "matches", 9));
            s.RecordMatch(R(false, 25, 0, 0, 0f, 30f), -1);
            Assert.AreEqual(0, s.GetProgress("wins"));
            Assert.AreEqual(0, s.GetProgress("top3"));
            Assert.AreEqual(1, s.GetProgress("matches"));
        }

        [Test]
        public void RecordMatch_Top3Loser_CountsTop3()
        {
            var s = Make(null, D("t", "top3", 9));
            s.RecordMatch(new MatchResult(false, 5, 40, 0, 0, 0f, 10f, 0f, null, 3, 4, "X", 0), -1);
            Assert.AreEqual(1, s.GetProgress("top3"));
        }

        [Test]
        public void RecordMatch_SurvivalAndRank_KeepBest()
        {
            var s = Make(null, D("s", "survival", 99999), D("r", "rank", 99));
            s.RecordMatch(R(false, 10, 0, 0, 0f, 500f), 5);
            s.RecordMatch(R(false, 10, 0, 0, 0f, 100f), 2);
            Assert.AreEqual(500, s.GetProgress("survival"));
            Assert.AreEqual(5, s.GetProgress("rank"));
        }

        [Test]
        public void RecordMatch_NegativeDamageOrSurvival_TreatedAsZero()
        {
            var s = Make(null, D("d", "damage", 99), D("s", "survival", 99));
            s.RecordMatch(R(false, 10, 0, 0, -50f, -9f), -1);
            Assert.AreEqual(0, s.GetProgress("damage"));
            Assert.AreEqual(0, s.GetProgress("survival"));
        }

        [Test]
        public void Queries_NullArgs_AreSafe()
        {
            var s = Make(null, D("k", "kills", 1));
            Assert.IsFalse(s.IsUnlocked(null));
            Assert.AreEqual(0, s.GetProgress((string)null));
        }
    }

    [TestFixture]
    public sealed class CoverageLocalizationTests
    {
        [Test]
        public void Merge_SimpleObject_AddsKeys()
        {
            var t = new LocalizationTable();
            Assert.IsTrue(t.Merge("{\"a\":\"Merhaba\",\"b\":\"Dünya\"}"));
            Assert.IsTrue(t.TryGet("a", out var v));
            Assert.AreEqual("Merhaba", v);
            Assert.AreEqual(2, t.Count);
        }

        [Test]
        public void Merge_Overwrites_ExistingKey()
        {
            var t = new LocalizationTable();
            t.Merge("{\"a\":\"1\"}");
            t.Merge("{\"a\":\"2\"}");
            t.TryGet("a", out var v);
            Assert.AreEqual("2", v);
            Assert.AreEqual(1, t.Count);
        }

        [Test]
        public void Merge_Escapes_AreDecoded()
        {
            var t = new LocalizationTable();
            t.Merge("{\"k\":\"a\\nb\\t\\\"q\\\"\\u00fc\\\\\"}");
            t.TryGet("k", out var v);
            Assert.AreEqual("a\nb\t\"q\"ü\\", v);
        }

        [Test]
        public void Merge_EmptyOrNull_ReturnsFalse()
        {
            var t = new LocalizationTable();
            Assert.IsFalse(t.Merge(null));
            Assert.IsFalse(t.Merge(""));
            Assert.IsFalse(t.Merge("   "));
        }

        [Test]
        public void Merge_NotAnObject_ReturnsFalse()
        {
            var t = new LocalizationTable();
            Assert.IsFalse(t.Merge("[1,2]"));
            Assert.IsFalse(t.Merge("\"x\""));
        }

        [Test]
        public void Merge_EmptyObject_IsOkAndEmpty()
        {
            var t = new LocalizationTable();
            Assert.IsTrue(t.Merge("{}"));
            Assert.AreEqual(0, t.Count);
        }

        [Test]
        public void Merge_Truncated_ReturnsFalse_KeepsPartial()
        {
            var t = new LocalizationTable();
            Assert.IsFalse(t.Merge("{\"a\":\"1\",\"b\":\"2"));
            Assert.IsTrue(t.TryGet("a", out _));
        }

        [Test]
        public void Merge_NonStringValues_AreSkipped()
        {
            var t = new LocalizationTable();
            Assert.IsTrue(t.Merge("{\"n\":12,\"s\":\"ok\",\"b\":true}"));
            Assert.IsFalse(t.TryGet("n", out _));
            Assert.IsTrue(t.TryGet("s", out _));
        }

        [Test]
        public void Merge_BadUnicodeEscape_ReturnsFalse()
        {
            var t = new LocalizationTable();
            Assert.IsFalse(t.Merge("{\"a\":\"\\u12\"}"));
        }

        [Test]
        public void TryGet_NullKey_DoesNotThrow()
        {
            var t = new LocalizationTable();
            Assert.IsFalse(t.TryGet(null, out _));
        }

        [Test]
        public void Merge_WhitespaceTolerant()
        {
            var t = new LocalizationTable();
            Assert.IsTrue(t.Merge("  {\n  \"a\" : \"x\" ,\n \"b\":\"y\"\n}  "));
            Assert.AreEqual(2, t.Count);
        }

        [Test]
        public void NormalizeLanguage_UnknownFallsBackToDefault()
        {
            Assert.AreEqual(LocalizationTable.DefaultLanguage, LocalizationTable.NormalizeLanguage("xx"));
            Assert.AreEqual(LocalizationTable.DefaultLanguage, LocalizationTable.NormalizeLanguage(null));
            Assert.AreEqual("en", LocalizationTable.NormalizeLanguage("en"));
        }

        [Test]
        public void IndexOf_KnownCodesMatchLanguageArray()
        {
            for (var i = 0; i < LocalizationTable.Languages.Length; i++)
                Assert.AreEqual(i, LocalizationTable.IndexOf(LocalizationTable.Languages[i]));
            Assert.AreEqual(0, LocalizationTable.IndexOf("zz"));
        }

        [Test]
        public void LanguageNames_MatchLanguageCount()
        {
            Assert.AreEqual(LocalizationTable.Languages.Length, LocalizationTable.LanguageNames.Length);
        }
    }

    [TestFixture]
    public sealed class CoverageControlSchemeTests
    {
        [Test]
        public void DefaultBindings_HaveNoConflicts()
        {
            var entries = ControlScheme.BuildEntries(new InputBindingMap());
            Assert.AreEqual(0, ControlScheme.FindConflicts(entries).Count);
        }

        [Test]
        public void NullMap_FallsBackToDefaults()
        {
            Assert.AreEqual(ControlScheme.BuildEntries(new InputBindingMap()).Count, ControlScheme.BuildEntries(null).Count);
        }

        [Test]
        public void SameKeyInOverlappingContexts_DifferentLabels_Conflicts()
        {
            var list = new List<ControlEntry>
            {
                new ControlEntry(ControlContext.OnFoot, "K", "A", true),
                new ControlEntry(ControlContext.OnFoot | ControlContext.Driver, "K", "B", true),
            };
            Assert.AreEqual(1, ControlScheme.FindConflicts(list).Count);
        }

        [Test]
        public void SameKey_DisjointContexts_NoConflict()
        {
            var list = new List<ControlEntry>
            {
                new ControlEntry(ControlContext.OnFoot, "K", "A", true),
                new ControlEntry(ControlContext.Spectator, "K", "B", true),
            };
            Assert.AreEqual(0, ControlScheme.FindConflicts(list).Count);
        }

        [Test]
        public void SameKey_SameLabel_IsAlias_NotConflict()
        {
            var list = new List<ControlEntry>
            {
                new ControlEntry(ControlContext.OnFoot, "K", "A", true),
                new ControlEntry(ControlContext.OnFoot, "K", "A", true),
            };
            Assert.AreEqual(0, ControlScheme.FindConflicts(list).Count);
        }

        [Test]
        public void DifferentKeys_NeverConflict()
        {
            var list = new List<ControlEntry>
            {
                new ControlEntry(ControlContext.Always, "K", "A", true),
                new ControlEntry(ControlContext.Always, "L", "B", true),
            };
            Assert.AreEqual(0, ControlScheme.FindConflicts(list).Count);
        }

        [Test]
        public void EmptyList_NoConflicts()
        {
            Assert.AreEqual(0, ControlScheme.FindConflicts(new List<ControlEntry>()).Count);
        }

        [Test]
        public void RebindToFixedKey_ProducesConflict()
        {
            var map = new InputBindingMap();
            map.Set(BindAction.Reload, "F1"); // F1 = emir (sabit)
            Assert.Greater(ControlScheme.FindConflicts(ControlScheme.BuildEntries(map)).Count, 0);
        }

        [Test]
        public void Reserved_KeysDetected_AndNullSafe()
        {
            Assert.IsTrue(ControlScheme.IsReserved("Escape"));
            Assert.IsTrue(ControlScheme.IsReserved("F1"));
            Assert.IsFalse(ControlScheme.IsReserved("W"));
            Assert.IsFalse(ControlScheme.IsReserved(null));
            Assert.IsFalse(ControlScheme.IsReserved(""));
            Assert.IsFalse(ControlScheme.IsReserved("escape"), "büyük/küçük harf duyarlı");
        }

        [Test]
        public void FixedEntries_AreNeverRebindable_AndHaveLabels()
        {
            foreach (var e in ControlScheme.FixedEntries())
            {
                Assert.IsFalse(e.Rebindable);
                Assert.IsFalse(string.IsNullOrEmpty(e.Label));
                Assert.IsFalse(string.IsNullOrEmpty(e.Input));
                Assert.AreNotEqual(ControlContext.None, e.Context);
            }
        }

        [Test]
        public void EveryBindAction_HasContext()
        {
            foreach (var a in InputBindingMap.All)
                Assert.AreNotEqual(ControlContext.None, ControlScheme.ContextOf(a));
        }

        [Test]
        public void DefaultKeys_AreNotReserved()
        {
            foreach (var a in InputBindingMap.All)
                foreach (var k in InputBindingMap.DefaultKeys(a))
                    Assert.IsFalse(ControlScheme.IsReserved(k), a + " -> " + k);
        }

        [Test]
        public void BindingMap_SetStealsKeyFromOtherAction()
        {
            var map = new InputBindingMap();
            map.Set(BindAction.Heal, "G");
            map.Set(BindAction.Boost, "G");
            Assert.IsFalse(map.Has(BindAction.Heal, "G"));
            Assert.AreEqual(BindAction.Boost, map.FindOwner("G"));
        }

        [Test]
        public void BindingMap_SerializeRoundTrip_AfterRebind()
        {
            var map = new InputBindingMap();
            map.Set(BindAction.Jump, "Digit9");
            var copy = InputBindingMap.Deserialize(map.Serialize());
            Assert.IsTrue(copy.Has(BindAction.Jump, "Digit9"));
            Assert.AreEqual(map.Serialize(), copy.Serialize());
        }

        [Test]
        public void BindingMap_Deserialize_Garbage_KeepsDefaults()
        {
            var copy = InputBindingMap.Deserialize("???\n=\nNoSuchAction=Q\n");
            Assert.AreEqual(new InputBindingMap().Serialize(), copy.Serialize());
        }
    }
}
