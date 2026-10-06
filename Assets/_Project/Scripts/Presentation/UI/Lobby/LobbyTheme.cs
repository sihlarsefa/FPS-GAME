using UnityEngine;

namespace Project.Presentation.UI.Lobby
{
    /// <summary>
    /// Lobi arayüzü teması: koyu zemin + kırmızı vurgu renk sabitleri ve saf (Unity nesnesi gerektirmeyen) hareket matematiği.
    /// Tüm lobi görünümleri (sekme, oyuncu kartı, vinyet, parlayan düğme) renklerini buradan alır.
    /// </summary>
    public static class LobbyTheme
    {
        // ------------------------------------------------------------------ Renk paleti
        public static readonly Color Bg = UiTheme.Hex(0x0B, 0x0C, 0x0E, 0xFF);
        public static readonly Color Panel = UiTheme.Hex(0x14, 0x16, 0x19, 0xEE);
        public static readonly Color PanelRaised = UiTheme.Hex(0x1E, 0x21, 0x25, 0xF2);
        public static readonly Color Border = UiTheme.Hex(0x34, 0x38, 0x3D, 0xFF);
        public static readonly Color Red = UiTheme.Hex(0xD4, 0x1F, 0x2B, 0xFF);
        public static readonly Color RedBright = UiTheme.Hex(0xFF, 0x3B, 0x47, 0xFF);
        public static readonly Color RedDeep = UiTheme.Hex(0x6E, 0x0E, 0x14, 0xFF);
        public static readonly Color RedGlow = new Color(0.83f, 0.12f, 0.17f, 0.35f);
        public static readonly Color Gold = UiTheme.Hex(0xE2, 0xB8, 0x4A, 0xFF);
        public static readonly Color Text = UiTheme.Hex(0xF2, 0xF2, 0xF0, 0xFF);
        public static readonly Color TextDim = UiTheme.Hex(0x9A, 0xA0, 0xA3, 0xFF);

        // ------------------------------------------------------------------ Süreler
        public const float HoverSeconds = 0.14f;
        public const float PanelInSeconds = 0.28f;
        public const float PanelOutSeconds = 0.18f;
        public const float PanelSlide = 36f;
        public const float TabSlideSeconds = 0.22f;
        public const float VignettePeriod = 6f;

        // ------------------------------------------------------------------ Sekmeler
        public static readonly string[] TabNames = { "ANA ÜSSÜ", "VİTRİN", "GÖREVLER", "AYARLAR" };
        public const int TabCount = 4;

        // ------------------------------------------------------------------ Saf matematik
        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            var u = 1f - t;
            return 1f - u * u * u;
        }

        public static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>Hover ilerlemesini hedefe doğru sabit hızla (saniyede 1/HoverSeconds) yaklaştırır.</summary>
        public static float StepToward(float current, float target, float dt)
        {
            var step = dt / HoverSeconds;
            return Mathf.MoveTowards(current, target, step);
        }

        /// <summary>Hover parlama gücü 0..1 (ease uygulanmış).</summary>
        public static float GlowAlpha(float hover, float pressed)
        {
            return Mathf.Clamp01(EaseOutCubic(hover) * 0.85f + pressed * 0.15f);
        }

        /// <summary>Panel geçişinde 0..1 ilerlemeden (alfa, x kayması). Girişte soldan sağa kayar, çıkışta geri.</summary>
        public static void PanelPose(float t, bool entering, out float alpha, out float offsetX)
        {
            var e = entering ? EaseOutCubic(t) : 1f - EaseInOutCubic(t);
            alpha = Mathf.Clamp01(e);
            offsetX = (1f - e) * PanelSlide * (entering ? 1f : -1f);
        }

        /// <summary>Panel geçişinin toplam süresi.</summary>
        public static float PanelDuration(bool entering) => entering ? PanelInSeconds : PanelOutSeconds;

        /// <summary>Vinyet nabzı: 0..1 arası yumuşak salınım (kırmızı kenar parlaması için).</summary>
        public static float VignettePulse(float time)
        {
            var p = Mathf.Repeat(time / VignettePeriod, 1f);
            return 0.5f - 0.5f * Mathf.Cos(p * Mathf.PI * 2f);
        }

        /// <summary>Vinyet kenar alfası: taban + nabız.</summary>
        public static float VignetteAlpha(float time, float baseAlpha = 0.55f, float swing = 0.12f)
        {
            return Mathf.Clamp01(baseAlpha + (VignettePulse(time) - 0.5f) * 2f * swing);
        }

        /// <summary>OYNA düğmesi nabzı: 0..1 (periyot 1.6 sn).</summary>
        public static float PlayPulse(float time)
        {
            var p = Mathf.Repeat(time / 1.6f, 1f);
            return 0.5f - 0.5f * Mathf.Cos(p * Mathf.PI * 2f);
        }

        /// <summary>Geçerli sekme indeksi (dairesel).</summary>
        public static int WrapTab(int index, int delta)
        {
            var n = TabCount;
            return ((index + delta) % n + n) % n;
        }

        /// <summary>Sekme göstergesinin (altı çizgi) normalize x merkezi 0..1 (sekmeler eşit genişlikte).</summary>
        public static float TabCenter01(int index)
        {
            return (Mathf.Clamp(index, 0, TabCount - 1) + 0.5f) / TabCount;
        }

        /// <summary>K/D oranı; ölüm 0 ise öldürme sayısı.</summary>
        public static float KdRatio(int kills, int deaths)
        {
            return deaths <= 0 ? kills : (float)kills / deaths;
        }

        /// <summary>Kazanma yüzdesi 0..100 (maç 0 ise 0).</summary>
        public static int WinPercent(int wins, int matches)
        {
            if (matches <= 0) return 0;
            return Mathf.Clamp(Mathf.RoundToInt(100f * wins / matches), 0, 100);
        }
    }
}
