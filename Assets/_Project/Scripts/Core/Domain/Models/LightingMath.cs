using System;

namespace Project.Core.Domain
{
    /// <summary>Otomatik pozlama (göz uyumu) parametreleri. EV = log2 çarpan; sahne parlaklığı log2 ortalamasına göre hedef EV bulunur.</summary>
    public readonly struct ExposureParams
    {
        /// <summary>Orta gri hedef parlaklık (doğrusal HDR, ~0,18-0,3).</summary>
        public readonly float ReferenceLuma;
        /// <summary>0..1: telafi derecesi. 1 = tam (iç mekân da dış mekân kadar parlak), &lt;1 = iç mekân gerçekçi biçimde karanlık kalır.</summary>
        public readonly float Strength;
        public readonly float MinEv, MaxEv;
        /// <summary>Sahne parlaklaşınca (EV düşer) uyum hızı, 1/sn (hızlı).</summary>
        public readonly float SpeedToBright;
        /// <summary>Sahne kararınca (EV yükselir) uyum hızı, 1/sn (yavaş).</summary>
        public readonly float SpeedToDark;

        public ExposureParams(float referenceLuma, float strength, float minEv, float maxEv, float speedToBright, float speedToDark)
        {
            ReferenceLuma = referenceLuma; Strength = strength; MinEv = minEv; MaxEv = maxEv;
            SpeedToBright = speedToBright; SpeedToDark = speedToDark;
        }
    }

    /// <summary>Otomatik pozlama kalite kademesi: ızgara çözünürlüğü (log-ortalama için), Düşük'te kapalı.</summary>
    public readonly struct ExposureTier
    {
        public readonly bool Enabled;
        /// <summary>Parlaklık ızgarası kenarı (piksel); 8'in katı.</summary>
        public readonly int GridSize;
        /// <summary>Izgara hücresi başına örnek (kareköküyle: 3 = 3x3 ... gölgelendirici sabiti 4x4 kullanır; bilgi amaçlı).</summary>
        public readonly int SamplesPerAxis;

        public ExposureTier(bool enabled, int gridSize, int samplesPerAxis)
        {
            Enabled = enabled; GridSize = gridSize; SamplesPerAxis = samplesPerAxis;
        }
    }

    /// <summary>
    /// Saf (Unity bağımlılığı yok) aydınlatma matematiği: güneş renk sıcaklığı/yoğunluk eğrisi (yüksekliğe göre), gökyüzü ortam ölçeği,
    /// hava durumuna göre gölge gücü, iç mekân gölge dolgusu ve göz uyumu (otomatik pozlama) hesapları.
    /// </summary>
    public static class LightingMath
    {
        public const float SunKelvinHorizon = 2000f;
        public const float SunKelvinNoon = 5800f;
        /// <summary>Bu yükseklikten (derece) sonra güneş tam öğle sıcaklığındadır.</summary>
        public const float SunFullElevationDeg = 45f;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        // ------------------------------------------------------------ renk sıcaklığı / güneş

        /// <summary>Güneş yüksekliğine (derece) göre renk sıcaklığı: ufukta 2000 K, öğlen 5800 K. Eğri hızlı yükselir (gün doğumundan ilk 10° içinde ~3500 K).</summary>
        public static float SunKelvin(float elevationDeg)
        {
            var t = Clamp01(elevationDeg / SunFullElevationDeg);
            var s = (float)Math.Sqrt(t);
            return SunKelvinHorizon + (SunKelvinNoon - SunKelvinHorizon) * s;
        }

        /// <summary>Kara cisim yaklaşık sRGB rengi (Tanner Helland yaklaşımı, 1000-40000 K). Sonuç 0..1; dizi {r,g,b}.</summary>
        public static float[] KelvinToRgb(float kelvin)
        {
            var t = Clamp(kelvin, 1000f, 40000f) / 100f;
            float r, g, b;
            if (t <= 66f)
            {
                r = 255f;
                g = 99.4708025861f * (float)Math.Log(t) - 161.1195681661f;
                b = t <= 19f ? 0f : 138.5177312231f * (float)Math.Log(t - 10f) - 305.0447927307f;
            }
            else
            {
                r = 329.698727446f * (float)Math.Pow(t - 60f, -0.1332047592);
                g = 288.1221695283f * (float)Math.Pow(t - 60f, -0.0755148492);
                b = 255f;
            }
            return new[] { Clamp01(r / 255f), Clamp01(g / 255f), Clamp01(b / 255f) };
        }

