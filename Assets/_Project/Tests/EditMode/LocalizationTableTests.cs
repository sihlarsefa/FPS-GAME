using NUnit.Framework;
using Project.Application.Localization;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Tests
{
    public class LocalizationTableTests
    {
        [Test]
        public void Merge_ParsesStringsAndEscapes()
        {
            var t = new LocalizationTable();
            Assert.IsTrue(t.Merge("{ \"a.b\": \"Şehit \\\"x\\\"\\n\\u0041\", \"n\": 3, \"c.d\": \"y\" }"));
            Assert.IsTrue(t.TryGet("a.b", out var v));
            Assert.AreEqual("Şehit \"x\"\nA", v);
            Assert.IsTrue(t.TryGet("c.d", out v));
            Assert.AreEqual(2, t.Count);
        }

        [Test]
        public void Merge_BadJson_ReturnsFalse()
        {
            Assert.IsFalse(new LocalizationTable().Merge("{ \"a\": \"x"));
            Assert.IsFalse(new LocalizationTable().Merge(null));
        }

        [Test]
        public void NormalizeLanguage_FallsBackToTurkish()
        {
            Assert.AreEqual("tr", LocalizationTable.NormalizeLanguage("xx"));
            Assert.AreEqual("de", LocalizationTable.NormalizeLanguage("DE"));
        }

        [Test]
        public void Settings_Language_SanitizedAndDefault()
        {
            Assert.AreEqual("tr", new GameSettings().Language);
            var s = SettingsService.Sanitize(new GameSettings { Language = "zz" });
            Assert.AreEqual("tr", s.Language);
            Assert.AreEqual("ar", SettingsService.Sanitize(new GameSettings { Language = "ar" }).Language);
        }
    }
}
