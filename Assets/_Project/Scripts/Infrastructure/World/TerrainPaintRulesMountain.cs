using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Dağ boyama saf kuralları: dik yüzde kaya + koyulaşma, eğime duyarlı kar bandı (test edilebilir).</summary>
    public static partial class TerrainPaintRules
    {
        /// <summary>Dik yüz eşiği (derece): üstünde kar tutmaz, kaya açığa çıkar.</summary>
        public const float CliffSlope = 35f;

        /// <summary>Dik yamaç (&gt;35°) kaya ağırlığı [0,0.95]; yükseldikçe biraz artar.</summary>
        public static float CliffRock(float slopeDegrees, float height)
        {
            var s = TerrainNoise.SmoothStep(CliffSlope - 3f, CliffSlope + 9f, slopeDegrees);
            var alt = 1f + 0.1f * TerrainNoise.SmoothStep(60f, 130f, height);
            return Mathf.Clamp01(s * 0.9f * alt);
        }

        /// <summary>Kaya yüzünde makro koyulaşma çarpanı [0.55,1] (1 = değişiklik yok); dikleştikçe koyulaşır.</summary>
        public static float CliffDarkening(float slopeDegrees)
        {
            return 1f - 0.45f * TerrainNoise.SmoothStep(CliffSlope, 55f, slopeDegrees);
        }

        /// <summary>
        /// Kar maskesi [0,1]: yükseklik bandı (snowLine -7..+6, gürültüyle oynar) x eğim (dik yüz çıplak kalır,
        /// 32-46° arası kar yamalı çözülür). Gürültü [-1,1].
        /// </summary>
        public static float SlopeSnow(float height, float snowLine, float slopeDegrees, float noise)
        {
            var band = TerrainNoise.SmoothStep(snowLine - 7f, snowLine + 6f, height + noise * 6f);
            var lo = 32f + noise * 3f;
            var hi = 46f + noise * 3f;
            return band * (1f - TerrainNoise.SmoothStep(lo, hi, slopeDegrees));
        }

        /// <summary>Kar bandında dik yüzde açığa çıkan kaya [0,0.9].</summary>
        public static float SnowExposedRock(float height, float snowLine, float slopeDegrees)
        {
            var band = TerrainNoise.SmoothStep(snowLine - 7f, snowLine + 6f, height);
            return band * TerrainNoise.SmoothStep(30f, 42f, slopeDegrees) * 0.9f;
        }

        /// <summary>Yüksek rakımda sırt keskinleştirme genliği (m): yalnızca kenar dağlarında (edgeT) ve yükseklikte.</summary>
        public static float RidgeSharpen(float edgeT, float ridgedNoise)
        {
            var e = Mathf.Clamp01(edgeT);
            return e * e * 14f * (Mathf.Clamp01(ridgedNoise) - 0.4f);
        }
    }
}
