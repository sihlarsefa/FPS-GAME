using System;

namespace Project.Core.Domain
{
    /// <summary>Hacimsel sis için hava durumu (Açık/Sisli/Yağmurlu). WeatherKind'dan eşlenir (Kar -> Sisli).</summary>
    public enum VolumetricWeather
    {
        Acik = 0,
        Sisli = 1,
        Yagmurlu = 2
    }

    /// <summary>Hacimsel sis parametreleri (saf veri). Birim: metre.</summary>
    public struct VolumetricFogParams
    {
        /// <summary>Zemin seviyesinde sönümleme katsayısı (1/m). 0.01 ≈ 100 m görüş.</summary>
        public float Density;
        /// <summary>Yükseklik arttıkça yoğunluğun üstel azalma hızı (1/m).</summary>
        public float HeightFalloff;
        /// <summary>Üstel azalmanın başladığı dünya yüksekliği (m).</summary>
        public float BaseHeight;
        /// <summary>Henyey-Greenstein anizotropisi g (-0.99..0.99); büyük = ileri saçılma (güneşe bakınca parlar).</summary>
        public float Anisotropy;
        /// <summary>Güneş (ana ışık) saçılım çarpanı.</summary>
        public float SunIntensity;
        /// <summary>Ortam ışığı (izotropik) saçılım çarpanı.</summary>
        public float AmbientIntensity;
        /// <summary>Işın yürütme azami mesafe (m); gökyüzü pikselleri için üst sınır.</summary>
        public float MaxDistance;
        /// <summary>Sis albedosu (doğrusal RGB, 0..1).</summary>
        public float TintR, TintG, TintB;

        public static VolumetricFogParams Lerp(VolumetricFogParams a, VolumetricFogParams b, float t)
        {
            t = VolumetricFogMath.Clamp01(t);
            return new VolumetricFogParams
            {
                Density = VolumetricFogMath.Mix(a.Density, b.Density, t),
                HeightFalloff = VolumetricFogMath.Mix(a.HeightFalloff, b.HeightFalloff, t),
                BaseHeight = VolumetricFogMath.Mix(a.BaseHeight, b.BaseHeight, t),
                Anisotropy = VolumetricFogMath.Mix(a.Anisotropy, b.Anisotropy, t),
                SunIntensity = VolumetricFogMath.Mix(a.SunIntensity, b.SunIntensity, t),
                AmbientIntensity = VolumetricFogMath.Mix(a.AmbientIntensity, b.AmbientIntensity, t),
                MaxDistance = VolumetricFogMath.Mix(a.MaxDistance, b.MaxDistance, t),
                TintR = VolumetricFogMath.Mix(a.TintR, b.TintR, t),
                TintG = VolumetricFogMath.Mix(a.TintG, b.TintG, t),
                TintB = VolumetricFogMath.Mix(a.TintB, b.TintB, t)
            };
        }

        /// <summary>Değerleri güvenli aralığa çeker (NaN/negatif/aşırı değerler).</summary>
        public VolumetricFogParams Sanitized()
        {
            var p = this;
            p.Density = VolumetricFogMath.Finite(p.Density, 0.004f, 0f, 0.2f);
            p.HeightFalloff = VolumetricFogMath.Finite(p.HeightFalloff, 0.05f, 0f, 1f);
            p.BaseHeight = VolumetricFogMath.Finite(p.BaseHeight, 0f, -1000f, 5000f);
            p.Anisotropy = VolumetricFogMath.Finite(p.Anisotropy, 0.6f, -0.95f, 0.95f);
            p.SunIntensity = VolumetricFogMath.Finite(p.SunIntensity, 1f, 0f, 8f);
            p.AmbientIntensity = VolumetricFogMath.Finite(p.AmbientIntensity, 0.3f, 0f, 4f);
            p.MaxDistance = VolumetricFogMath.Finite(p.MaxDistance, 200f, 10f, 2000f);
            p.TintR = VolumetricFogMath.Finite(p.TintR, 1f, 0f, 4f);
            p.TintG = VolumetricFogMath.Finite(p.TintG, 1f, 0f, 4f);
            p.TintB = VolumetricFogMath.Finite(p.TintB, 1f, 0f, 4f);
            return p;
        }
    }

