using System.Collections.Generic;

namespace Project.Infrastructure.World
{
    /// <summary>Çukur analizi sonucu: su birikintisi adayı (saf veri).</summary>
    public struct TerrainPit
    {
        public float X, Z, Depth, Radius;
    }

    /// <summary>
    /// Arazi mikro-detay saf matematiği (Unity nesnesi yok → EditMode testli): yol kenarı aşınma şeridi, çukur analizi
    /// (su birikintisi yerleşimi), kaya çatlağı skoru, toprak-çim geçiş bandı. Hepsi belirlenimci.
    /// </summary>
    public static class TerrainMicroDetailMath
    {
        /// <summary>Yol kenarı aşınma yoğunluğu (0..1): kenardan hemen dışarıda zirve, width kadar uzakta 0. Yol içi sönük kalır.</summary>
        public static float RoadWear(float edgeDistance, float width)
        {
            if (float.IsInfinity(edgeDistance) || float.IsNaN(edgeDistance) || width <= 1e-4f) return 0f;
            if (edgeDistance < -0.5f) return 0f;
            if (edgeDistance < 0f) return (edgeDistance + 0.5f) / 0.5f * 0.6f;
            if (edgeDistance >= width) return 0f;
            var t = edgeDistance / width;
            return (1f - t) * (1f - t) * 0.4f + (1f - t) * 0.6f;
        }

        /// <summary>Toprak-çim geçiş bandı (0..1): çim ağırlığı 0.5 civarında zirve, uçlarda 0.</summary>
        public static float GrassSoilBand(float grassWeight)
        {
            var w = grassWeight < 0f ? 0f : (grassWeight > 1f ? 1f : grassWeight);
            return 1f - System.Math.Abs(2f * w - 1f);
        }

        /// <summary>Kaya çatlağı skoru (0..1): dik eğim + gürültü [-1,1] birleşimi. minSlope altında 0.</summary>
        public static float RockCrackScore(float slopeDeg, float noise, float minSlope = 38f)
        {
            if (slopeDeg < minSlope) return 0f;
            var s = (slopeDeg - minSlope) / 30f;
            s = s > 1f ? 1f : s;
            var n = (noise + 1f) * 0.5f;
            n = n < 0f ? 0f : (n > 1f ? 1f : n);
            return s * n;
        }

        /// <summary>Hücrenin çevre halkasına göre çukur derinliği (m): halka ortalaması - merkez. Negatif = tümsek.</summary>
        public static float PitDepth(float[] heights, int res, int ix, int iz, int ring)
        {
            if (heights == null || ring < 1 || ix < ring || iz < ring || ix >= res - ring || iz >= res - ring) return 0f;
            var sum = 0f; var n = 0;
            for (var k = -ring; k <= ring; k++)
            {
                sum += heights[(iz - ring) * res + ix + k]; sum += heights[(iz + ring) * res + ix + k];
                n += 2;
                if (k > -ring && k < ring)
                {
                    sum += heights[(iz + k) * res + ix - ring]; sum += heights[(iz + k) * res + ix + ring];
                    n += 2;
                }
            }
            return sum / n - heights[iz * res + ix];
        }

        /// <summary>
        /// Çukurları tarar: derinlik >= minDepth, yerel minimum, yükseklik >= minHeight (su altı değil). Derinliğe göre azalan sıralı,
        /// maxCount ile kırpılır. stride hücre adımıdır.
        /// </summary>
        public static List<TerrainPit> FindPits(float[] heights, int res, float cell, float originX, float originZ,
            float minDepth, float minHeight, int ring, int stride, int maxCount)
        {
            var list = new List<TerrainPit>();
            if (heights == null || res < 2 * ring + 1 || stride < 1 || maxCount <= 0) return list;
            for (var iz = ring; iz < res - ring; iz += stride)
            for (var ix = ring; ix < res - ring; ix += stride)
            {
                var h = heights[iz * res + ix];
                if (h < minHeight) continue;
                var d = PitDepth(heights, res, ix, iz, ring);
                if (d < minDepth) continue;
                if (heights[iz * res + ix - 1] < h || heights[iz * res + ix + 1] < h ||
                    heights[(iz - 1) * res + ix] < h || heights[(iz + 1) * res + ix] < h) continue;
                list.Add(new TerrainPit { X = originX + ix * cell, Z = originZ + iz * cell, Depth = d, Radius = ring * cell });
            }
            list.Sort((a, b) => b.Depth.CompareTo(a.Depth));
            if (list.Count > maxCount) list.RemoveRange(maxCount, list.Count - maxCount);
            return list;
        }

        /// <summary>Kalite kademesine (0..3) göre mikro-detay çıkartma bütçesi: [yol aşınma, birikinti, kaya çatlağı, geçiş bandı].</summary>
        public static int[] Budget(int tier)
        {
            tier = tier < 0 ? 0 : (tier > 3 ? 3 : tier);
            switch (tier)
            {
                case 0: return new[] { 0, 0, 0, 0 };
                case 1: return new[] { 40, 16, 20, 16 };
                case 2: return new[] { 90, 32, 45, 36 };
                default: return new[] { 160, 56, 80, 64 };
            }
        }
    }
}
