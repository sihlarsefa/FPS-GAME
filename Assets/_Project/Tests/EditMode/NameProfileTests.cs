using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class NameProfileTests
    {
        [TearDown]
        public void Reset()
        {
            NameProfile.Current = NameProfileKind.Gercek;
            NameProfile.Resolver = null;
        }

        [Test]
        public void GercekProfilEskiAdlariKorur()
        {
            NameProfile.Current = NameProfileKind.Gercek;
            Assert.AreEqual("SAR 9", WeaponCatalog.GetDisplayName(WeaponIds.Sar9));
            Assert.AreEqual("Kirpi Zırhlı Aracı", NameProfile.Get(NameProfile.VehicleKirpi, "x"));
        }

        [Test]
        public void KurgusalProfilAdiDegistirir()
        {
            NameProfile.Current = NameProfileKind.Kurgusal;
            Assert.AreEqual("Kartal-9", WeaponCatalog.GetDisplayName(WeaponIds.Sar9));
            Assert.AreNotEqual("T-70 Helikopteri", NameProfile.Get(NameProfile.VehicleHeli, "T-70 Helikopteri"));
        }

        [Test]
        public void TumSilahlarinKurgusalAdiVarVeFarkli()
        {
            foreach (var w in WeaponCatalog.All)
            {
                Assert.IsTrue(NameProfile.Has(w.WeaponId), w.WeaponId);
                Assert.AreNotEqual(NameProfile.Get(w.WeaponId, "?", NameProfileKind.Gercek), NameProfile.Get(w.WeaponId, "?", NameProfileKind.Kurgusal), w.WeaponId);
            }
        }

        [Test]
        public void KimlikleriProfilDegistirmez()
        {
            var before = WeaponCatalog.All.Count;
            NameProfile.Current = NameProfileKind.Kurgusal;
            Assert.AreEqual(before, WeaponCatalog.All.Count);
            Assert.IsTrue(WeaponCatalog.Contains(WeaponIds.Sar9));
        }

        [Test]
        public void CozucuAnahtariOncelikliVeYedekCalisir()
        {
            NameProfile.Resolver = k => k == "name.k.pistol_sar9" ? "Test Ad" : null;
            Assert.AreEqual("Test Ad", NameProfile.Get(WeaponIds.Sar9, "f", NameProfileKind.Kurgusal));
            Assert.AreEqual("SAR 9", NameProfile.Get(WeaponIds.Sar9, "f", NameProfileKind.Gercek));
            Assert.AreEqual("yedek", NameProfile.Get("bilinmeyen", "yedek"));
        }

        [Test]
        public void AyarKirpilirVeUygulaProfiliAyarlar()
        {
            var s = new GameSettings { NameProfile = 7 };
            Assert.AreEqual(NameProfileKind.Gercek, NameProfile.Normalize(5));
            Assert.AreEqual(NameProfileKind.Kurgusal, NameProfile.Normalize(1));
            Assert.AreEqual("name.k.x", NameProfile.Key("x", NameProfileKind.Kurgusal));
            Assert.AreEqual(7, s.NameProfile);
        }
    }
}