        /// <summary>Kelvin rengi, parlaklığı 1'e normalize edilmiş (renk çarpanı olarak; yoğunluğu değiştirmez). Dizi {r,g,b}.</summary>
        public static float[] KelvinTint(float kelvin)
        {
            var c = KelvinToRgb(kelvin);
            var l = 0.2126f * c[0] + 0.7152f * c[1] + 0.0722f * c[2];
            if (l < 1e-4f)
                return new[] { 1f, 1f, 1f };
            return new[] { c[0] / l, c[1] / l, c[2] / l };
        }

        /// <summary>Kasten-Young hava kütlesi (güneş yüksekliği derece; zenit = 1).</summary>
        public static float AirMass(float elevationDeg)
        {
            var e = Clamp(elevationDeg, 0f, 90f);
            var rad = e * (float)Math.PI / 180f;
            return 1f / ((float)Math.Sin(rad) + 0.50572f * (float)Math.Pow(e + 6.07995f, -1.6364f));
        }

        /// <summary>Güneş ışığı yoğunluğu (öğlen = 1): atmosfer sönümü exp(-k * (AM-1)). Ufukta ~0,05; 10°'de ~0,5.</summary>
        public static float SunIntensityFactor(float elevationDeg)
        {
            if (elevationDeg <= 0f)
                return 0.04f;
            var am = AirMass(elevationDeg);
            var zenith = AirMass(90f);
            var f = (float)Math.Exp(-0.12f * (am - zenith));
            return Clamp(f, 0.04f, 1f);
        }

        /// <summary>
        /// Mevcut ön ayar güneş rengine fiziksel sıcaklık tonunu karıştırır (sanatsal tasarımı ezmemek için ağırlık). Dizi {r,g,b}.
        /// weight 0 = ön ayar aynen, 1 = ön ayar parlaklığıyla tam Kelvin tonu.
        /// </summary>
        public static float[] BlendSunColor(float presetR, float presetG, float presetB, float elevationDeg, float weight)
        {
            var tint = KelvinTint(SunKelvin(elevationDeg));
            var w = Clamp01(weight);
            // Ön ayar rengi tonun altında/üstünde: geometrik karışım (çarpımsal) ile ışık şiddeti korunur.
            var tr = 1f + (tint[0] - 1f) * w;
            var tg = 1f + (tint[1] - 1f) * w;
            var tb = 1f + (tint[2] - 1f) * w;
            return new[] { presetR * tr, presetG * tg, presetB * tb };
        }

        // ------------------------------------------------------------ ortam / gölge

        /// <summary>Gökyüzü ortam ışığı çarpanı: yağmur/kar örtüsünde doğrudan güneş azalır, dağınık gökyüzü oranı artar. Alçak güneşte hafif düşer.</summary>
        public static float AmbientScale(float elevationDeg, WeatherKind weather)
        {
            var elev = Clamp01(elevationDeg / 40f);
            var sky = 0.85f + 0.15f * elev;
            switch (weather)
            {
                case WeatherKind.Yagmur: return sky * 1.7f;
                case WeatherKind.Kar: return sky * 1.9f;
                default: return sky;
            }
        }

        /// <summary>Güneş gölge gücü (0..1): açık = 1, bulutlu/yağmurlu yumuşak ve zayıf, karlı daha da dağınık. Alçak güneşte uzun gölgeler korunur.</summary>
        public static float ShadowStrength(WeatherKind weather, float elevationDeg)
        {
            float baseS;
            switch (weather)
            {
                case WeatherKind.Yagmur: baseS = 0.25f; break;
                case WeatherKind.Kar: baseS = 0.30f; break;
                default: baseS = 1f; break;
            }
            // Ufuk yakınında güneş zayıf: gölge kontrastı doğrudan ışığa göre bir miktar düşer.
            var low = 1f - Clamp01(elevationDeg / 8f);
            return Clamp(baseS * (1f - 0.12f * low), 0.2f, 1f);
        }

        /// <summary>
        /// Gölge dolgusu (iç mekân/gölge içindeki dolaylı ışık artışı, 0..1 ek): gölge = ortam ışığı; prob'larla tutarlı olsun diye zemin/ekvator
        /// ortamını bu oranda kaldırır. Gündüz ve bulutlu havada yüksek; gece 0'a yakın (gece iç mekân karanlık kalır).
        /// </summary>
        public static float ShadeFill(float elevationDeg, WeatherKind weather)
        {
            if (elevationDeg <= 0f)
                return 0.02f;
            var f = 0.10f + 0.12f * Clamp01(elevationDeg / 45f);
            if (weather != WeatherKind.Acik)
                f += 0.05f;
            return Clamp(f, 0f, 0.3f);
        }

