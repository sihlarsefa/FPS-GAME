using System.Collections.Generic;
using NUnit.Framework;
using Project.Presentation.UI.Crosshair;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class CrosshairSettingsTests
    {
        private sealed class MemStore : ICrosshairPrefsStore
        {
            private readonly Dictionary<string, float> _f = new Dictionary<string, float>();
            public bool HasKey(string key) => _f.ContainsKey(key);
            public float GetFloat(string key, float fallback) => _f.TryGetValue(key, out var v) ? v : fallback;
            public int GetInt(string key, int fallback) => _f.TryGetValue(key, out var v) ? (int)v : fallback;
            public void SetFloat(string key, float value) => _f[key] = value;
            public void SetInt(string key, int value) => _f[key] = value;
        }

        [Test]
        public void Load_EmptyStore_ReturnsDefaults()
        {
            var s = CrosshairSettings.Load(new MemStore());
            Assert.AreEqual(11f, s.LineLength, 0.001f);
            Assert.IsTrue(s.CenterDot);
            Assert.IsTrue(s.DynamicSpread);
        }

        [Test]
        public void SaveLoad_RoundTrips()
        {
            var store = new MemStore();
            var s = CrosshairSettings.Default();
            s.LineLength = 20f; s.CenterDot = false; s.UseCustomColor = true; s.ColorG = 0.25f;
            s.AdsMode = CrosshairAdsMode.Daralt; s.ColorBlind = CrosshairColorBlindMode.Tritanopi;
            s.HitMarkerSize = 1.5f;
            CrosshairSettings.Save(s, store);
            var l = CrosshairSettings.Load(store);
            Assert.AreEqual(20f, l.LineLength, 0.001f);
            Assert.IsFalse(l.CenterDot);
            Assert.IsTrue(l.UseCustomColor);
            Assert.AreEqual(0.25f, l.ColorG, 0.001f);
            Assert.AreEqual((int)CrosshairAdsMode.Daralt, (int)l.AdsMode);
            Assert.AreEqual((int)CrosshairColorBlindMode.Tritanopi, (int)l.ColorBlind);
            Assert.AreEqual(1.5f, l.HitMarkerSize, 0.001f);
        }

        [Test]
        public void Sanitize_ClampsAndFixesNaN()
        {
            var s = CrosshairSettings.Default();
            s.LineLength = 999f; s.LineThickness = float.NaN; s.Opacity = -3f; s.AdsMode = (CrosshairAdsMode)77;
            s.ColorBlind = (CrosshairColorBlindMode)(-4); s.SpreadMultiplier = 50f;
            s.Sanitize();
            Assert.AreEqual(40f, s.LineLength, 0.001f);
            Assert.AreEqual(2f, s.LineThickness, 0.001f);
            Assert.AreEqual(0.1f, s.Opacity, 0.001f);
            Assert.AreEqual((int)CrosshairAdsMode.Gizle, (int)s.AdsMode);
            Assert.AreEqual((int)CrosshairColorBlindMode.Kapali, (int)s.ColorBlind);
            Assert.AreEqual(2f, s.SpreadMultiplier, 0.001f);
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var a = CrosshairSettings.Default();
            var b = a.Clone();
            b.LineLength = 30f;
            Assert.AreEqual(11f, a.LineLength, 0.001f);
        }
    }

    public sealed class CrosshairMathTests
    {
        [Test]
        public void Spread_StaticWhenDynamicOff()
        {
            var s = CrosshairSettings.Default();
            s.DynamicSpread = false; s.CenterGap = 4f;
            Assert.AreEqual(9f, CrosshairMath.SpreadToPixels(10f, 70f, 540f, s), 0.001f);
        }

        [Test]
        public void Spread_GrowsWithAngleAndShrinksWithFov()
        {
            var s = CrosshairSettings.Default();
            var small = CrosshairMath.SpreadToPixels(1f, 70f, 540f, s);
            var big = CrosshairMath.SpreadToPixels(4f, 70f, 540f, s);
            var wide = CrosshairMath.SpreadToPixels(4f, 110f, 540f, s);
            Assert.Greater(big, small);
            Assert.Less(wide, big);
        }

        [Test]
        public void Spread_ClampedAndNaNSafe()
        {
            var s = CrosshairSettings.Default();
            Assert.IsTrue(CrosshairMath.SpreadToPixels(999f, 70f, 540f, s) <= CrosshairMath.MaxGapPx + 0.01f);
            Assert.AreEqual(CrosshairMath.MinGapPx, CrosshairMath.SpreadToPixels(float.NaN, 70f, 540f, s), 0.001f);
        }

        [Test]
        public void SpreadMultiplier_Scales()
        {
            var a = CrosshairSettings.Default(); a.SpreadMultiplier = 0.5f;
            var b = CrosshairSettings.Default(); b.SpreadMultiplier = 1.5f;
            Assert.Greater(CrosshairMath.SpreadToPixels(3f, 70f, 540f, b), CrosshairMath.SpreadToPixels(3f, 70f, 540f, a));
        }

        [Test]
        public void SmoothGap_ExpandsFasterThanItRecovers()
        {
            var s = CrosshairSettings.Default();
            var up = CrosshairMath.SmoothGap(10f, 50f, 0.05f, s) - 10f;
            var down = 50f - CrosshairMath.SmoothGap(50f, 10f, 0.05f, s);
            Assert.Greater(up, down);
            Assert.AreEqual(10f, CrosshairMath.SmoothGap(10f, 50f, 0f, s), 0.0001f);
        }

        [Test]
        public void Visibility_AdsModes()
        {
            var s = CrosshairSettings.Default();
            s.AdsMode = CrosshairAdsMode.Gizle;
            Assert.AreEqual(0f, CrosshairMath.TargetVisibility(s, false, true, false, false), 0.001f);
            s.AdsMode = CrosshairAdsMode.Daralt; s.AdsOpacity = 0.4f;
            Assert.AreEqual(0.4f, CrosshairMath.TargetVisibility(s, false, true, false, false), 0.001f);
            s.AdsMode = CrosshairAdsMode.Goster;
            Assert.AreEqual(1f, CrosshairMath.TargetVisibility(s, false, true, false, false), 0.001f);
            Assert.AreEqual(0f, CrosshairMath.TargetVisibility(s, false, true, true, false), 0.001f);
            Assert.AreEqual(0f, CrosshairMath.TargetVisibility(s, true, false, false, false), 0.001f);
            Assert.AreEqual(0f, CrosshairMath.TargetVisibility(s, false, false, false, true), 0.001f);
            Assert.AreEqual(1f, CrosshairMath.TargetVisibility(s, false, false, false, false), 0.001f);
        }

        [Test]
        public void ResolveKind_PriorityAndArmorToggle()
        {
            var s = CrosshairSettings.Default();
            Assert.AreEqual((int)HitKind.KafadanOldurme, (int)CrosshairMath.ResolveKind(true, true, false, s));
            Assert.AreEqual((int)HitKind.Oldurme, (int)CrosshairMath.ResolveKind(false, true, true, s));
            Assert.AreEqual((int)HitKind.Kafa, (int)CrosshairMath.ResolveKind(true, false, true, s));
            Assert.AreEqual((int)HitKind.Zirh, (int)CrosshairMath.ResolveKind(false, false, true, s));
            s.HitMarkerShowArmor = false;
            Assert.AreEqual((int)HitKind.Govde, (int)CrosshairMath.ResolveKind(false, false, true, s));
        }

        [Test]
        public void Merge_KeepsHigherPriorityWhileActive()
        {
            Assert.AreEqual((int)HitKind.Kafa, (int)CrosshairMath.Merge(HitKind.Kafa, true, HitKind.Govde));
            Assert.AreEqual((int)HitKind.Govde, (int)CrosshairMath.Merge(HitKind.Kafa, false, HitKind.Govde));
            Assert.AreEqual((int)HitKind.Oldurme, (int)CrosshairMath.Merge(HitKind.Kafa, true, HitKind.Oldurme));
        }

        [Test]
        public void Style_KillLongerAndBiggerThanBody()
        {
            var body = CrosshairMath.StyleFor(HitKind.Govde, CrosshairColorBlindMode.Kapali);
            var kill = CrosshairMath.StyleFor(HitKind.Oldurme, CrosshairColorBlindMode.Kapali);
            var hkill = CrosshairMath.StyleFor(HitKind.KafadanOldurme, CrosshairColorBlindMode.Kapali);
            Assert.Greater(kill.Duration, body.Duration);
            Assert.Greater(hkill.Duration, kill.Duration);
            Assert.Greater(kill.PopScale, body.PopScale);
        }

        [Test]
        public void Palette_AllKindsDistinctInEveryMode()
        {
            var kinds = new[] { HitKind.Govde, HitKind.Zirh, HitKind.Kafa, HitKind.Oldurme };
            for (var m = 0; m <= 4; m++)
            {
                var mode = (CrosshairColorBlindMode)m;
                for (var i = 0; i < kinds.Length; i++)
                    for (var j = i + 1; j < kinds.Length; j++)
                    {
                        var a = CrosshairMath.PaletteColor(kinds[i], mode);
                        var b = CrosshairMath.PaletteColor(kinds[j], mode);
                        var d = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
                        Assert.Greater(d, 0.3f);
                    }
            }
        }

        [Test]
        public void Palette_RedGreenModesAvoidPureRedKill()
        {
            var k = CrosshairMath.PaletteColor(HitKind.Oldurme, CrosshairColorBlindMode.Deuteranopi);
            Assert.Greater(k.b, 0.5f); // mor-pembe: mavi bileşeni yüksek
        }

        [Test]
        public void HitAlpha_FullThenFades()
        {
            Assert.AreEqual(1f, CrosshairMath.HitAlpha(0.28f, 0.28f, 1f), 0.001f);
            Assert.AreEqual(0.5f, CrosshairMath.HitAlpha(0.07f, 0.28f, 1f), 0.001f);
            Assert.AreEqual(0f, CrosshairMath.HitAlpha(0f, 0.28f, 1f), 0.001f);
            Assert.AreEqual(0.4f, CrosshairMath.HitAlpha(0.28f, 0.28f, 0.4f), 0.001f);
        }

        [Test]
        public void HitScale_PopsThenSettles()
        {
            Assert.AreEqual(1f, CrosshairMath.HitScale(0f, 1.2f, 1f), 0.001f);
            Assert.AreEqual(1.2f, CrosshairMath.HitScale(CrosshairMath.PopDuration * 0.5f, 1.2f, 1f), 0.001f);
            Assert.AreEqual(1f, CrosshairMath.HitScale(0.2f, 1.2f, 1f), 0.001f);
            Assert.AreEqual(1.5f, CrosshairMath.HitScale(0.2f, 1f, 1.5f), 0.001f);
        }

        [Test]
        public void Contrast_BlackWhiteIs21AndOutlinePicksOpposite()
        {
            Assert.AreEqual(21f, CrosshairMath.ContrastRatio(Color.black, Color.white), 0.05f);
            var onWhite = CrosshairMath.OutlineColorFor(Color.white, 0.6f);
            var onBlack = CrosshairMath.OutlineColorFor(Color.black, 0.6f);
            Assert.AreEqual(0f, onWhite.r, 0.001f);
            Assert.AreEqual(1f, onBlack.r, 0.001f);
            Assert.AreEqual(0.6f, onWhite.a, 0.001f);
        }

        [Test]
        public void HighContrast_ForcesOutline()
        {
            var s = CrosshairSettings.Default();
            s.Outline = false; s.OutlineOpacity = 0.2f;
            Assert.AreEqual(0f, CrosshairMath.EffectiveOutlineOpacity(s), 0.001f);
            s.ColorBlind = CrosshairColorBlindMode.YuksekKontrast;
            Assert.AreEqual(0.8f, CrosshairMath.EffectiveOutlineOpacity(s), 0.001f);
        }
    }
}
