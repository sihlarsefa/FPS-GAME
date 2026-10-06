#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public class ProceduralPbrTests
    {
        private static readonly PbrSurface[] All =
        {
            PbrSurface.Stone, PbrSurface.Plaster, PbrSurface.Brick, PbrSurface.Wood, PbrSurface.Metal,
            PbrSurface.Concrete, PbrSurface.CamoFabric, PbrSurface.GunMetal, PbrSurface.Polymer, PbrSurface.Sand
        };

        [Test]
        public void Generate_IsDeterministic_AndHasVariation()
        {
            foreach (var s in All)
            {
                var a = ProceduralPbr.Generate(s, 64, 5);
                var b = ProceduralPbr.Generate(s, 64, 5);
                CollectionAssert.AreEqual(a.Height, b.Height, s.ToString());
                var min = 1f; var max = 0f;
                foreach (var h in a.Height) { min = Mathf.Min(min, h); max = Mathf.Max(max, h); }
                Assert.Greater(max - min, 0.05f, s + " height variation");
            }
        }

        [Test]
        public void Normal_FlatHeight_PointsUp_AndIsUnitLength()
        {
            var flat = new float[16 * 16];
            var n = ProceduralPbr.NormalFromHeight(flat, 16, 5f);
            Assert.AreEqual(128, n[0].r, 1);
            Assert.AreEqual(128, n[0].g, 1);
            Assert.AreEqual(255, n[0].b, 1);

            var px = ProceduralPbr.Generate(PbrSurface.Stone, 64, 1);
            foreach (var c in ProceduralPbr.NormalFromHeight(px.Height, 64, px.NormalStrength))
            {
                var x = c.r / 255f * 2f - 1f; var y = c.g / 255f * 2f - 1f; var z = c.b / 255f * 2f - 1f;
                Assert.AreEqual(1f, Mathf.Sqrt(x * x + y * y + z * z), 0.05f);
                Assert.AreEqual(255, c.a);
            }
        }

        [Test]
        public void Normal_Slope_TiltsAgainstGradient()
        {
            var h = new float[16 * 16];
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 16; x++)
                    h[y * 16 + x] = x < 8 ? x / 8f : (16 - x) / 8f; // sırt: sol yarı yükseliyor
            var n = ProceduralPbr.NormalFromHeight(h, 16, 8f);
            Assert.Less(n[4 * 16 + 4].r, 128);
            Assert.Greater(n[4 * 16 + 12].r, 128);
        }

        [Test]
        public void Mask_IsMetalOnlyForMetalSurfaces()
        {
            var gun = ProceduralPbr.Generate(PbrSurface.GunMetal, 32, 2);
            var wood = ProceduralPbr.Generate(PbrSurface.Wood, 32, 2);
            Assert.Greater(gun.Mask[10].r, 180);
            Assert.AreEqual(0, wood.Mask[10].r);
        }

        [Test]
        public void SurfaceFor_MapsKeyMaterials()
        {
            Assert.AreEqual(PbrSurface.Brick, ProceduralPbr.SurfaceFor(MaterialId.Brick));
            Assert.AreEqual(PbrSurface.GunMetal, ProceduralPbr.SurfaceFor(MaterialId.GunMetal));
            Assert.AreEqual(PbrSurface.CamoFabric, ProceduralPbr.SurfaceFor(MaterialId.CamoWoodland));
            Assert.AreEqual(PbrSurface.None, ProceduralPbr.SurfaceFor(MaterialId.Glass));
        }

        [Test]
        public void Get_CachesAndBuildsLinearNormal()
        {
            var a = ProceduralPbr.Get(PbrSurface.Plaster, 3, 64);
            var b = ProceduralPbr.Get(PbrSurface.Plaster, 3, 64);
            Assert.AreSame(a, b);
            Assert.IsTrue(a.IsValid);
            Assert.AreEqual(64, a.Normal.width);
        }
    }
}
#endif
