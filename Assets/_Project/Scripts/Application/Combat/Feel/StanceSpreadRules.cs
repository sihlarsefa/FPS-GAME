using System;
using Project.Core.Domain;

namespace Project.Application.Combat.Feel
{
    /// <summary>Atış duruşu: yayılım ve tepme çarpanlarının seçildiği durum.</summary>
    public enum ShooterStance
    {
        Standing = 0,
        Crouching = 1,
        Prone = 2,
        Walking = 3,
        Sprinting = 4,
        Airborne = 5,
        /// <summary>Siper/duvar üstüne dayanmış (CoD mount benzeri).</summary>
        Mounted = 6
    }

    /// <summary>
    /// Saf duruş/hareket çarpanları. Battlefield: koşu yayılımı çok artırır, yürüme azaltır, çömelme daha çok,
    /// yatış+durgun en düşük. CoD: mount dikey/yatay tepmeyi belirgin keser. Çarpanlar kalça (hip) yayılımına
    /// tam, ADS'ye <see cref="AdsMovementDamping"/> ile yumuşatılarak uygulanır.
    /// </summary>
    public static class StanceSpreadRules
    {
        /// <summary>ADS'de hareket cezası bu orana sönümlenir (1'e doğru çekilir): 0.45 = cezanın %45'i kalır.</summary>
        public const float AdsMovementDamping = 0.45f;

        /// <summary>Çömelirken/yatarken hareket (sürünme) yayılımı bu kadar artar.</summary>
        public const float CrawlPenalty = 1.25f;

        public static float SpreadMultiplier(ShooterStance stance)
        {
            switch (stance)
            {
                case ShooterStance.Standing: return 1.00f;
                case ShooterStance.Crouching: return 0.70f;
                case ShooterStance.Prone: return 0.45f;
                case ShooterStance.Walking: return 1.45f;
                case ShooterStance.Sprinting: return 2.60f;
                case ShooterStance.Airborne: return 3.20f;
                case ShooterStance.Mounted: return 0.35f;
                default: return 1f;
            }
        }

        public static float RecoilMultiplier(ShooterStance stance)
        {
            switch (stance)
            {
                case ShooterStance.Crouching: return 0.85f;
                case ShooterStance.Prone: return 0.65f;
                case ShooterStance.Walking: return 1.10f;
                case ShooterStance.Sprinting: return 1.35f;
                case ShooterStance.Airborne: return 1.50f;
                case ShooterStance.Mounted: return 0.50f;
                default: return 1f;
            }
        }

        /// <summary>Toparlanma hızı çarpanı: iyi duruş daha hızlı toparlar.</summary>
        public static float RecoveryMultiplier(ShooterStance stance)
        {
            switch (stance)
            {
                case ShooterStance.Crouching: return 1.15f;
                case ShooterStance.Prone: return 1.30f;
                case ShooterStance.Mounted: return 1.40f;
                case ShooterStance.Sprinting: return 0.7f;
                case ShooterStance.Airborne: return 0.5f;
                default: return 1f;
            }
        }

        /// <summary>
        /// Duruşu bayraklardan çıkarır. Öncelik: havada > koşu > mount > yatış > çömelme > yürüme > ayakta.
        /// </summary>
        public static ShooterStance Resolve(bool grounded, bool sprinting, bool mounted, bool prone, bool crouching, float horizontalSpeed)
        {
            if (!grounded) return ShooterStance.Airborne;
            if (sprinting) return ShooterStance.Sprinting;
            if (mounted) return ShooterStance.Mounted;
            if (prone) return ShooterStance.Prone;
            if (crouching) return ShooterStance.Crouching;
            return horizontalSpeed > 0.3f ? ShooterStance.Walking : ShooterStance.Standing;
        }

        /// <summary>Hareket halindeki çömelme/yatış için sürünme cezası dahil yayılım çarpanı (ADS harmanlı).</summary>
        public static float EffectiveSpread(ShooterStance stance, float horizontalSpeed, float adsBlend)
        {
            var m = SpreadMultiplier(stance);
            var moving = horizontalSpeed > 0.3f;
            if (moving && (stance == ShooterStance.Crouching || stance == ShooterStance.Prone))
                m *= CrawlPenalty;
            var ads = Clamp01(adsBlend);
            // ADS'de ceza (m>1) sönümlenir; bonus (m<1) aynen kalır.
            var adsM = m > 1f ? 1f + (m - 1f) * AdsMovementDamping : m;
            return m + (adsM - m) * ads;
        }

        /// <summary>
        /// Kategori bazlı ADS yayılım oranı (ADS yayılımı / kalça yayılımı). Keskin nişancı en dar,
        /// pompalı neredeyse değişmez (saçma zaten dağılır).
        /// </summary>
        public static float AdsSpreadRatio(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Pistol: return 0.40f;
                case WeaponCategory.Smg: return 0.35f;
                case WeaponCategory.AssaultRifle: return 0.28f;
                case WeaponCategory.Dmr: return 0.15f;
                case WeaponCategory.Sniper: return 0.05f;
                case WeaponCategory.Shotgun: return 0.75f;
                case WeaponCategory.Lmg: return 0.40f;
                default: return 0.5f;
            }
        }

        /// <summary>Nişan blend'i (0 kalça..1 ADS) ile kategoriye göre taban yayılım oranı (yumuşak eğri).</summary>
        public static float AdsRatioAt(WeaponCategory category, float adsBlend)
        {
            var b = Clamp01(adsBlend);
            var s = b * b * (3f - 2f * b);
            return 1f + (AdsSpreadRatio(category) - 1f) * s;
        }

        private static float Clamp01(float v) { return float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v; }
    }
}
