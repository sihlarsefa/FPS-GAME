#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Content;

namespace Project.Tests
{
    public class ContentValidationRulesTests
    {
        [Test]
        public void NameConvention_ParsesWeaponAndVehicle()
        {
            Assert.IsTrue(ContentNameConvention.TryParse("HK_W_ar_mpt76.prefab", out var k, out var id));
            Assert.AreEqual(ContentNameKind.Weapon, k);
            Assert.AreEqual("ar_mpt76", id);
            Assert.IsTrue(ContentNameConvention.TryParse("Assets/x/HK_V_t70", out k, out id));
            Assert.AreEqual(ContentNameKind.Vehicle, k);
            Assert.AreEqual("t70", id);
        }

        [Test]
        public void NameConvention_VegIsNotVehicle()
        {
            Assert.IsTrue(ContentNameConvention.TryParse("HK_VEG_pine_02", out var k, out var id));
            Assert.AreEqual(ContentNameKind.Vegetation, k);
            Assert.AreEqual("pine", ContentNameConvention.StripVariantSuffix(id));
        }

        [Test]
        public void NameConvention_RejectsOthers()
        {
            Assert.IsFalse(ContentNameConvention.TryParse("Tree", out _, out _));
            Assert.IsFalse(ContentNameConvention.TryParse("HK_W_", out _, out _));
            Assert.IsFalse(ContentNameConvention.TryParse(null, out _, out _));
        }

        [Test]
        public void Sockets_RequiredAreErrors_OptionalAreWarnings()
        {
            Assert.AreEqual(ContentSeverity.Hata, ContentValidationRules.CheckSocket("Muzzle", false));
            Assert.AreEqual(ContentSeverity.Uyari, ContentValidationRules.CheckSocket("Sight", false));
            Assert.AreEqual(ContentSeverity.Ok, ContentValidationRules.CheckSocket("Muzzle", true));
        }

        [Test]
        public void Scale_Levels()
        {
            Assert.AreEqual(ContentSeverity.Ok, ContentValidationRules.CheckScale(0.9f, 0.3f, 1.8f));
            Assert.AreEqual(ContentSeverity.Uyari, ContentValidationRules.CheckScale(2.5f, 0.3f, 1.8f));
            Assert.AreEqual(ContentSeverity.Hata, ContentValidationRules.CheckScale(90f, 0.3f, 1.8f));
            Assert.AreEqual(ContentSeverity.Hata, ContentValidationRules.CheckScale(0f, 0.3f, 1.8f));
        }

        [Test]
        public void Triangles_AndLod()
        {
            Assert.AreEqual(ContentSeverity.Ok, ContentValidationRules.CheckTriangles(100, 200));
            Assert.AreEqual(ContentSeverity.Uyari, ContentValidationRules.CheckTriangles(300, 200));
            Assert.AreEqual(ContentSeverity.Hata, ContentValidationRules.CheckTriangles(500, 200));
            Assert.AreEqual(ContentSeverity.Hata, ContentValidationRules.CheckLodGroup(ContentNameKind.Vegetation, false));
            Assert.AreEqual(ContentSeverity.Ok, ContentValidationRules.CheckLodGroup(ContentNameKind.Weapon, false));
        }

        [Test]
        public void Shader_AndLicense()
        {
            Assert.IsTrue(ContentValidationRules.IsUrpLitShader("Universal Render Pipeline/Lit"));
            Assert.IsFalse(ContentValidationRules.IsUrpLitShader("Standard"));
            var readme = "| MPT76Pack | https://x | CC0 |";
            Assert.IsTrue(ContentValidationRules.HasLicenseRecord(readme, "Foo", "Assets/ThirdParty/Weapons/MPT76Pack/a.fbx"));
            Assert.IsFalse(ContentValidationRules.HasLicenseRecord(readme, "Foo", "Assets/ThirdParty/Weapons/Other/a.fbx"));
        }
    }
}
#endif
