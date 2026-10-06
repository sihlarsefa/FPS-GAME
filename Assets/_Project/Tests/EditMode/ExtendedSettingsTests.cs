using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Settings;
using Project.Core.Interfaces;

namespace Project.Tests.EditMode
{
    public sealed class ExtendedSettingsTests
    {
        private sealed class MemStore : ISettingsStore
        {
            public readonly Dictionary<string, float> Data = new Dictionary<string, float>();
            public int Saves;
            public bool HasKey(string key) => Data.ContainsKey(key);
            public float GetFloat(string key, float fallback) => Data.TryGetValue(key, out var v) ? v : fallback;
            public int GetInt(string key, int fallback) => Data.TryGetValue(key, out var v) ? (int)v : fallback;
            public void SetFloat(string key, float value) => Data[key] = value;
            public void SetInt(string key, int value) => Data[key] = value;
            public void Save() => Saves++;
        }

        // ---- FOV ----
        [Test]
        public void Fov_HorizontalVertical_RoundTrip()
        {
            var v = FovMath.HorizontalToVertical(103f, 16f / 9f);
            Assert.AreEqual(103f, FovMath.VerticalToHorizontal(v, 16f / 9f), 0.01f);
        }

        [Test]
        public void Fov_Known16x9Value()
        {
            // 90 derece yatay @16:9 yaklaşık 58.7 derece dikey.
            Assert.AreEqual(58.72f, FovMath.HorizontalToVertical(90f, 16f / 9f), 0.1f);
        }

        [Test]
        public void Fov_HorPlus_UltrawideIsWider()
        {
            var wide = FovMath.HorPlus(90f, 21f / 9f);
            Assert.Greater(wide, 100f);
            Assert.AreEqual(90f, FovMath.HorPlus(90f, 16f / 9f), 0.01f);
        }

        [Test]
        public void Fov_ZoomedFov_TanRatio()
        {
            var f = FovMath.ZoomedFov(64f, 2f);
            Assert.AreEqual(2f, FovMath.ZoomFromFov(64f, f), 0.01f);
            Assert.Less(f, 64f);
            Assert.AreEqual(64f, FovMath.ZoomedFov(64f, 0.5f), 0.01f);
        }

        [Test]
        public void Fov_AdsIndependentIgnoresHip()
        {
            Assert.AreEqual(40f, FovMath.AdsFov(64f, 3f, true, 40f), 0.001f);
            Assert.AreEqual(40f, FovMath.AdsFov(100f, 3f, true, 40f), 0.001f);
            Assert.Less(FovMath.AdsFov(64f, 3f, false, 40f), 25f);
        }

        [Test]
        public void Fov_ViewmodelScaleAndArea()
        {
            Assert.AreEqual(1f, FovMath.ViewmodelApparentScale(54f), 0.001f);
            Assert.Less(FovMath.ViewmodelApparentScale(80f), 1f);
            Assert.AreEqual(1f, FovMath.RelativeVisibleArea(64f), 0.001f);
            Assert.Greater(FovMath.RelativeVisibleArea(90f), 1.5f);
        }

        [Test]
        public void Fov_KickScaledByMotion()
        {
            Assert.AreEqual(70f, FovMath.KickedFov(64f, 6f, 1f), 0.001f);
            Assert.AreEqual(64f, FovMath.KickedFov(64f, 6f, 0f), 0.001f);
        }

        // ---- ADS hassasiyet ----
        [Test]
        public void Ads_StageBoundaries()
        {
            Assert.AreEqual(0, AdsSensitivityModel.StageForZoom(1f));
            Assert.AreEqual(1, AdsSensitivityModel.StageForZoom(2f));
            Assert.AreEqual(2, AdsSensitivityModel.StageForZoom(4f));
            Assert.AreEqual(3, AdsSensitivityModel.StageForZoom(8f));
        }

        [Test]
        public void Ads_MatchCoefficient_Extremes()
        {
            var angle = AdsSensitivityModel.MatchScale(64f, 32f, 0f);
            Assert.AreEqual(0.5f, angle, 0.001f);
            var tan = AdsSensitivityModel.MatchScale(64f, 32f, 1f);
            var expected = (float)(System.Math.Tan(16 * FovMath.DegToRad) / System.Math.Tan(32 * FovMath.DegToRad));
            Assert.AreEqual(expected, tan, 0.001f);
            var mid = AdsSensitivityModel.MatchScale(64f, 32f, 0.5f);
            Assert.IsTrue(mid < angle && mid > tan || mid > angle && mid < tan);
        }

