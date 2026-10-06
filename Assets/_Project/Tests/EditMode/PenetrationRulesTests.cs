using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public sealed class PenetrationRulesTests
    {
        [Test]
        public void CaliberPower_Ordering()
        {
            Assert.Greater(PenetrationRules.CaliberPower(AmmoType.Mm762), PenetrationRules.CaliberPower(AmmoType.Mm556));
            Assert.Greater(PenetrationRules.CaliberPower(AmmoType.Mm556), PenetrationRules.CaliberPower(AmmoType.Mm9));
            Assert.Greater(PenetrationRules.CaliberPower(AmmoType.Mm9), PenetrationRules.CaliberPower(AmmoType.Gauge12));
        }

        [Test]
        public void Concrete_And_Solid_NeverPenetrate()
        {
            Assert.IsFalse(PenetrationRules.CanPenetrate(PenetrationMaterial.Concrete, AmmoType.Mm762, 0.01f));
            Assert.IsFalse(PenetrationRules.CanPenetrate(PenetrationMaterial.Solid, AmmoType.Mm762, 0.01f));
            Assert.AreEqual(0f, PenetrationRules.DamageFactor(PenetrationMaterial.Concrete, AmmoType.Mm762, 0.1f));
        }

        [Test]
        public void ThicknessLimit_ScalesWithCaliber()
        {
            Assert.IsTrue(PenetrationRules.CanPenetrate(PenetrationMaterial.Wood, AmmoType.Mm762, 0.4f));
            Assert.IsFalse(PenetrationRules.CanPenetrate(PenetrationMaterial.Wood, AmmoType.Mm9, 0.4f));
            Assert.IsFalse(PenetrationRules.CanPenetrate(PenetrationMaterial.Wood, AmmoType.Mm762, 0.5f));
            Assert.IsTrue(PenetrationRules.CanPenetrate(PenetrationMaterial.Foliage, AmmoType.Gauge12, 0.4f));
        }

        [Test]
        public void DamageFactor_DropsWithThickness_AndRisesWithCaliber()
        {
            var thin = PenetrationRules.DamageFactor(PenetrationMaterial.Wood, AmmoType.Mm762, 0.02f);
            var thick = PenetrationRules.DamageFactor(PenetrationMaterial.Wood, AmmoType.Mm762, 0.4f);
            Assert.Greater(thin, thick);
            Assert.Greater(PenetrationRules.DamageFactor(PenetrationMaterial.Plaster, AmmoType.Mm762, 0.05f),
                PenetrationRules.DamageFactor(PenetrationMaterial.Plaster, AmmoType.Mm9, 0.05f));
            Assert.Greater(PenetrationRules.VelocityFactor(PenetrationMaterial.Wood, AmmoType.Mm762, 0.05f),
                PenetrationRules.VelocityFactor(PenetrationMaterial.Wood, AmmoType.Mm9, 0.05f));
            Assert.LessOrEqual(thin, 1f);
            Assert.Greater(thick, 0f);
        }

        [Test]
        public void Ricochet_OnlyMetalConcrete_ShallowAngles()
        {
            Assert.AreEqual(0f, PenetrationRules.RicochetChance(PenetrationMaterial.Wood, AmmoType.Mm762, 5f));
            Assert.AreEqual(0f, PenetrationRules.RicochetChance(PenetrationMaterial.Concrete, AmmoType.Mm762, 45f));
            var shallow = PenetrationRules.RicochetChance(PenetrationMaterial.Concrete, AmmoType.Mm762, 3f);
            var steeper = PenetrationRules.RicochetChance(PenetrationMaterial.Concrete, AmmoType.Mm762, 15f);
            Assert.Greater(shallow, steeper);
            Assert.Greater(steeper, 0f);
            Assert.Less(PenetrationRules.RicochetChance(PenetrationMaterial.Concrete, AmmoType.Gauge12, 3f), shallow);
        }

        [Test]
        public void StillLethal_Threshold()
        {
            Assert.IsTrue(PenetrationRules.StillLethal(0.5f));
            Assert.IsFalse(PenetrationRules.StillLethal(0.05f));
        }
    }
}
