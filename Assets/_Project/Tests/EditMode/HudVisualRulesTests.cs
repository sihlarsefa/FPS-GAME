using NUnit.Framework;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class HudVisualRulesTests
    {
        [Test]
        public void HitMarkerScale_NeverExceedsMaxPixels()
        {
            var scale = HudVisualRules.ClampHitMarkerScale(5f, 8f, 3f);
            Assert.LessOrEqual(HudVisualRules.HitMarkerExtent(5f, 8f, scale), HudVisualRules.MaxHitMarkerPx + 0.01f);
            Assert.AreEqual(1f, HudVisualRules.ClampHitMarkerScale(5f, 8f, float.NaN), 1e-4f);
        }

        [Test]
        public void CrosshairScale_ClampedAndNaNSafe()
        {
            Assert.AreEqual(2f, HudVisualRules.ClampCrosshairScale(50f));
            Assert.AreEqual(0.5f, HudVisualRules.ClampCrosshairScale(-3f));
            Assert.AreEqual(1f, HudVisualRules.ClampCrosshairScale(float.NaN));
        }

        [Test]
        public void Indicator_OnlyForRealDamageAndDistantSource()
        {
            var o = Vector3.zero;
            Assert.IsFalse(HudVisualRules.IndicatorShouldShow(0f, o, new Vector3(10f, 0f, 0f)));
            Assert.IsFalse(HudVisualRules.IndicatorShouldShow(-5f, o, new Vector3(10f, 0f, 0f)));
            Assert.IsFalse(HudVisualRules.IndicatorShouldShow(10f, o, new Vector3(0.1f, 5f, 0f)));
            Assert.IsFalse(HudVisualRules.IndicatorShouldShow(10f, o, new Vector3(float.NaN, 0f, 0f)));
            Assert.IsFalse(HudVisualRules.IndicatorShouldShow(float.NaN, o, new Vector3(10f, 0f, 0f)));
            Assert.IsTrue(HudVisualRules.IndicatorShouldShow(10f, o, new Vector3(10f, 0f, 0f)));
        }

        [Test]
        public void IndicatorAlpha_FadesToZero()
        {
            Assert.AreEqual(0.9f, HudVisualRules.IndicatorAlpha(1.6f, 1.6f, 1f), 1e-4f);
            Assert.Less(HudVisualRules.IndicatorAlpha(0.3f, 1.6f, 1f), 0.5f);
            Assert.AreEqual(0f, HudVisualRules.IndicatorAlpha(0f, 1.6f, 1f));
            Assert.AreEqual(0f, HudVisualRules.IndicatorAlpha(1f, 0f, 1f));
        }

        [Test]
        public void SizeCaps_AreSane()
        {
            Assert.LessOrEqual(HudVisualRules.MaxHitMarkerPx, 32f);
            Assert.LessOrEqual(HudVisualRules.MaxIndicatorPx, 120f);
        }
    }
}
