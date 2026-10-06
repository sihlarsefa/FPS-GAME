using NUnit.Framework;

using Project.EditorTools;

namespace Project.Tests
{
    public sealed class PurchasedPackRulesTests
    {
        [TestCase("AK47_Rifle", PackWeaponClass.AssaultRifle)]
        [TestCase("M4A1", PackWeaponClass.AssaultRifle)]
        [TestCase("SniperRifle_02", PackWeaponClass.Sniper)]
        [TestCase("Shotgun_Benelli", PackWeaponClass.Shotgun)]
        [TestCase("Pistol_Glock", PackWeaponClass.Pistol)]
        [TestCase("MP5_SMG", PackWeaponClass.Smg)]
        [TestCase("M249_MachineGun", PackWeaponClass.Lmg)]
        [TestCase("Rifle_Magazine", PackWeaponClass.None)]
        [TestCase("Tree_01", PackWeaponClass.None)]
        [TestCase("", PackWeaponClass.None)]
        public void ClassifiesByName(string name, PackWeaponClass expected)
        {
            Assert.That(PurchasedPackRules.ClassifyWeapon(name), Is.EqualTo(expected));
        }

        [Test]
        public void TokenizeSplitsCamelDigitsAndSeparators()
        {
            CollectionAssert.AreEqual(new[] { "ak", "47", "rifle", "low" }, PurchasedPackRules.Tokenize("AK47_Rifle-low"));
        }

        [TestCase(0.9f, 0.25f, 0.06f, true)]
        [TestCase(0.06f, 0.25f, 0.9f, true)]
        [TestCase(0.2f, 0.14f, 0.03f, true)]
        [TestCase(1.8f, 0.3f, 0.1f, false)]
        [TestCase(0.1f, 0.05f, 0.02f, false)]
        [TestCase(0.8f, 0.7f, 0.6f, false)]
        public void WeaponBoundsNeedLongThin(float x, float y, float z, bool ok)
        {
            Assert.That(PurchasedPackRules.IsWeaponBounds(x, y, z), Is.EqualTo(ok));
        }

        [Test]
        public void HumanoidHighPolySoldierBeatsPlainMesh()
        {
            int hero = PurchasedPackRules.ScoreCharacter("Soldier_Hero", true, 24000, true, 1.8f);
            int plain = PurchasedPackRules.ScoreCharacter("Prop_Mesh", false, 24000, false, 1.8f);
            Assert.That(hero, Is.GreaterThan(plain));
            Assert.That(PurchasedPackRules.ScoreCharacter("Zombie", true, 8000, false, 1.8f),
                Is.LessThan(PurchasedPackRules.ScoreCharacter("Soldier", true, 8000, false, 1.8f)));
        }

        [Test]
        public void LodGroupAndTriangleThresholdAddScore()
        {
            Assert.That(PurchasedPackRules.ScoreCharacter("x", true, 6000, true, 1.8f),
                Is.GreaterThan(PurchasedPackRules.ScoreCharacter("x", true, 4000, false, 1.8f)));
        }

        [Test]
        public void ExcludedPathsSkipProjectAndThirdParty()
        {
            Assert.That(PurchasedPackRules.IsExcludedPath("Assets/_Project/x.fbx"), Is.True);
            Assert.That(PurchasedPackRules.IsExcludedPath("Assets/ThirdParty/Weapons/a.fbx"), Is.True);
            Assert.That(PurchasedPackRules.IsExcludedPath("Assets/Packs/Editor/a.fbx"), Is.True);
            Assert.That(PurchasedPackRules.IsExcludedPath("Assets/MilitaryPack/Prefabs/Soldier.prefab"), Is.False);
        }

        [Test]
        public void AssignIdsCyclesModelsAndMapsClassIds()
        {
            var a = PurchasedPackRules.AssignIds(PackWeaponClass.AssaultRifle, 2);
            Assert.That(a[0].Key, Is.EqualTo("ar_mpt76"));
            Assert.That(a[0].Value, Is.EqualTo(0));
            Assert.That(a[1].Value, Is.EqualTo(1));
            Assert.That(a[2].Value, Is.EqualTo(0));
            Assert.That(PurchasedPackRules.AssignIds(PackWeaponClass.Sniper, 1)[0].Key, Is.EqualTo("sr_jng90"));
            Assert.That(PurchasedPackRules.AssignIds(PackWeaponClass.Lmg, 0).Count, Is.EqualTo(0));
        }

        [Test]
        public void PriorityKeepsHigherScoringPurchasedOverride()
        {
            Assert.That(PurchasedPackRules.ShouldReplace(false, -1, 10, false), Is.True);  // Quaternius -> paket
            Assert.That(PurchasedPackRules.ShouldReplace(true, 60, 50, false), Is.False);  // iyi paket korunur
            Assert.That(PurchasedPackRules.ShouldReplace(true, 50, 60, false), Is.True);
            Assert.That(PurchasedPackRules.ShouldReplace(true, 99, 1, true), Is.True);
            Assert.That(PurchasedPackRules.ParseScoreLabel("pk_score_55"), Is.EqualTo(55));
            Assert.That(PurchasedPackRules.ParseScoreLabel("bounds_x"), Is.EqualTo(-1));
        }
    }
}
