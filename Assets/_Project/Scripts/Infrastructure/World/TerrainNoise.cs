using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Tohumlu 2B gradyan (Perlin) gürültüsü, fBm, sırt (ridged) ve periyodik (döşenebilir) türevleri. Belirlenimci, motor
    /// gürültüsüne (Mathf.PerlinNoise) bağımlı değildir; aynı tohum aynı araziyi üretir.
    /// </summary>
    public sealed class TerrainNoise
    {
        private readonly int[] _perm = new int[512];

        public TerrainNoise(int seed)
        {
            var p = new int[256];
            for (var i = 0; i < 256; i++)
                p[i] = i;

            var rng = new System.Random(seed);
            for (var i = 255; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                var t = p[i];
                p[i] = p[j];
                p[j] = t;
            }

            for (var i = 0; i < 512; i++)
                _perm[i] = p[i & 255];
        }

        /// <summary>Gradyan gürültüsü, yaklaşık [-1, 1].</summary>
        public float Perlin(float x, float y)
        {
            var xf = Mathf.Floor(x);
            var yf = Mathf.Floor(y);
            var xi = (int)xf & 255;
            var yi = (int)yf & 255;
            x -= xf;
            y -= yf;
            var u = Fade(x);
            var v = Fade(y);
            var aa = _perm[_perm[xi] + yi];
            var ab = _perm[_perm[xi] + yi + 1];
            var ba = _perm[_perm[xi + 1] + yi];
            var bb = _perm[_perm[xi + 1] + yi + 1];
            var x1 = Lerp(Grad(aa, x, y), Grad(ba, x - 1f, y), u);
            var x2 = Lerp(Grad(ab, x, y - 1f), Grad(bb, x - 1f, y - 1f), u);
            return Lerp(x1, x2, v) * 1.41f;
        }

        /// <summary>Periyodik gradyan gürültüsü (periodX/periodY kafes hücresinde tekrar eder; döşenebilir doku için).</summary>
        public float PerlinPeriodic(float x, float y, int periodX, int periodY)
        {
            periodX = Mathf.Max(1, periodX);
            periodY = Mathf.Max(1, periodY);
            var xf = Mathf.Floor(x);
            var yf = Mathf.Floor(y);
            var x0 = Mod((int)xf, periodX);
            var y0 = Mod((int)yf, periodY);
            var x1i = Mod(x0 + 1, periodX);
            var y1i = Mod(y0 + 1, periodY);
            x -= xf;
            y -= yf;
            var u = Fade(x);
            var v = Fade(y);
            var aa = _perm[_perm[x0 & 255] + (y0 & 255)];
            var ab = _perm[_perm[x0 & 255] + (y1i & 255)];
            var ba = _perm[_perm[x1i & 255] + (y0 & 255)];
            var bb = _perm[_perm[x1i & 255] + (y1i & 255)];
            var a = Lerp(Grad(aa, x, y), Grad(ba, x - 1f, y), u);
            var b = Lerp(Grad(ab, x, y - 1f), Grad(bb, x - 1f, y - 1f), u);
            return Lerp(a, b, v) * 1.41f;
        }

        /// <summary>Fraktal Brown hareketi, yaklaşık [-1, 1].</summary>
        public float Fbm(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amplitude = 1f, norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                sum += Perlin(x, y) * amplitude;
                norm += amplitude;
                amplitude *= gain;
                x = x * lacunarity + 17.13f;
                y = y * lacunarity - 9.71f;
            }

            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Döşenebilir fBm (temel periyot period hücre; her oktav periyodu ikiye katlanır).</summary>
        public float FbmPeriodic(float x, float y, int period, int octaves, float gain = 0.5f)
        {
            float sum = 0f, amplitude = 1f, norm = 0f;
            var p = period;
            for (var i = 0; i < octaves; i++)
            {
                sum += PerlinPeriodic(x, y, p, p) * amplitude;
                norm += amplitude;
                amplitude *= gain;
                x *= 2f;
                y *= 2f;
                p *= 2;
            }

            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Sırt (ridged multifractal) gürültüsü, [0, 1]. Keskin dağ sırtları için.</summary>
        public float Ridged(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amplitude = 1f, norm = 0f, weight = 1f;
            for (var i = 0; i < octaves; i++)
            {
                var n = 1f - Mathf.Abs(Perlin(x, y));
                n *= n;
                n *= weight;
                weight = Mathf.Clamp01(n * 1.6f);
                sum += n * amplitude;
                norm += amplitude;
                amplitude *= gain;
                x = x * lacunarity + 31.7f;
                y = y * lacunarity + 5.3f;
            }

            return norm > 0f ? Mathf.Clamp01(sum / norm * 1.25f) : 0f;
        }

        private static int Mod(int a, int m)
        {
            var r = a % m;
            return r < 0 ? r + m : r;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Grad(int hash, float x, float y)
        {
            switch (hash & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        // ------------------------------------------------------------------ Ortak matematik

        /// <summary>Hermite yumuşak basamak; e0 &gt; e1 ise ters yönde çalışır.</summary>
        public static float SmoothStep(float e0, float e1, float x)
        {
            if (Mathf.Approximately(e0, e1))
                return x < e0 ? 0f : 1f;
            var t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Yumuşak minimum (k: geçiş genişliği, m).</summary>
        public static float SmoothMin(float a, float b, float k)
        {
            if (k <= 0f)
                return Mathf.Min(a, b);
            var h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        /// <summary>Noktanın [a,b] doğru parçasına uzaklığı ve parça parametresi (0..1).</summary>
        public static float SegmentDistance(float px, float pz, float ax, float az, float bx, float bz, out float t)
        {
            var dx = bx - ax;
            var dz = bz - az;
            var lenSq = dx * dx + dz * dz;
            t = lenSq > 1e-6f ? Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / lenSq) : 0f;
            var cx = ax + dx * t - px;
            var cz = az + dz * t - pz;
            return Mathf.Sqrt(cx * cx + cz * cz);
        }
    }
}
