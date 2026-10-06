using NUnit.Framework;
using Project.Application.Services;
using Project.Infrastructure.AI;
using Project.Presentation.UI;

namespace Project.Tests.EditMode
{
    /// <summary>G26 entegrasyon: bot kapatıcı pencere / bomba aralığı geçişleri ve HUD uzuv göstergesi saf kuralları.</summary>
    public sealed class G26IntegrationTests
    {
        // ---------------------------------------------------------------- bot: kapatıcı pencere + bomba aralığı

        [Test]
        public void Suppress_DefaultWindowUnchanged_RolledWindowExtends()
        {
            Assert.IsFalse(BotSquadTactics.ShouldSuppress(6f, true, 0.8f, 0.9f));            // eski sabit pencere 3,5 sn
            Assert.IsTrue(BotSquadTactics.ShouldSuppress(6f, true, 0.8f, 0.9f, 8f));         // RC2 v3 4-8 sn penceresi
            Assert.IsFalse(BotSquadTactics.ShouldSuppress(9f, true, 0.8f, 0.9f, 8f));
            Assert.IsFalse(BotSquadTactics.ShouldSuppress(0.1f, true, 0.8f, 0.9f, 8f));       // hedef daha yeni kayboldu
        }

        [Test]
        public void GrenadeWorthwhile_BrainRange_IsTwelveToThirty()
        {
            var min = BotCombatRules.FragMinRange;
            var max = BotCombatRules.FragMaxRange;
            Assert.IsFalse(BotSquadTactics.GrenadeWorthwhile(10f, true, 40f, 6f, min, max));
            Assert.IsTrue(BotSquadTactics.GrenadeWorthwhile(12f, true, 40f, 6f, min, max));
            Assert.IsTrue(BotSquadTactics.GrenadeWorthwhile(28f, true, 40f, 6f, min, max));
            Assert.IsFalse(BotSquadTactics.GrenadeWorthwhile(31f, true, 40f, 6f, min, max));
            // Varsayılan (eski 9-25 m) davranışı korunur.
            Assert.IsTrue(BotSquadTactics.GrenadeWorthwhile(10f, true, 40f, 6f));
            Assert.IsFalse(BotSquadTactics.GrenadeWorthwhile(28f, true, 40f, 6f));
        }

        // ---------------------------------------------------------------- HUD uzuv göstergesi

        [Test]
        public void LimbCaptions_DeriveFromRules_InTurkish()
        {
            Assert.AreEqual("-%25 HIZ", LimbStateRules.LegCaption(1));
            Assert.AreEqual("-%40 HIZ", LimbStateRules.LegCaption(2));   // üst sınır
            Assert.AreEqual("+%30 SARSINTI", LimbStateRules.ArmCaption(1));
            Assert.AreEqual("+%60 SARSINTI", LimbStateRules.ArmCaption(2));
            Assert.AreEqual("-1 CAN/3sn · 12sn", LimbStateRules.BleedCaption(11.2f));
            Assert.AreEqual("-1 CAN/3sn · 1sn", LimbStateRules.BleedCaption(0f));
        }

        [Test]
        public void LimbTooltips_AreFullSentences_WithBandageHint()
        {
            StringAssert.Contains("Bacak yarası ×1", LimbStateRules.LegTooltip(1));
            StringAssert.Contains("-%25", LimbStateRules.LegTooltip(1));
            StringAssert.Contains("Sargı Bezi", LimbStateRules.LegTooltip(1));
            StringAssert.Contains("Kol yarası ×2", LimbStateRules.ArmTooltip(2));
            StringAssert.Contains("ADS", LimbStateRules.ArmTooltip(2));
            StringAssert.Contains("Kanama", LimbStateRules.BleedTooltip(9f));
            StringAssert.Contains("9 sn", LimbStateRules.BleedTooltip(9f));
        }

        [Test]
        public void LimbSeverity_AndPulse_AreBounded()
        {
            Assert.AreEqual(0, LimbStateRules.Severity(0));
            Assert.AreEqual(1, LimbStateRules.Severity(1));
            Assert.AreEqual(2, LimbStateRules.Severity(2));
            for (var t = 0f; t < 10f; t += 0.07f)
                Assert.That(LimbStateRules.BleedPulse(t), Is.InRange(0.54f, 1.001f));
        }

        [Test]
        public void DropSilhouette_RoundBottom_PointedTop()
        {
            Assert.IsTrue(LimbStateRules.InsideDrop(0.5f, 0.34f));    // yuvarlak merkez
            Assert.IsTrue(LimbStateRules.InsideDrop(0.5f, 0.9f));     // sivri uç gövdesi
            Assert.IsFalse(LimbStateRules.InsideDrop(0.5f, 0.99f));   // uç üstü
            Assert.IsFalse(LimbStateRules.InsideDrop(0.9f, 0.9f));    // yanlar boş
            Assert.IsFalse(LimbStateRules.InsideDrop(0.5f, 0.02f));   // alt boşluk
        }
    }
}
