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
        public void TanReadsTan()
        {
            var t = GunMaterials.Get("tan").Albedo;
            Assert.Greater(t.r, t.b + 0.15f);
            Assert.Greater(t.r, t.g);
        }
    }
}
#endif
