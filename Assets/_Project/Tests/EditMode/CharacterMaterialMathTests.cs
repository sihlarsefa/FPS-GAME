#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Characters;

namespace Project.Tests.EditMode
{
    public sealed class CharacterMaterialMathTests
    {
        [Test]
        public void WrapDiffuse_ZeroWrapIsLambert()
        {
            Assert.AreEqual(0.5f, CharacterMaterialMath.WrapDiffuse(0.5f, 0f), 1e-5f);
            Assert.AreEqual(0f, CharacterMaterialMath.WrapDiffuse(-0.3f, 0f), 1e-5f);
        }

        [Test]
        public void WrapDiffuse_LightsTerminatorAndStaysInRange()
        {
            Assert.Greater(CharacterMaterialMath.WrapDiffuse(0f, 0.5f), 0f);
            Assert.AreEqual(1f, CharacterMaterialMath.WrapDiffuse(1f, 0.5f), 1e-5f);
            for (var n = -1f; n <= 1f; n += 0.1f)
                Assert.GreaterOrEqual(CharacterMaterialMath.WrapExtra(n, 0.5f), -1e-5f);
        }

        [Test]
        public void DirtGradient_IncreasesTowardsFeet()
        {
            Assert.AreEqual(0f, CharacterMaterialMath.DirtGradient(0.1f, -0.05f, 0.4f), 1e-5f);
            Assert.AreEqual(1f, CharacterMaterialMath.DirtGradient(-0.6f, -0.05f, 0.4f), 1e-5f);
            Assert.Greater(CharacterMaterialMath.DirtGradient(-0.3f, -0.05f, 0.4f), CharacterMaterialMath.DirtGradient(-0.1f, -0.05f, 0.4f));
        }

        [Test]
        public void SpecularOcclusion_FullAoGivesOneAndCavityDarkens()
        {
            Assert.AreEqual(1f, CharacterMaterialMath.SpecularOcclusion(0.7f, 1f, 0.5f), 1e-4f);
            Assert.Less(CharacterMaterialMath.SpecularOcclusion(0.7f, 0.3f, 0.5f), 1f);
            var v = CharacterMaterialMath.SpecularOcclusion(0.4f, 0.5f, 0.2f);
            Assert.That(v, Is.InRange(0f, 1f));
        }

        [Test]
        public void Wetness_DarkensFabricAndRaisesSmoothness()
        {
            var wet = CharacterMaterialMath.Wet(1f, 0.8f);
            Assert.AreEqual(0.8f, wet, 1e-5f);
            Assert.Less(CharacterMaterialMath.WetAlbedoScale(wet, 0.6f), 1f);
            Assert.AreEqual(1f, CharacterMaterialMath.WetAlbedoScale(0f, 0.6f), 1e-5f);
            Assert.Greater(CharacterMaterialMath.WetSmoothness(0.1f, 1f, true), 0.1f);
            Assert.Greater(CharacterMaterialMath.WetSmoothness(0.2f, 1f, false), 0.6f);
            Assert.AreEqual(0.2f, CharacterMaterialMath.WetSmoothness(0.2f, 0f, false), 1e-5f);
        }

        [Test]
        public void EdgeWear_NeedsSlopeAndStrength()
        {
            Assert.AreEqual(0f, CharacterMaterialMath.EdgeWear(0f, 1f, 1f), 1e-5f);
            Assert.AreEqual(0f, CharacterMaterialMath.EdgeWear(0.5f, 1f, 0f), 1e-5f);
            Assert.Greater(CharacterMaterialMath.EdgeWear(0.4f, 0.8f, 0.45f), 0f);
        }

        [Test]
        public void Dirt_IsClampedAndDustFollowsUpFacing()
        {
            Assert.AreEqual(1f, CharacterMaterialMath.Dirt(1f, 1f, 1f, 1f, 1f), 1e-5f);
            Assert.Greater(CharacterMaterialMath.Dirt(0f, 0.8f, 1f, 0.3f, 1f), CharacterMaterialMath.Dirt(0f, 0.8f, 0f, 0.3f, 1f));
        }

        [Test]
        public void Defaults_AreSane_ForEveryKind()
        {
            foreach (CharacterMaterialKind k in System.Enum.GetValues(typeof(CharacterMaterialKind)))
            {
                var p = CharacterMaterialMath.Defaults(k);
                Assert.That(p.Smoothness, Is.InRange(0f, 1f), k.ToString());
                Assert.That(p.DirtStrength, Is.InRange(0f, 1f), k.ToString());
                Assert.That(p.WetDarken, Is.InRange(0.2f, 1f), k.ToString());
                Assert.Greater(p.DetailTiling, 1f, k.ToString());
            }

            Assert.Greater(CharacterMaterialMath.Defaults(CharacterMaterialKind.Fabric).SheenStrength, 0f);
            Assert.Greater(CharacterMaterialMath.Defaults(CharacterMaterialKind.Skin).SssStrength, 0f);
            Assert.Greater(CharacterMaterialMath.Defaults(CharacterMaterialKind.HelmetPaint).EdgeWear, 0f);
            Assert.Greater(CharacterMaterialMath.Defaults(CharacterMaterialKind.NvgLens).LensEmission, 0f);
            Assert.Greater(CharacterMaterialMath.Defaults(CharacterMaterialKind.Leather).DirtStrength,
                CharacterMaterialMath.Defaults(CharacterMaterialKind.Skin).DirtStrength);
        }

