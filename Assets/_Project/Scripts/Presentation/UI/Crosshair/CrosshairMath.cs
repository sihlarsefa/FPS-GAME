using UnityEngine;

namespace Project.Presentation.UI.Crosshair
{
    /// <summary>İsabet türü; büyük değer daha yüksek öncelik (saçma isabetlerinde en önemlisi kalır).</summary>
    public enum HitKind
    {
        Govde = 0,
        Zirh = 1,
        Kafa = 2,
        Oldurme = 3,
        KafadanOldurme = 4
    }

    /// <summary>Bir isabet türünün görsel/zamanlama parametreleri.</summary>
    public readonly struct HitStyle
    {
        public readonly Color Color;
        public readonly float Duration;   // sn (süre çarpanı uygulanmamış)
        public readonly float PopScale;   // >1: ilk 80 ms'de büyüyüp oturur
        public readonly float ArmLength;  // px
        public readonly float ArmOffset;  // px (merkezden)

        public HitStyle(Color color, float duration, float popScale, float armLength, float armOffset)
        {
            Color = color; Duration = duration; PopScale = popScale; ArmLength = armLength; ArmOffset = armOffset;
        }
    }

    /// <summary>Nişangâh saf mantığı: yayılım→piksel, yumuşatma, ADS, isabet türü/paleti, kontrast. Unity nesnesi yaratmaz.</summary>
    public static class CrosshairMath
    {
        public const float PopDuration = 0.08f;
        public const float MaxSpreadDegrees = 60f;
        public const float MinGapPx = 5f;
        public const float MaxGapPx = 140f;

        // ---------------- Yayılım ----------------

        /// <summary>
        /// Koni yarı açısını (derece) ekran pikseline çevirir: tan(yayılım)/tan(FOV/2) * ekranYarıYüksekliği.
        /// Dinamik kapalıysa yalnızca sabit taban boşluk döner.
        /// </summary>
        public static float SpreadToPixels(float spreadDegrees, float fovDegrees, float halfScreenHeight, CrosshairSettings s)
        {
            var baseGap = MinGapPx + s.CenterGap;
            if (!s.DynamicSpread)
                return baseGap;
            if (float.IsNaN(spreadDegrees) || spreadDegrees < 0f)
                spreadDegrees = 0f;
            if (float.IsNaN(fovDegrees))
                fovDegrees = 70f;
            fovDegrees = Mathf.Clamp(fovDegrees, 5f, 150f);
            if (halfScreenHeight < 1f)
                halfScreenHeight = 540f;

            var t = Mathf.Tan(Mathf.Min(spreadDegrees, MaxSpreadDegrees) * Mathf.Deg2Rad)
                    / Mathf.Tan(fovDegrees * 0.5f * Mathf.Deg2Rad);
            var px = t * halfScreenHeight * s.SpreadMultiplier + 3f + s.CenterGap;
            return Mathf.Clamp(px, baseGap, MaxGapPx + s.CenterGap);
        }

