using System;

namespace Project.Core.Domain
{
    /// <summary>Saf atmosfer matematiği (Unity bağımlılığı yok): yükseklik sisi, aerial perspective, Mie hâlesi, gökyüzü kimliği.</summary>
    public static class AtmosphereMath
    {
        /// <summary>Yükseklik sisi çarpanı: kamera yükseldikçe yoğunluk üstel azalır (1 = zemin seviyesi, taban 0.35).</summary>
        public static float HeightFogFactor(float cameraHeight, float referenceHeight, float falloff)
        {
            var h = Math.Max(0f, cameraHeight - referenceHeight);
            var f = (float)Math.Exp(-h * Math.Max(0f, falloff));
            return Math.Max(0.35f, Math.Min(1f, f));
        }

        /// <summary>Aerial perspective karışımı (0..1): mesafe arttıkça uzak renk sise/gökyüzüne kayar. Beer-Lambert.</summary>
        public static float AerialBlend(float distance, float density)
        {
            if (distance <= 0f || density <= 0f)
                return 0f;
            return 1f - (float)Math.Exp(-distance * density);
        }

        /// <summary>Gündüz açık havada 400 m'de dağların sönümü (hedef: 400 m'de %38, 1 km'de %70).</summary>
        public static float AerialDensity(TimeOfDay t, WeatherKind w)
        {
            float d = t == TimeOfDay.Gece ? 0.0030f : t == TimeOfDay.Safak ? 0.0018f : t == TimeOfDay.Aksam ? 0.0016f : 0.0012f;
            if (w != WeatherKind.Acik) d *= 1.4f;
            return d;
        }

        /// <summary>Saat bazlı aerial tint RGB (0..1): şafak pembe-gri, öğle nötr mavi, akşam amber, gece çelik mavisi.</summary>
        public static float[] AerialTint(TimeOfDay t)
        {
            switch (t)
            {
                case TimeOfDay.Safak: return new[] { 0.74f, 0.66f, 0.70f };
                case TimeOfDay.Aksam: return new[] { 0.86f, 0.62f, 0.38f };
                case TimeOfDay.Gece: return new[] { 0.20f, 0.28f, 0.40f };
                default: return new[] { 0.62f, 0.74f, 0.88f };
            }
        }

        /// <summary>Aerial tint'in sis rengine karışım gücü (0..1). Kapalı havada gri sis baskın olduğundan azalır.</summary>
        public static float AerialTintStrength(TimeOfDay t, WeatherKind w)
        {
            float s = t == TimeOfDay.Safak ? 0.22f : t == TimeOfDay.Aksam ? 0.20f : t == TimeOfDay.Gece ? 0.12f : 0.10f;
            return w == WeatherKind.Acik ? s : s * 0.5f;
        }

        /// <summary>Tint'i verilen RGB'ye lerp'ler (Unity'siz). baseRgb ve çıktı: 3 elemanlı.</summary>
        public static float[] ApplyAerialTint(float[] baseRgb, TimeOfDay t, WeatherKind w)
        {
            var tint = AerialTint(t);
            var k = AerialTintStrength(t, w);
            return new[]
            {
                baseRgb[0] + (tint[0] - baseRgb[0]) * k,
                baseRgb[1] + (tint[1] - baseRgb[1]) * k,
                baseRgb[2] + (tint[2] - baseRgb[2]) * k,
            };
        }

        /// <summary>Vadi tabanı yakınlığı (0..1): kamera düşükse 1. Yaklaşık: 4 m altı tam çukur, 40 m üstü yok (smoothstep).</summary>
        public static float ValleyDepth01(float cameraHeight)
        {
            var x = Math.Max(0f, Math.Min(1f, (cameraHeight - 4f) / 36f));
            return 1f - x * x * (3f - 2f * x);
        }

        /// <summary>Sabah sis gölü çarpanı (>= 1): vadi tabanında yoğunluk artar; en güçlü şafak, öğlen/yağmurda çok az.</summary>
        public static float ValleyFogMultiplier(float cameraHeight, TimeOfDay t, WeatherKind w)
        {
            float gain = t == TimeOfDay.Safak ? 0.9f : t == TimeOfDay.Gece ? 0.25f : t == TimeOfDay.Aksam ? 0.2f : 0.08f;
            if (w != WeatherKind.Acik) gain *= 0.5f;
            return 1f + gain * ValleyDepth01(cameraHeight);
        }

        /// <summary>600 m üstü sırt haze bandı çarpanı (1..1.4): 600 m'den 900 m'ye doğru yumuşakça artar, aerial yoğunluğuna uygulanır.</summary>
        public static float RidgeHazeMultiplier(float cameraHeight)
        {
            var x = Math.Max(0f, Math.Min(1f, (cameraHeight - 600f) / 300f));
            return 1f + 0.4f * x * x * (3f - 2f * x);
        }

        /// <summary>Henyey-Greenstein faz fonksiyonu (Mie saçılması). g: anizotropi (0.76 tipik).</summary>
        public static float MiePhase(float cosTheta, float g)
        {
            g = Math.Max(-0.99f, Math.Min(0.99f, g));
            var g2 = g * g;
            var denom = 1f + g2 - 2f * g * cosTheta;
            return (1f - g2) / (4f * (float)Math.PI * (float)Math.Pow(Math.Max(1e-4f, denom), 1.5f));
        }

        /// <summary>Güneş hâlesi parlaklık çarpanı (1 = bugünkü görünüm): gün batımı güçlü, kapalı havada zayıf, gece yok.</summary>
        public static float MieHaloGain(TimeOfDay t, WeatherKind w)
        {
            if (t == TimeOfDay.Gece)
                return 1f;
            float g = t == TimeOfDay.Safak || t == TimeOfDay.Aksam ? 1.6f : 1.25f;
            return w == WeatherKind.Acik ? g : 1f;
        }

        /// <summary>HDRI SkyOverride kimliği (ContentIds.Sky*): akşam/şafak "sunset", yağmur/kar "cloudy", aksi "day_clear".</summary>
        public static string SkyId(TimeOfDay t, WeatherKind w)
        {
            if (t == TimeOfDay.Safak || t == TimeOfDay.Aksam) return "sunset";
            if (w != WeatherKind.Acik) return "cloudy";
            return "day_clear";
        }

        /// <summary>Hacimsel ışık: Orta (1, en ucuz yapılandırma) ve üstü; Düşük (0) kapalı. VolumetricFogMath.ForTier ile aynı.</summary>
        public static bool VolumetricAllowed(int qualityLevel, bool userEnabled) => userEnabled && qualityLevel >= 1;
    }
}
