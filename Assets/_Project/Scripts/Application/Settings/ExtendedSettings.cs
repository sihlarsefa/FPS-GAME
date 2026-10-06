using System;
using System.Collections.Generic;

namespace Project.Application.Settings
{
    /// <summary>Görüntü ek tercihleri: bağımsız ADS FOV'si, parlaklık, parlama azaltma.</summary>
    public sealed class DisplayExtraSettings
    {
        /// <summary>true: ADS FOV'si sabit derece (profesyonellerin çoğu tercihi); false: dünya FOV'sine bağlı.</summary>
        public bool AdsFovIndependent;
        /// <summary>Bağımsız ADS FOV'si (dikey derece) 15..60.</summary>
        public float AdsFovDegrees = 40f;
        /// <summary>Parlaklık (gama) 0.7..1.4; 1 = nötr.</summary>
        public float Brightness = 1f;
        /// <summary>Namlu ateşi/parlama flaş şiddeti 0..1 (1 = tam). Işığa hassas oyuncular için.</summary>
        public float FlashIntensity = 1f;
        /// <summary>FOV'yi menüde yatay (16:9) derece olarak göster.</summary>
        public bool ShowHorizontalFov = true;

        public void Sanitize()
        {
            AdsFovDegrees = SettingsMath.Clamp(AdsFovDegrees, 15f, 60f, 40f);
            Brightness = SettingsMath.Clamp(Brightness, 0.7f, 1.4f, 1f);
            FlashIntensity = SettingsMath.Clamp(FlashIntensity, 0f, 1f, 1f);
        }

        /// <summary>Gamadan renk üssü: çıktı = girdi^(1/gama).</summary>
        public float GammaExponent() => 1f / Math.Max(0.1f, Brightness);

        public DisplayExtraSettings Clone() => (DisplayExtraSettings)MemberwiseClone();
    }

    /// <summary>
    /// Mevcut GameSettings'e dokunmadan eklenen ayarların toplamı. Kalıcılık <see cref="ExtendedSettingsStore"/> ile;
    /// anahtarlar <see cref="Keys"/> içinde sabittir ("ext." öneki).
    /// </summary>
    public sealed class ExtendedSettings
    {
        public static class Keys
        {
            public const string Version = "ext.version";
            public const string AdsIron = "ext.ads.iron";
            public const string AdsLow = "ext.ads.low";
            public const string AdsMid = "ext.ads.mid";
            public const string AdsHigh = "ext.ads.high";
            public const string AdsMatch = "ext.ads.match";
            public const string MouseRaw = "ext.mouse.raw";
            public const string MouseAccel = "ext.mouse.accel";
            public const string MouseAccelStrength = "ext.mouse.accelStrength";
            public const string MouseAccelCap = "ext.mouse.accelCap";
            public const string MouseSmoothing = "ext.mouse.smoothing";
            public const string MouseYx = "ext.mouse.yx";
            public const string MouseDpi = "ext.mouse.dpi";
            public const string AudioMode = "ext.audio.mode";
            public const string AudioRange = "ext.audio.range";
            public const string AudioFootsteps = "ext.audio.footsteps";
            public const string AudioUnfocused = "ext.audio.unfocused";
            public const string AudioLfe = "ext.audio.lfe";
            public const string AudioDucking = "ext.audio.ducking";
            public const string CbKind = "ext.vis.cbKind";
            public const string CbCustom = "ext.vis.cbCustom";
            public const string CbFilter = "ext.vis.cbFilter";
            public const string MotionScale = "ext.vis.motion";
            public const string HeadBob = "ext.vis.headBob";
            public const string EnemyOutline = "ext.vis.outline";
            public const string HighContrast = "ext.vis.highContrast";
            public const string AdsFovIndependent = "ext.disp.adsFovIndep";
            public const string AdsFovDegrees = "ext.disp.adsFovDeg";
            public const string Brightness = "ext.disp.brightness";
            public const string Flash = "ext.disp.flash";
            public const string ShowHFov = "ext.disp.showHFov";
        }

