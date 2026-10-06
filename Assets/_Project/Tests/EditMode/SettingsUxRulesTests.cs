using NUnit.Framework;
using Project.Presentation.UI;

namespace Project.Tests
{
    public sealed class SettingsUxRulesTests
    {
        [Test] public void FoldRemovesTurkishDiacritics() => Assert.That(SettingsUxRules.Fold("Çözünürlük ŞĞİı"), Is.EqualTo("cozunurluk sgii"));

        [Test]
        public void MatchesIsDiacriticInsensitiveAndMultiWord()
        {
            Assert.That(SettingsUxRules.Matches("Görüş alanı (FOV)", "gorus"), Is.True);
            Assert.That(SettingsUxRules.Matches("Görüş alanı (FOV)", "fov alan"), Is.True);
            Assert.That(SettingsUxRules.Matches("Ana ses", "grafik"), Is.False);
            Assert.That(SettingsUxRules.Matches("Ana ses", "  "), Is.True);
        }

        [Test]
        public void FovWarningOnlyAtExtremes()
        {
            Assert.That(SettingsUxRules.FovWarning(90f).Length, Is.EqualTo(0));
            Assert.That(SettingsUxRules.FovWarning(60f).Length, Is.GreaterThan(0));
            Assert.That(SettingsUxRules.FovWarning(105f).Length, Is.GreaterThan(0));
        }

        [Test]
        public void ViewmodelWarningAtRangeEnds()
        {
            Assert.That(SettingsUxRules.ViewmodelFovWarning(65f, 50f, 80f).Length, Is.EqualTo(0));
            Assert.That(SettingsUxRules.ViewmodelFovWarning(50f, 50f, 80f).Length, Is.GreaterThan(0));
            Assert.That(SettingsUxRules.ViewmodelFovWarning(80f, 50f, 80f).Length, Is.GreaterThan(0));
        }
    }
}
