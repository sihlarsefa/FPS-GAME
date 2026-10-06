using System;

namespace Project.Application.Settings
{
    /// <summary>Çıkış cihazı önayarı (Battlefield/Tarkov/Rocket League benzeri).</summary>
    public enum AudioOutputMode
    {
        Kulaklik = 0,
        Hoparlor = 1,
        TvSoundbar = 2,
        GeceModu = 3
    }

    /// <summary>Bir önayarın sayısal karşılığı: dinamik aralık sıkıştırıcısı, EQ rafları ve mekânsallaştırma.</summary>
    public readonly struct AudioMixProfile
    {
        /// <summary>Sıkıştırıcı eşiği (dBFS). Üstü oran kadar bastırılır.</summary>
        public readonly float ThresholdDb;
        /// <summary>Sıkıştırma oranı (1 = kapalı, 4 = 4:1).</summary>
        public readonly float Ratio;
        /// <summary>Sıkıştırma sonrası kazanç telafisi (dB).</summary>
        public readonly float MakeupDb;
        /// <summary>Düşük raf (80 Hz) kazancı dB.</summary>
        public readonly float LowShelfDb;
        /// <summary>Yüksek raf (6 kHz) kazancı dB; ayak sesi/yön ipuçları için.</summary>
        public readonly float HighShelfDb;
        /// <summary>Kafa ilişkili aktarım (HRTF) tarzı mekânsal karışım 0..1 (kulaklıkta yüksek).</summary>
        public readonly float SpatialBlend;
        /// <summary>Patlama/silah gibi yüksek olayların tavan sınırı dBFS (limiter).</summary>
        public readonly float LimiterDb;

        public AudioMixProfile(float threshold, float ratio, float makeup, float low, float high, float spatial, float limiter)
        {
            ThresholdDb = threshold;
            Ratio = ratio;
            MakeupDb = makeup;
            LowShelfDb = low;
            HighShelfDb = high;
            SpatialBlend = spatial;
            LimiterDb = limiter;
        }
    }

    /// <summary>Çıkış önayarları + statik sıkıştırma eğrisi (testlenebilir saf mantık).</summary>
    public static class AudioMixPresets
    {
        public static readonly string[] Names = { "Kulaklık", "Hoparlör", "TV / Soundbar", "Gece modu" };

        public static AudioMixProfile For(AudioOutputMode mode)
        {
            switch (mode)
            {
                // Kulaklık: geniş dinamik, ayak sesi bölgesi hafif öne, tam mekânsal.
                case AudioOutputMode.Kulaklik: return new AudioMixProfile(-6f, 1.5f, 1f, 0f, 2f, 1f, -1f);
                // Hoparlör: orta sıkıştırma, mekânsal azaltılmış (çapraz konuşma).
                case AudioOutputMode.Hoparlor: return new AudioMixProfile(-14f, 2.5f, 3f, -1f, 1f, 0.35f, -2f);
                // TV/Soundbar: güçlü sıkıştırma, düşük bas kısılmış.
                case AudioOutputMode.TvSoundbar: return new AudioMixProfile(-18f, 3.5f, 5f, -3f, 1.5f, 0.2f, -2.5f);
                // Gece: çok güçlü sıkıştırma, sessizler yükselir, patlamalar tavanlanır.
                default: return new AudioMixProfile(-24f, 5f, 8f, -6f, 0f, 0.5f, -6f);
            }
        }

        /// <summary>Statik sıkıştırma eğrisi: giriş dBFS → çıkış dBFS (diz-dirsek yok, sert diz).</summary>
        public static float CompressDb(AudioMixProfile p, float inputDb)
        {
            var o = inputDb <= p.ThresholdDb
                ? inputDb
                : p.ThresholdDb + (inputDb - p.ThresholdDb) / Math.Max(1f, p.Ratio);
            o += p.MakeupDb;
            return o > p.LimiterDb ? p.LimiterDb : o;
        }