        [Test]
        public void Ads_NoZoomNoScaling()
        {
            var s = new AdsSensitivitySettings();
            Assert.AreEqual(0.8f, AdsSensitivityModel.Resolve(s, 0.8f, true, 64f, 1f), 0.001f);
        }

        [Test]
        public void Ads_HigherZoomIsSlower()
        {
            var s = new AdsSensitivitySettings();
            var low = AdsSensitivityModel.Resolve(s, 0.8f, true, 64f, 2f);
            var high = AdsSensitivityModel.Resolve(s, 0.8f, true, 64f, 6f);
            Assert.Less(high, low);
        }

        [Test]
        public void Ads_FovRelativeOff_UsesStageOnly()
        {
            var s = new AdsSensitivitySettings();
            Assert.AreEqual(0.8f * s.HighZoom, AdsSensitivityModel.Resolve(s, 0.8f, false, 64f, 6f), 0.001f);
        }

        [Test]
        public void Ads_SettersClamp()
        {
            var s = new AdsSensitivitySettings();
            s.SetStage(2, 99f);
            Assert.AreEqual(AdsSensitivityModel.MaxStageMultiplier, s.MidZoom, 0.001f);
            s.SetStage(2, float.NaN);
            Assert.AreEqual(1f, s.MidZoom, 0.001f);
        }

        [Test]
        public void Ads_Cm360()
        {
            Assert.AreEqual(50f, AdsSensitivityModel.Cm360Ads(25f, 0.5f), 0.001f);
            Assert.IsTrue(float.IsPositiveInfinity(AdsSensitivityModel.Cm360Ads(25f, 0f)));
        }

        // ---- Fare ----
        [Test]
        public void Mouse_DefaultsAreRawNoAccel()
        {
            var m = new MouseInputSettings();
            Assert.IsTrue(m.RawInput);
            Assert.IsFalse(m.Acceleration);
            Assert.AreEqual(0f, m.SmoothingMs, 0.0001f);
        }

        [Test]
        public void Mouse_NoAccel_IsIdentity()
        {
            var p = new MouseDeltaProcessor();
            p.Process(37f, -12f, 0.016f, out var x, out var y);
            Assert.AreEqual(37f, x, 0.0001f);
            Assert.AreEqual(-12f, y, 0.0001f);
        }

        [Test]
        public void Mouse_AccelGrowsWithSpeedAndCaps()
        {
            var s = new MouseInputSettings { Acceleration = true, AccelStrength = 0.2f, AccelCap = 1.5f };
            var slow = MouseDeltaProcessor.AccelGain(s, 1f, 10f);
            var fast = MouseDeltaProcessor.AccelGain(s, 50f, 10f);
            Assert.Greater(slow, 0.99f);
            Assert.Less(slow, fast);
            Assert.AreEqual(1.5f, fast, 0.0001f);
        }

        [Test]
        public void Mouse_YxRatio_ScalesY()
        {
            var p = new MouseDeltaProcessor { Settings = new MouseInputSettings { YxRatio = 0.5f } };
            p.Process(10f, 10f, 0.016f, out var x, out var y);
            Assert.AreEqual(10f, x, 0.0001f);
            Assert.AreEqual(5f, y, 0.0001f);
        }

        [Test]
        public void Mouse_Smoothing_ConservesTotalMotion()
        {
            var p = new MouseDeltaProcessor { Settings = new MouseInputSettings { SmoothingMs = 30f } };
            float sum = 0f;
            p.Process(100f, 0f, 0.008f, out var x, out _);
            sum += x;
            Assert.Less(x, 100f);
            for (var i = 0; i < 200; i++)
            {
                p.Process(0f, 0f, 0.008f, out x, out _);
                sum += x;
            }
            Assert.AreEqual(100f, sum, 0.5f);
        }

        [Test]
        public void Mouse_Cm360_RoundTrip()
        {
            var cm = SensitivityConverter.Cm360(0.12f, 800);
            Assert.AreEqual(0.12f, SensitivityConverter.DegreesPerCount(cm, 800), 0.0005f);
            // 3000 sayım / 800 dpi * 2.54 = 9.525 cm
            Assert.AreEqual(9.525f, cm, 0.01f);
            Assert.IsTrue(float.IsPositiveInfinity(SensitivityConverter.Cm360(0f, 800)));
        }

        [Test]
        public void Mouse_FromOtherGame()
        {
            // CS2 sens 1.0 * yaw 0.022 → bu oyunda 0.022 derece/sayım, birim 0.1 ise 0.22.
            Assert.AreEqual(0.22f, SensitivityConverter.FromOtherGame(1f, 0.022f, 0.1f), 0.001f);
        }

