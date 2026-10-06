#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Project.Application.Dialogue;

namespace Project.Tests.EditMode
{
    /// <summary>Resources/Audio/dialogue_lines_v2.csv: ayrışır, kimlikler benzersiz, kompozisyon parçaları eksiksiz.</summary>
    [TestFixture]
    public sealed class DialogueV2DataTests
    {
        private static DialogueLineBook Load()
        {
            var path = Path.Combine(UnityEngine.Application.dataPath, "_Project/Resources/Audio/dialogue_lines_v2.csv");
            Assert.IsTrue(File.Exists(path), "dialogue_lines_v2.csv bulunamadı");
            return DialogueLineBook.FromCsv(File.ReadAllText(path));
        }

        [Test]
        public void Csv_HasEnoughLinesAndAllStressStates()
        {
            var book = Load();
            Assert.Greater(book.Count, 400);
            var list = new List<DialogueLine>();
            string[] cats =
            {
                DialogueCats.Reload, DialogueCats.MagEmpty, DialogueCats.GrenadeThrow, DialogueCats.Wounded, DialogueCats.HitSelf,
                DialogueCats.EnemyDown, DialogueCats.CoverMe, DialogueCats.Advance, DialogueCats.Smoke
            };
            foreach (var c in cats)
            {
                foreach (var s in new[] { DialogueStress.Calm, DialogueStress.Combat, DialogueStress.Panic })
                    Assert.Greater(book.Collect(c, s, list), 1, c + "/" + s);
            }
        }

        [Test]
        public void Csv_AllCompositionPartsExist()
        {
            var book = Load();
            for (var h = 1; h <= 12; h++)
            {
                Assert.IsNotNull(book.Get("clk_" + h.ToString("00")), "clk " + h);
                Assert.IsNotNull(book.Get("clks_" + h.ToString("00")), "clks " + h);
            }

            var ids = new List<string>();
            for (var d = 5; d <= 2000; d += 5)
            {
                ids.Clear();
                TurkishNumbers.ToPartIds(d, ids);
                foreach (var id in ids)
                    Assert.IsNotNull(book.Get(id), id);
            }

            foreach (var t in PhraseComposer.TargetIds)
                Assert.IsNotNull(book.Get(t), t);
            Assert.IsNotNull(book.Get("unit_metre"));
            Assert.IsNotNull(book.Get("unit_metre_yakin"));
        }

        [Test]
        public void Composer_WorksOnRealBookForAllClockHours()
        {
            var book = Load();
            var rolls = new System.Random(5);
            for (var h = 0; h < 12; h++)
            {
                foreach (var s in new[] { DialogueStress.Calm, DialogueStress.Combat, DialogueStress.Panic })
                {
                    var p = PhraseComposer.ComposeSpotted(book, new BarkMemory(), 1, s, h * 30f, 40f + h * 70f, null, () => (float)rolls.NextDouble());
                    Assert.IsNotNull(p);
                    Assert.IsTrue(p.Text.Contains("Saat"));
                }
            }
        }

        [Test]
        public void Csv_RankAckCategoriesHaveAllStress()
        {
            var book = Load();
            var list = new List<DialogueLine>();
            foreach (var c in new[] { DialogueCats.AckEnlisted, DialogueCats.AckNco, DialogueCats.AckPeer, DialogueCats.AckSuperior })
                Assert.Greater(book.Collect(c, DialogueStress.Combat, list), 1, c);
        }
    }
}
#endif
