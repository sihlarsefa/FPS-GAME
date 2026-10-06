using System;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Nehir akıntı/köpük kuralları (saf, Unity bağımlılığı yok, deterministik). Hız 0..1 normalleştirilmiş akıntıdır:
    /// eğim ve daralma arttıkça hızlanır. Köpük yoğunluğu, taş halkası, çakıl şeridi ve su sesi şiddeti bundan türer.
    /// </summary>
    public static class RiverFlowRules
    {
        public const int MinRocks = 6;
        public const int MaxRocks = 10;

        /// <summary>Kesit akıntı hızı 0..1: drop = yatay metre başına dikey düşüş (m/m), width = nehir genişliği, baseWidth = ortalama.</summary>
        public static float Speed01(float drop, float width, float baseWidth)
        {
            var slope = Clamp(Math.Abs(drop), 0f, 0.2f);
            var narrow = baseWidth > 0.01f && width > 0.01f ? Clamp(baseWidth / width - 1f, -0.5f, 1f) : 0f;
            return Clamp(0.2f + slope * 12f + narrow * 0.45f, 0f, 1f);
        }

        /// <summary>Akıntı yönünde köpük şeridi yoğunluğu 0..1: yavaş kesitte yok, hızlıda yoğun.</summary>
        public static float FoamDensity(float speed01)
        {
            var t = Clamp((speed01 - 0.35f) / 0.5f, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Köpük şeridi boyu (m): hızlı kesitte uzar.</summary>
        public static float StreakLength(float speed01) => 3f + 9f * Clamp(speed01, 0f, 1f);

        /// <summary>Kesit başına köpük şeridi sayısı (0..3).</summary>
        public static int StreakCount(float speed01) => (int)Math.Round(3f * FoamDensity(speed01));

        /// <summary>Taş etrafı köpük halkası yarıçapı (m): taş boyutu ve akıntı ile büyür.</summary>
        public static float RockRingRadius(float rockSize, float speed01) => rockSize * (0.9f + 0.5f * Clamp(speed01, 0f, 1f)) + 0.5f;

        /// <summary>Nehir uzunluğuna göre nehir içi taş adedi (6..10).</summary>
        public static int RockCount(float riverLength) => (int)Clamp((float)Math.Round(riverLength / 80f), MinRocks, MaxRocks);

        /// <summary>i. taşın nehir üzerindeki konumu 0..1 (tabakalı + sabit jitter; sınırlardan uzak).</summary>
        public static float RockT(int i, int n, int seed)
        {
            var jitter = Hash01(seed, i * 2 + 1) - 0.5f;
            return Clamp((i + 0.5f + jitter * 0.7f) / Math.Max(1, n), 0.04f, 0.96f);
        }

        /// <summary>i. taşın yanal ofseti -0.6..0.6 (nehir yarı genişliği oranı).</summary>
        public static float RockLateral(int i, int seed) => (Hash01(seed, i * 2 + 2) - 0.5f) * 1.2f;

        /// <summary>Taş boyutu 0.9..2.1 m.</summary>
        public static float RockSize(int i, int seed) => 0.9f + 1.2f * Hash01(seed + 17, i * 3 + 5);

        /// <summary>Sığ kenar çakıl şeridi genişliği (m): eğim dikleştikçe daralır, en az 0.6.</summary>
        public static float GravelWidth(float bankSlope) => Clamp(2.4f - 2.2f * Clamp(bankSlope, 0f, 1f), 0.6f, 2.4f);

        /// <summary>Köprü ayağı akıntı izi uzunluğu (m): hıza bağlı.</summary>
        public static float PierWakeLength(float speed01) => 3.5f + 6f * Clamp(speed01, 0f, 1f);

        /// <summary>Su sesi şiddeti 0..1: akıntı hızı ve nehre uzaklık (m). 40 m ötesinde sessiz. Ambience için ENTEGRASYON girdisi.</summary>
        public static float WaterSoundIntensity(float speed01, float distanceToRiver)
        {
            var near = 1f - Clamp((distanceToRiver - 4f) / 36f, 0f, 1f);
            return Clamp((0.25f + 0.75f * Clamp(speed01, 0f, 1f)) * near, 0f, 1f);
        }

        /// <summary>Deterministik 0..1 hash.</summary>
        public static float Hash01(int seed, int k)
        {
            unchecked
            {
                var h = (uint)(seed * 73856093) ^ (uint)(k * 19349663);
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