        [Test]
        public void Mouse_SanitizeClamps()
        {
            var m = new MouseInputSettings { AccelStrength = 5f, AccelCap = 0f, SmoothingMs = -3f, YxRatio = 9f, Dpi = 1 };
            m.Sanitize();
            Assert.AreEqual(1f, m.AccelStrength, 0.001f);
            Assert.AreEqual(1f, m.AccelCap, 0.001f);
            Assert.AreEqual(0f, m.SmoothingMs, 0.001f);
            Assert.AreEqual(1.5f, m.YxRatio, 0.001f);
            Assert.AreEqual(100, m.Dpi);
        }

        // ---- Ses ----
        [Test]
        public void Audio_CompressionBelowThresholdLinear()
        {
            var p = AudioMixPresets.For(AudioOutputMode.Hoparlor);
            Assert.AreEqual(-30f + p.MakeupDb, AudioMixPresets.CompressDb(p, -30f), 0.001f);
        }

        [Test]
        public void Audio_NightNarrowerThanHeadphones()
        {
            var hp = AudioMixPresets.EffectiveRangeDb(AudioMixPresets.For(AudioOutputMode.Kulaklik), -40f, -3f);
            var night = AudioMixPresets.EffectiveRangeDb(AudioMixPresets.For(AudioOutputMode.GeceModu), -40f, -3f);
            Assert.Less(night, hp);
        }

        [Test]
        public void Audio_LimiterCapsOutput()
        {
            var p = AudioMixPresets.For(AudioOutputMode.Kulaklik);
            Assert.AreEqual(p.LimiterDb, AudioMixPresets.CompressDb(p, 6f), 0.001f);
        }

        [Test]
        public void Audio_DynamicRangeSliderMonotonic()
        {
            var narrow = AudioMixPresets.EffectiveRangeDb(AudioMixPresets.WithDynamicRange(AudioOutputMode.Hoparlor, 0f), -40f, -6f);
            var mid = AudioMixPresets.EffectiveRangeDb(AudioMixPresets.WithDynamicRange(AudioOutputMode.Hoparlor, 0.5f), -40f, -6f);
            var wide = AudioMixPresets.EffectiveRangeDb(AudioMixPresets.WithDynamicRange(AudioOutputMode.Hoparlor, 1f), -40f, -6f);
            Assert.Less(narrow, mid);
            Assert.Less(mid, wide);
        }

        [Test]
        public void Audio_FootstepEmphasisRaisesHighShelf()
        {
            var a = new AudioMixSettings { FootstepEmphasis = 0f };
            var b = new AudioMixSettings { FootstepEmphasis = 1f };
            Assert.AreEqual(6f, b.Resolve().HighShelfDb - a.Resolve().HighShelfDb, 0.001f);
        }

        [Test]
        public void Audio_DbConversions()
        {
            Assert.AreEqual(0.5f, SettingsMath.DbToLinear(-6.0206f), 0.001f);
            Assert.AreEqual(-6.0206f, SettingsMath.LinearToDb(0.5f), 0.001f);
            Assert.AreEqual(-120f, SettingsMath.LinearToDb(0f), 0.001f);
        }

        // ---- Renk körlüğü ----
        [Test]
        public void ColorBlind_HexRoundTrip()
        {
            Assert.AreEqual(0x4DA3FF, Rgb.FromHex(0x4DA3FF).ToHex());
        }

        [Test]
        public void ColorBlind_OffAndCustomDoNotSimulate()
        {
            var c = new Rgb(0.2f, 0.7f, 0.4f);
            Assert.AreEqual(c.ToHex(), ColorBlindHud.Simulate(ColorBlindKind.Kapali, c).ToHex());
        }

        [Test]
        public void ColorBlind_SimulationConfusesRedGreen()
        {
            var red = new Rgb(1f, 0f, 0f);
            var green = new Rgb(0f, 0.8f, 0f);
            var normal = ColorBlindHud.Distance(red, green);
            var deut = ColorBlindHud.Distance(ColorBlindHud.Simulate(ColorBlindKind.Deuteranopi, red), ColorBlindHud.Simulate(ColorBlindKind.Deuteranopi, green));
            Assert.Less(deut, normal);
        }

