using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public sealed class BarrierModelTests
    {
        [Test]
        public void ThinBarriers_PassRifle_WithEnergyLoss()
        {
            var e = BarrierModel.MuzzleEnergy(AmmoType.Mm762);
            foreach (var m in new[] { PenetrationMaterial.Wood, PenetrationMaterial.Plaster, PenetrationMaterial.ThinMetal })
            {
                var x = BarrierModel.ExitEnergy(m, e, 0.03f);
                Assert.Greater(x, 0f, m.ToString());
                Assert.Less(x, e, m.ToString());
                Assert.Greater(BarrierModel.Row(m).ExitSpreadDeg, 0f);
            }
        }

        [Test]
        public void Brick_StopsRifles_338PunchesOnce()
        {
            const float t = 0.12f;
            Assert.IsFalse(BarrierModel.Penetrates(PenetrationMaterial.Brick, BarrierModel.MuzzleEnergy(AmmoType.Mm762), t));
            Assert.IsFalse(BarrierModel.Penetrates(PenetrationMaterial.Brick, BarrierModel.MuzzleEnergy(AmmoType.Mm556), t));
            var after = BarrierModel.ExitEnergy(PenetrationMaterial.Brick, BarrierModel.Muzzle338Joules, t);
            Assert.Greater(after, 0f);
            Assert.AreEqual(0f, BarrierModel.ExitEnergy(PenetrationMaterial.Brick, after, t));
        }

        [Test]
        public void Sandbag_AbsorbsEverything_AndConcreteStops()
        {
            Assert.IsFalse(BarrierModel.Penetrates(PenetrationMaterial.Sandbag, BarrierModel.Muzzle338Joules, 0.01f));
            Assert.IsFalse(BarrierModel.Penetrates(PenetrationMaterial.Solid, 1e9f, 0.01f));
            Assert.IsFalse(BarrierModel.Penetrates(PenetrationMaterial.Concrete, BarrierModel.Muzzle338Joules, 0.2f));
        }

        [Test]
        public void Energy_MonotonicInThickness_AndEntry()
        {
            var prev = float.MaxValue;
            for (var t = 0f; t <= 0.4f; t += 0.02f)
            {
                var x = BarrierModel.ExitEnergy(PenetrationMaterial.Wood, 3400f, t);
                Assert.LessOrEqual(x, prev);
                prev = x;
            }
            Assert.GreaterOrEqual(BarrierModel.ExitEnergy(PenetrationMaterial.Wood, 3400f, 0.1f),
                BarrierModel.ExitEnergy(PenetrationMaterial.Wood, 2000f, 0.1f));
        }

        [Test]
        public void Evaluate_ExitDataConsistent()
        {
            var r = BarrierModel.Evaluate(PenetrationMaterial.Wood, 3400f, 840f, 0.1f, 0f, 0f, 1f, 0.7f, -0.3f);
            Assert.IsTrue(r.Penetrated);
            Assert.Less(r.ExitSpeed, 840f);
            Assert.Greater(r.ExitSpeed, 0f);
            Assert.That(r.DamageScale, Is.InRange(0f, 1f));
            var len = System.Math.Sqrt(r.DirX * r.DirX + r.DirY * r.DirY + r.DirZ * r.DirZ);
            Assert.AreEqual(1.0, len, 1e-3);
            Assert.Greater(r.DirZ, 0.95f);
            var stop = BarrierModel.Evaluate(PenetrationMaterial.Sandbag, 3400f, 840f, 0.1f, 0f, 0f, 1f, 0f, 0f);
            Assert.IsFalse(stop.Penetrated);
        }

        [Test]
        public void EffectiveThickness_GrowsAtGrazingAngles()
        {
            Assert.AreEqual(0.1f, BarrierModel.EffectiveThickness(0.1f, 90f), 1e-4f);
            Assert.Greater(BarrierModel.EffectiveThickness(0.1f, 30f), BarrierModel.EffectiveThickness(0.1f, 60f));
        }

        [Test]
        public void Plate_CentersOnly_ShoulderAndSideBypass()
        {
            Assert.AreEqual(ArmorCoverage.Plate, ArmorZones.Classify(0.05f, 0.45f, 10f));
            Assert.AreNotEqual(ArmorCoverage.Plate, ArmorZones.Classify(0.20f, 0.45f, 10f));   // omuz/yan kenar
            Assert.AreNotEqual(ArmorCoverage.Plate, ArmorZones.Classify(0.05f, 0.45f, 80f));   // yandan
            Assert.AreEqual(ArmorCoverage.Bypass, ArmorZones.Classify(0.35f, 0.45f, 10f));
            Assert.AreEqual(0f, ArmorZones.CoverageFactor(ArmorCoverage.Bypass, 0f));
            Assert.Greater(ArmorZones.CoverageFactor(ArmorCoverage.Plate, 0f), ArmorZones.CoverageFactor(ArmorCoverage.SoftArmor, 0f));
        }

        [Test]
        public void Azimuth_FrontBackSymmetric_SideIs90()
        {
            Assert.AreEqual(0f, ArmorZones.AzimuthDeg(0f, 1f, 0f, 1f), 1e-3f);
            Assert.AreEqual(0f, ArmorZones.AzimuthDeg(0f, -1f, 0f, 1f), 1e-3f);
            Assert.AreEqual(90f, ArmorZones.AzimuthDeg(1f, 0f, 0f, 1f), 1e-2f);
        }

        [Test]
        public void Helmet_RimVsFaceVsShell()
        {
            Assert.AreEqual(HelmetHitZone.Shell, ArmorZones.ClassifyHelmet(0.15f, 0f, true));
            Assert.AreEqual(HelmetHitZone.Rim, ArmorZones.ClassifyHelmet(0.04f, 0f, true));
            Assert.AreEqual(HelmetHitZone.Face, ArmorZones.ClassifyHelmet(-0.05f, 10f, true));
            Assert.AreEqual(HelmetHitZone.Shell, ArmorZones.ClassifyHelmet(-0.05f, 10f, false));
            Assert.Greater(ArmorZones.HelmetProtection(HelmetHitZone.Shell), ArmorZones.HelmetProtection(HelmetHitZone.Rim));
            Assert.Greater(ArmorZones.HelmetProtection(HelmetHitZone.Rim), ArmorZones.HelmetProtection(HelmetHitZone.Face));
        }
    }
}
