using System;

namespace Project.Application.Settings
{
    /// <summary>
    /// Yakınlaştırma basamağına göre ADS hassasiyet çarpanı (CoD MWII: düşük/yüksek yakınlaştırma ayrı çarpanlar).
    /// Basamaklar: 0 = ironsight/kırmızı nokta (&lt;1.5x), 1 = düşük zoom (1.5-3x), 2 = orta (3-5x), 3 = yüksek (&gt;=5x).
    /// Fiziksel eşleşme katsayısı (monitör mesafesi eşleşmesi benzeri) 0..1: 0 = açı oranı, 1 = tan oranı (merkezde
    /// ekran pikseli başına dönüş aynı). Aradaki değerler geometrik karışımdır.
    /// </summary>
    public static class AdsSensitivityModel
    {
        public const int StageCount = 4;
        public const float MinStageMultiplier = 0.2f;
        public const float MaxStageMultiplier = 2f;

        /// <summary>Yakınlaştırma çarpanından basamak (0..3).</summary>
        public static int StageForZoom(float zoom)
        {
            if (zoom < 1.5f) return 0;
            if (zoom < 3f) return 1;
            if (zoom < 5f) return 2;
            return 3;
        }

        /// <summary>Hipfire FOV'sinden ADS FOV'sine geçişte, eşleşme katsayısıyla fiziksel çarpan.</summary>
        public static float MatchScale(float hipFov, float adsFov, float matchCoefficient)
        {
            var c = Clamp01(matchCoefficient);
            if (adsFov >= hipFov - 0.01f)
                return 1f;
            var angle = adsFov / hipFov;
            var tan = Math.Tan(adsFov * FovMath.DegToRad * 0.5) / Math.Tan(hipFov * FovMath.DegToRad * 0.5);
            return (float)Math.Pow(angle, 1.0 - c) * (float)Math.Pow(tan, c);
        }

        /// <summary>
        /// Son çarpan: genel ADS çarpanı * basamak çarpanı * (FOV göreli ise) fiziksel eşleşme. Sonuç [0.02, 3] aralığına kırpılır.
        /// </summary>
        public static float Resolve(AdsSensitivitySettings s, float baseAdsMultiplier, bool fovRelative, float hipFov, float zoom)
        {
            var stage = StageForZoom(zoom);
            var result = baseAdsMultiplier * s.StageMultiplier(stage);
            if (fovRelative && zoom > 1.001f)
            {
                var adsFov = FovMath.ZoomedFov(hipFov, zoom);
                result *= MatchScale(hipFov, adsFov, s.MatchCoefficient);
            }
            return result < 0.02f ? 0.02f : result > 3f ? 3f : result;
        }

        /// <summary>İki hassasiyet değerini (cm/360) karşılaştırmak için ADS'te dönüş uzunluğu.</summary>
        public static float Cm360Ads(float hipCm360, float resolvedMultiplier)
        {
            return resolvedMultiplier <= 0.0001f ? float.PositiveInfinity : hipCm360 / resolvedMultiplier;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>Basamak çarpanları + eşleşme katsayısı (kalıcı veri).</summary>
    public sealed class AdsSensitivitySettings
    {
        public float IronSights = 1f;
        public float LowZoom = 0.9f;
        public float MidZoom = 0.8f;
        public float HighZoom = 0.7f;
        /// <summary>0..1 (varsayılan 0.5: açı ve tan arası).</summary>
        public float MatchCoefficient = 0.5f;

        public float StageMultiplier(int stage)
        {
            switch (stage)
            {
                case 0: return IronSights;
                case 1: return LowZoom;
                case 2: return MidZoom;
                default: return HighZoom;
            }
        }

        public void SetStage(int stage, float value)
        {
            var v = SettingsMath.Clamp(value, AdsSensitivityModel.MinStageMultiplier, AdsSensitivityModel.MaxStageMultiplier, 1f);
            switch (stage)
            {
                case 0: IronSights = v; break;
                case 1: LowZoom = v; break;
                case 2: MidZoom = v; break;
                default: HighZoom = v; break;
            }
        }

        public void Sanitize()
        {
            for (var i = 0; i < AdsSensitivityModel.StageCount; i++)
                SetStage(i, StageMultiplier(i));
            MatchCoefficient = SettingsMath.Clamp(MatchCoefficient, 0f, 1f, 0.5f);
        }

        public AdsSensitivitySettings Clone() => (AdsSensitivitySettings)MemberwiseClone();
    }
}
