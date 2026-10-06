using System;

namespace Project.Application.AI
{
    public enum PeekStyle
    {
        /// <summary>Kısa omuz bakışı: 0.25–0.4 sn, ateş yok, bilgi toplar (jiggle).</summary>
        Shoulder = 0,
        /// <summary>Lean + ateş: köşeye yaslanıp 0.8–1.4 sn atış.</summary>
        LeanFire = 1,
        /// <summary>Geniş açılım: köşeyi yayla tarayarak (slice-the-pie) aç, ateş hazır.</summary>
        WidePie = 2,
        /// <summary>Önceden nişanlı hızlı çıkış-vur (pre-aim): bilinen tutuş noktası.</summary>
        PreAim = 3,
        /// <summary>Kör/üstten ateş (silahı siperin üstüne uzat): yüksek baskı, düşük isabet.</summary>
        BlindFire = 4
    }

    /// <summary>
    /// Köşe/siper "peek" planlayıcı (saf mantık). Tarkov/CoD botları aynı noktadan tekrar tekrar çıkmaz:
    /// peek stili bilgiye (düşman konumu biliniyor mu), baskıya, silaha ve bir önceki peek'in ceza geçmişine göre seçilir.
    /// Aynı kenardan arka arkaya peek'e öngörülebilirlik cezası (oyuncu pre-fire eder).
    /// </summary>
    public static class BotPeekPlanner
    {
        public const int MaxRepeatsBeforeRelocate = 2;

        /// <summary>
        /// Stil seç. knownEnemy: bilinen düşman konumu; underFire: baskı 0..1; skill01; closeRange: &lt; 18 m;
        /// repeatsAtEdge: aynı kenardan art arda peek sayısı; rng01 [0,1].
        /// </summary>
        public static PeekStyle ChooseStyle(bool knownEnemy, float suppression01, float skill01, bool closeRange,
            int repeatsAtEdge, float ammo01, float rng01)
        {
            if (ammo01 < 0.15f)
                return PeekStyle.Shoulder;
            if (suppression01 > 0.75f)
                return rng01 < 0.55f ? PeekStyle.BlindFire : PeekStyle.Shoulder;
            if (!knownEnemy)
                return skill01 > 0.5f && rng01 < 0.6f ? PeekStyle.WidePie : PeekStyle.Shoulder;
            if (repeatsAtEdge >= MaxRepeatsBeforeRelocate)
                return PeekStyle.Shoulder; // oyuncu pre-fire eder: bilgi topla, yer değiştir
            if (closeRange)
                return rng01 < 0.5f + skill01 * 0.3f ? PeekStyle.PreAim : PeekStyle.LeanFire;
            return rng01 < 0.35f + skill01 * 0.35f ? PeekStyle.LeanFire : PeekStyle.PreAim;
        }

        /// <summary>Stilin süresi (sn) — maruz kalma penceresi.</summary>
        public static float ExposureSeconds(PeekStyle style, float skill01, float rng01)
        {
            float lo, hi;
            switch (style)
            {
                case PeekStyle.Shoulder: lo = 0.25f; hi = 0.4f; break;
                case PeekStyle.LeanFire: lo = 0.8f; hi = 1.4f; break;
                case PeekStyle.WidePie: lo = 0.9f; hi = 1.6f; break;
                case PeekStyle.PreAim: lo = 0.45f; hi = 0.8f; break;
                default: lo = 0.9f; hi = 1.8f; break;
            }
            var t = lo + (hi - lo) * Clamp01(rng01);
            return t * (1.1f - 0.2f * Clamp01(skill01)); // kıdemli daha kısa maruz kalır
        }

        /// <summary>Peek'te gövdenin dışarı çıkma oranı 0..1 (1 = tam açık, 0 = yalnız silah/göz).</summary>
        public static float BodyExposure(PeekStyle style)
        {
            switch (style)
            {
                case PeekStyle.Shoulder: return 0.3f;
                case PeekStyle.LeanFire: return 0.45f;
                case PeekStyle.WidePie: return 0.75f;
                case PeekStyle.PreAim: return 0.9f;
                default: return 0.1f;
            }
        }

        /// <summary>Stilin atış isabet çarpanı (1 = normal).</summary>
        public static float AccuracyMultiplier(PeekStyle style)
        {
            switch (style)
            {
                case PeekStyle.Shoulder: return 0f;     // ateş etmez
                case PeekStyle.LeanFire: return 0.9f;
                case PeekStyle.WidePie: return 1.0f;
                case PeekStyle.PreAim: return 1.15f;
                default: return 0.35f;
            }
        }

        /// <summary>Saklanma süresi (sn): peek sonrası; art arda tekrar edildikçe uzar (rutin kırma), baskı arttıkça uzar.</summary>
        public static float HiddenSeconds(int repeatsAtEdge, float suppression01, float skill01, float rng01)
        {
            var t = 0.9f + Clamp01(rng01) * 1.6f;
            t *= 1f + Clamp01(suppression01) * 0.9f;
            t *= 1f + Math.Max(0, repeatsAtEdge) * 0.25f;
            t *= 1.15f - 0.3f * Clamp01(skill01);
            return t;
        }

        /// <summary>Yer değiştir: aynı kenardan çok peek yapıldı ya da düşman o kenarı hedef aldı.</summary>
        public static bool ShouldRelocate(int repeatsAtEdge, bool enemyPreAimingEdge, float suppression01)
        {
            return repeatsAtEdge > MaxRepeatsBeforeRelocate || enemyPreAimingEdge || suppression01 > 0.85f;
        }

        /// <summary>
        /// Kenar seçimi: -1 sol, +1 sağ. Sağ elli silah sağdan açılınca gövde daha çok siperde kalır;
        /// bu yüzden varsayılan sağ yön (rightHanded) bonuslu. Son peek yönü tekrar edilmez.
        /// </summary>
        public static int ChooseEdge(bool leftOpen, bool rightOpen, int lastEdge, bool rightHanded)
        {
            if (leftOpen && !rightOpen) return -1;
            if (rightOpen && !leftOpen) return 1;
            if (!leftOpen && !rightOpen) return 0;
            if (lastEdge != 0)
                return -lastEdge;
            return rightHanded ? 1 : -1;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
