using NUnit.Framework;
using Project.Core.Domain;
using Project.Presentation.UI;

namespace Project.Tests.EditMode
{
    public sealed class LoadingScreenTests
    {
        [Test]
        public void Tips_AreFortyUniqueAndNonEmpty()
        {
            Assert.AreEqual(40, LoadingTips.Count);
            var seen = new System.Collections.Generic.HashSet<string>();
            for (var i = 0; i < LoadingTips.Count; i++)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(LoadingTips.Get(i)));
                Assert.IsTrue(seen.Add(LoadingTips.Get(i)));
            }
            Assert.AreEqual(LoadingTips.Get(0), LoadingTips.Get(40));
            Assert.IsNotNull(LoadingTips.Get(-1));
        }

        [Test]
        public void Tips_NextNeverRepeatsPrevious()
        {
            for (var p = 0; p < LoadingTips.Count; p++)
                for (var r = 0; r <= 10; r++)
                    Assert.AreNotEqual(p, LoadingTips.Next(p, r / 10f));
        }

        [Test]
        public void Progress_IsMonotonicAndReachesOne()
        {
            var m = new LoadingProgressModel();
            var last = 0f;
            foreach (LoadPhase ph in System.Enum.GetValues(typeof(LoadPhase)))
                for (var i = 0; i <= 4; i++)
                {
                    var v = m.Report(ph, i / 4f);
                    Assert.GreaterOrEqual(v, last);
                    last = v;
                }
            Assert.AreEqual(1f, last, 1e-4f);
            Assert.AreEqual(last, m.Report(LoadPhase.SceneLoad, 0.1f), 1e-6f);
            m.Reset();
            Assert.AreEqual(0f, m.Value);
            Assert.AreEqual(0.30f, m.Report(LoadPhase.WorldGen, float.NaN), 1e-6f);
        }

        [Test]
        public void Briefing_ContainsMapModeAndTeams()
        {
            var t = LoadingBriefing.Build("Mavi Liman", GameMode.BattleRoyale, 4, 10);
            StringAssert.Contains("Mavi Liman", t);
            StringAssert.Contains("4 tim x 10 asker", t);
            StringAssert.Contains("Tim Battle Royale", t);
            StringAssert.DoesNotContain("tim x", LoadingBriefing.Build("X", GameMode.Training, 4, 10));
        }

#if UNITY_EDITOR
        [Test]
        public void Silhouette_DrawsPixelsForEveryMap()
        {
            for (var i = 0; i < MapCatalog.Count; i++)
            {
                var layout = Project.Infrastructure.World.MapLayout.Create(MapCatalog.IdAt(i), 1);
                var px = LoadingMapSilhouette.Render(layout, 128);
                Assert.AreEqual(128 * 128, px.Length);
                var opaque = 0;
                foreach (var c in px) if (c.a > 0) opaque++;
                Assert.Greater(opaque, 128 * 128 / 2);
            }
            Assert.AreEqual(64 * 64, LoadingMapSilhouette.Render(null, 64).Length);
        }
#endif
    }
}