        [Test]
        public void ColorBlind_PresetPalettesSeparateEnemyFromTeam()
        {
            var deut = ColorBlindHud.PaletteFor(ColorBlindKind.Deuteranopi, new Rgb(1, 1, 1));
            Assert.Greater(ColorBlindHud.EnemyTeamSeparation(ColorBlindKind.Deuteranopi, deut), 120f);
            var prot = ColorBlindHud.PaletteFor(ColorBlindKind.Protanopi, new Rgb(1, 1, 1));
            Assert.Greater(ColorBlindHud.EnemyTeamSeparation(ColorBlindKind.Protanopi, prot), 100f);
            var trit = ColorBlindHud.PaletteFor(ColorBlindKind.Tritanopi, new Rgb(1, 1, 1));
            Assert.Greater(ColorBlindHud.EnemyTeamSeparation(ColorBlindKind.Tritanopi, trit), 100f);
        }

        [Test]
        public void ColorBlind_DefaultRedGreenIsWarnedForDeuteranopia()
        {
            var normal = ColorBlindHud.PaletteFor(ColorBlindKind.Kapali, new Rgb(1, 1, 1));
            Assert.IsTrue(ColorBlindHud.EnemyTeamSeparation(ColorBlindKind.Deuteranopi, normal)
                < ColorBlindHud.EnemyTeamSeparation(ColorBlindKind.Deuteranopi, ColorBlindHud.PaletteFor(ColorBlindKind.Deuteranopi, new Rgb(1, 1, 1))));
        }

        [Test]
        public void ColorBlind_ContrastRatioBounds()
        {
            Assert.AreEqual(21f, ColorBlindHud.ContrastRatio(new Rgb(0, 0, 0), new Rgb(1, 1, 1)), 0.01f);
            Assert.AreEqual(1f, ColorBlindHud.ContrastRatio(new Rgb(0.5f, 0.5f, 0.5f), new Rgb(0.5f, 0.5f, 0.5f)), 0.001f);
        }

        [Test]
        public void ColorBlind_DaltonizeOffReturnsSame()
        {
            var c = new Rgb(0.3f, 0.6f, 0.9f);
            Assert.AreEqual(c.ToHex(), ColorBlindHud.Daltonize(ColorBlindKind.Deuteranopi, c, 0f).ToHex());
            Assert.AreEqual(c.ToHex(), ColorBlindHud.Daltonize(ColorBlindKind.Kapali, c, 1f).ToHex());
        }

        // ---- Tuş çakışması ----
        [Test]
        public void Keybind_SameContextConflicts()
        {
            var e = new List<BindingEntry> { new BindingEntry("Ateş", "yürüyüş", "Mouse0"), new BindingEntry("Bıçak", "yürüyüş", "Mouse0") };
            Assert.AreEqual(1, KeybindConflictRules.FindConflicts(e).Count);
        }

        [Test]
        public void Keybind_DifferentContextsAllowed_CommonConflictsWithAll()
        {
            var ok = new List<BindingEntry> { new BindingEntry("Gaz", "araç", "W"), new BindingEntry("İleri", "yürüyüş", "W") };
            Assert.AreEqual(0, KeybindConflictRules.FindConflicts(ok).Count);
            var bad = new List<BindingEntry> { new BindingEntry("Harita", "ortak", "M"), new BindingEntry("Gaz", "araç", "m") };
            Assert.AreEqual(1, KeybindConflictRules.FindConflicts(bad).Count);
        }

        [Test]
        public void Keybind_ReservedAndOwner()
        {
            Assert.IsTrue(KeybindConflictRules.IsReserved("escape"));
            var e = new List<BindingEntry> { new BindingEntry("Zıpla", "yürüyüş", "Space") };
            Assert.AreEqual("Zıpla", KeybindConflictRules.FindOwner(e, "Çömel", "yürüyüş", "Space"));
            Assert.IsTrue(KeybindConflictRules.FindOwner(e, "Zıpla", "yürüyüş", "Space") == null);
            Assert.AreEqual("(ayrılmış)", KeybindConflictRules.FindOwner(e, "X", "yürüyüş", "Escape"));
        }

        [Test]
        public void Keybind_Pretty()
        {
            Assert.AreEqual("LEFT SHIFT", KeybindConflictRules.Pretty("leftShift"));
            Assert.AreEqual("—", KeybindConflictRules.Pretty(""));
        }

        // ---- Kalıcılık / paylaşım ----
        [Test]
        public void Store_EmptyLoadsDefaults()
        {
            var s = ExtendedSettingsStore.Load(new MemStore());
            Assert.IsTrue(s.Mouse.RawInput);
            Assert.IsFalse(s.Mouse.Acceleration);
            Assert.AreEqual(0.7f, s.Ads.HighZoom, 0.001f);
            Assert.IsTrue(ExtendedSettingsStore.Load(null).SameAs(ExtendedSettingsStore.NewDefault()));
        }

