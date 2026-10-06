using System;

namespace Project.Infrastructure.Audio.Weather
{
    /// <summary>Şimşek parlaması zarfı (saf): toplam 0.1-0.3 sn içinde 1-3 darbe. Tohumdan deterministik.</summary>
    public static class LightningFlash
    {
        public const float MinTotal = 0.1f;
        public const float MaxTotal = 0.3f;

        public static int PulseCount(int seed) => 1 + (int)((uint)Hash(seed) % 3u);

        /// <summary>Toplam parlama süresi (sn) [0.1, 0.3].</summary>
        public static float TotalSeconds(int seed) => MinTotal + (MaxTotal - MinTotal) * ((Hash(seed ^ 0x5bd1e995) & 0xFFFF) / 65535f);

        /// <summary>t: şimşekten beri sn. Dönüş 0..1 parlaklık.</summary>
        public static float Envelope(float t, int seed)
        {
            var total = TotalSeconds(seed);
            if (t < 0f || t >= total) return 0f;
            var n = PulseCount(seed);
            var slot = total / n;
            var k = Math.Min(n - 1, (int)(t / slot));
            var local = (t - k * slot) / slot; // 0..1
            var amp = k == 0 ? 1f : 0.75f;
            // hızlı yükselme (%20), üstel sönüm
            var shape = local < 0.2f ? local / 0.2f : (float)Math.Exp(-(local - 0.2f) * 3.5f);
            return Math.Min(1f, amp * shape);
        }

        private static int Hash(int x)
        {
            unchecked
            {
                var h = (uint)x * 2654435761u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (int)(h & 0x7FFFFFFF);
            }
        }
    }
}