        [Test]
        public void TextureHeights_AreDeterministicBoundedAndVaried()
        {
            foreach (CharacterMaterialKind k in System.Enum.GetValues(typeof(CharacterMaterialKind)))
            {
                var a = CharacterTextureGen.Height(k, 64, 3);
                var b = CharacterTextureGen.Height(k, 64, 3);
                Assert.AreEqual(64 * 64, a.Length);
                float min = 1f, max = 0f;
                for (var i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(a[i], b[i]);
                    Assert.That(a[i], Is.InRange(0f, 1f));
                    if (a[i] < min) min = a[i];
                    if (a[i] > max) max = a[i];
                }

                if (k != CharacterMaterialKind.NvgLens)
                    Assert.Greater(max - min, 0.1f, k.ToString());
            }
        }

        [Test]
        public void ShaderPaths_AreHarekatCharacter()
        {
            // Güvenli mod (varsayılan): Skin/Fabric URP/Lit kullanır (bkz. CharacterMaterials.UseCustomClothSkinShaders).
            // Tip başlatıcı Unity yerel köprüsü ister; koşucuda yoksa test atlanır.
            try
            {
                foreach (CharacterMaterialKind k in System.Enum.GetValues(typeof(CharacterMaterialKind)))
                {
                    var p = CharacterMaterials.ShaderPath(k);
                    var beklenen = k == CharacterMaterialKind.Skin || k == CharacterMaterialKind.Fabric
                        ? "Universal Render Pipeline/Lit"
                        : null;
                    if (beklenen != null)
                        Assert.AreEqual(beklenen, p);
                    else
                        StringAssert.StartsWith("HAREKAT/Character/", p);
                }
            }
            catch (System.TypeInitializationException)
            {
                Assert.Ignore("Unity yerel köprüsü gerekir");
            }
        }

        private static float MeanSrgbLuminance(UnityEngine.Texture2D t)
        {
            var px = t.GetPixels32();
            double sum = 0;
            foreach (var c in px)
                sum += (c.r * 0.2126 + c.g * 0.7152 + c.b * 0.0722) / 255.0;
            return (float)(sum / px.Length);
        }

        [Test]
        public void DigitalCamoFabric_Orman_MeanLuminanceIsOliveDark()
        {
            var tex = Project.Infrastructure.Rendering.ProceduralTextures.DigitalCamoFabric(
                new UnityEngine.Color(0.42f, 0.41f, 0.25f), new UnityEngine.Color(0.31f, 0.22f, 0.14f),
                new UnityEngine.Color(0.27f, 0.31f, 0.18f), new UnityEngine.Color(0.66f, 0.58f, 0.42f), 1);
            var lum = MeanSrgbLuminance(tex);
            Assert.Less(lum, 0.6f, "kamuflaj dokusu fazla parlak");
            Assert.That(lum, Is.InRange(0.25f, 0.45f));
            Assert.IsFalse(tex.isDataSRGB == false, "kamuflaj albedo dokusu sRGB olmalı (linear=true parlatır)");
        }

        [Test]
        public void DigitalCamoFabric_OtherPalettes_NotBlownOut()
        {
            var tex = Project.Infrastructure.Rendering.ProceduralTextures.DigitalCamoFabric(
                new UnityEngine.Color(0.47f, 0.47f, 0.44f), new UnityEngine.Color(0.35f, 0.36f, 0.33f),
                new UnityEngine.Color(0.25f, 0.27f, 0.23f), new UnityEngine.Color(0.62f, 0.62f, 0.58f), 2);
            Assert.Less(MeanSrgbLuminance(tex), 0.6f);
        }

        [Test]
        public void GearBoost_BrightensGearOnly_AndStaysInRange()
        {
            var olive = new UnityEngine.Color(0.33f, 0.34f, 0.21f);
            var g = CharacterMaterials.GearBoost(CharacterMaterialKind.Cordura, olive);
            Assert.Greater(g.g, olive.g);
            Assert.That(g.r, Is.InRange(0f, 1f));
            Assert.That(g.g, Is.InRange(0.35f, 0.5f));
            Assert.AreEqual(olive, CharacterMaterials.GearBoost(CharacterMaterialKind.Fabric, olive));
            Assert.AreEqual(1f, CharacterMaterials.GearBoost(CharacterMaterialKind.Rubber, UnityEngine.Color.white).r, 1e-5f);
        }

        [Test]
        public void Defaults_GearDirtAndSheenWithinBounds()
        {
            foreach (CharacterMaterialKind k in System.Enum.GetValues(typeof(CharacterMaterialKind)))
            {
                var p = CharacterMaterialMath.Defaults(k);
                Assert.That(p.SheenStrength, Is.InRange(0f, 0.5f), k.ToString());
                Assert.That(p.EdgeWear, Is.InRange(0f, 1f), k.ToString());
                Assert.That(p.Metallic, Is.InRange(0f, 1f), k.ToString());
                if (k != CharacterMaterialKind.Skin && k != CharacterMaterialKind.Fabric && k != CharacterMaterialKind.NvgLens)
                    Assert.LessOrEqual(p.DirtStrength, 0.9f, k.ToString());
            }
        }
    }
}
#endif
