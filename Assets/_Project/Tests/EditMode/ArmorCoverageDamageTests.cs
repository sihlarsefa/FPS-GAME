using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    /// <summary>G26 entegrasyon: ArmorZones kapsaması hasar hesabında (plaka orta gövde, miğfer kabuk/kenar/yüz).</summary>
    public sealed class ArmorCoverageDamageTests
    {
        private const float Eps = 1e-3f;

        private static ArmorPiece Armor(float reduction) => new ArmorPiece("armor_test", 2, 100f, reduction, 100f);

        [Test]
        public void FullCoverage_MatchesLegacyDamage()
        {
            var w = WeaponCatalog.Get(WeaponIds.Mpt76);
            var legacy = DamageCalculator.ComputeBulletDamage(w, BodyPart.Torso, 10f, Armor(0.5f));
            var full = DamageCalculator.ComputeBulletDamage(w, BodyPart.Torso, 10f, Armor(0.5f), 1f, 1f);
            Assert.AreEqual(legacy.Damage, full.Damage, Eps);
            Assert.AreEqual(legacy.ArmorAbsorbed, full.ArmorAbsorbed, Eps);
        }

        [Test]
        public void ZeroCoverage_BypassesArmor_AndDoesNotWearIt()
        {
            var w = WeaponCatalog.Get(WeaponIds.Mpt76);
            var vest = Armor(0.5f);
            var r = DamageCalculator.ComputeBulletDamage(w, BodyPart.Torso, 10f, vest, 1f, 0f);
            Assert.AreEqual(w.Damage, r.Damage, Eps);
            Assert.AreEqual(0f, r.ArmorAbsorbed, Eps);
            Assert.AreEqual(100f, vest.Durability, Eps);
        }

        [Test]
        public void PartialCoverage_AbsorbsBetweenNoneAndFull()
        {
            var w = WeaponCatalog.Get(WeaponIds.Mpt76);
            var none = DamageCalculator.ComputeBulletDamage(w, BodyPart.Torso, 10f, Armor(0.5f), 1f, 0f).Damage;
            var full = DamageCalculator.ComputeBulletDamage(w, BodyPart.Torso, 10f, Armor(0.5f), 1f, 1f).Damage;
            var half = DamageCalculator.ComputeBulletDamage(w, BodyPart.Torso, 10f, Armor(0.5f), 1f, 0.5f).Damage;
            Assert.Less(full, half);
            Assert.Less(half, none);
        }

        [Test]
        public void CoverageOutsideRange_IsClamped()
        {
            var w = WeaponCatalog.Get(WeaponIds.Mpt76);
            var over = DamageCalculator.ComputeBulletDamage(w, BodyPart.Torso, 10f, Armor(0.5f), 1f, 5f).Damage;
            var full = DamageCalculator.ComputeBulletDamage(w, BodyPart.Torso, 10f, Armor(0.5f), 1f, 1f).Damage;
            var under = DamageCalculator.ComputeBulletDamage(w, BodyPart.Torso, 10f, Armor(0.5f), 1f, -3f).Damage;
            Assert.AreEqual(full, over, Eps);
            Assert.AreEqual(w.Damage, under, Eps);
        }

        [Test]
        public void TorsoFactor_PlateCenterStrong_ShoulderAndSideBypass()
        {
            var center = ArmorZones.TorsoFactor(0.05f, 0.45f, 10f);
            var softSide = ArmorZones.TorsoFactor(0.20f, 0.45f, 10f);   // omuz/yan kenar: yumuşak zırh
            var flank = ArmorZones.TorsoFactor(0.05f, 0.45f, 80f);      // tam yandan: plaka atlar
            var outside = ArmorZones.TorsoFactor(0.35f, 0.45f, 10f);    // kol hizası: zırh yok
            Assert.Greater(center, 0.7f);
            Assert.Greater(center, softSide);
            Assert.Greater(softSide, 0f);
            Assert.AreEqual(softSide, flank, Eps);
            Assert.AreEqual(0f, outside, Eps);
        }

        [Test]
        public void HelmetFactor_ShellFull_RimPartial_FaceNone()
        {
            Assert.AreEqual(1f, ArmorZones.HelmetFactor(0.15f, 0f, true), Eps);
            var rim = ArmorZones.HelmetFactor(0.04f, 0f, true);
            Assert.That(rim, Is.InRange(0.01f, 0.99f));
            Assert.AreEqual(0f, ArmorZones.HelmetFactor(-0.05f, 10f, true), Eps);
            Assert.AreEqual(1f, ArmorZones.HelmetFactor(-0.05f, 10f, false), Eps); // arkadan alçak vuruş kabuğa gider
        }

        [Test]
        public void FaceShot_TakesFullHeadshotDamage_ShellShotIsReduced()
        {
            var w = WeaponCatalog.Get(WeaponIds.Mpt76);
            var face = DamageCalculator.ComputeBulletDamage(w, BodyPart.Head, 10f, Armor(0.5f), 1f, ArmorZones.HelmetFactor(-0.05f, 10f, true));
            var shell = DamageCalculator.ComputeBulletDamage(w, BodyPart.Head, 10f, Armor(0.5f), 1f, ArmorZones.HelmetFactor(0.15f, 0f, true));
            Assert.AreEqual(w.Damage * 2f, face.Damage, Eps);
            Assert.AreEqual(0f, face.ArmorAbsorbed, Eps);
            Assert.Less(shell.Damage, face.Damage);
            Assert.Greater(shell.ArmorAbsorbed, 0f);
        }
    }
}