        /// <summary>
        /// Asimetrik üstel yumuşatma: açılırken hızlı (atış anında nişangâh "patlar"), toparlanırken yavaş
        /// (CS2/Valorant'taki gibi: açılma anlık, kapanma gözle izlenir).
        /// </summary>
        public static float SmoothGap(float current, float target, float deltaTime, CrosshairSettings s)
        {
            if (deltaTime <= 0f)
                return current;
            var rate = target > current ? s.ExpandRate : s.RecoverRate;
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-deltaTime * rate));
        }

        // ---------------- ADS ----------------

        /// <summary>ADS/dürbün/eşya kullanımı için hedef opaklık (0..1, ayar opaklığı dahil değil).</summary>
        public static float TargetVisibility(CrosshairSettings s, bool hiddenByHud, bool aiming, bool scoped, bool usingItem)
        {
            if (hiddenByHud || usingItem)
                return 0f;
            if (scoped)
                return 0f; // dürbün retikülü zaten var
            if (!aiming)
                return 1f;
            switch (s.AdsMode)
            {
                case CrosshairAdsMode.Gizle: return 0f;
                case CrosshairAdsMode.Daralt: return s.AdsOpacity;
                default: return 1f;
            }
        }

        /// <summary>ADS'de daraltma kipinde boşluk çarpanı (nişan alırken yayılım zaten düşer; ek %25 daralma).</summary>
        public static float AdsGapScale(CrosshairSettings s, bool aiming)
            => aiming && s.AdsMode == CrosshairAdsMode.Daralt ? 0.75f : 1f;

        // ---------------- İsabet işareti ----------------

        public static HitKind ResolveKind(bool headshot, bool kill, bool armorAbsorbed, CrosshairSettings s)
        {
            if (kill)
                return headshot ? HitKind.KafadanOldurme : HitKind.Oldurme;
            if (headshot)
                return HitKind.Kafa;
            if (armorAbsorbed && s.HitMarkerShowArmor)
                return HitKind.Zirh;
            return HitKind.Govde;
        }

        /// <summary>
        /// Aktif işaret sürerken gelen yeni isabeti birleştirir: öncelik yüksek olan kalır
        /// (pompalıda 8 saçmadan biri kafaya gelirse beyaz isabet kafa rengini ezmez).
        /// </summary>
        public static HitKind Merge(HitKind current, bool currentActive, HitKind incoming)
            => currentActive && current > incoming ? current : incoming;

        public static HitStyle StyleFor(HitKind kind, CrosshairColorBlindMode mode)
        {
            var color = PaletteColor(kind, mode);
            switch (kind)
            {
                case HitKind.Zirh: return new HitStyle(color, 0.26f, 1f, 8f, 5f);
                case HitKind.Kafa: return new HitStyle(color, 0.34f, 1.08f, 9f, 5f);
                case HitKind.Oldurme: return new HitStyle(color, 0.55f, 1.15f, 10f, 6f);
                case HitKind.KafadanOldurme: return new HitStyle(color, 0.70f, 1.28f, 12f, 6f);
                default: return new HitStyle(color, 0.28f, 1f, 8f, 5f);
            }
        }

        private static Color Rgb(int hex) => new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, 1f);

        /// <summary>
        /// Paletler: varsayılan beyaz/amber/mavi/kırmızı. Kırmızı-yeşil körlüğünde kırmızı yerine
        /// Okabe-Ito renkleri (sarı, gök mavisi, mor-pembe) kullanılır; tritanopide mavi/sarı eksenden kaçınılır.
        /// </summary>
        public static Color PaletteColor(HitKind kind, CrosshairColorBlindMode mode)
        {
            switch (mode)
            {
                case CrosshairColorBlindMode.Deuteranopi:
                case CrosshairColorBlindMode.Protanopi:
                    switch (kind)
                    {
                        case HitKind.Zirh: return Rgb(0x56B4E9);
                        case HitKind.Kafa: return Rgb(0xF0E442);
                        case HitKind.Oldurme: return Rgb(0xCC79A7);
                        case HitKind.KafadanOldurme: return Rgb(0xFF4FD8);
                        default: return Color.white;
                    }
                case CrosshairColorBlindMode.Tritanopi:
                    switch (kind)
                    {
                        case HitKind.Zirh: return Rgb(0x009E73);
                        case HitKind.Kafa: return Rgb(0xFF8A00);
                        case HitKind.Oldurme: return Rgb(0xFF2D2D);
                        case HitKind.KafadanOldurme: return Rgb(0xFF2DAA);
                        default: return Color.white;
                    }
                case CrosshairColorBlindMode.YuksekKontrast:
                    switch (kind)
                    {
                        case HitKind.Zirh: return Rgb(0x00FFFF);
                        case HitKind.Kafa: return Rgb(0xFFFF00);
                        case HitKind.Oldurme: return Rgb(0xFF00FF);
                        case HitKind.KafadanOldurme: return Rgb(0xFF00FF);
                        default: return Color.white;
                    }
                default:
                    switch (kind)
                    {
                        case HitKind.Zirh: return Rgb(0x8EB8D8);
                        case HitKind.Kafa: return Rgb(0xF2A900);
                        case HitKind.Oldurme: return Rgb(0xE23A32);
                        case HitKind.KafadanOldurme: return Rgb(0xFF5A3C);
                        default: return Color.white;
                    }
            }
        }

        /// <summary>İşaretin kalan süreye göre opaklığı: ilk yarı tam, son yarı doğrusal söner.</summary>
        public static float HitAlpha(float remaining, float duration, float settingsOpacity)
        {
            if (duration <= 0.0001f || remaining <= 0f)
                return 0f;
            var t = Mathf.Clamp01(remaining / duration);
            return (t < 0.5f ? t * 2f : 1f) * settingsOpacity;
        }

        /// <summary>İlk <see cref="PopDuration"/> sn'de sinüs yayı ile büyüyüp oturan ölçek.</summary>
        public static float HitScale(float elapsed, float popScale, float sizeSetting)
        {
            var s = 1f;
            if (popScale > 1f && elapsed >= 0f && elapsed < PopDuration)
                s = 1f + (popScale - 1f) * Mathf.Sin(elapsed / PopDuration * Mathf.PI);
            return s * sizeSetting;
        }

        // ---------------- Kontrast ----------------

        /// <summary>WCAG göreli parlaklık (sRGB doğrusallaştırılmış).</summary>
        public static float RelativeLuminance(Color c)
        {
            return 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        }

        private static float Lin(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);

        public static float ContrastRatio(Color a, Color b)
        {
            var la = RelativeLuminance(a);
            var lb = RelativeLuminance(b);
            var hi = Mathf.Max(la, lb);
            var lo = Mathf.Min(la, lb);
            return (hi + 0.05f) / (lo + 0.05f);
        }

        /// <summary>Nişangâh rengine göre en okunur kontur rengi (açık renge siyah, koyu renge beyaz).</summary>
        public static Color OutlineColorFor(Color crosshair, float opacity)
        {
            var dark = ContrastRatio(crosshair, Color.black);
            var light = ContrastRatio(crosshair, Color.white);
            var c = dark >= light ? Color.black : Color.white;
            c.a = Mathf.Clamp01(opacity);
            return c;
        }

        /// <summary>Renk körü kipinde kontur zorunlu ve en az 0.8 opaklıkta olur.</summary>
        public static float EffectiveOutlineOpacity(CrosshairSettings s)
        {
            if (s.ColorBlind == CrosshairColorBlindMode.YuksekKontrast)
                return Mathf.Max(0.8f, s.OutlineOpacity);
            return s.Outline ? s.OutlineOpacity : 0f;
        }
    }
}
