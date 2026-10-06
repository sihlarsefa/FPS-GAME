#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Rendering;

namespace Project.Tests.EditMode
{
    public sealed class MemoryJanitorRulesTests
    {
        [Test] public void Son2SahnedeKullanilanKorunur()
        {
            Assert.IsTrue(MemoryJanitorRules.ShouldKeep(5, 5));
            Assert.IsTrue(MemoryJanitorRules.ShouldKeep(4, 5));
            Assert.IsFalse(MemoryJanitorRules.ShouldKeep(3, 5));
            Assert.IsFalse(MemoryJanitorRules.ShouldKeep(-1, 5));
        }

        [Test] public void EvictableSayisi()
        {
            Assert.AreEqual(2, MemoryJanitorRules.CountEvictable(new[] { 1, 2, 4, 5 }, 5));
            Assert.AreEqual(0, MemoryJanitorRules.CountEvictable(null, 5));
        }

        [Test] public void YalnizMenuMacGecisindeTemizler()
        {
            Assert.IsTrue(MemoryJanitorRules.ShouldCleanup("MainMenu", "Operation", true, 100f));
            Assert.IsTrue(MemoryJanitorRules.ShouldCleanup("Operation", "MainMenu", true, 100f));
            Assert.IsFalse(MemoryJanitorRules.ShouldCleanup("Operation", "Training", true, 100f));
            Assert.IsFalse(MemoryJanitorRules.ShouldCleanup("MainMenu", "MainMenu", true, 100f));
        }

        [Test] public void SikTekrarVeAdditiveAtlanir()
        {
            Assert.IsFalse(MemoryJanitorRules.ShouldCleanup("MainMenu", "Operation", true, 5f));
            Assert.IsFalse(MemoryJanitorRules.ShouldCleanup("MainMenu", "Operation", false, 100f));
            Assert.IsFalse(MemoryJanitorRules.ShouldCleanup("MainMenu", "", true, 100f));
        }

        [Test] public void MenuSanatiYalnizMactaBirakilir()
        {
            Assert.IsTrue(MemoryJanitorRules.ShouldReleaseMenuArt("Operation"));
            Assert.IsFalse(MemoryJanitorRules.ShouldReleaseMenuArt("MainMenu"));
        }

        [Test] public void LogBicimi()
        {
            var s = MemoryJanitorRules.FormatLog("MainMenu", "Op", 300f, 250.5f);
            StringAssert.StartsWith("[BELLEK]", s);
            StringAssert.Contains("300 MB -> 250.5 MB", s);
        }
    }
}
#endif
