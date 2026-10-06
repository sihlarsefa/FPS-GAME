#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Rendering;

namespace Project.Tests.EditMode
{
    public sealed class AutoQualityRulesTests
    {
        [Test] public void HighEndPicksUltra() => Assert.AreEqual(3, AutoQualityRules.ChooseTier("NVIDIA GeForce RTX 4080", 16000, 16, 4f));
        [Test] public void IntegratedPicksLow() => Assert.AreEqual(0, AutoQualityRules.ChooseTier("Intel UHD Graphics", 1024, 4, 0f));
        [Test] public void SlowProbeLowersTier() => Assert.AreEqual(2, AutoQualityRules.ChooseTier("NVIDIA GeForce RTX 4080", 16000, 16, 25f));
        [Test] public void TierAlwaysInRange()
        {
            Assert.AreEqual(0, AutoQualityRules.ChooseTier(null, 0, 1, 100f));
            Assert.LessOrEqual(AutoQualityRules.ChooseTier("RTX 5090", 32000, 32, 1f), 3);
        }
        [Test] public void NextNotchDropsAndRestores()
        {
            Assert.AreEqual(1, AutoQualityRules.NextNotch(0, 25f));
            Assert.AreEqual(0, AutoQualityRules.NextNotch(1, 10f));
            Assert.AreEqual(1, AutoQualityRules.NextNotch(1, 17f));
            Assert.AreEqual(AutoQualityRules.MaxNotch, AutoQualityRules.NextNotch(AutoQualityRules.MaxNotch, 40f));
            Assert.AreEqual(0, AutoQualityRules.NextNotch(0, 5f));
        }
        [Test] public void ScaleStaysWithinBounds()
        {
            Assert.AreEqual(1f, AutoQualityRules.ScaleFor(1f, 0), 1e-4f);
            Assert.AreEqual(0.7f, AutoQualityRules.ScaleFor(1f, 3), 1e-4f);
            Assert.GreaterOrEqual(AutoQualityRules.ScaleFor(0.67f, 3), AutoQualityRules.AbsoluteMinScale);
            Assert.AreEqual(0.5f, AutoQualityRules.ScaleFor(0.5f, 3), 1e-4f);
        }
        [Test] public void FirstRunMessageNamesTier() => StringAssert.Contains("YÜKSEK", AutoQualityRules.FirstRunMessage(2));
        [Test] public void DynNoticeHysteresis()
        {
            Assert.IsTrue(AutoQualityRules.ShouldNotifyDynScale(0, 1, 10f, -1f));
            Assert.IsFalse(AutoQualityRules.ShouldNotifyDynScale(0, 1, 60f, 10f));
            Assert.IsTrue(AutoQualityRules.ShouldNotifyDynScale(0, 1, 131f, 10f));
            Assert.IsFalse(AutoQualityRules.ShouldNotifyDynScale(2, 1, 500f, -1f));
        }
        [Test] public void SlowP95SuggestsAfterThreeMinutes()
        {
            var t = AutoQualityRules.SlowTracker.New();
            Assert.IsFalse(t.Update(0f, 40f, 60, 2));
            Assert.IsFalse(t.Update(170f, 40f, 60, 2));
            Assert.IsTrue(t.Update(181f, 40f, 60, 2));
            Assert.IsFalse(t.Update(400f, 40f, 60, 2));
        }
        [Test] public void SlowP95ResetsAndIgnoresLowestTier()
        {
            var t = AutoQualityRules.SlowTracker.New();
            t.Update(0f, 40f, 60, 2);
            t.Update(100f, 20f, 60, 2);
            Assert.IsFalse(t.Update(200f, 40f, 60, 2));
            var u = AutoQualityRules.SlowTracker.New();
            u.Update(0f, 40f, 60, 0);
            Assert.IsFalse(u.Update(500f, 40f, 60, 0));
        }
        [Test] public void DecisionCsvLineFormat() => Assert.AreEqual("autoquality,olcek-dus,2,1,0.9,22.5", AutoQualityRules.DecisionCsvLine("olcek-dus", 2, 1, 0.9f, 22.5f));
        [Test] public void OffendersRankedByOverage()
        {
            var o = AutoQualityRules.RankOffenders(10000, 100, 0, 10f);
            Assert.AreEqual(1, o.Length);
            Assert.AreEqual("DrawCall", o[0]);
            Assert.AreEqual(0, AutoQualityRules.RankOffenders(100, 100, 100, 10f).Length);
        }
    }
}
#endif
