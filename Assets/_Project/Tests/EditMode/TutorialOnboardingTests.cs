using NUnit.Framework;
using Project.Core.Interfaces;
using Project.Presentation.Tutorial;
using System.Collections.Generic;

namespace Project.Tests
{
    public class TutorialOnboardingTests
    {
        private sealed class MemStore : ISettingsStore
        {
            private readonly Dictionary<string, int> _d = new Dictionary<string, int>();
            public bool HasKey(string k) => _d.ContainsKey(k);
            public float GetFloat(string k, float f) => f;
            public int GetInt(string k, int f) => _d.TryGetValue(k, out var v) ? v : f;
            public void SetFloat(string k, float v) { }
            public void SetInt(string k, int v) => _d[k] = v;
            public void Save() { }
        }

        [Test]
        public void Tip_ShowsOncePerSession_AndMutePersists()
        {
            var store = new MemStore();
            var t = new TutorialTipTracker(store);
            Assert.IsTrue(t.ShouldShow(TutorialTipId.FirstLoot));
            t.MarkShown(TutorialTipId.FirstLoot);
            Assert.IsFalse(t.ShouldShow(TutorialTipId.FirstLoot));
            var t2 = new TutorialTipTracker(store);
            Assert.IsTrue(t2.ShouldShow(TutorialTipId.FirstLoot));
            t2.MuteForever(TutorialTipId.FirstLoot);
            Assert.IsFalse(new TutorialTipTracker(store).ShouldShow(TutorialTipId.FirstLoot));
            new TutorialTipTracker(store).ResetAll();
            Assert.IsTrue(new TutorialTipTracker(store).ShouldShow(TutorialTipId.FirstLoot));
        }

        [Test]
        public void Tip_StopsAfterMaxShows()
        {
            var store = new MemStore();
            for (var i = 0; i < TutorialTipTracker.MaxShows; i++)
                new TutorialTipTracker(store).MarkShown(TutorialTipId.FirstAds);
            Assert.IsFalse(new TutorialTipTracker(store).ShouldShow(TutorialTipId.FirstAds));
        }

        [Test]
        public void Hints_UseActiveBinding_ViaResolver()
        {
            TutorialInputHints.KeyResolver = a => a == Project.Application.Services.BindAction.Reload ? "T" : "-";
            try
            {
                Assert.AreEqual("T", TutorialInputHints.Label("Reload", false));
                Assert.AreEqual("F", TutorialInputHints.Label("Interact", false)); // "-" -> varsayılan
                Assert.AreEqual("X", TutorialInputHints.Label("Reload", true));
            }
            finally { TutorialInputHints.KeyResolver = null; }
        }

        [Test]
        public void Progress_FlagAndMatchCounter()
        {
            var s = new MemStore();
            Assert.IsFalse(TutorialProgress.IsCompleted(s));
            Assert.IsTrue(TutorialProgress.MarkCompleted(s));
            Assert.IsFalse(TutorialProgress.MarkCompleted(s));
            Assert.IsTrue(TutorialProgress.IsCompleted(s));
            Assert.AreEqual(1, TutorialProgress.RegisterMatch(s));
            Assert.AreEqual(2, TutorialProgress.RegisterMatch(s));
        }

        [Test]
        public void Tips_DensityDropsOverFirstMatches()
        {
            Assert.Greater(TutorialTipTracker.MaxTipsPerMatch(1), TutorialTipTracker.MaxTipsPerMatch(2));
            Assert.Greater(TutorialTipTracker.MaxTipsPerMatch(2), TutorialTipTracker.MaxTipsPerMatch(3));
            var t = new TutorialTipTracker(new MemStore()) { MatchIndex = 4 };
            t.MarkShown(TutorialTipId.FirstLoot);
            Assert.IsFalse(t.ShouldShow(TutorialTipId.FirstAds));
        }

        [Test]
        public void Hints_AdaptToGamepad()
        {
            Assert.AreEqual("R", TutorialInputHints.Label("Reload", false));
            Assert.AreEqual("X", TutorialInputHints.Label("Reload", true));
            StringAssert.Contains("[RT]", TutorialInputHints.Format("x", "Fire", true));
        }

        [Test]
        public void Checklist_MarksByStep_AndRenders()
        {
            var c = new TutorialChecklist();
            Assert.IsTrue(c.MarkByStep("poly_01_move"));
            Assert.IsFalse(c.MarkByStep("poly_01_move"));
            Assert.IsFalse(c.MarkByStep("poly_99"));
            StringAssert.Contains("[x] Hareket et", c.Render(false));
            Assert.AreEqual("shoot", c.Next().Value.Id);
            c.Mark("shoot"); c.Mark("reload"); c.Mark("grenade"); c.Mark("heal");
            Assert.IsTrue(c.AllDone);
        }
    }
}
