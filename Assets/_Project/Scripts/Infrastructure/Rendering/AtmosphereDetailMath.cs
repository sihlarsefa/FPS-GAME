using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Atmosfer detayı kademe ayarı.</summary>
    public readonly struct AtmosphereDetailTier
    {
        public readonly int HazeRings;
        public readonly bool Cirrus;
        public readonly int DustMax;
        public readonly bool Glare;
        public readonly int FlareGhosts;

        public AtmosphereDetailTier(int hazeRings, bool cirrus, int dustMax, bool glare, int flareGhosts)
        {
            HazeRings = hazeRings; Cirrus = cirrus; DustMax = dustMax; Glare = glare; FlareGhosts = flareGhosts;
        }
    }

    /// <summary>Uzak sis halkası mesh verisi (saf; Unity Mesh'e AtmosphereDetail dönüştürür).</summary>
    public sealed class HazeRingData
    {
        public Vector3[] Vertices;
        public float[] Alpha;
        public int[] Triangles;
    }

    /// <summary>Atmosfer detayı saf matematiği: kademe tablosu, toz/parlama görünürlük eğrileri, sis halkası mesh verisi, sirüs gürültüsü.</summary>
    public static class AtmosphereDetailMath
    {
        public const int HazeRows = 3;

        public static AtmosphereDetailTier ForTier(int tier)
        {
            if (tier <= 0) return new AtmosphereDetailTier(0, false, 0, false, 0);
            if (tier == 1) return new AtmosphereDetailTier(1, true, 0, true, 0);
            if (tier == 2) return new AtmosphereDetailTier(2, true, 48, true, 3);
            return new AtmosphereDetailTier(2, true, 96, true, 4);
        }

        /// <summary>Uzak sis halkası opaklığı (0 = en uzak halka). Yakın halka biraz daha seyrek.</summary>
        public static float HazeAlpha(TimeOfDay time, WeatherKind weather, int ringIndex)
        {
            float a;
            if (weather == WeatherKind.Kar) a = 0.42f;
            else if (weather == WeatherKind.Yagmur) a = 0.38f;
            else if (time == TimeOfDay.Safak || time == TimeOfDay.Aksam) a = 0.3f;
            else if (time == TimeOfDay.Gece) a = 0.1f;
            else a = 0.22f;
            return Mathf.Clamp01(a * (ringIndex <= 0 ? 1f : 0.75f));
        }

        /// <summary>Yüksek irtifa sirüs kapsaması (açık havada belirgin, kapalı havada yok).</summary>
        public static float CirrusCoverage(TimeOfDay time, WeatherKind weather)
        {
            if (weather != WeatherKind.Acik) return 0f;
            return time == TimeOfDay.Gece ? 0.12f : time == TimeOfDay.Gunduz ? 0.3f : 0.4f;
        }

        public static float CirrusAlpha(TimeOfDay time, WeatherKind weather) => weather == WeatherKind.Acik ? (time == TimeOfDay.Gece ? 0.15f : 0.26f) : 0f;

        /// <summary>Sirüs doku alfası: geniş (0.6) yumuşak geçiş, tepe 0.9 ile sınırlı; sert kenar/çizgi yok. 0..1.</summary>
        public static float CirrusSoftAlpha(float density, float coverage)
        {
            float lo = 1f - Mathf.Clamp01(coverage) * 0.9f;
            float t = Mathf.Clamp01((density - lo) / 0.6f);
            return t * t * (3f - 2f * t) * 0.9f;
        }

        /// <summary>Kümülüs doku piksel dizisi (tek seferlik): 6 oktav, geniş yumuşak eşik, güneşli üst (parlak) + gri alt, alfa bandlaşmasız.</summary>
        public static Color32[] BuildCumulusPixels(int size, float coverage, int seed)
        {
            var px = new Color32[size * size];
            float inv = 1f / size;
            float off = 2f * inv;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x * inv, v = y * inv;
                // alan bükme: blok/halka yerine organik kıvrım
                float wu = u + 0.045f * (SkyWaterRules.TileableFbm(u, v, 3, 2, seed + 101) - 0.5f);
                float wv = v + 0.045f * (SkyWaterRules.TileableFbm(u, v, 3, 2, seed + 202) - 0.5f);
                float d = SkyWaterRules.TileableFbm(wu, wv, 4, 6, seed);
                float dl = SkyWaterRules.TileableFbm(wu + off, wv + off, 4, 6, seed);
                float a = CumulusAlpha(d, coverage);
                // güneşe (u+,v+) bakan yüz parlak, gölge yüzü gri; yoğun çekirdek alta doğru grileşir
                float lit = Mathf.Clamp01(0.55f + (d - dl) * 9f);
                float core = Mathf.Clamp01((d - (1f - coverage)) * 2.2f);
                float shade = Mathf.Lerp(0.66f, 1f, lit) * Mathf.Lerp(1f, 0.88f, core * (1f - lit));
                px[y * size + x] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(shade) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(shade * 0.995f) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(shade * 1.02f) * 255f), (byte)Mathf.RoundToInt(a * 255f));
            }

            return px;
        }

        /// <summary>Kümülüs alfası: 0.5 genişlikli smoothstep (eski 0.28) + hafif üs ile yumuşak kenar. 0..1.</summary>
        public static float CumulusAlpha(float density, float coverage)
        {
            float lo = 1f - Mathf.Clamp01(coverage) - 0.05f;
            float t = Mathf.Clamp01((density - lo) / 0.5f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Toz zerreleri görünür olmalı mı: güneş ufuk üstünde, hava açık, gündüz/şafak/akşam, hacimsel sis etkin.</summary>
        public static bool DustVisible(TimeOfDay time, WeatherKind weather, float sunElevation01, bool fogActive)
        {
            if (!fogActive || weather != WeatherKind.Acik || time == TimeOfDay.Gece)
                return false;
            return sunElevation01 > 0.04f;
        }

        /// <summary>Toz parlaklığı 0..1: güneşe bakarken ileri saçılım (Mie) ile artar. dot = kamera ileri . güneş yönü (-1..1).</summary>
        public static float DustAlpha(float dotViewSun, float max)
        {
            float t = Mathf.Clamp01((dotViewSun + 0.2f) / 1.1f);
            float forward = t * t * (3f - 2f * t);
            return Mathf.Clamp01(max) * (0.15f + 0.85f * forward);
        }

        /// <summary>Güneş parlaması (glare) şiddeti 0..1: güneşe doğru bakma konisi x görünürlük (engel yok).</summary>
        public static float GlareIntensity(float dotViewSun, float visibility, WeatherKind weather, TimeOfDay time)
        {
            if (time == TimeOfDay.Gece)
                return 0f;
            float t = Mathf.Clamp01((dotViewSun - 0.55f) / 0.45f);
            float cone = t * t * (3f - 2f * t);
            float w = weather == WeatherKind.Acik ? 1f : 0.2f;
            return cone * Mathf.Clamp01(visibility) * w;
        }

        /// <summary>Lens hayalet konumu: güneş görüntü noktasından ekran merkezine göre yansıtılmış hat (k = -0.4, 0.3, 0.8...).</summary>
        public static Vector2 GhostViewport(Vector2 sunViewport, float k)
        {
            var c = new Vector2(0.5f, 0.5f);
            return c + (sunViewport - c) * k;
        }

        /// <summary>Hayalet k değerleri (hat üzerinde).</summary>
        public static float GhostK(int index)
        {
            switch (index)
            {
                case 0: return -0.35f;
                case 1: return 0.35f;
                case 2: return 0.8f;
                default: return -0.8f;
            }
        }

        /// <summary>Yumuşak yaklaşma (üstel), dt bağımsız.</summary>
        public static float Approach(float current, float target, float rate, float dt)
        {
            float k = 1f - Mathf.Exp(-Mathf.Max(0f, rate) * Mathf.Max(0f, dt));
            return current + (target - current) * k;
        }

        /// <summary>Uzak sis halkası: HazeRows satır (alt, tepe, üst) x (segments+1) köşe. Alfa: alt 0.55, tepe 1, üst 0 (çarpan renkte).</summary>
        public static HazeRingData BuildHazeRing(float radius, float bottom, float peak, float top, int segments)
        {
            if (segments < 3) segments = 3;
            int cols = segments + 1;
            var d = new HazeRingData
            {
                Vertices = new Vector3[HazeRows * cols],
                Alpha = new float[HazeRows * cols],
                Triangles = new int[(HazeRows - 1) * segments * 6]
            };
            float[] ys = { bottom, peak, top };
            float[] al = { 0.55f, 1f, 0f };
            for (int r = 0; r < HazeRows; r++)
            for (int s = 0; s < cols; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                int i = r * cols + s;
                d.Vertices[i] = new Vector3(Mathf.Cos(a) * radius, ys[r], Mathf.Sin(a) * radius);
                d.Alpha[i] = al[r];
            }

            int t = 0;
            for (int r = 0; r < HazeRows - 1; r++)
            for (int s = 0; s < segments; s++)
            {
                int a = r * cols + s;
                int b = a + 1;
                int c = a + cols;
                int e = c + 1;
                d.Triangles[t++] = a; d.Triangles[t++] = c; d.Triangles[t++] = b;
                d.Triangles[t++] = b; d.Triangles[t++] = c; d.Triangles[t++] = e;
            }

            return d;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static int Mod(int a, int n)
        {
            int r = a % n;
            return r < 0 ? r + n : r;
        }

        /// <summary>İki eksende farklı periyotlu döngüsel değer gürültüsü 0..1 (sirüs çizgileri için anizotropik).</summary>
        public static float PeriodicValueNoise(float x, float y, int px, int py, int seed)
        {
            px = Mathf.Max(1, px); py = Mathf.Max(1, py);
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Mod(x0, px), xb = Mod(x0 + 1, px), ya = Mod(y0, py), yb = Mod(y0 + 1, py);
            float v00 = Hash(xa, ya, seed), v10 = Hash(xb, ya, seed), v01 = Hash(xa, yb, seed), v11 = Hash(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(v00, v10, fx), Mathf.Lerp(v01, v11, fx), fy);
        }

        /// <summary>Sirüs yoğunluğu 0..1; u, v in [0,1) döngüsel. hafif gerilmiş geniş tüyler (ince çizgi yok).</summary>
        public static float CirrusDensity(float u, float v, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int px = 3, py = 5; // düşük anizotropi: ince çizgi/çizik yok, geniş tüy
            for (int o = 0; o < 3; o++)
            {
                sum += amp * PeriodicValueNoise(u * px, v * py, px, py, seed + o * 17);
                norm += amp;
                amp *= 0.5f;
                px *= 2; py *= 2;
            }

            return sum / norm;
        }
    }
}
