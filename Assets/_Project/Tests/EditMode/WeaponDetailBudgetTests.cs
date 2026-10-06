using NUnit.Framework;
using Project.Infrastructure.Weapons;

namespace Project.Tests
{
    public sealed class WeaponDetailBudgetTests
    {
        [Test]
        public void LimitLod1IsHalfOfLod0()
        {
            Assert.AreEqual(12000, WeaponDetailBudget.Limit(0));
            Assert.AreEqual(WeaponDetailBudget.Limit(0) / 2, WeaponDetailBudget.Limit(1));
        }

        [Test]
        public void CanAffordRespectsLimit()
        {
            Assert.IsTrue(WeaponDetailBudget.CanAfford(11000, 1000, 0));
            Assert.IsTrue(!WeaponDetailBudget.CanAfford(11000, 1001, 0));
            Assert.IsTrue(!WeaponDetailBudget.CanAfford(5000, 1001, 1));
            Assert.IsTrue(!WeaponDetailBudget.CanAfford(0, -1, 0));
        }

        [Test]
        public void RailTeethLod1IsHalf()
        {
            Assert.AreEqual(29, WeaponDetailBudget.RailTeeth(0.29f, 0));
            Assert.AreEqual(14, WeaponDetailBudget.RailTeeth(0.29f, 1));
            Assert.AreEqual(1, WeaponDetailBudget.RailTeeth(0.001f, 1));
        }

        [Test]
        public void CountHalvesInLod1()
        {
            Assert.AreEqual(6, WeaponDetailBudget.Count(6, 0));
            Assert.AreEqual(3, WeaponDetailBudget.Count(6, 1));
            Assert.AreEqual(1, WeaponDetailBudget.Count(1, 1));
            Assert.AreEqual(0, WeaponDetailBudget.Count(0, 1));
        }

        [Test]
        public void RailCostFitsBudgetForLongRail()
        {
            Assert.IsTrue(WeaponDetailBudget.RailVerts(0.29f, 0) < WeaponDetailBudget.MaxVerticesLod0);
            Assert.Greater(WeaponDetailBudget.RailVerts(0.29f, 0), WeaponDetailBudget.RailVerts(0.29f, 1));
        }

        [Test]
        public void ShapeCostEstimates()
        {
            Assert.AreEqual(24, WeaponDetailBudget.FlatBoxVerts);
            Assert.AreEqual(256, WeaponDetailBudget.TubeVerts(6));
            Assert.AreEqual(20 * 4 + 2 * 20 * 3, WeaponDetailBudget.CylinderVerts(8));
        }
    }
}
