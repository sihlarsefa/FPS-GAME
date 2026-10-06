using NUnit.Framework;
using Project.Core.Domain;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class HudRulesTests
    {
        [Test]
        public void AmmoAlpha_FullThenFadesToIdle()
        {
            Assert.AreEqual(1f, HudRules.AmmoAlpha(0f), 1e-4f);
            Assert.AreEqual(1f, HudRules.AmmoAlpha(HudRules.AmmoIdleDelay), 1e-4f);
            Assert.AreEqual(HudRules.AmmoIdleAlpha, HudRules.AmmoAlpha(60f), 1e-4f);
            var mid = HudRules.AmmoAlpha(HudRules.AmmoIdleDelay + HudRules.AmmoFadeSeconds * 0.5f);
            Assert.Greater(mid, HudRules.AmmoIdleAlpha);
            Assert.Less(mid, 1f);
        }

        [TestCase(0, 30, "BOŞ")]
        [TestCase(30, 30, "DOLU")]
        [TestCase(26, 30, "DOLUYA YAKIN")]
        [TestCase(20, 30, "YARIDAN FAZLA")]
        [TestCase(15, 30, "YARIM")]
        [TestCase(8, 30, "YARIDAN AZ")]
        [TestCase(2, 30, "BİTMEK ÜZERE")]
        public void MagCheck_Buckets(int ammo, int size, string expected)
        {
            Assert.AreEqual(expected, HudRules.MagCheckLabel(ammo, size));
        }

        [Test]
        public void Stamina_VisibleOnlyWhenNotFull()
        {
            Assert.IsFalse(HudRules.StaminaVisible(1f, false));
            Assert.IsTrue(HudRules.StaminaVisible(0.7f, false));
            Assert.IsTrue(HudRules.StaminaVisible(1f, true));
            Assert.AreEqual(0f, HudRules.StaminaTargetAlpha(1f, false));
            Assert.AreEqual(1f, HudRules.StaminaTargetAlpha(0.1f, false));
        }

        [Test]
        public void StanceLabels_Turkish()
        {
            Assert.AreEqual("AYAKTA", HudRules.StanceLabel(Stance.Standing));
            Assert.AreEqual("ÇÖMEL", HudRules.StanceLabel(Stance.Crouching));
            Assert.AreEqual("YATIK", HudRules.StanceLabel(Stance.Prone));
        }

        [Test]
        public void Grenade_ThreatAndUrgency()
        {
            Assert.IsTrue(HudRules.GrenadeIsThreat(10f, 9f, false));
            Assert.IsFalse(HudRules.GrenadeIsThreat(30f, 9f, false));
            Assert.IsFalse(HudRules.GrenadeIsThreat(5f, 9f, true));
            Assert.IsFalse(HudRules.GrenadeIsThreat(5f, 0f, false));
            Assert.Greater(HudRules.GrenadeUrgency(2f, 9f, 0.5f), HudRules.GrenadeUrgency(15f, 9f, 3.5f));
        }

        [Test]
        public void RelativeBearing_SignsAndOpacity()
        {
            // Kuzeye bakarken doğudaki hedef sağda (+90).
            Assert.AreEqual(90f, HudRules.RelativeBearing(Vector3.zero, 0f, new Vector3(10f, 0f, 0f)), 0.5f);
            Assert.AreEqual(-90f, HudRules.RelativeBearing(Vector3.zero, 0f, new Vector3(-10f, 0f, 0f)), 0.5f);
            Assert.AreEqual(0.3f, HudRules.ClampOpacity(0f), 1e-4f);
            Assert.AreEqual(1f, HudRules.ClampOpacity(float.NaN), 1e-4f);
        }
    }
}
