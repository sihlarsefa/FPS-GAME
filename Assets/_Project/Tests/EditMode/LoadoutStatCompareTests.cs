using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Presentation.UI;

namespace Project.Tests
{
    public sealed class LoadoutStatCompareTests
    {
        private static WeaponDefinitionData Rifle => WeaponCatalog.Get(WeaponIds.Mpt76);

        [Test]
        public void Grade_Thresholds()
        {
            Assert.AreEqual("S", LoadoutStatCompare.Grade(0.9f));
            Assert.AreEqual("A", LoadoutStatCompare.Grade(0.75f));
            Assert.AreEqual("B", LoadoutStatCompare.Grade(0.55f));
            Assert.AreEqual("C", LoadoutStatCompare.Grade(0.35f));
            Assert.AreEqual("D", LoadoutStatCompare.Grade(0.1f));
            Assert.AreEqual("S", LoadoutStatCompare.Grade(5f));
            Assert.AreEqual("D", LoadoutStatCompare.Grade(-1f));
        }

        [Test]
        public void Segments_GainKeepsOldAsSolid_LossKeepsNewAsSolid()
        {
            var gain = LoadoutStatCompare.Segments(0.40f, 0.55f);
            Assert.AreEqual(0.40f, gain.Solid, 0.001f);
            Assert.AreEqual(0.15f, gain.Extra, 0.001f);
            Assert.AreEqual(StatTrend.Better, gain.Trend);

            var loss = LoadoutStatCompare.Segments(0.60f, 0.50f);
            Assert.AreEqual(0.50f, loss.Solid, 0.001f);
            Assert.AreEqual(0.10f, loss.Extra, 0.001f);
            Assert.AreEqual(StatTrend.Worse, loss.Trend);

            var same = LoadoutStatCompare.Segments(0.5f, 0.501f);
            Assert.AreEqual(StatTrend.Same, same.Trend);
            Assert.AreEqual(0f, same.Extra, 0.0001f);
        }

        [Test]
        public void ReplaceInSlot_SwapsOnlyThatSlot_AndDoesNotMutateInput()
        {
            var input = new List<string> { ItemIds.Scope4x, ItemIds.Suppressor };
            var swapped = LoadoutStatCompare.ReplaceInSlot(input, AttachmentSlot.Sight, ItemIds.RedDot);
            Assert.AreEqual(2, swapped.Count);
            Assert.IsTrue(swapped.Contains(ItemIds.RedDot));
            Assert.IsTrue(swapped.Contains(ItemIds.Suppressor));
            Assert.IsFalse(swapped.Contains(ItemIds.Scope4x));
            Assert.AreEqual(2, input.Count);
            Assert.IsTrue(input.Contains(ItemIds.Scope4x));

            var cleared = LoadoutStatCompare.ReplaceInSlot(input, AttachmentSlot.Muzzle, null);
            Assert.AreEqual(1, cleared.Count);
            Assert.AreEqual(ItemIds.Scope4x, cleared[0]);
        }

        [Test]
        public void ReplaceInSlot_IgnoresNullAndUnknownIds()
        {
            var r = LoadoutStatCompare.ReplaceInSlot(new List<string> { null, "", "yok.boyle.id" }, AttachmentSlot.Grip, ItemIds.VerticalGrip);
            Assert.AreEqual(2, r.Count);
            Assert.AreEqual("yok.boyle.id", r[0]);
            Assert.AreEqual(ItemIds.VerticalGrip, r[1]);
        }

        [Test]
        public void PreviewSlot_SniperStock_ImprovesControl_AndAddsWeight()
        {
            var cmp = LoadoutStatCompare.PreviewSlot(Rifle, new List<string>(), AttachmentSlot.Stock, ItemIds.SniperStock);
            Assert.IsNotNull(cmp);
            Assert.Greater(cmp.Bars[3].Points, 0);
            Assert.Greater(cmp.WeightDelta, 0f);
            Assert.AreEqual(StatTrend.Worse, LoadoutStatCompare.WeightTrend(cmp.WeightDelta));
            Assert.Greater(cmp.BetterCount, 0);
        }

        [Test]
        public void PreviewSlot_ExtendedMag_AddsMagazineRounds()
        {
            var cmp = LoadoutStatCompare.PreviewSlot(Rifle, null, AttachmentSlot.Magazine, ItemIds.ExtMag);
            Assert.Greater(cmp.MagazineDelta, 0);
            Assert.IsFalse(cmp.IsIdentical);
        }

