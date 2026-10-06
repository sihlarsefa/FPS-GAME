using NUnit.Framework;
using Project.Infrastructure.World;

namespace Project.Tests.EditMode
{
    public class TreeScatterRulesTests
    {
        [Test]
        public void StandMask_InRange_AndClearingsCut()
        {
            Assert.AreEqual(0f, TreeScatterRules.StandMask(0.2f, 0.3f), 1e-4f);
            Assert.AreEqual(1f, TreeScatterRules.StandMask(0.9f, 0.3f), 1e-4f);
            Assert.AreEqual(0.1f, TreeScatterRules.StandMask(0.9f, 0.95f), 1e-4f);
        }

        [Test]
        public void EdgeWeight_PeaksAtHalf()
        {
            Assert.AreEqual(1f, TreeScatterRules.EdgeWeight(0.5f), 1e-4f);
            Assert.AreEqual(0f, TreeScatterRules.EdgeWeight(0f), 1e-4f);
            Assert.AreEqual(0f, TreeScatterRules.EdgeWeight(1f), 1e-4f);
        }

        [Test]
        public void RoadLineBoost_OnlyInBand()
        {
            Assert.AreEqual(1f, TreeScatterRules.RoadLineBoost(1f), 1e-4f);
            Assert.AreEqual(1f, TreeScatterRules.RoadLineBoost(30f), 1e-4f);
            Assert.AreEqual(1f, TreeScatterRules.RoadLineBoost(float.PositiveInfinity), 1e-4f);
            Assert.AreEqual(3.2f, TreeScatterRules.RoadLineBoost(7f), 1e-4f);
        }

        [Test]
        public void PickKind_PinesHighOaksLow_BushesAtEdges()
        {
            Assert.AreEqual(Project.Infrastructure.World.TreeKind.Oak, TreeScatterRules.PickKind(0.7f, 10f, 0f));
            var high = TreeScatterRules.PickKind(0.7f, 120f, 0f);
            Assert.IsTrue(high == TreeKind.PineA || high == TreeKind.PineB);
            Assert.AreEqual(TreeKind.Bush, TreeScatterRules.PickKind(0.2f, 10f, 1f));
            Assert.Greater(TreeScatterRules.BushChance(1f), TreeScatterRules.BushChance(0f));
        }

        [Test]
        public void AcceptsLog_OnlyDeepInForest()
        {
            Assert.IsFalse(TreeScatterRules.AcceptsLog(0.4f, 0.1f));
            Assert.IsTrue(TreeScatterRules.AcceptsLog(0.9f, 0.1f));
            Assert.IsFalse(TreeScatterRules.AcceptsLog(0.9f, 0.9f));
        }
    }
}