    /// <summary>Kalite kademesine göre hacimsel sis maliyet ayarı (0 Düşük, 1 Orta, 2 Yüksek, 3 Ultra).</summary>
    public readonly struct VolumetricTierConfig
    {
        public readonly bool Enabled;
        public readonly int Steps;
        /// <summary>Çözünürlük bölücü: 4 = çeyrek çözünürlük, 2 = yarım.</summary>
        public readonly int ResolutionDivisor;
        public readonly bool Temporal;
        public readonly bool ShadowRays;
        public readonly float MaxDistance;

        public VolumetricTierConfig(bool enabled, int steps, int div, bool temporal, bool shadowRays, float maxDistance)
        {
            Enabled = enabled; Steps = steps; ResolutionDivisor = div; Temporal = temporal; ShadowRays = shadowRays; MaxDistance = maxDistance;
        }

        /// <summary>1080p'de göreli maliyet (Orta = 1). Piksel sayısı x adım sayısı.</summary>
        public float RelativeCost(int width, int height, float baseline)
        {
            if (!Enabled || ResolutionDivisor <= 0) return 0f;
            var px = (double)Math.Max(1, width / ResolutionDivisor) * Math.Max(1, height / ResolutionDivisor);
            return baseline <= 0f ? 0f : (float)(px * Steps / baseline);
        }
    }

    /// <summary>Saf hacimsel sis matematiği (Unity bağımlılığı yok): faz fonksiyonu, yükseklik yoğunluğu, optik derinlik, adım dağılımı, zamansal ağırlık, mavi gürültü.</summary>
    public static class VolumetricFogMath
    {
        public const int NoiseSize = 64;

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Mix(float a, float b, float t) => a + (b - a) * t;