        [Test]
        public void Store_RoundTrip()
        {
            var store = new MemStore();
            var s = ExtendedSettingsStore.NewDefault();
            s.Ads.LowZoom = 1.1f;
            s.Mouse.Acceleration = true;
            s.Mouse.Dpi = 1600;
            s.Audio.OutputMode = (int)AudioOutputMode.GeceModu;
            s.Visual.Kind = (int)ColorBlindKind.Ozel;
            s.Visual.CustomColorHex = 0xFF9F1C;
            s.Display.AdsFovIndependent = true;
            ExtendedSettingsStore.Save(store, s);
            Assert.AreEqual(1, store.Saves);
            var l = ExtendedSettingsStore.Load(store);
            Assert.IsTrue(s.SameAs(l));
            Assert.AreEqual(0xFF9F1C, l.Visual.CustomColorHex);
            Assert.IsTrue(l.Mouse.Acceleration);
        }

        [Test]
        public void Store_KeysAreUniqueAndPrefixed()
        {
            var seen = new HashSet<string>();
            foreach (var kv in new ExtendedSettings().ToPairs())
            {
                Assert.IsTrue(kv.Key.StartsWith("ext."));
                Assert.IsTrue(seen.Add(kv.Key));
            }
        }

        [Test]
        public void Store_CorruptValuesSanitized()
        {
            var store = new MemStore();
            store.Data[ExtendedSettings.Keys.AdsHigh] = float.NaN;
            store.Data[ExtendedSettings.Keys.MouseDpi] = -5f;
            store.Data[ExtendedSettings.Keys.AudioMode] = 99f;
            var s = ExtendedSettingsStore.Load(store);
            Assert.AreEqual(1f, s.Ads.HighZoom, 0.001f);
            Assert.AreEqual(100, s.Mouse.Dpi);
            Assert.AreEqual(AudioMixPresets.Names.Length - 1, s.Audio.OutputMode);
        }

        [Test]
        public void ShareCode_RoundTrip()
        {
            var s = ExtendedSettingsStore.NewDefault();
            s.Ads.MidZoom = 0.65f;
            s.Mouse.SmoothingMs = 12f;
            s.Visual.CustomColorHex = 0x2EE6C8;
            var code = SettingsShareCode.Encode(s);
            Assert.IsTrue(code.StartsWith("HK1-"));
            Assert.IsTrue(SettingsShareCode.TryDecode(code, out var back));
            Assert.IsTrue(s.SameAs(back));
            Assert.AreEqual(0x2EE6C8, back.Visual.CustomColorHex);
        }

        [Test]
        public void ShareCode_RejectsTamperedOrGarbage()
        {
            var code = SettingsShareCode.Encode(ExtendedSettingsStore.NewDefault());
            var tampered = code.Replace("HK1-", "HK1-9");
            Assert.IsFalse(SettingsShareCode.TryDecode(tampered, out _));
            Assert.IsFalse(SettingsShareCode.TryDecode("", out _));
            Assert.IsFalse(SettingsShareCode.TryDecode("merhaba", out _));
            Assert.IsFalse(SettingsShareCode.TryDecode("HK1-1.2.3-00000000", out _));
        }

        [Test]
        public void Hub_PublishRaisesChangedAndSanitizes()
        {
            ExtendedSettingsHub.ResetForTests();
            ExtendedSettings got = null;
            ExtendedSettingsHub.Changed += x => got = x;
            var s = ExtendedSettingsStore.NewDefault();
            s.Ads.HighZoom = 50f;
            ExtendedSettingsHub.Publish(s);
            Assert.IsTrue(got != null);
            Assert.AreEqual(AdsSensitivityModel.MaxStageMultiplier, ExtendedSettingsHub.Current.Ads.HighZoom, 0.001f);
            Assert.Less(ExtendedSettingsHub.ResolveAdsMultiplier(0.8f, true, 64f, 6f), 2f);
            ExtendedSettingsHub.ResetForTests();
        }

        [Test]
        public void Display_BrightnessGamma()
        {
            var d = new DisplayExtraSettings { Brightness = 2f };
            d.Sanitize();
            Assert.AreEqual(1.4f, d.Brightness, 0.001f);
            Assert.AreEqual(1f / 1.4f, d.GammaExponent(), 0.001f);
        }
    }
}