        [Test]
        public void PreviewSlot_SameAsCurrent_IsIdentical()
        {
            var cmp = LoadoutStatCompare.PreviewSlot(Rifle, new[] { ItemIds.Suppressor }, AttachmentSlot.Muzzle, ItemIds.Suppressor);
            Assert.IsTrue(cmp.IsIdentical);
            Assert.AreEqual(0, cmp.OverallDelta);
            Assert.AreEqual("değişim yok", LoadoutStatCompare.Summary(cmp));
        }

        [Test]
        public void PreviewSlot_NullWeapon_ReturnsNull()
        {
            Assert.IsNull(LoadoutStatCompare.PreviewSlot(null, null, AttachmentSlot.Grip, ItemIds.VerticalGrip));
        }

        [Test]
        public void RankSlotOptions_MarksCurrent_AndSortsByOverallDescending()
        {
            var options = LoadoutSelection.CompatibleAttachments(AttachmentSlot.Grip, Rifle.Category);
            var ranked = LoadoutStatCompare.RankSlotOptions(Rifle, new List<string>(), AttachmentSlot.Grip, options, null);
            Assert.AreEqual(options.Count, ranked.Count);
            var currents = 0;
            for (var i = 0; i < ranked.Count; i++)
            {
                if (ranked[i].IsCurrent)
                {
                    currents++;
                    Assert.IsNull(ranked[i].ItemId);
                    Assert.AreEqual("YOK", ranked[i].Name);
                }

                if (i > 0)
                    Assert.IsTrue(ranked[i - 1].VsCurrent.OverallDelta >= ranked[i].VsCurrent.OverallDelta);
            }

            Assert.AreEqual(1, currents);
        }

        [Test]
        public void Overall_InRange_AndWeightsDifferByCategory()
        {
            var set = LoadoutStats.Compute(Rifle);
            var overall = LoadoutStatCompare.Overall(set, Rifle.Category);
            Assert.That(overall, Is.InRange(0, 100));
            var a = LoadoutStatCompare.WeightsFor(WeaponCategory.Sniper);
            var b = LoadoutStatCompare.WeightsFor(WeaponCategory.Smg);
            Assert.AreEqual(LoadoutStats.BarCount, a.Length);
            Assert.AreNotEqual(a[2], b[2]);
            a[0] = 99f;
            Assert.AreNotEqual(99f, LoadoutStatCompare.WeightsFor(WeaponCategory.Sniper)[0]);
        }

        [Test]
        public void Overall_EmptySet_IsZero()
        {
            Assert.AreEqual(0, LoadoutStatCompare.Overall(default, WeaponCategory.AssaultRifle));
        }

        [Test]
        public void Signed_And_WeightText_Format()
        {
            Assert.AreEqual("+5", LoadoutStatCompare.Signed(5));
            Assert.AreEqual("-3", LoadoutStatCompare.Signed(-3));
            Assert.AreEqual("0", LoadoutStatCompare.Signed(0));
            Assert.AreEqual("+0.4 kg", LoadoutStatCompare.WeightText(0.4f));
            Assert.AreEqual("-0.25 kg", LoadoutStatCompare.WeightText(-0.25f));
            Assert.AreEqual(string.Empty, LoadoutStatCompare.WeightText(0.001f));
            Assert.AreEqual(StatTrend.Better, LoadoutStatCompare.WeightTrend(-0.3f));
            Assert.AreEqual(StatTrend.Same, LoadoutStatCompare.WeightTrend(0f));
        }

        [Test]
        public void CategoryPercentile_InRange()
        {
            for (var i = 0; i < LoadoutStats.BarCount; i++)
                Assert.That(LoadoutStatCompare.CategoryPercentile(Rifle, i), Is.InRange(0, 100));
            Assert.AreEqual(0, LoadoutStatCompare.CategoryPercentile(null, 0));
        }

        [Test]
        public void DeltaLine_ColorsGoodGreenAndBadRed_OnlyChangedStats()
        {
            var cmp = LoadoutStatCompare.PreviewSlot(Rifle, null, AttachmentSlot.Stock, ItemIds.SniperStock);
            var line = LoadoutCompareView.DeltaLine(cmp);
            StringAssert.Contains("KNT", line);
            StringAssert.Contains("<color=#", line);
        }
    }
}
