using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Gökyüzü katmanları kademe tablosu (0 Düşük ... 3 Ultra).</summary>
    public readonly struct SkyLayersTier
    {
        public readonly int StarCount;
        public readonly bool HighCloudLayer;
        public readonly bool GodRays;
        public readonly int CloudTexSize;
        public readonly bool Twinkle;

        public SkyLayersTier(int stars, bool high, bool rays, int texSize, bool twinkle)
        {
            StarCount = stars; HighCloudLayer = high; GodRays = rays; CloudTexSize = texSize; Twinkle = twinkle;
        }
    }

    /// <summary>
    /// Gökyüzü/hava görselleri saf matematiği (Unity nesnesi gerektirmez): iki hızlı bulut katmanı, fırtına kararması + rüzgâr senkronu,
    /// ışık huzmesi alfa'sı, yıldız alanı ve ay evresi. SkyLayers bileşeni bunları kare başına uygular.
    /// </summary>
    public static class SkyLayersMath
    {
        public const double SynodicMonthDays = 29.530588853;
        /// <summary>Bilinen yeni ay: 2000-01-06 18:14 UTC, Unix günü cinsinden.</summary>
        public const double KnownNewMoonUnixDays = 10957.759722;

        public static SkyLayersTier ForTier(int tier)
        {
            if (tier <= 0) return new SkyLayersTier(160, false, false, 128, false);
            if (tier == 1) return new SkyLayersTier(320, true, true, 192, false);
            if (tier == 2) return new SkyLayersTier(520, true, true, 256, true);
            return new SkyLayersTier(800, true, true, 256, true);
        }

        /// <summary>Alçak katman hızı (derece/sn): rüzgârla (WindStrength 0.35..1.15) senkron artar.</summary>
        public static float LowLayerSpeed(float baseSpeed, float wind) => baseSpeed * (0.6f + 2.2f * Mathf.Clamp01(wind));

        /// <summary>Yüksek katman, alçağın ~0.45 katı hızla (paralaks: yüksek olan yavaş görünür).</summary>
        public static float HighLayerSpeed(float baseSpeed, float wind) => LowLayerSpeed(baseSpeed, wind) * 0.45f;

        /// <summary>Fırtına kararma çarpanı (1 = normal, ~0.35 = kapkara). Bulutluluk, yağmur ve fırtına birleşir.</summary>
        public static float StormDarken(float cloud, float rain, float storm)
        {
            var d = 0.25f * Mathf.Clamp01(cloud) + 0.3f * Mathf.Clamp01(rain) + 0.2f * Mathf.Clamp01(storm);
            return Mathf.Clamp(1f - d, 0.35f, 1f);
        }

        /// <summary>Yüksek katman daha az kararır (alttaki katman fırtına tabanını verir).</summary>
        public static float HighLayerDarken(float darken) => Mathf.Lerp(1f, darken, 0.6f);

        /// <summary>Bulut katman alfa'sı: fırtınada yoğunlaşır (0.55..1).</summary>
        public static float CloudAlpha(float cloud, float rain) => Mathf.Clamp01(0.55f + 0.3f * Mathf.Clamp01(cloud) + 0.15f * Mathf.Clamp01(rain));

        /// <summary>Gün evresine göre bulut taban rengi (kararmadan önce).</summary>
        public static Color CloudBase(TimeOfDay time)
        {
            switch (time)
            {
                case TimeOfDay.Safak: return new Color(1f, 0.78f, 0.74f);
                case TimeOfDay.Aksam: return new Color(1f, 0.62f, 0.42f);
                case TimeOfDay.Gece: return new Color(0.16f, 0.19f, 0.3f);
                default: return new Color(0.97f, 0.98f, 1f);
            }
        }

        /// <summary>Hava türünden varsayılan örnek (WeatherSystem yokken: menü/lobi).</summary>
        public static WeatherSample DefaultSample(WeatherKind weather)
        {
            if (weather == WeatherKind.Yagmur) return new WeatherSample { Cloud = 0.95f, Rain = 0.8f, Storm = 0.6f };
            if (weather == WeatherKind.Kar) return new WeatherSample { Cloud = 0.8f, Rain = 0.2f, Storm = 0f };
            return new WeatherSample { Cloud = 0.25f };
        }

        /// <summary>Işık huzmesi alfa'sı (0..0.6): yalnız gündüz/şafak/akşam, bulutta kırılarak sızar, yağmurda söner, güneşe bakınca artar.</summary>
        public static float GodRayAlpha(TimeOfDay time, float cloud, float rain, float facing)
        {
            if (time == TimeOfDay.Gece) return 0f;
            var tod = time == TimeOfDay.Gunduz ? 0.35f : 0.6f;
            // Hafif bulut en iyi huzmeyi verir; tam açık ve tam kapalı daha zayıf.
            var c = Mathf.Clamp01(cloud);
            var shape = 4f * c * (1f - c) * 0.6f + 0.4f * (1f - c);
            var wet = 1f - Mathf.Clamp01(rain * 1.6f);
            return Mathf.Clamp01(tod * shape * wet * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(facing)));
        }

        /// <summary>Huzme dokusu: açı+yarıçapa göre yoğunluk (0..1). Işınlar açıya bağlı gürültüyle kümelenir.</summary>
        public static float RayIntensity(float angle01, float radius01, int seed)
        {
            if (radius01 >= 1f) return 0f;
            var rays = 14;
            var a = angle01 * rays;
            var i = Mathf.FloorToInt(a);
            var f = a - i;
            var h0 = Hash01(i % rays, seed);
            var h1 = Hash01((i + 1) % rays, seed);
            var n = Mathf.Lerp(h0, h1, f * f * (3f - 2f * f));
            var falloff = Mathf.Pow(1f - radius01, 1.6f);
            return Mathf.Clamp01(falloff * (0.35f + 0.65f * n));
        }

        // ---- Yıldızlar ----

        /// <summary>Yıldız görünürlüğü (0..1): gece 1, şafak/akşam çok az; bulut ve fırtına örter.</summary>
        public static float StarVisibility(TimeOfDay time, float cloud, float storm)
        {
            var t = time == TimeOfDay.Gece ? 1f : time == TimeOfDay.Safak || time == TimeOfDay.Aksam ? 0.12f : 0f;
            return Mathf.Clamp01(t * (1f - 0.92f * Mathf.Clamp01(cloud)) * (1f - Mathf.Clamp01(storm)));
        }

        /// <summary>i. yıldızın birim yönü: ufuk üstü (yükseklik >= minEl), üstte seyrelen dağılım.</summary>
        public static Vector3 StarDirection(int i, int seed)
        {
            var el = Mathf.Lerp(0.14f, 1f, Mathf.Sqrt(Hash01(i, seed + 1)));
            var az = Hash01(i, seed + 2) * Mathf.PI * 2f;
            var r = Mathf.Sqrt(Mathf.Max(0f, 1f - el * el));
            return new Vector3(Mathf.Cos(az) * r, el, Mathf.Sin(az) * r);
        }

        /// <summary>Yıldız parlaklığı (0.35..1): çoğu sönük, az sayıda parlak.</summary>
        public static float StarBrightness(int i, int seed)
        {
            var h = Hash01(i, seed + 3);
            return 0.35f + 0.65f * h * h * h;
        }

        /// <summary>Titreşim çarpanı (0.7..1): A/B grubu zıt fazlı.</summary>
        public static float Twinkle(float t, int group) => 0.85f + 0.15f * Mathf.Sin(t * 2.3f + group * Mathf.PI);

        // ---- Ay evresi ----

        /// <summary>Evre 0..1 (0 yeni ay, 0.5 dolunay). daysSinceUnixEpoch = UTC gün sayısı (kesirli).</summary>
        public static float MoonPhase(double daysSinceUnixEpoch)
        {
            var p = (daysSinceUnixEpoch - KnownNewMoonUnixDays) / SynodicMonthDays;
            p -= System.Math.Floor(p);
            return (float)p;
        }

        /// <summary>Aydınlık oranı 0..1 (yeni 0, dolunay 1).</summary>
        public static float MoonIllumination(float phase) => 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * Mathf.Repeat(phase, 1f)));

        /// <summary>Ay evresine göre disk pikselinin parlaklığı. x,y -1..1 disk koordinatı; disk dışı -1. Büyüyen ayda sağ taraf aydınlık.</summary>
        public static float MoonPixel(float x, float y, float phase)
        {
            var r2 = x * x + y * y;
            if (r2 >= 1f) return -1f;
            var z = Mathf.Sqrt(1f - r2);
            var th = 2f * Mathf.PI * Mathf.Repeat(phase, 1f);
            var lx = Mathf.Sin(th);
            var lz = -Mathf.Cos(th);
            var dot = x * lx + z * lz;
            var lit = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dot + 0.04f) / 0.14f));
            // Krater benzeri düşük frekanslı leke (deterministik).
            var crater = 0.88f + 0.12f * Hash01(Mathf.FloorToInt((x + 1f) * 5f) * 16 + Mathf.FloorToInt((y + 1f) * 5f), 77);
            return Mathf.Lerp(0.05f, 0.95f * crater, lit);
        }

        /// <summary>Ay görünürlüğü: yalnız gece, bulutla azalır.</summary>
        public static float MoonVisibility(TimeOfDay time, float cloud, float storm)
        {
            var t = time == TimeOfDay.Gece ? 1f : time == TimeOfDay.Safak || time == TimeOfDay.Aksam ? 0.25f : 0f;
            return Mathf.Clamp01(t * (1f - 0.7f * Mathf.Clamp01(cloud)) * (1f - 0.5f * Mathf.Clamp01(storm)));
        }

        public static float Hash01(int i, int seed)
        {
            unchecked
            {
                var h = (uint)(i * 374761393 + seed * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