        /// <summary>İki giriş seviyesi arasındaki çıkış farkı: dinamik aralığın ne kadar daraldığını ölçer.</summary>
        public static float EffectiveRangeDb(AudioMixProfile p, float quietDb, float loudDb)
        {
            return CompressDb(p, loudDb) - CompressDb(p, quietDb);
        }

        public static AudioOutputMode FromIndex(int i)
        {
            return (AudioOutputMode)SettingsMath.ClampInt(i, 0, Names.Length - 1);
        }

        /// <summary>
        /// Kullanıcının "dinamik aralık" kaydırıcısı (0 = en dar/gece, 1 = geniş/kulaklık) önayarın sıkıştırmasını ölçekler:
        /// eşik ve oran önayar ile referans (kulaklık) arasında doğrusal karışır.
        /// </summary>
        public static AudioMixProfile WithDynamicRange(AudioOutputMode mode, float dynamicRange01)
        {
            var baseP = For(mode);
            var wide = For(AudioOutputMode.Kulaklik);
            var t = SettingsMath.Clamp(dynamicRange01, 0f, 1f, 0.5f);
            // 0.5 = önayar olduğu gibi; 1 = kulaklık değerlerine, 0 = gece değerlerine doğru.
            var narrow = For(AudioOutputMode.GeceModu);
            var target = t >= 0.5f ? wide : narrow;
            var k = Math.Abs(t - 0.5f) * 2f;
            return new AudioMixProfile(
                Lerp(baseP.ThresholdDb, target.ThresholdDb, k),
                Lerp(baseP.Ratio, target.Ratio, k),
                Lerp(baseP.MakeupDb, target.MakeupDb, k),
                baseP.LowShelfDb, baseP.HighShelfDb, baseP.SpatialBlend, baseP.LimiterDb);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    /// <summary>Ses ek tercihleri (kalıcı veri).</summary>
    public sealed class AudioMixSettings
    {
        public int OutputMode;
        /// <summary>0..1; 0.5 = önayar.</summary>
        public float DynamicRange = 0.5f;
        /// <summary>Ayak sesi/yön vurgusu 0..1 (yüksek raf kazancına +6 dB'ye kadar ekler).</summary>
        public float FootstepEmphasis = 0.4f;
        /// <summary>Oyun odakta değilken sesi kıs (0..1 kalan seviye).</summary>
        public float UnfocusedVolume = 0.3f;
        /// <summary>Düşük frekans (patlama bas) sarsıntı çıktısı 0..1.</summary>
        public float LowFrequencyIntensity = 1f;
        /// <summary>Çevre sesi ile telsiz konuşurken otomatik kısma (ducking) açık mı.</summary>
        public bool VoiceDucking = true;

        public void Sanitize()
        {
            OutputMode = SettingsMath.ClampInt(OutputMode, 0, AudioMixPresets.Names.Length - 1);
            DynamicRange = SettingsMath.Clamp(DynamicRange, 0f, 1f, 0.5f);
            FootstepEmphasis = SettingsMath.Clamp(FootstepEmphasis, 0f, 1f, 0.4f);
            UnfocusedVolume = SettingsMath.Clamp(UnfocusedVolume, 0f, 1f, 0.3f);
            LowFrequencyIntensity = SettingsMath.Clamp(LowFrequencyIntensity, 0f, 1f, 1f);
        }

        /// <summary>Efektif profil: dinamik aralık + ayak sesi vurgusu uygulanmış.</summary>
        public AudioMixProfile Resolve()
        {
            var p = AudioMixPresets.WithDynamicRange(AudioMixPresets.FromIndex(OutputMode), DynamicRange);
            return new AudioMixProfile(p.ThresholdDb, p.Ratio, p.MakeupDb, p.LowShelfDb * LowFrequencyIntensity,
                p.HighShelfDb + 6f * FootstepEmphasis, p.SpatialBlend, p.LimiterDb);
        }

        public AudioMixSettings Clone() => (AudioMixSettings)MemberwiseClone();
    }
}
