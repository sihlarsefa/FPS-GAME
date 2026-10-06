using NUnit.Framework;
using Project.Core.Domain;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class HudTimerRulesTests
    {
        private static readonly Color W = Color.white;
        private static readonly Color A = new Color(1f, 0.66f, 0f);
        private static readonly Color R = Color.red;

        [Test]
        public void CountdownColor_WhiteThenAmberThenRed()
        {
            Assert.AreEqual(W, HudTimerRules.CountdownColor(120f, W, A, R));
            Assert.AreEqual(A, HudTimerRules.CountdownColor(10f, W, A, R));
            Assert.AreEqual(R, HudTimerRules.CountdownColor(3f, W, A, R));
            Assert.AreEqual(R, HudTimerRules.CountdownColor(0f, W, A, R));
            var mid = HudTimerRules.CountdownColor(20f, W, A, R);
            Assert.AreNotEqual(W, mid);
            Assert.AreNotEqual(A, mid);
            Assert.AreEqual(W, HudTimerRules.CountdownColor(float.NaN, W, A, R));
        }

        [Test]
        public void Easing_ClampedAndMonotonic()
        {
            Assert.AreEqual(0f, HudTimerRules.EaseOut(-1f), 1e-5f);
            Assert.AreEqual(1f, HudTimerRules.EaseOut(2f), 1e-5f);
            Assert.Greater(HudTimerRules.EaseOut(0.5f), 0.5f);
            Assert.AreEqual(0.5f, HudTimerRules.EaseInOut(0.5f), 1e-5f);
            Assert.AreEqual(0f, HudTimerRules.EaseInOut(float.NaN), 1e-5f);
        }

        [Test]
        public void ClampAnim_KeepsWithinRange()
        {
            Assert.AreEqual(HudTimerRules.AnimMin, HudTimerRules.ClampAnim(0.01f));
            Assert.AreEqual(HudTimerRules.AnimMax, HudTimerRules.ClampAnim(5f));
            Assert.AreEqual(HudTimerRules.AnimDefault, HudTimerRules.ClampAnim(float.NaN));
        }

        [Test]
        public void Approach_ReachesTargetInAnimTime()
        {
            var v = 0f;
            for (var i = 0; i < 10; i++)
                v = HudTimerRules.Approach(v, 1f, 0.02f, 0.2f);
            Assert.AreEqual(1f, v, 1e-4f);
            Assert.AreEqual(0f, HudTimerRules.Approach(0f, 1f, 0f, 0.2f));
        }

        [Test]
        public void RemainingFraction_HandlesBadDuration()
        {
            Assert.AreEqual(0.5f, HudTimerRules.RemainingFraction(30f, 60f), 1e-5f);
            Assert.AreEqual(0f, HudTimerRules.RemainingFraction(30f, 0f));
            Assert.AreEqual(1f, HudTimerRules.RemainingFraction(90f, 60f));
        }

        [Test]
        public void BannerSlide_InHoldOut()
        {
            Assert.AreEqual(1f, HudTimerRules.BannerSlide(0f, 2f, 0.2f), 1e-5f);
            Assert.AreEqual(0f, HudTimerRules.BannerSlide(1f, 2f, 0.2f), 1e-5f);
            Assert.AreEqual(1f, HudTimerRules.BannerSlide(2f, 2f, 0.2f), 1e-5f);
            Assert.AreEqual(1f, HudTimerRules.BannerSlide(-1f, 2f, 0.2f), 1e-5f);
            Assert.Greater(HudTimerRules.BannerSlide(1.9f, 2f, 0.2f), 0f);
        }

        [Test]
        public void ArrowRotation_NegatesAndGuards()
        {
            Assert.AreEqual(-90f, HudTimerRules.ArrowRotation(90f));
            Assert.AreEqual(0f, HudTimerRules.ArrowRotation(float.NaN));
        }

        [Test]
        public void ZoneStinger_ByStage()
        {
            Assert.IsTrue(HudTimerRules.TryStingerForZone(ZoneStage.Waiting, 0, 5, out var k));
            Assert.AreEqual(HudStingerKind.ZoneAnnounced, k);
            Assert.IsTrue(HudTimerRules.TryStingerForZone(ZoneStage.Shrinking, 1, 5, out k));
            Assert.AreEqual(HudStingerKind.ZoneClosing, k);
            Assert.IsTrue(HudTimerRules.TryStingerForZone(ZoneStage.Shrinking, 4, 5, out k));
            Assert.AreEqual(HudStingerKind.ZoneFinal, k);
            Assert.IsFalse(HudTimerRules.TryStingerForZone(ZoneStage.Idle, 0, 5, out _));
        }
    }
}
