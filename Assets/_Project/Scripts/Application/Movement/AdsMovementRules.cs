using System;

namespace Project.Application.Movement
{
    /// <summary>
    /// Nişan alırken (ADS) hareket kuralları. Referans: CoD'da ADS yürüme ~%60-70, Tarkov'da ağır silahla ~%50;
    /// keskin nişancı dürbünlerinde daha da yavaş. Çömelik/yüzüstü ADS göreli olarak daha az yavaşlar (zaten yavaş).
    /// </summary>
    public static class AdsMovementRules
    {
        private static float C01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>
        /// Tam ADS'deki yürüme çarpanı. <paramref name="stanceIndex"/> 0 ayakta, 1 çömelik, 2 yüzüstü.
        /// <paramref name="weaponKg"/> silah ağırlığı, <paramref name="zoom"/> dürbün büyütmesi (1 = açık/kırmızı nokta).
        /// </summary>
        public static float FullAdsSpeedFactor(int stanceIndex, float weaponKg, float zoom)
        {
            var baseFactor = stanceIndex == 2 ? 0.88f : stanceIndex == 1 ? 0.78f : 0.64f;
            var kg = float.IsNaN(weaponKg) ? 3.5f : weaponKg;
            var weight = 1f - 0.025f * (kg - 3.5f);
            weight = weight < 0.8f ? 0.8f : weight > 1.08f ? 1.08f : weight;
            var z = float.IsNaN(zoom) || zoom < 1f ? 1f : zoom;
            // 4x ve üstü: her ek 1x'te %2 daha yavaş, en çok %12.
            var zoomPenalty = 1f - Math.Min(0.12f, Math.Max(0f, z - 1f) * 0.02f);
            return baseFactor * weight * zoomPenalty;
        }

        /// <summary>ADS ilerlemesine (0..1) göre yumuşak karışan hız çarpanı: kalkma ADS'ye girerken hemen yavaşlatmaz.</summary>
        public static float SpeedFactor(float adsProgress, int stanceIndex, float weaponKg, float zoom)
        {
            var p = C01(adsProgress);
            // Başta yavaş, sonda hızlı etki (smoothstep): yarım ADS'de ~%50 etki.
            var eased = p * p * (3f - 2f * p);
            var full = FullAdsSpeedFactor(stanceIndex, weaponKg, zoom);
            return 1f + (full - 1f) * eased;
        }

        /// <summary>ADS'de ivmelenme çarpanı: nişanlıyken yön değiştirmek daha az çevik.</summary>
        public static float AccelerationFactor(float adsProgress) => 1f - 0.25f * C01(adsProgress);

        /// <summary>ADS'de koşu yasak: koşu istenirse ADS bırakılmalı (CoD davranışı) — ADS ilerlemesi bu değerin altındaysa koşu serbest.</summary>
        public static bool SprintAllowed(float adsProgress) => C01(adsProgress) < 0.15f;

        /// <summary>
        /// Yan yürüyüş (strafe) ADS'de kısıtlanır: yatay girdi payı arttıkça ek %0-10 yavaşlama (Tarkov hissi).
        /// </summary>
        public static float StrafePenalty(float adsProgress, float strafeAmount01) => 1f - 0.10f * C01(adsProgress) * C01(strafeAmount01);

        /// <summary>ADS'ye girişte hareket-yayılımı (hareket doğruluğu) çarpanı: durarak nişanda 1, yürürken 1.6'ya kadar.</summary>
        public static float MovingAdsSpreadFactor(float speed01) => 1f + 0.6f * C01(speed01);
    }
}
