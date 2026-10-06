using NUnit.Framework;
using Project.Infrastructure.Weapons;
using Project.Infrastructure.Weapons.Skins;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class WeaponSkinTests
    {
        [Test]
        public void Catalog_HasSixUniqueSkins()
        {
            Assert.AreEqual(6, WeaponSkinCatalog.All.Length);
            for (int i = 0; i < WeaponSkinCatalog.All.Length; i++)
            {
                var s = WeaponSkinCatalog.All[i];
                Assert.IsFalse(string.IsNullOrEmpty(s.Id));
                Assert.IsFalse(string.IsNullOrEmpty(s.Name));
                for (int j = i + 1; j < WeaponSkinCatalog.All.Length; j++)
                    Assert.IsFalse(s.Id == WeaponSkinCatalog.All[j].Id);
            }
            WeaponSkin x;
            Assert.IsTrue(WeaponSkinCatalog.TryGet("bordo_bere", out x));
            Assert.IsFalse(WeaponSkinCatalog.TryGet("yok", out x));
        }

        [Test]
        public void Catalog_ColorAndScalarBounds()
        {
            foreach (var s in WeaponSkinCatalog.All)
            {
                float l = GunMaterials.Luma(s.Primary);
                Assert.IsTrue(l >= GunMaterials.MinAlbedoLuma - 0.03f && l <= GunMaterials.MaxAlbedoLuma);
                Assert.IsTrue(s.Wear >= 0f && s.Wear <= 1f);
                Assert.IsTrue(s.Metallic >= 0f && s.Metallic <= GunMaterials.MetallicCap);
                Assert.IsTrue(s.Smoothness >= 0f && s.Smoothness <= 1f);
            }
        }

        [Test]
        public void Bordo_HasRedAccent()
        {
            WeaponSkin s;
            Assert.IsTrue(WeaponSkinCatalog.TryGet("bordo_bere", out s));
            Assert.Greater(s.Secondary.r, s.Secondary.g * 3f);
        }

        [Test]
        public void TextureSizes_FollowTier()
        {
            Assert.AreEqual(256, WeaponSkinTextures.SizeForTier(0));
            Assert.AreEqual(1024, WeaponSkinTextures.SizeForTier(3));
            Assert.AreEqual(1024, WeaponSkinTextures.SizeForTier(9));
        }

        [Test]
        public void EdgeWear_HigherAtEdgeThanCenter()
        {
            float edge = WeaponSkinTextures.EdgeWear(0, 128, 256, 0.8f, 1);
            float mid = WeaponSkinTextures.EdgeWear(128, 128, 256, 0.8f, 1);
            Assert.Greater(edge, mid);
            Assert.AreEqual(0f, WeaponSkinTextures.EdgeWear(0, 128, 256, 0f, 1), 0.0001f);
        }
    }
}
