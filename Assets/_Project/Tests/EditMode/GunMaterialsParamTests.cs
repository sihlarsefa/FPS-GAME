#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Weapons;

namespace Project.Tests.EditMode
{
    public sealed class GunMaterialsParamTests
    {
        [Test]
        public void Table_AlbedoReadableAndMetalCapped()
        {
            foreach (var p in GunMaterials.Table)
            {
                float l = GunMaterials.Luma(p.Albedo);
                Assert.GreaterOrEqual(l, GunMaterials.MinAlbedoLuma, p.Key);
                Assert.LessOrEqual(l, GunMaterials.MaxAlbedoLuma, p.Key);
                Assert.LessOrEqual(p.Metallic, GunMaterials.MetallicCap, p.Key);
            }
        }

        [Test]
        public void Table_NoBluePurpleCast()
        {
            foreach (var p in GunMaterials.Table)
            {
                Assert.IsTrue(p.Albedo.b <= p.Albedo.r + 0.02f && p.Albedo.b <= p.Albedo.g + 0.02f, p.Key);
                if (p.Key == "steel" || p.Key == "anod" || p.Key == "poly")
                {
                    Assert.Less(p.Albedo.r, 0.12f, p.Key);
                    Assert.LessOrEqual(p.Metallic, 0.5f, p.Key);
                    Assert.LessOrEqual(p.Smoothness, 0.5f, p.Key);
                }
            }

            Assert.AreEqual(0f, GunMaterials.Get("poly").Metallic, 0.001f);
        }

        [Test]
        public void TanReadsTan()
        {
            var t = GunMaterials.Get("tan").Albedo;
            Assert.Greater(t.r, t.b + 0.15f);
            Assert.Greater(t.r, t.g);
        }
    }
}
#endif
