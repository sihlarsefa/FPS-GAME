using NUnit.Framework;
using Project.Presentation.UI;

namespace Project.Tests
{
    public sealed class LobbySoundscapeTests
    {
        [Test]
        public void MenuSceneOnly()
        {
            Assert.That(LobbySoundscapeRules.IsMenuScene("MainMenu"), Is.True);
            Assert.That(LobbySoundscapeRules.IsMenuScene("KuzgunVadisi"), Is.False);
        }

        [Test]
        public void RadioDelay_Within20To40()
        {
            Assert.That(LobbySoundscapeRules.NextRadioDelay(0f), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(LobbySoundscapeRules.NextRadioDelay(1f), Is.EqualTo(40f).Within(1e-4f));
            Assert.That(LobbySoundscapeRules.NextRadioDelay(5f), Is.EqualTo(40f).Within(1e-4f));
        }

        [Test]
        public void Scale_RespectsSettings()
        {
            Assert.That(LobbySoundscapeRules.Scale(0.5f, 0f), Is.EqualTo(0f));
            Assert.That(LobbySoundscapeRules.Scale(0.5f, 0.5f), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(LobbySoundscapeRules.Level(float.NaN), Is.EqualTo(1f));
        }
    }
}
