using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class MapCatalogTests
    {
        [Test]
        public void Normalize_UnknownOrEmpty_FallsBackToKuzgun()
        {
            Assert.AreEqual(MapCatalog.Kuzgun, MapCatalog.Normalize(null));
            Assert.AreEqual(MapCatalog.Kuzgun, MapCatalog.Normalize(""));
            Assert.AreEqual(MapCatalog.Kuzgun, MapCatalog.Normalize("bilinmeyen"));
            Assert.AreEqual(MapCatalog.Kuzgun, MapCatalog.Normalize("Atış Poligonu"));
        }

        [Test]
        public void Normalize_AcceptsIdAndDisplayName()
        {
            Assert.AreEqual(MapCatalog.AyazGecidi, MapCatalog.Normalize("ayaz"));
            Assert.AreEqual(MapCatalog.AyazGecidi, MapCatalog.Normalize("Ayaz Geçidi"));
            Assert.AreEqual(MapCatalog.Kuzgun, MapCatalog.Normalize("Kuzgun Vadisi"));
        }

        [Test]
        public void DisplayNamesAndIds_AreAligned()
        {
            var names = MapCatalog.DisplayNames();
            Assert.AreEqual(MapCatalog.Count, names.Length);
            for (var i = 0; i < names.Length; i++)
                Assert.AreEqual(i, MapCatalog.IndexOf(MapCatalog.IdAt(i)));
            Assert.AreEqual("Ayaz Geçidi", MapCatalog.DisplayName("ayaz"));
            Assert.AreEqual(500f, MapCatalog.HalfSize("ayaz"));
            Assert.AreEqual(512f, MapCatalog.HalfSize(null));
        }
    }
}