        public const int CurrentVersion = 1;

        public AdsSensitivitySettings Ads = new AdsSensitivitySettings();
        public MouseInputSettings Mouse = new MouseInputSettings();
        public AudioMixSettings Audio = new AudioMixSettings();
        public VisualAccessibilitySettings Visual = new VisualAccessibilitySettings();
        public DisplayExtraSettings Display = new DisplayExtraSettings();

        public void Sanitize()
        {
            Ads ??= new AdsSensitivitySettings();
            Mouse ??= new MouseInputSettings();
            Audio ??= new AudioMixSettings();
            Visual ??= new VisualAccessibilitySettings();
            Display ??= new DisplayExtraSettings();
            Ads.Sanitize();
            Mouse.Sanitize();
            Audio.Sanitize();
            Visual.Sanitize();
            Display.Sanitize();
        }

        public ExtendedSettings Clone()
        {
            return new ExtendedSettings
            {
                Ads = Ads.Clone(),
                Mouse = Mouse.Clone(),
                Audio = Audio.Clone(),
                Visual = Visual.Clone(),
                Display = Display.Clone()
            };
        }

        /// <summary>Tüm alanların (anahtar, değer) listesi — kaydetme, karşılaştırma ve paylaşım kodu için tek kaynak.</summary>
        public List<KeyValuePair<string, float>> ToPairs()
        {
            var l = new List<KeyValuePair<string, float>>(32);
            void A(string k, float v) => l.Add(new KeyValuePair<string, float>(k, v));
            A(Keys.AdsIron, Ads.IronSights);
            A(Keys.AdsLow, Ads.LowZoom);
            A(Keys.AdsMid, Ads.MidZoom);
            A(Keys.AdsHigh, Ads.HighZoom);
            A(Keys.AdsMatch, Ads.MatchCoefficient);
            A(Keys.MouseRaw, Mouse.RawInput ? 1f : 0f);
            A(Keys.MouseAccel, Mouse.Acceleration ? 1f : 0f);
            A(Keys.MouseAccelStrength, Mouse.AccelStrength);
            A(Keys.MouseAccelCap, Mouse.AccelCap);
            A(Keys.MouseSmoothing, Mouse.SmoothingMs);
            A(Keys.MouseYx, Mouse.YxRatio);
            A(Keys.MouseDpi, Mouse.Dpi);
            A(Keys.AudioMode, Audio.OutputMode);
            A(Keys.AudioRange, Audio.DynamicRange);
            A(Keys.AudioFootsteps, Audio.FootstepEmphasis);
            A(Keys.AudioUnfocused, Audio.UnfocusedVolume);
            A(Keys.AudioLfe, Audio.LowFrequencyIntensity);
            A(Keys.AudioDucking, Audio.VoiceDucking ? 1f : 0f);
            A(Keys.CbKind, Visual.Kind);
            A(Keys.CbCustom, Visual.CustomColorHex);
            A(Keys.CbFilter, Visual.FilterStrength);
            A(Keys.MotionScale, Visual.MotionScale);
            A(Keys.HeadBob, Visual.HeadBob);
            A(Keys.EnemyOutline, Visual.EnemyOutline);
            A(Keys.HighContrast, Visual.HighContrastHud ? 1f : 0f);
            A(Keys.AdsFovIndependent, Display.AdsFovIndependent ? 1f : 0f);
            A(Keys.AdsFovDegrees, Display.AdsFovDegrees);
            A(Keys.Brightness, Display.Brightness);
            A(Keys.Flash, Display.FlashIntensity);
            A(Keys.ShowHFov, Display.ShowHorizontalFov ? 1f : 0f);
            return l;
        }

