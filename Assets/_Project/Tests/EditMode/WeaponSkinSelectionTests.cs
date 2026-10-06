#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Weapons.Skins;

namespace Project.Tests.EditMode
{
    public sealed class WeaponSkinSelectionTests
    {
        [Test]
        public void KeyFor_UsesPrefixAndFallback()
        {
            Assert.AreEqual("harekat.skin.mpt55", WeaponSkinSelection.KeyFor("mpt55"));
            Assert.AreEqual("harekat.skin.genel", WeaponSkinSelection.KeyFor(null));
        }

        [Test]
        public void IndexOf_FindsCatalogEntries()
        {
            Assert.AreEqual(0, WeaponSkinSelection.IndexOf("col_kamuflaj"));
            Assert.AreEqual(-1, WeaponSkinSelection.IndexOf("none"));
            Assert.AreEqual(-1, WeaponSkinSelection.IndexOf("yok_boyle"));
            Assert.AreEqual(-1, WeaponSkinSelection.IndexOf(null));
        }

        [Test]
        public void Catalog_FirstSkinIsCorrectlySpelled()
        {
            Assert.AreEqual("Çöl Kamuflaj", WeaponSkinCatalog.All[0].Name);
        }
    }
}
#endif
