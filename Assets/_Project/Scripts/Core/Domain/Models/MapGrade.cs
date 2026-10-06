using System;

namespace Project.Core.Domain
{
    /// <summary>Harita başına renk derecelendirme verisi (saf C#; render katmanı değerleri VolumeProfile'a uygular).</summary>
    public readonly struct MapGrade
    {
        public readonly float Temperature, Tint, Saturation, Contrast, ExposureOffset;
        public readonly float[] ColorFilter;   // r,g,b
        public readonly float[] Shadows;       // r,g,b,w (SMH vektörü: w = parlaklık kayması)
        public readonly float[] Midtones;
        public readonly float[] Highlights;
        public readonly float[] SunTint;       // r,g,b güneş rengi çarpanı
        public readonly float SunIntensityMul;
        public readonly float FogMul;

        public MapGrade(float temperature, float tint, float saturation, float contrast, float exposureOffset,
            float[] colorFilter, float[] shadows, float[] midtones, float[] highlights, float[] sunTint, float sunIntensityMul, float fogMul)
        {
            Temperature = temperature; Tint = tint; Saturation = saturation; Contrast = contrast; ExposureOffset = exposureOffset;
            ColorFilter = colorFilter; Shadows = shadows; Midtones = midtones; Highlights = highlights;
            SunTint = sunTint; SunIntensityMul = sunIntensityMul; FogMul = fogMul;
        }
    }

    /// <summary>Harita derecelendirme tablosu: Kuzgun sıcak-zeytin, Ayaz soğuk-mavi, Mavi Liman parlak-turkuaz, Kartal Yaylası altın-yeşil.</summary>
    public static class MapGradeTable
    {
        public static MapGrade For(string mapId)
        {
            switch (MapCatalog.Normalize(mapId))
            {
                case MapCatalog.AyazGecidi:
                    return new MapGrade(-10f, -4f, -12f, 14f, -0.02f,
                        new[] { 0.96f, 0.98f, 1.02f },
                        new[] { 0.9f, 0.97f, 1.1f, -0.03f }, new[] { 0.96f, 1f, 1.06f, 0f }, new[] { 1f, 1.02f, 1.06f, 0.02f },
                        new[] { 0.9f, 0.96f, 1.08f }, 0.95f, 1.1f);
                case MapCatalog.MaviLiman:
                    return new MapGrade(6f, 3f, 6f, 8f, 0.1f,
                        new[] { 0.97f, 1.03f, 1.02f },
                        new[] { 0.9f, 1.02f, 1.06f, 0f }, new[] { 0.98f, 1.03f, 1.03f, 0.01f }, new[] { 1.04f, 1.03f, 0.98f, 0.03f },
                        new[] { 1.02f, 1.02f, 0.98f }, 1.12f, 1f);
                case MapCatalog.KartalYaylasi: // altın-yeşil yayla: sıcak güneş, canlı çimen
                    return new MapGrade(6f, 3f, 2f, 9f, 0.06f,
                        new[] { 1.02f, 1.03f, 0.95f },
                        new[] { 0.95f, 1.0f, 0.93f, -0.01f }, new[] { 1.02f, 1.04f, 0.94f, 0.01f }, new[] { 1.05f, 1.03f, 0.95f, 0.03f },
                        new[] { 1.06f, 1.02f, 0.9f }, 1.08f, 0.9f);
                default: // Kuzgun
                    return new MapGrade(5f, 1f, -6f, 12f, 0f,
                        new[] { 1f, 1f, 0.96f },
                        new[] { 0.97f, 0.97f, 1.0f, -0.02f }, new[] { 1.02f, 1.01f, 0.93f, 0f }, new[] { 1.03f, 1.01f, 0.96f, 0.02f },
                        new[] { 1.06f, 0.98f, 0.86f }, 1f, 1f);
            }
        }

        /// <summary>Kelvin (1000..12000) → yaklaşık sRGB 0..1 (Tanner Helland yaklaşımı).</summary>
        public static float[] KelvinToRgb(float kelvin)
        {
            var t = Math.Max(1000f, Math.Min(12000f, kelvin)) / 100f;
            float r, g, b;
            r = t <= 66f ? 255f : 329.698727446f * (float)Math.Pow(t - 60f, -0.1332047592);
            g = t <= 66f ? 99.4708025861f * (float)Math.Log(t) - 161.1195681661f
                         : 288.1221695283f * (float)Math.Pow(t - 60f, -0.0755148492);
            b = t >= 66f ? 255f : t <= 19f ? 0f : 138.5177312231f * (float)Math.Log(t - 10f) - 305.0447927307f;
            return new[] { C(r), C(g), C(b) };
        }

        private static float C(float v) => Math.Max(0f, Math.Min(255f, v)) / 255f;

        /// <summary>ADS bulanıklığı için gaussian DoF bitiş mesafesi: ads01 arttıkça yakın bulanıklık (m).</summary>
        public static float AdsBlurStart(float ads01) => 0.1f;
        public static float AdsBlurEnd(float ads01) => ads01 <= 0f ? 1000f : Math.Max(0.6f, 1.4f - 0.8f * Math.Min(1f, ads01));
    }
}