        /// <summary>Anahtar → değer okuyucudan nesneyi kurar (eksik anahtar varsayılanı korur) ve kırpar.</summary>
        public static ExtendedSettings FromReader(Func<string, float, float> read)
        {
            var d = new ExtendedSettings();
            var s = d.Clone();
            float F(string k, float def) => read(k, def);
            bool B(string k, bool def) => read(k, def ? 1f : 0f) >= 0.5f;
            s.Ads.IronSights = F(Keys.AdsIron, d.Ads.IronSights);
            s.Ads.LowZoom = F(Keys.AdsLow, d.Ads.LowZoom);
            s.Ads.MidZoom = F(Keys.AdsMid, d.Ads.MidZoom);
            s.Ads.HighZoom = F(Keys.AdsHigh, d.Ads.HighZoom);
            s.Ads.MatchCoefficient = F(Keys.AdsMatch, d.Ads.MatchCoefficient);
            s.Mouse.RawInput = B(Keys.MouseRaw, d.Mouse.RawInput);
            s.Mouse.Acceleration = B(Keys.MouseAccel, d.Mouse.Acceleration);
            s.Mouse.AccelStrength = F(Keys.MouseAccelStrength, d.Mouse.AccelStrength);
            s.Mouse.AccelCap = F(Keys.MouseAccelCap, d.Mouse.AccelCap);
            s.Mouse.SmoothingMs = F(Keys.MouseSmoothing, d.Mouse.SmoothingMs);
            s.Mouse.YxRatio = F(Keys.MouseYx, d.Mouse.YxRatio);
            s.Mouse.Dpi = (int)Math.Round(F(Keys.MouseDpi, d.Mouse.Dpi));
            s.Audio.OutputMode = (int)Math.Round(F(Keys.AudioMode, d.Audio.OutputMode));
            s.Audio.DynamicRange = F(Keys.AudioRange, d.Audio.DynamicRange);
            s.Audio.FootstepEmphasis = F(Keys.AudioFootsteps, d.Audio.FootstepEmphasis);
            s.Audio.UnfocusedVolume = F(Keys.AudioUnfocused, d.Audio.UnfocusedVolume);
            s.Audio.LowFrequencyIntensity = F(Keys.AudioLfe, d.Audio.LowFrequencyIntensity);
            s.Audio.VoiceDucking = B(Keys.AudioDucking, d.Audio.VoiceDucking);
            s.Visual.Kind = (int)Math.Round(F(Keys.CbKind, d.Visual.Kind));
            s.Visual.CustomColorHex = (int)Math.Round(F(Keys.CbCustom, d.Visual.CustomColorHex));
            s.Visual.FilterStrength = F(Keys.CbFilter, d.Visual.FilterStrength);
            s.Visual.MotionScale = F(Keys.MotionScale, d.Visual.MotionScale);
            s.Visual.HeadBob = F(Keys.HeadBob, d.Visual.HeadBob);
            s.Visual.EnemyOutline = F(Keys.EnemyOutline, d.Visual.EnemyOutline);
            s.Visual.HighContrastHud = B(Keys.HighContrast, d.Visual.HighContrastHud);
            s.Display.AdsFovIndependent = B(Keys.AdsFovIndependent, d.Display.AdsFovIndependent);
            s.Display.AdsFovDegrees = F(Keys.AdsFovDegrees, d.Display.AdsFovDegrees);
            s.Display.Brightness = F(Keys.Brightness, d.Display.Brightness);
            s.Display.FlashIntensity = F(Keys.Flash, d.Display.FlashIntensity);
            s.Display.ShowHorizontalFov = B(Keys.ShowHFov, d.Display.ShowHorizontalFov);
            s.Sanitize();
            return s;
        }

        /// <summary>Alan eşitliği (sanitize sonrası değerlerle, küçük tolerans).</summary>
        public bool SameAs(ExtendedSettings other)
        {
            if (other == null)
                return false;
            var a = ToPairs();
            var b = other.ToPairs();
            for (var i = 0; i < a.Count; i++)
                if (Math.Abs(a[i].Value - b[i].Value) > 0.0005f)
                    return false;
            return true;
        }
    }
}