        // ------------------------------------------------------------ otomatik pozlama

        /// <summary>Saat + harita için göz uyumu parametreleri. Gündüz dış mekân parlak, iç mekân (Strength&lt;1) gerçekçi karanlık, gece düşük hedefli.</summary>
        public static ExposureParams ExposureFor(TimeOfDay time, string mapId)
        {
            float refLuma, strength, minEv, maxEv;
            switch (time)
            {
                case TimeOfDay.Safak: refLuma = 0.17f; strength = 0.6f; minEv = -1.2f; maxEv = 1.8f; break;
                case TimeOfDay.Aksam: refLuma = 0.17f; strength = 0.6f; minEv = -1.2f; maxEv = 1.8f; break;
                case TimeOfDay.Gece: refLuma = 0.05f; strength = 0.45f; minEv = -0.6f; maxEv = 2.6f; break;
                default: refLuma = 0.36f; strength = 0.65f; minEv = -0.8f; maxEv = 1.8f; break;
            }

            // Harita: kar/yayla parlak (düşük üst sınır), liman/vadi dengeli.
            switch (MapCatalog.Normalize(mapId))
            {
                case MapCatalog.AyazGecidi: minEv -= 0.4f; maxEv -= 0.2f; break;      // kar yansıması: parlak sahnede daha çok kıs
                case MapCatalog.MaviLiman: minEv -= 0.2f; break;
                case MapCatalog.KartalYaylasi: maxEv += 0.1f; break;
            }

            return new ExposureParams(refLuma, strength, minEv, maxEv, 3.5f, 0.6f);
        }

        /// <summary>Kademe tablosu: 0 Düşük kapalı; 1 Orta 32 ızgara; 2/3 64 ızgara.</summary>
        public static ExposureTier ExposureForTier(int tier)
        {
            if (tier <= 0)
                return new ExposureTier(false, 32, 4);
            if (tier == 1)
                return new ExposureTier(true, 32, 4);
            return new ExposureTier(true, 64, 4);
        }

        /// <summary>Sahne log2-ortalama parlaklığından (avgLog2Luma) hedef EV: (log2(ref) - avg) * strength, [MinEv, MaxEv] aralığına kırpılır.</summary>
        public static float TargetEv(float avgLog2Luma, in ExposureParams p)
        {
            var raw = ((float)Math.Log(Math.Max(1e-4f, p.ReferenceLuma), 2.0) - avgLog2Luma) * Clamp01(p.Strength);
            return Clamp(raw, p.MinEv, p.MaxEv);
        }

        /// <summary>Hedef EV'ye üstel uyum. Sahne parlaklaşırsa (hedef EV düşer) hızlı, kararırsa yavaş. dt saniye (0,1 sn'ye kırpılır). Üstel kararlı (aşmaz).</summary>
        public static float AdaptEv(float currentEv, float targetEv, float dt, in ExposureParams p)
        {
            var d = Clamp(dt, 0f, 0.1f);
            var speed = targetEv < currentEv ? p.SpeedToBright : p.SpeedToDark;
            var k = 1f - (float)Math.Exp(-Math.Max(0f, speed) * d);
            return currentEv + (targetEv - currentEv) * k;
        }

        /// <summary>EV'den doğrusal çarpan (2^EV).</summary>
        public static float EvToMultiplier(float ev) => (float)Math.Pow(2.0, ev);

        /// <summary>
        /// Merkez ağırlıklı ızgara ağırlığı (gölgelendirici aynısını kullanır): 1 merkezde, kenarda 0,35. uv 0..1.
        /// Ufuk/gökyüzü patlamasının tüm pozlamayı çökertmemesi için merkez öncelikli.
        /// </summary>
        public static float CenterWeight(float u, float v)
        {
            var dx = u - 0.5f;
            var dy = v - 0.5f;
            var r2 = (dx * dx + dy * dy) * 4f; // köşede 2
            return 0.35f + 0.65f * (float)Math.Exp(-r2 * 1.4f);
        }

        /// <summary>sRGB-uyumlu doğrusal parlaklık (Rec.709).</summary>
        public static float Luminance(float r, float g, float b) => 0.2126f * r + 0.7152f * g + 0.0722f * b;
    }
}
