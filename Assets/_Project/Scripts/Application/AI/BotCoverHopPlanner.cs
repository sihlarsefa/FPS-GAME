using System;

namespace Project.Application.AI
{
    /// <summary>
    /// Siperden siper ilerleme (saf mantık). Bir sonraki siper adayı: hedefe yaklaştırır ama açıkta koşu mesafesi sınırlı,
    /// koşu sırasında hedef görüş konisinde kalma süresi minimumda, dostlarla aynı siperi paylaşmaz.
    /// Koşu mesafesi tipik 6–14 m (CoD/Squad: kısa sıçramalar); koşu süresi = mesafe / sürat.
    /// </summary>
    public static class BotCoverHopPlanner
    {
        public const float MinHop = 4f;
        public const float MaxHop = 15f;

        /// <summary>
        /// Aday puanı (YÜKSEK = iyi). hopDistance: koşu (m), gain: hedefe yaklaşma (m, pozitif = yaklaşır),
        /// exposedFraction: koşu hattının hedefe açık oranı, allyDistance: en yakın dost-siper mesafesi, high: yüksek siper.
        /// </summary>
        public static float Score(float hopDistance, float gain, float exposedFraction, float allyDistance, bool high,
            float suppression01, bool enemySuppressedByAllies)
        {
            if (hopDistance < MinHop || hopDistance > MaxHop)
                return float.MinValue;
            var s = gain * 1.4f;
            s -= hopDistance * 0.9f;
            s -= Clamp01(exposedFraction) * 22f * (enemySuppressedByAllies ? 0.5f : 1f);
            if (allyDistance < 2f) s -= 15f;
            if (high) s += 4f;
            s -= Clamp01(suppression01) * hopDistance * 0.8f; // baskı altında uzun koşu daha pahalı
            return s;
        }

        /// <summary>Koşu süresi (sn).</summary>
        public static float HopSeconds(float hopDistance, float speed)
        {
            return hopDistance / Math.Max(1.5f, speed);
        }

        /// <summary>Sıçramaya ne zaman kalkılır: dostlar bastırıyor, kendi baskım düşük, en az 1.0 sn ara.</summary>
        public static bool ShouldHop(bool alliesSuppressing, float suppression01, float secondsInCover, float health01, bool enemyReloadingOrLost)
        {
            if (secondsInCover < 1.0f) return false;
            if (suppression01 > 0.7f && !alliesSuppressing) return false;
            if (health01 < 0.4f) return false;
            return alliesSuppressing || enemyReloadingOrLost;
        }

        /// <summary>Sıçrama sürekliliği: yarı yolda ateş altında kalan bot en yakın siperlerden birine kısa kaçar (ileri/geri).</summary>
        public static bool ShouldAbortHop(float progress01, float newDamage, float suppression01)
        {
            if (progress01 > 0.6f) return false;   // dönmektense bitir
            return newDamage > 0.15f || suppression01 > 0.8f;
        }

        /// <summary>Hedefe dönük "sprint" mı yoksa eğilerek koşu mu: uzak ve açıksa koş; yakın ve baskı varsa çömelik.</summary>
        public static bool UseSprint(float hopDistance, float suppression01, bool enemyAiming)
        {
            if (enemyAiming && hopDistance > 8f) return false;
            return suppression01 < 0.6f;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
