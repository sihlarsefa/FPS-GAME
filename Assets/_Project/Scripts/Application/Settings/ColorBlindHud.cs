using System;

namespace Project.Application.Settings
{
    /// <summary>Unity'den bağımsız RGB (0..1).</summary>
    public readonly struct Rgb
    {
        public readonly float R, G, B;
        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }

        public static Rgb FromHex(int hex) => new Rgb(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

        public int ToHex()
        {
            return (ToByte(R) << 16) | (ToByte(G) << 8) | ToByte(B);
        }

        private static int ToByte(float v) => (int)Math.Round(Math.Min(1f, Math.Max(0f, v)) * 255f);

        /// <summary>WCAG göreli parlaklık.</summary>
        public float Luminance()
        {
            return 0.2126f * Lin(R) + 0.7152f * Lin(G) + 0.0722f * Lin(B);
        }

        private static float Lin(float c) => c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4);
    }

    /// <summary>Renk körlüğü türü (Battlefield 2042: Kapalı/Deuteranopi/Tritanopi/Protanopi/Özel).</summary>
    public enum ColorBlindKind { Kapali = 0, Deuteranopi = 1, Protanopi = 2, Tritanopi = 3, Ozel = 4 }

    /// <summary>HUD rolleri için renkler (takım, tim arkadaşı, düşman, nötr, tehlike).</summary>
    public readonly struct HudPalette
    {
        public readonly Rgb Team, Squad, Enemy, Neutral, Danger;
        public HudPalette(Rgb team, Rgb squad, Rgb enemy, Rgb neutral, Rgb danger)
        {
            Team = team; Squad = squad; Enemy = enemy; Neutral = neutral; Danger = danger;
        }
    }

    /// <summary>
    /// Renk körlüğü HUD paletleri + simülasyon/düzeltme matrisleri (Machado 2009, şiddet 1.0, doğrusal olmayan RGB üzerinde
    /// yaklaşım) ve palet ayırt edilebilirlik doğrulaması.
    /// </summary>
    public static class ColorBlindHud
    {
        public static readonly string[] Names = { "Kapalı", "Deuteranopi", "Protanopi", "Tritanopi", "Özel" };

        // Machado ve ark. 2009 tam şiddet matrisleri (satır öncelikli).
        private static readonly float[] Protan = { 0.152286f, 1.052583f, -0.204868f, 0.114503f, 0.786281f, 0.099216f, -0.003882f, -0.048116f, 1.051998f };
        private static readonly float[] Deutan = { 0.367322f, 0.860646f, -0.227968f, 0.280085f, 0.672501f, 0.047413f, -0.011820f, 0.042940f, 0.968881f };
        private static readonly float[] Tritan = { 1.255528f, -0.076749f, -0.178779f, -0.078411f, 0.930809f, 0.147602f, 0.004733f, 0.691367f, 0.303900f };

        public static HudPalette PaletteFor(ColorBlindKind kind, Rgb custom)
        {
            switch (kind)
            {
                case ColorBlindKind.Deuteranopi:
                case ColorBlindKind.Protanopi:
                    // Kırmızı-yeşil ayrımı yerine mavi/turuncu eksen (parlaklık farkı büyük).
                    return new HudPalette(Rgb.FromHex(0x4DA3FF), Rgb.FromHex(0x00D4FF), Rgb.FromHex(0xFF9F1C), Rgb.FromHex(0xDDDDDD), Rgb.FromHex(0xFFD60A));
                case ColorBlindKind.Tritanopi:
                    // Mavi-sarı karışıklığı yerine kırmızı/camgöbeği.
                    return new HudPalette(Rgb.FromHex(0x2EE6C8), Rgb.FromHex(0x66FFE0), Rgb.FromHex(0xFF3B5C), Rgb.FromHex(0xE8E8E8), Rgb.FromHex(0xFF8FA3));
                case ColorBlindKind.Ozel:
                    return new HudPalette(custom, Mix(custom, 1f, 0.35f), Complement(custom), Rgb.FromHex(0xDDDDDD), Rgb.FromHex(0xFFD60A));
                default:
                    return new HudPalette(Rgb.FromHex(0x4CD964), Rgb.FromHex(0x5AC8FA), Rgb.FromHex(0xFF3B30), Rgb.FromHex(0xDDDDDD), Rgb.FromHex(0xFF9500));
            }
        }

        /// <summary>Verilen kör türünün bir rengi nasıl gördüğünü simüle eder (palet testi için). Kapalı/Özel: değişmez.</summary>
        public static Rgb Simulate(ColorBlindKind kind, Rgb c)
        {
            float[] m;
            switch (kind)
            {
                case ColorBlindKind.Protanopi: m = Protan; break;
                case ColorBlindKind.Deuteranopi: m = Deutan; break;
                case ColorBlindKind.Tritanopi: m = Tritan; break;
                default: return c;
            }
            return new Rgb(Sat(m[0] * c.R + m[1] * c.G + m[2] * c.B), Sat(m[3] * c.R + m[4] * c.G + m[5] * c.B), Sat(m[6] * c.R + m[7] * c.G + m[8] * c.B));
        }

        /// <summary>Kaybolan bilgiyi diğer kanallara kaydıran basit daltonizasyon (tam ekran düzeltmesi için); strength 0..1.</summary>
        public static Rgb Daltonize(ColorBlindKind kind, Rgb c, float strength)
        {
            if (kind == ColorBlindKind.Kapali || kind == ColorBlindKind.Ozel || strength <= 0f)
                return c;
            var sim = Simulate(kind, c);
            var er = c.R - sim.R;
            var eg = c.G - sim.G;
            var eb = c.B - sim.B;
            float r = c.R, g = c.G, b = c.B;
            if (kind == ColorBlindKind.Tritanopi)
            {
                r += 0.7f * eb * strength; g += 0.7f * eb * strength;
            }
            else
            {
                g += (0.7f * er + 1.0f * eg) * strength;
                b += (0.7f * er + 1.0f * eb) * strength;
            }
            return new Rgb(Sat(r), Sat(g), Sat(b));
        }

        /// <summary>Basit algısal uzaklık (ağırlıklı RGB Öklid, "redmean").</summary>
        public static float Distance(Rgb a, Rgb b)
        {
            var rm = (a.R + b.R) * 0.5f * 255f;
            var dr = (a.R - b.R) * 255f;
            var dg = (a.G - b.G) * 255f;
            var db = (a.B - b.B) * 255f;
            return (float)Math.Sqrt((2f + rm / 256f) * dr * dr + 4f * dg * dg + (2f + (255f - rm) / 256f) * db * db);
        }

        /// <summary>Palet, hedef kör türüne göre simüle edildiğinde düşman ile takım arasındaki uzaklık (eşik ~ 120 yeterli).</summary>
        public static float EnemyTeamSeparation(ColorBlindKind simulateAs, HudPalette p)
        {
            return Distance(Simulate(simulateAs, p.Enemy), Simulate(simulateAs, p.Team));
        }

        /// <summary>Özel palet için tavsiye: düşman-takım ayrımı yetersizse uyarı metni, iyiyse null.</summary>
        public static string SeparationWarning(ColorBlindKind simulateAs, HudPalette p)
        {
            var d = EnemyTeamSeparation(simulateAs, p);
            return d < 120f ? "Düşman ve takım renkleri bu görme türünde ayırt edilemeyebilir." : null;
        }

        /// <summary>Metin/zemin kontrast oranı (WCAG 1..21).</summary>
        public static float ContrastRatio(Rgb a, Rgb b)
        {
            var la = a.Luminance();
            var lb = b.Luminance();
            var hi = Math.Max(la, lb);
            var lo = Math.Min(la, lb);
            return (hi + 0.05f) / (lo + 0.05f);
        }

        private static Rgb Mix(Rgb c, float target, float t)
        {
            return new Rgb(c.R + (target - c.R) * t, c.G + (target - c.G) * t, c.B + (target - c.B) * t);
        }

        private static Rgb Complement(Rgb c)
        {
            var mx = Math.Max(c.R, Math.Max(c.G, c.B));
            var mn = Math.Min(c.R, Math.Min(c.G, c.B));
            var s = mx + mn;
            return new Rgb(s - c.R, s - c.G, s - c.B);
        }

        private static float Sat(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>Renk körlüğü ve görsel erişilebilirlik ek tercihleri (kalıcı veri).</summary>
    public sealed class VisualAccessibilitySettings
    {
        public int Kind;
        public int CustomColorHex = 0x4DA3FF;
        /// <summary>Tam ekran düzeltme filtresi gücü 0..1 (0 = yalnızca HUD paleti).</summary>
        public float FilterStrength;
        /// <summary>Hareket azaltma: kafa sallanması, FOV vuruşu, ADS titremesi ölçeği 0..1 (1 = tam hareket).</summary>
        public float MotionScale = 1f;
        public float HeadBob = 1f;
        /// <summary>Düşman çerçeve/ana hat vurgusu 0..1.</summary>
        public float EnemyOutline;
        public bool HighContrastHud;

        public void Sanitize()
        {
            Kind = SettingsMath.ClampInt(Kind, 0, ColorBlindHud.Names.Length - 1);
            CustomColorHex = CustomColorHex & 0xFFFFFF;
            FilterStrength = SettingsMath.Clamp(FilterStrength, 0f, 1f, 0f);
            MotionScale = SettingsMath.Clamp(MotionScale, 0f, 1f, 1f);
            HeadBob = SettingsMath.Clamp(HeadBob, 0f, 1f, 1f);
            EnemyOutline = SettingsMath.Clamp(EnemyOutline, 0f, 1f, 0f);
        }

        public HudPalette Palette()
        {
            return ColorBlindHud.PaletteFor((ColorBlindKind)Kind, Rgb.FromHex(CustomColorHex));
        }

        public VisualAccessibilitySettings Clone() => (VisualAccessibilitySettings)MemberwiseClone();
    }
}
