using System;

namespace Project.Core.Domain
{
    /// <summary>Kalite kademesi başına mesh çim ayarı (saf veri; Unity bağımlılığı yok).</summary>
    public readonly struct GrassTierConfig
    {
        public readonly bool Enabled;
        /// <summary>Çimin çizildiği en uzak mesafe (m).</summary>
        public readonly float DrawDistance;
        /// <summary>Tam yoğunlukta m² başına çim demeti (kümesi).</summary>
        public readonly float DensityPerSqm;
        /// <summary>Kare başına en fazla kaç hücre üretilir (CPU bütçesi).</summary>
        public readonly int CellBuildsPerFrame;
        /// <summary>Aynı anda izlenen ezici (oyuncu/araç) sayısı (0..8).</summary>
        public readonly int Interactors;
        /// <summary>Çim kümesi başına bıçak sayısı çarpanı için LOD ölçeği (1 = tam).</summary>
        public readonly float DetailScale;

        public GrassTierConfig(bool enabled, float drawDistance, float densityPerSqm, int cellBuildsPerFrame, int interactors, float detailScale)
        {
            Enabled = enabled;
            DrawDistance = drawDistance;
            DensityPerSqm = densityPerSqm;
            CellBuildsPerFrame = cellBuildsPerFrame;
            Interactors = interactors;
            DetailScale = detailScale;
        }
    }

    /// <summary>Mesh çim saf kuralları: kademe tablosu, katman yoğunluğu, hücre/hash, mesafe seyreltme, ezilme, en yakın seçim.</summary>
    public static class GrassRules
    {
        public const float CellSize = 16f;
        public const int VariantCount = 3;
        /// <summary>Mesafe sonunda çimin boyunu sıfıra indirdiği şerit (m); ani belirmeyi önler.</summary>
        public const float FadeWidth = 10f;

        /// <summary>Düşük kapalı; Orta/Yüksek/Ultra artan mesafe ve yoğunluk.</summary>
        public static GrassTierConfig ForTier(int tier)
        {
            switch (Clamp(tier, 0, 3))
            {
                case 0: return new GrassTierConfig(false, 0f, 0f, 0, 0, 0f);
                case 1: return new GrassTierConfig(true, 40f, 3f, 2, 2, 0.7f);
                case 2: return new GrassTierConfig(true, 65f, 6f, 3, 4, 1f);
                default: return new GrassTierConfig(true, 95f, 10f, 4, 8, 1f);
            }
        }

        /// <summary>Arazi katmanı (TerrainLayerKind sırası: Çim, Kuru çim, Toprak, Kaya, Çakıl, Çamur, Kar, Asfalt) başına çim çarpanı.</summary>
        public static float LayerDensity(int layerIndex)
        {
            switch (layerIndex)
            {
                case 0: return 1.0f;   // çim
                case 1: return 0.75f;  // kuru çim
                case 2: return 0.12f;  // toprak
                case 3: return 0f;     // kaya
                case 4: return 0.03f;  // çakıl
                case 5: return 0.1f;   // çamur
                case 6: return 0f;     // kar
                default: return 0f;    // asfalt ve bilinmeyen
            }
        }

        /// <summary>Katman ağırlıklarından (toplam ~1) çim yoğunluğu 0..1. Yol/kaya/kar yoğunluğu düşürür.</summary>
        public static float Density(float[] weights, int count)
        {
            if (weights == null) return 0f;
            float d = 0f;
            for (var i = 0; i < count && i < weights.Length; i++)
                d += weights[i] * LayerDensity(i);
            return Clamp01(d);
        }

        /// <summary>Kuruluk 0..1: kuru çimin çim+kuru çim içindeki payı.</summary>
        public static float Dryness(float[] weights, int count)
        {
            if (weights == null || count < 2) return 0f;
            float g = weights[0], dg = weights[1];
            float s = g + dg;
            return s < 0.0001f ? 0f : Clamp01(dg / s);
        }

        /// <summary>Eğim çarpanı: 25 dereceye kadar 1, 42 derece ve üstü 0.</summary>
        public static float SlopeFactor(float slopeDegrees)
        {
            if (slopeDegrees <= 25f) return 1f;
            if (slopeDegrees >= 42f) return 0f;
            return 1f - (slopeDegrees - 25f) / 17f;
        }

        public static int CellIndex(float worldCoord) => (int)Math.Floor(worldCoord / CellSize);

        /// <summary>Hücrede üretilecek aday sayısı (kabul olasılığı yoğunluk haritasıyla ayrıca uygulanır).</summary>
        public static int CandidatesPerCell(GrassTierConfig cfg)
        {
            return Math.Max(0, (int)Math.Round(CellSize * CellSize * cfg.DensityPerSqm));
        }

        /// <summary>Hücre merkezinin kameraya olan yatay mesafesi, çizim mesafesi + hücre yarı çapraz içinde mi.</summary>
        public static bool CellInRange(int cx, int cz, float camX, float camZ, float drawDistance)
        {
            float mx = (cx + 0.5f) * CellSize - camX;
            float mz = (cz + 0.5f) * CellSize - camZ;
            float r = drawDistance + CellSize * 0.7072f;
            return mx * mx + mz * mz <= r * r;
        }

        /// <summary>Hücre tutma mesafesi (bellek): çizim mesafesinin biraz ötesi, histerezis için.</summary>
        public static bool CellShouldKeep(int cx, int cz, float camX, float camZ, float drawDistance)
        {
            return CellInRange(cx, cz, camX, camZ, drawDistance + CellSize * 1.5f);
        }

