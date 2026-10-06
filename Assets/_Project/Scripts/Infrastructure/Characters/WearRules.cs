using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Savaş yıpranması (Yıpranma 0..1) saf kuralları: maç içi yükselme ve doku önbelleğini sınırlayan nicemleme.
    /// Başlangıç 0.15, indirilen (downed) başına +0.2, alınan hasar ve maç süresi küçük katkı; üst sınır 1.
    /// </summary>
    public static class WearRules
    {
        public const float MatchStart = 0.15f;
        public const float PerDowned = 0.2f;
        public const float DamageWeight = 0.25f;
        public const float TimeWeight = 0.2f;
        /// <summary>Süre katkısının doyduğu maç süresi (sn).</summary>
        public const float TimeSaturateSeconds = 1200f;
        public const int Variants = 5;

        // ------------------------------------------------------------------ Karartma sınırları (görsel denge)
        // Yıpranma doku fırınları (CharacterTextureGen) karartmayı ÇARPAN YIĞINI olarak değil, sınırlı yama/ton kayması olarak uygular.
        // Ölçüt: ortalama sRGB parlaklık oranı = yıpranmış doku / temiz doku (0.299R + 0.587G + 0.114B). 2026-10-06: eski fırın
        // wear 1.0'da üniformada ~0.85-0.88, yüzde ~0.88 veriyordu (siyaha inmiyordu); yeni fırın taban altına İNEMEZ.

        /// <summary>Görsel güç doyma noktası: bu yıpranmadan sonra (0.70) fırın ek güç uygulamaz. 0.85 gibi hero değeri 1.0 kovasına düşse de
        /// görünüm 0.66 kovasından (güç 0.94) fazla ağırlaşmaz; <see cref="Quantize"/> API'si değişmez.</summary>
        public const float VisualSaturation = 0.70f;

        /// <summary>wear 1.0'da üniforma ortalama parlaklık oranı tabanı (tasarım; fırında koruma geçişi uygular). Kabul tabanı 0.62'nin üstünde pay bırakır.</summary>
        public const float UniformLuminanceFloor = 0.80f;

        /// <summary>wear 1.0'da yüz (ten × çarpan dokusu, ön yüz) ortalama parlaklık oranı tasarım hedefi (testle doğrulanır). Kabul tabanı 0.60.</summary>
        public const float FaceLuminanceFloor = 0.85f;

        /// <summary>Fırının kullandığı 0..1 görsel güç: wear / 0.70 (üstü 1'e doyar). 0 → 0, 0.33 → 0.47, 0.66 → 0.94, 0.85 ve 1.0 → 1.</summary>
        public static float VisualStrength(float wear) => Mathf.Clamp01(Mathf.Clamp01(wear) / VisualSaturation);

        /// <summary>Verilen görsel güçte izin verilen en düşük ortalama parlaklık oranı: 1 (güç 0) → floorAtFull (güç 1) doğrusal.</summary>
        public static float LuminanceFloor(float visualStrength, float floorAtFull) =>
            Mathf.Lerp(1f, Mathf.Clamp01(floorAtFull), Mathf.Clamp01(visualStrength));

        // Sert ekipman (shader) yıpranma skalerleri: kir/toz hafif artar, kenar aşınması İNCE kalır.
        // Eski (wear 1.0'da): kir +0.35, toz +0.25, kenar max(·,0.2)+0.5 → Cordura 0.70, kask 0.95. Yeni: +0.20, +0.15, max(·,0.1)+0.2 → 0.30 / 0.65.

        /// <summary>Gear shader kir gücü: taban + 0.20 × görsel güç (0..1). wear 0 → taban.</summary>
        public static float GearDirt(float baseDirt, float wear) => Mathf.Clamp01(baseDirt + 0.20f * VisualStrength(wear));

        /// <summary>Gear shader toz gücü: taban + 0.15 × görsel güç (0..1). wear 0 → taban.</summary>
        public static float GearDust(float baseDust, float wear) => Mathf.Clamp01(baseDust + 0.15f * VisualStrength(wear));

        /// <summary>Boya/cordura kenar aşınması: max(taban, 0.1) + 0.2 × görsel güç (0..1). Çizik/kenar ince kalır. wear 0 → taban.</summary>
        public static float GearEdgeWear(float baseEdgeWear, float wear) =>
            wear <= 0f ? baseEdgeWear : Mathf.Clamp01(Mathf.Max(baseEdgeWear, 0.1f) + 0.2f * VisualStrength(wear));

        /// <summary>damageFraction: bu maçta alınan toplam hasarın can havuzuna oranı (0..1+). downedCount: kaç kez yere düştü.</summary>
        public static float Compute(float damageFraction, float matchSeconds, int downedCount)
        {
            var w = MatchStart
                    + PerDowned * Mathf.Max(0, downedCount)
                    + DamageWeight * Mathf.Clamp01(damageFraction)
                    + TimeWeight * Mathf.Clamp01(matchSeconds / TimeSaturateSeconds);
            return Mathf.Clamp01(w);
        }

        /// <summary>0..3 seviye indeksi (0, .33, .66, 1'e en yakın).</summary>
        public static int LevelIndex(float wear) => Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(wear) * 3f), 0, 3);

        /// <summary>Yıpranmayı 0 / 0.33 / 0.66 / 1 değerlerine nicemler (doku önbelleği sınırlı kalır).</summary>
        public static float Quantize(float wear)
        {
            switch (LevelIndex(wear))
            {
                case 1: return 0.33f;
                case 2: return 0.66f;
                case 3: return 1f;
                default: return 0f;
            }
        }

        /// <summary>İki yıpranma değeri farklı doku seviyesine düşüyor mu (malzeme yeniden bağlamak gerekir mi).</summary>
        public static bool NeedsRebind(float oldWear, float newWear) => LevelIndex(oldWear) != LevelIndex(newWear);

        /// <summary>Tohumdan 5 belirleyici yüz varyantından biri (0..4).</summary>
        public static int FaceVariant(int seed)
        {
            unchecked { return (int)(((uint)(seed * 2654435761u)) >> 8) % Variants; }
        }

        /// <summary>Ter parlaması için ten pürüzsüzlük artışı (nicemlenmiş seviyeye göre).</summary>
        public static float SkinSmoothnessBoost(float quantizedWear) => 0.1f * Mathf.Clamp01(quantizedWear);
    }
}
