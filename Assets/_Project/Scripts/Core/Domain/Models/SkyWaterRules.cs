using System;

namespace Project.Core.Domain
{
    /// <summary>
    /// Su ve gökyüzü için saf mantık (Unity bağımlılığı yok): bulut kapsaması, döngüsel (tileable) gürültü, ışık huzmesi gücü,
    /// su altı karışımı ve kıyı çizgisi kesişim araması.
    /// </summary>
    public static class SkyWaterRules
    {
        /// <summary>Bulut kapsaması 0..1 (gece biraz seyrek, yağmur/kar kapalı gök).</summary>
        public static float CloudCoverage(TimeOfDay time, WeatherKind weather)
        {
            float c = weather == WeatherKind.Yagmur ? 0.88f : weather == WeatherKind.Kar ? 0.8f : 0.42f;
            if (weather == WeatherKind.Acik)
            {
                if (time == TimeOfDay.Safak || time == TimeOfDay.Aksam) c += 0.1f;
                else if (time == TimeOfDay.Gece) c -= 0.08f;
            }

            return Clamp01(c);
        }

        /// <summary>Bulut katmanı kaydırma hızı (derece/sn); yağmurda biraz hızlı.</summary>
        public static float CloudScrollSpeed(WeatherKind weather) => weather == WeatherKind.Yagmur ? 0.9f : weather == WeatherKind.Kar ? 0.5f : 0.35f;

        /// <summary>Bulut yoğunluğunu kapsama eşiğiyle alfaya çevirir (yumuşak kenar).</summary>
        public static float CloudAlpha(float density, float coverage)
        {
            float lo = 1f - Clamp01(coverage);
            return SmoothStep(lo, lo + 0.28f, density);
        }

        /// <summary>Güneş diski/halesi görünür mü (gece ay gösterilir).</summary>
        public static bool SunVisible(TimeOfDay time) => time != TimeOfDay.Gece;

        /// <summary>Gün batımı/şafak ışık huzmesi gücü 0..1 (yalnız şafak/akşam; yağmur/karda zayıf).</summary>
        public static float ShaftStrength(TimeOfDay time, WeatherKind weather)
        {
            if (time != TimeOfDay.Safak && time != TimeOfDay.Aksam)
                return 0f;
            return weather == WeatherKind.Acik ? 1f : 0.35f;
        }

        /// <summary>Uzak dağ silüeti rengi için sise karışım oranı (gece koyu, gündüz havadan etkili = açık).</summary>
        public static float MountainFogMix(TimeOfDay time, WeatherKind weather)
        {
            float m = time == TimeOfDay.Gece ? 0.45f : 0.62f;
            if (weather != WeatherKind.Acik) m += 0.18f;
            return Clamp01(m);
        }

        /// <summary>Kamera su yüzeyinin ne kadar altında (0 = üstte, 1 = tamamen su altı); 0,3 m'de tamamlanır.</summary>
        public static float UnderwaterBlend(float cameraY, float waterLevel)
        {
            return Clamp01((waterLevel - cameraY) / 0.3f);
        }

        /// <summary>Su altı sis yoğunluğu: normal yoğunluktan yüksek sabite karıştırılır.</summary>
        public static float UnderwaterFogDensity(float normalDensity, float blend) => Lerp(normalDensity, 0.11f, Clamp01(blend));

        /// <summary>Fresnel benzeri pürüzsüzlük: yukarıdan (dik) bakışta düşük, sıyırarak bakışta yüksek. lookDownDot = |kamera ileri . yukarı|.</summary>
        public static float WaterSmoothness(float lookDownDot)
        {
            float f = 1f - Clamp01(Math.Abs(lookDownDot));
            return Lerp(0.62f, 0.97f, f * f);
        }

        /// <summary>
        /// Yükseklik örneklerinde (dizin 0 = su tarafı, artan = kara tarafı) ilk su→kara geçişinin mesafesi (adım * indeks, doğrusal
        /// ara değerleme). Bulunamazsa -1.
        /// </summary>
        public static float FindCrossing(float[] heights, float step, float level)
        {
            if (heights == null)
                return -1f;
            for (int i = 0; i + 1 < heights.Length; i++)
            {
                float a = heights[i], b = heights[i + 1];
                if (a < level && b >= level)
                {
                    float t = (level - a) / Math.Max(1e-5f, b - a);
                    return (i + t) * step;
                }
            }

            return -1f;
        }

        /// <summary>Döngüsel (periyotlu) değer gürültüsü, 0..1. x,y birim kafes; period kafes hücresi sayısı.</summary>
        public static float TileableNoise(float x, float y, int period, int seed)
        {
            if (period < 1) period = 1;
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(Mod(x0, period), Mod(y0, period), seed);
            float b = Hash(Mod(x0 + 1, period), Mod(y0, period), seed);
            float c = Hash(Mod(x0, period), Mod(y0 + 1, period), seed);
            float d = Hash(Mod(x0 + 1, period), Mod(y0 + 1, period), seed);
            return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
        }

        /// <summary>Döngüsel fBm 0..1; u,v 0..1 doku koordinatı (1'de başa sarar).</summary>
        public static float TileableFbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int period = Math.Max(1, basePeriod);
            for (int o = 0; o < Math.Max(1, octaves); o++)
            {
                sum += amp * TileableNoise(u * period, v * period, period, seed + o * 17);
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }

            return sum / norm;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static int Mod(int a, int m) => ((a % m) + m) % m;
        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        private static float SmoothStep(float a, float b, float v)
        {
            float t = Clamp01((v - a) / Math.Max(1e-5f, b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
