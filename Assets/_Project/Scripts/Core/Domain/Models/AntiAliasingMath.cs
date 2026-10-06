using System;

namespace Project.Core.Domain
{
    /// <summary>Kenar yumuşatma yöntemi (saf veri; URP AntialiasingMode ile Infrastructure'da eşlenir).</summary>
    public enum AaMode
    {
        None = 0,
        Fxaa = 1,
        SmaaHigh = 2,
        /// <summary>URP Temporal AA (quality High).</summary>
        Taa = 3,
        /// <summary>STP yükseltici etkinken zamansal AA'yı STP üstlenir (kamerada TAA modu istenir).</summary>
        Stp = 4
    }

    /// <summary>Bir kademenin kenar yumuşatma + keskinleştirme kararı.</summary>
    public readonly struct AaTier
    {
        public readonly AaMode Mode;
        /// <summary>CAS taban gücü (0..1); kullanıcı "Keskinlik" kaydırıcısı bununla çarpılır.</summary>
        public readonly float SharpenBase;

        public AaTier(AaMode mode, float sharpenBase)
        {
            Mode = mode; SharpenBase = sharpenBase;
        }

        public bool IsTemporal => Mode == AaMode.Taa || Mode == AaMode.Stp;
    }

    /// <summary>
    /// AA + keskinlik + doku mip kayması kararları (saf matematik, testli). Düşük FXAA, Orta SMAA High,
    /// Yüksek TAA (STP yükselticiyse STP), Ultra TAA/STP. Zamansal AA yığınlı (overlay silah) kamerada kapalıysa SMAA'ya düşülür.
    /// </summary>
    public static class AntiAliasingMath
    {
        public const int TierCount = 4;

        /// <summary>
        /// URP TAA/STP kamera yığınındaki overlay kamerayla birlikte çalışmaz (hayalet/sürüklenme riski). Unity'de görsel olarak
        /// doğrulanırsa true yapılır; false iken silah görünürken taban kamera SMAA'ya düşer, silah keskin kalır.
        /// </summary>
        public const bool TemporalSupportsOverlayStack = false;

        /// <summary>Kademe tablosu: istenen yöntem (yükseltici/yığın dikkate alınmadan).</summary>
        public static AaMode DesiredMode(int tier)
        {
            switch (Clamp(tier))
            {
                case 0: return AaMode.Fxaa;
                case 1: return AaMode.SmaaHigh;
                default: return AaMode.Taa;
            }
        }

        /// <summary>CAS taban gücü: Düşük/Orta düşük çözünürlükte yumuşak görüntüyü toparlar, Yüksek/Ultra TAA bulanıklığını alır.</summary>
        public static float SharpenBaseFor(int tier)
        {
            switch (Clamp(tier))
            {
                case 0: return 0.35f;
                case 1: return 0.40f;
                case 2: return 0.55f;
                default: return 0.50f;
            }
        }

        /// <summary>Kademe için karar; stpUpscaler true ise zamansal mod STP olur.</summary>
        public static AaTier ForTier(int tier, bool stpUpscaler)
        {
            var mode = DesiredMode(tier);
            if (mode == AaMode.Taa && stpUpscaler)
                mode = AaMode.Stp;
            return new AaTier(mode, SharpenBaseFor(tier));
        }

        /// <summary>
        /// Etkin yöntem: zamansal mod + overlay yığını (silah kamerası görünür) + yığın desteği yoksa SMAA High'a düşer.
        /// Silah taban kameranın AA'sını devralır; hayalet olmaz.
        /// </summary>
        public static AaMode Effective(AaMode desired, bool overlayStackActive, bool temporalSupportsStack = TemporalSupportsOverlayStack)
        {
            var temporal = desired == AaMode.Taa || desired == AaMode.Stp;
            if (temporal && overlayStackActive && !temporalSupportsStack)
                return AaMode.SmaaHigh;
            return desired;
        }

        /// <summary>Kullanıcı kaydırıcısı (0..1, varsayılan 0,5 = tablo değeri) ile taban gücü birleştirir. 0 = kapalı, 1 = taban x 2 (üst sınır 1).</summary>
        public static float EffectiveSharpness(float tierBase, float userSlider)
        {
            var u = Clamp01(userSlider);
            return Clamp01(Clamp01(tierBase) * u * 2f);
        }

        /// <summary>Keskinlik geçişi çalıştırılsın mı (çok küçük güç = atla).</summary>
        public static bool SharpenWorthRunning(float sharpness) => sharpness >= 0.02f;

        /// <summary>
        /// Doku mip kayması (LOD bias): zamansal AA/yükseltme altında dokular bulanık örneklenir; negatif kayma bunu telafi eder.
        /// Çözünürlük ölçeği &lt; 1 ise log2(ölçek) (en çok -1), zamansal modda en az -0,25 (ölçek 1'de bile hafif).
        /// </summary>
        public static float MipBias(float renderScale, AaMode mode)
        {
            var scale = renderScale <= 0.01f ? 1f : Math.Min(renderScale, 1f);
            var temporal = mode == AaMode.Taa || mode == AaMode.Stp;
            var bias = (float)Math.Log(scale, 2.0);
            if (bias < -1f) bias = -1f;
            if (temporal && bias > -0.25f) bias = -0.25f;
            if (!temporal && scale >= 0.999f) bias = 0f;
            return bias;
        }

        private static int Clamp(int t) => t < 0 ? 0 : t >= TierCount ? TierCount - 1 : t;
        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