        /// <summary>Hücre içindeki rastgele sıralı örneklerin çizilecek oranı: yakında 1, uzakta azalır (en az 0.25).</summary>
        public static float KeepFraction(float distance, float drawDistance)
        {
            if (drawDistance <= 0f) return 0f;
            float t = distance / drawDistance;
            if (t <= 0.45f) return 1f;
            if (t >= 1f) return 0.25f;
            return 1f - (t - 0.45f) / 0.55f * 0.75f;
        }

        /// <summary>Çim boyu ölçeği (shader'la aynı): çizim mesafesinin sonunda FadeWidth şeridinde 1'den 0'a iner.</summary>
        public static float DistanceScale(float distance, float drawDistance)
        {
            if (drawDistance <= 0f) return 1f;
            float s = (drawDistance - distance) / FadeWidth;
            return Clamp01(s);
        }

        /// <summary>Hücre koordinatından ve indeksten deterministik 0..1 değer (aynı girdi aynı sonuç).</summary>
        public static float Hash01(int cx, int cz, int i, int seed)
        {
            unchecked
            {
                uint h = (uint)(cx * 73856093) ^ (uint)(cz * 19349663) ^ (uint)(i * 83492791) ^ (uint)(seed * 2654435761u);
                h ^= h >> 16; h *= 0x7feb352du;
                h ^= h >> 15; h *= 0x846ca68bu;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Düşük frekanslı, yumuşak yama gürültüsü 0..1 (çim boyu/rengi lekeleri).</summary>
        public static float PatchNoise(float x, float z, int seed)
        {
            float fx = x / 9f, fz = z / 9f;
            int ix = (int)Math.Floor(fx), iz = (int)Math.Floor(fz);
            float tx = fx - ix, tz = fz - iz;
            tx = tx * tx * (3f - 2f * tx);
            tz = tz * tz * (3f - 2f * tz);
            float a = Hash01(ix, iz, 7, seed), b = Hash01(ix + 1, iz, 7, seed);
            float c = Hash01(ix, iz + 1, 7, seed), d = Hash01(ix + 1, iz + 1, 7, seed);
            return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), tz);
        }

        /// <summary>Ezilme çarpanı 0..1 (smoothstep): ezicinin yarıçapı içinde 1'e yakın, dışında 0.</summary>
        public static float TrampleFactor(float distance, float radius)
        {
            if (radius <= 0.001f) return 0f;
            float f = Clamp01(1f - distance / radius);
            return f * f * (3f - 2f * f);
        }

        /// <summary>
        /// dist2 dizisinden (ilk count öğe) en yakın en fazla max öğenin indekslerini outIdx'e yazar (yakından uzağa); yazılan sayıyı döndürür.
        /// maxDist2 üstündekiler dışlanır.
        /// </summary>
        public static int PickNearest(float[] dist2, int count, int max, float maxDist2, int[] outIdx)
        {
            int n = 0;
            for (var i = 0; i < count; i++)
            {
                float d = dist2[i];
                if (d > maxDist2) continue;
                int pos = n;
                while (pos > 0 && dist2[outIdx[pos - 1]] > d) pos--;
                if (pos >= max) continue;
                int last = Math.Min(n, max - 1);
                for (var k = last; k > pos; k--) outIdx[k] = outIdx[k - 1];
                outIdx[pos] = i;
                if (n < max) n++;
            }
            return n;
        }

        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    /// <summary>
    /// Alphamap'ten pişirilmiş çim yoğunluk/kuruluk haritası (bayt), bilineer örnekleme.
    /// Dünya x,z → harita; harita dışında 0.
    /// </summary>
    public sealed class GrassDensityMap
    {
        private readonly byte[] _density;
        private readonly byte[] _dry;

        public int Width { get; }
        public int Height { get; }
        public float OriginX { get; }
        public float OriginZ { get; }
        public float SizeX { get; }
        public float SizeZ { get; }

        public GrassDensityMap(int width, int height, float originX, float originZ, float sizeX, float sizeZ)
        {
            Width = Math.Max(2, width);
            Height = Math.Max(2, height);
            OriginX = originX; OriginZ = originZ; SizeX = Math.Max(0.01f, sizeX); SizeZ = Math.Max(0.01f, sizeZ);
            _density = new byte[Width * Height];
            _dry = new byte[Width * Height];
        }

        public void Set(int ix, int iz, float density, float dryness)
        {
            if (ix < 0 || iz < 0 || ix >= Width || iz >= Height) return;
            _density[iz * Width + ix] = (byte)(GrassRules.Clamp01(density) * 255f + 0.5f);
            _dry[iz * Width + ix] = (byte)(GrassRules.Clamp01(dryness) * 255f + 0.5f);
        }

        public float SampleDensity(float x, float z) => Sample(_density, x, z);
        public float SampleDryness(float x, float z) => Sample(_dry, x, z);

        private float Sample(byte[] map, float x, float z)
        {
            float u = (x - OriginX) / SizeX, v = (z - OriginZ) / SizeZ;
            if (u < 0f || v < 0f || u > 1f || v > 1f) return 0f;
            float fx = u * (Width - 1), fz = v * (Height - 1);
            int x0 = (int)fx, z0 = (int)fz;
            int x1 = Math.Min(x0 + 1, Width - 1), z1 = Math.Min(z0 + 1, Height - 1);
            float tx = fx - x0, tz = fz - z0;
            float a = map[z0 * Width + x0], b = map[z0 * Width + x1];
            float c = map[z1 * Width + x0], d = map[z1 * Width + x1];
            return GrassRules.Lerp(GrassRules.Lerp(a, b, tx), GrassRules.Lerp(c, d, tx), tz) / 255f;
        }
    }
}
