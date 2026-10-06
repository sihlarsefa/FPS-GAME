using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// HUD okunabilirliği (EditMode testli): WCAG göreli parlaklık/kontrast oranı, arka plana göre gölge/dış çizgi şiddeti,
    /// minimum yazı boyutu. Hedef: oyun içi metin için 4.5:1, kritik HUD (mermi, can) için 7:1 (Xbox/GAG kılavuzları).
    /// </summary>
    public static class HudContrast
    {
        public const float TextRatio = 4.5f;
        public const float CriticalRatio = 7f;
        public const float MinFontPx = 14f;

        private static float Lin(float c) => c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        public static float Luminance(Color c) => 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);

        public static float Ratio(Color a, Color b)
        {
            var la = Luminance(a);
            var lb = Luminance(b);
            var hi = Mathf.Max(la, lb);
            var lo = Mathf.Min(la, lb);
            return (hi + 0.05f) / (lo + 0.05f);
        }

        /// <summary>
        /// Metin/arka plan oranı hedefin altındaysa gereken gölge opaklığı (0..0.95). Arka plan bilinmediği için en kötü durum
        /// olarak orta gri (parlak gökyüzü/kum) varsayılabilir: <see cref="WorstCaseBackground"/>.
        /// </summary>
        public static float ShadowAlpha(Color text, Color background, float targetRatio)
        {
            var r = Ratio(text, background);
            if (r >= targetRatio)
                return 0.35f;
            var deficit = Mathf.Clamp01((targetRatio - r) / targetRatio);
            return Mathf.Clamp(0.35f + deficit * 0.75f, 0.35f, 0.95f);
        }

        /// <summary>Parlak açık hava zemini (kum/gökyüzü) — metin için en kötü arka plan varsayımı.</summary>
        public static Color WorstCaseBackground => new Color(0.62f, 0.62f, 0.58f, 1f);

        /// <summary>Gölge kayması (px): küçük yazıda 1, orta 1.5, büyükte 2 (köşegen, sağ-aşağı).</summary>
        public static Vector2 ShadowOffset(float fontPx)
        {
            var d = fontPx < 18f ? 1f : fontPx < 28f ? 1.5f : 2f;
            return new Vector2(d, -d);
        }

        /// <summary>Metin rengi arka plana kıyasla yetersizse beyaza/koyuya çevirir (yüksek kontrastlı olan).</summary>
        public static Color EnsureReadable(Color text, Color background, float targetRatio)
        {
            if (Ratio(text, background) >= targetRatio)
                return text;
            var white = Ratio(Color.white, background);
            var black = Ratio(Color.black, background);
            var pick = white >= black ? Color.white : Color.black;
            pick.a = text.a;
            return pick;
        }

        /// <summary>Çözünürlüğe göre en az yazı boyutu: 1080p'de 14 px, ölçekle orantılı (tavan 28).</summary>
        public static int MinFont(float screenHeight) =>
            Mathf.Clamp(Mathf.RoundToInt(MinFontPx * Mathf.Max(0.5f, screenHeight / 1080f)), 10, 28);

        /// <summary>Yazı bileşenine gölge ekler (yoksa); zaten varsa güncelleyip döndürür.</summary>
        public static UnityEngine.UI.Shadow ApplyShadow(UnityEngine.UI.Text text, Color textColor, float targetRatio)
        {
            if (text == null)
                return null;
            var shadow = text.GetComponent<UnityEngine.UI.Shadow>();
            if (shadow == null)
                shadow = text.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, ShadowAlpha(textColor, WorstCaseBackground, targetRatio));
            shadow.effectDistance = ShadowOffset(text.fontSize);
            return shadow;
        }
    }
}
