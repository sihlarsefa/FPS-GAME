using System;

namespace Project.Application.Match.Flow
{
    /// <summary>
    /// Bölge dışı hasar kuralları (saf). PUBG mavi bölgesi hasarı "maksimum canın yüzdesi / sn" olarak tanımlar
    /// (çember 1 ≈ %0.4/sn, çember 5 ≈ %3/sn) ve Apex halkası 1.5 sn'lik tikle vurur; yani bölgeye ilk adım
    /// cezalandırılmaz, ama dışarıda kalmak zamanla acımasızlaşır. Bu sınıf üç şeyi verir:
    ///  - yüzde/sn ↔ can/sn dönüşümü,
    ///  - ilk vuruştan önce kısa lütuf süresi (siper/araç çıkışı için),
    ///  - dışarıda geçen süreye göre 1.0x → 1.5x yumuşak yükselen çarpan (kenarda oyalanmayı caydırır).
    /// </summary>
    public static class ZoneDamageRules
    {
        /// <summary>Dışarı çıkıldıktan sonra hasarın başlamadığı süre (sn). Apex tik aralığına yakın.</summary>
        public const float GraceSeconds = 1.5f;

        /// <summary>Çarpanın tavana ulaştığı dışarıda kalma süresi (sn).</summary>
        public const float RampSeconds = 15f;

        /// <summary>Uzun süre dışarıda kalanın hasar çarpanı üst sınırı.</summary>
        public const float MaxExposureMultiplier = 1.5f;

        public static float PercentToDps(float percentPerSecond, float maxHealth)
        {
            if (!IsFinite(percentPerSecond) || !IsFinite(maxHealth) || percentPerSecond <= 0f || maxHealth <= 0f)
                return 0f;
            return maxHealth * percentPerSecond * 0.01f;
        }

        public static float DpsToPercent(float dps, float maxHealth)
        {
            if (!IsFinite(dps) || !IsFinite(maxHealth) || dps <= 0f || maxHealth <= 0f)
                return 0f;
            return dps / maxHealth * 100f;
        }

        /// <summary>Dışarıda geçen süreye göre çarpan: lütuf süresinde 1, sonra smoothstep ile 1.5'e.</summary>
        public static float ExposureMultiplier(float secondsOutside)
        {
            if (!IsFinite(secondsOutside) || secondsOutside <= GraceSeconds)
                return 1f;

            var t = (secondsOutside - GraceSeconds) / (RampSeconds - GraceSeconds);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            var smooth = t * t * (3f - 2f * t);
            return 1f + (MaxExposureMultiplier - 1f) * smooth;
        }

        /// <summary>
        /// Bu tikte uygulanacak hasar. secondsOutsideBefore = tikten ÖNCE dışarıda geçen süre; lütuf süresi tik içine
        /// düşüyorsa tikin yalnızca lütuf sonrası kısmı hasar verir.
        /// </summary>
        public static float DamageForTick(float baseDps, float deltaTime, float secondsOutsideBefore)
        {
            if (!IsFinite(baseDps) || !IsFinite(deltaTime) || baseDps <= 0f || deltaTime <= 0f)
                return 0f;

            var before = IsFinite(secondsOutsideBefore) && secondsOutsideBefore > 0f ? secondsOutsideBefore : 0f;
            var after = before + deltaTime;
            if (after <= GraceSeconds)
                return 0f;

            var effectiveStart = before > GraceSeconds ? before : GraceSeconds;
            var effectiveDt = after - effectiveStart;
            var midpoint = effectiveStart + effectiveDt * 0.5f;
            return baseDps * effectiveDt * ExposureMultiplier(midpoint);
        }

        /// <summary>Tam canlı oyuncunun, sabit dışarıda kalarak ölme süresi (sn); iyileşme yok sayılır.</summary>
        public static float SecondsToDie(float baseDps, float health)
        {
            if (!IsFinite(baseDps) || !IsFinite(health) || baseDps <= 0f || health <= 0f)
                return float.PositiveInfinity;

            const float step = 0.25f;
            var t = 0f;
            var hp = health;
            // Üst sınır: sonsuz döngüye karşı 1 saat.
            while (hp > 0f && t < 3600f)
            {
                hp -= DamageForTick(baseDps, step, t);
                t += step;
            }

            return t;
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