        public static float Finite(float v, float fallback, float min, float max)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) v = fallback;
            return v < min ? min : v > max ? max : v;
        }

        /// <summary>Kademe tablosu. Düşük (0) KAPALI; Orta en ucuz; Ultra yarım çözünürlük.</summary>
        public static VolumetricTierConfig ForTier(int tier, bool userEnabled = true)
        {
            if (!userEnabled || tier <= 0) return new VolumetricTierConfig(false, 0, 4, false, false, 0f);
            switch (tier)
            {
                case 1: return new VolumetricTierConfig(true, 12, 4, true, true, 120f);
                case 2: return new VolumetricTierConfig(true, 24, 4, true, true, 220f);
                default: return new VolumetricTierConfig(true, 32, 2, true, true, 350f);
            }
        }

        /// <summary>Düşük çözünürlük boyutu (yukarı yuvarlanır, en az 1).</summary>
        public static int LowRes(int fullSize, int divisor)
        {
            if (divisor < 1) divisor = 1;
            return Math.Max(1, (fullSize + divisor - 1) / divisor);
        }

        /// <summary>Henyey-Greenstein, izotropik = 1 olacak biçimde normalize (4π ile çarpılmış): (1-g²)/(1+g²-2g·cos)^1.5.</summary>
        public static float HenyeyGreenstein(float cosTheta, float g)
        {
            g = Math.Max(-0.99f, Math.Min(0.99f, g));
            var denom = 1f + g * g - 2f * g * cosTheta;
            return (1f - g * g) / (float)Math.Pow(Math.Max(1e-4f, denom), 1.5);
        }

        /// <summary>Yükseklikteki sönümleme: density · exp(-max(0, y - base) · falloff).</summary>
        public static float HeightDensity(float density, float baseHeight, float falloff, float y)
        {
            var h = Math.Max(0f, y - baseHeight);
            return density * (float)Math.Exp(-h * Math.Max(0f, falloff));
        }

        /// <summary>
        /// Bir doğru parçası boyunca optik derinlik (analitik üstel yükseklik integrali).
        /// y0..y1 parçanın uç yükseklikleri, length uzunluk (m). Taban yüksekliğinin altı sabit yoğunluk sayılır.
        /// </summary>
        public static float OpticalDepth(float density, float baseHeight, float falloff, float y0, float y1, float length)
        {
            if (length <= 0f || density <= 0f) return 0f;
            var h0 = Math.Max(0f, y0 - baseHeight);
            var h1 = Math.Max(0f, y1 - baseHeight);
            var dh = h1 - h0;
            if (falloff <= 1e-6f || Math.Abs(dh) < 1e-4f)
                return density * length * (float)Math.Exp(-(h0 + h1) * 0.5f * Math.Max(0f, falloff));
            var integral = ((float)Math.Exp(-falloff * h0) - (float)Math.Exp(-falloff * h1)) / (falloff * dh);
            return density * length * integral;
        }

        public static float Transmittance(float opticalDepth) => (float)Math.Exp(-Math.Max(0f, opticalDepth));

        /// <summary>
        /// Dilim sınırı (m): i = 0..steps. Kareye yakın dağılım (power) yakını sık örnekler. i=0 -> 0, i=steps -> maxDistance.
        /// </summary>
        public static float SliceDistance(int i, int steps, float maxDistance, float power = 1.5f)
        {
            if (steps <= 0) return 0f;
            var t = Clamp01((float)i / steps);
            return maxDistance * (float)Math.Pow(t, Math.Max(1f, power));
        }

        /// <summary>Kare başına gürültü kayması: altın oran (0..1).</summary>
        public static float TemporalNoiseOffset(int frame)
        {
            var v = (frame & 0xFFFF) * 0.61803398875f;
            return v - (float)Math.Floor(v);
        }

        /// <summary>Radikal ters (Halton) dizisi; taban 2/3 ile alt-piksel kayması için.</summary>
        public static float Halton(int index, int radix)
        {
            if (radix < 2) radix = 2;
            double f = 1.0, r = 0.0;
            var i = Math.Max(0, index);
            while (i > 0)
            {
                f /= radix;
                r += f * (i % radix);
                i /= radix;
            }
            return (float)r;
        }

        /// <summary>
        /// Zamansal karışımda GEÇMİŞİN ağırlığı (0..1). Geçmiş yoksa/geçersizse 0 (yalnız mevcut kare).
        /// Kamera hızlı hareket ettikçe (piksel/kare) ağırlık düşer: gölgelenme (ghosting) azalır.
        /// </summary>
        public static float HistoryWeight(bool historyValid, float motionPixels, float maxWeight = 0.9f)
        {
            if (!historyValid) return 0f;
            var m = float.IsNaN(motionPixels) ? 0f : Math.Max(0f, motionPixels);
            var w = maxWeight * (1f - Clamp01(m / 24f) * 0.7f);
            return Clamp01(w);
        }

        /// <summary>Geçmiş geçerli mi: boyut aynı, ardışık kare, kamera sıçramadı (ışınlanma/kesme).</summary>
        public static bool HistoryUsable(int lastWidth, int lastHeight, int width, int height, int frameGap, float cameraJumpMeters, float jumpLimit = 25f)
        {
            if (lastWidth != width || lastHeight != height) return false;
            if (frameGap < 1 || frameGap > 2) return false;
            return cameraJumpMeters >= 0f && cameraJumpMeters <= jumpLimit;
        }

        /// <summary>Çift yönlü (bilateral) üst örnekleme derinlik ağırlığı: derinlik farkı büyüdükçe düşer.</summary>
        public static float BilateralWeight(float fullDepth, float lowDepth, float sigmaRel = 0.08f)
        {
            var d = Math.Abs(fullDepth - lowDepth);
            return (float)Math.Exp(-d / (sigmaRel * Math.Max(0.1f, fullDepth) + 0.05f));
        }

        /// <summary>
        /// Mavi gürültüye yakın karo üretir (size x size, 0..1 tekdüze dağılım): beyaz gürültüye tekrarlı yüksek-geçiren süzgeç + sıra eşitleme.
        /// Deterministik (seed). Karo dikişsizdir (sarmalı).
        /// </summary>
        public static float[] GenerateBlueNoise(int size, int seed, int iterations = 4)
        {
            size = Math.Max(4, size);
            var n = size * size;
            var rng = new Random(seed);
            var v = new float[n];
            for (var i = 0; i < n; i++) v[i] = (float)rng.NextDouble();

            var tmp = new float[n];
            var hp = new float[n];
            for (var it = 0; it < Math.Max(1, iterations); it++)
            {
                // ayrılabilir [1 2 1]/4 bulanıklık (sarmalı)
                for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                        tmp[y * size + x] = 0.25f * v[y * size + (x + size - 1) % size] + 0.5f * v[y * size + x] + 0.25f * v[y * size + (x + 1) % size];
                for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                    {
                        var b = 0.25f * tmp[((y + size - 1) % size) * size + x] + 0.5f * tmp[y * size + x] + 0.25f * tmp[((y + 1) % size) * size + x];
                        hp[y * size + x] = v[y * size + x] - b;
                    }
                RankRemap(hp, v);
            }
            return v;
        }

        private static void RankRemap(float[] src, float[] dst)
        {
            var n = src.Length;
            var idx = new int[n];
            for (var i = 0; i < n; i++) idx[i] = i;
            Array.Sort(idx, (a, b) =>
            {
                var c = src[a].CompareTo(src[b]);
                return c != 0 ? c : a.CompareTo(b);
            });
            for (var r = 0; r < n; r++) dst[idx[r]] = (r + 0.5f) / n;
        }
    }

    /// <summary>Hava + gün saati ön ayarları (saf veri). Açık/Sisli/Yağmurlu x Şafak/Gündüz/Akşam/Gece.</summary>
    public static class VolumetricFogPresets
    {
        public static readonly string[] WeatherNames = { "Açık", "Sisli", "Yağmurlu" };

        public static string Name(VolumetricWeather w) => WeatherNames[Math.Max(0, Math.Min(WeatherNames.Length - 1, (int)w))];

        public static VolumetricWeather FromWeather(WeatherKind w)
        {
            switch (w)
            {
                case WeatherKind.Yagmur: return VolumetricWeather.Yagmurlu;
                case WeatherKind.Kar: return VolumetricWeather.Sisli;
                default: return VolumetricWeather.Acik;
            }
        }

        public static VolumetricFogParams Get(TimeOfDay time, VolumetricWeather weather)
        {
            // Hava tabanı (gündüz)
            float density, falloff, g, sun, ambient, maxDist;
            switch (weather)
            {
                case VolumetricWeather.Sisli:
                    density = 0.016f; falloff = 0.035f; g = 0.55f; sun = 0.7f; ambient = 0.55f; maxDist = 140f; break;
                case VolumetricWeather.Yagmurlu:
                    density = 0.009f; falloff = 0.02f; g = 0.45f; sun = 0.45f; ambient = 0.5f; maxDist = 180f; break;
                default:
                    density = 0.0035f; falloff = 0.025f; g = 0.65f; sun = 1f; ambient = 0.3f; maxDist = 260f; break;
            }

            // Gün saati düzeltmesi
            float tr, tg, tb;
            switch (time)
            {
                case TimeOfDay.Safak:
                    density *= 1.7f; falloff = 0.045f; g += 0.05f; sun *= 0.85f; ambient *= 0.9f; tr = 0.95f; tg = 0.74f; tb = 0.66f; break;
                case TimeOfDay.Aksam:
                    density *= 1.2f; g += 0.08f; sun *= 1.1f; ambient *= 0.8f; tr = 1.0f; tg = 0.66f; tb = 0.45f; break;
                case TimeOfDay.Gece:
                    density *= 1.8f; g -= 0.1f; sun *= 0.12f; ambient *= 0.35f; maxDist *= 0.8f; tr = 0.12f; tg = 0.16f; tb = 0.28f; break;
                default:
                    tr = 0.80f; tg = 0.86f; tb = 0.95f; break;
            }

            return new VolumetricFogParams
            {
                Density = density, HeightFalloff = falloff, BaseHeight = 0f, Anisotropy = g,
                SunIntensity = sun, AmbientIntensity = ambient, MaxDistance = maxDist,
                TintR = tr, TintG = tg, TintB = tb
            }.Sanitized();
        }
    }
}
