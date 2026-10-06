using NUnit.Framework;
using Project.Infrastructure.Weapons;

namespace Project.Tests
{
    public sealed class WeaponFineDetailTests
    {
        [Test]
        public void BudgetFitsKnownExtraDetailCosts()
        {
            // En pahalı ek detay (G3 delik sırası + tırtıl) LOD1 bütçesinin çok altında kalır.
            var g3 = (8 + 5 * 2 * 2) * WeaponDetailBudget.FlatBoxVerts;
            Assert.IsTrue(WeaponDetailBudget.CanAfford(0, g3, 1));
            Assert.IsTrue(!WeaponDetailBudget.CanAfford(WeaponDetailBudget.MaxVerticesLod1 - 10, g3, 1));
        }

        [Test]
        public void Lod1HalvesRepeatedDetail()
        {
            Assert.AreEqual(10, WeaponDetailBudget.Count(5, 0) * 2);
            Assert.AreEqual(4, WeaponDetailBudget.Count(5, 1) * 2);
            Assert.AreEqual(3, WeaponDetailBudget.Count(6, 1));
        }

        [Test]
        public void ExtraFineDetailCoversRemainingWeapons()
        {
            Assert.IsTrue(WeaponDetailBudget.HasExtraFineDetail(WeaponStyle.G3a7));
            Assert.IsTrue(WeaponDetailBudget.HasExtraFineDetail(WeaponStyle.Mg3));
            Assert.IsTrue(WeaponDetailBudget.HasExtraFineDetail(WeaponStyle.Pmt76));
            Assert.IsTrue(WeaponDetailBudget.HasExtraFineDetail(WeaponStyle.Sar109));
            Assert.IsTrue(WeaponDetailBudget.HasExtraFineDetail(WeaponStyle.Sar9));
            Assert.IsTrue(WeaponDetailBudget.HasExtraFineDetail(WeaponStyle.Tp9));
            Assert.IsTrue(!WeaponDetailBudget.HasExtraFineDetail(WeaponStyle.Mpt76));
            Assert.IsTrue(!WeaponDetailBudget.HasExtraFineDetail(WeaponStyle.None));
        }
    }
}
