using System;
using Project.Core.Domain;

namespace Project.Application.Services
{
    public readonly struct DamageResult
    {
        public float Damage { get; }
        public float ArmorAbsorbed { get; }

        public DamageResult(float damage, float armorAbsorbed)
        {
            Damage = damage;
            ArmorAbsorbed = armorAbsorbed;
        }
    }

    /// <summary>
    /// Saf hasar formülleri. Zırh: hasar * (1 - DamageReduction) — kırık zırh etkisiz; emilen kadar dayanıklılık düşer.
    /// </summary>
    public static class DamageCalculator
    {
        public const float FistDamage = 18f;
        public const float FallDamageThreshold = 12f;

        /// <summary>Eşiğin üzerindeki her m/s çarpma hızı için düşme hasarı.</summary>
        public const float FallDamagePerMeterPerSecond = 7.5f;

        /// <summary>Yumruğun kafaya isabet çarpanı.</summary>
        public const float FistHeadMultiplier = 1.5f;

        /// <summary>Patlamada yeleğin etkinliği (kask etkisiz).</summary>
        public const float ExplosionVestEffectiveness = 0.5f;

        /// <summary>Patlama yarıçapının bu oranı içinde tam hasar uygulanır.</summary>
        public const float ExplosionFullDamageRadiusFraction = 0.15f;

        private const float DefaultHeadshotMultiplier = 2f;
        private const float DefaultLimbMultiplier = 0.8f;

        public static float BodyPartMultiplier(WeaponDefinitionData weapon, BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head:
                    return weapon != null ? Sanitize(weapon.HeadshotMultiplier, DefaultHeadshotMultiplier) : DefaultHeadshotMultiplier;
                case BodyPart.Arm:
                case BodyPart.Leg:
                    return weapon != null ? Sanitize(weapon.LimbMultiplier, DefaultLimbMultiplier) : DefaultLimbMultiplier;
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// Mesafe düşüşü: FalloffStart'a kadar 1, FalloffEnd ve ötesinde MinDamageFactor, arada doğrusal.
        /// </summary>
        public static float DistanceFactor(WeaponDefinitionData weapon, float distance)
        {
            if (weapon == null || float.IsNaN(distance))
                return 1f;

            var start = weapon.FalloffStart;
            var end = weapon.FalloffEnd;
            var min = Clamp01(weapon.MinDamageFactor);

            if (distance <= start)
                return 1f;
            if (end <= start || distance >= end)
                return min;

            var t = (distance - start) / (end - start);
            return 1f + (min - 1f) * t;
        }

        public static DamageResult ComputeBulletDamage(WeaponDefinitionData weapon, BodyPart part, float distance, ArmorPiece armor)
        {
            if (weapon == null || weapon.Damage <= 0f)
                return new DamageResult(0f, 0f);

            var raw = weapon.Damage * BodyPartMultiplier(weapon, part) * DistanceFactor(weapon, distance);
            return ApplyArmor(raw, armor);
        }

        /// <summary>
        /// Zırhı uygular: kırık değilse hasar * (1 - DamageReduction * etkinlik); emilen miktar kadar dayanıklılık düşer.
        /// </summary>
        public static DamageResult ApplyArmor(float rawDamage, ArmorPiece armor, float effectiveness = 1f)
        {
            if (float.IsNaN(rawDamage) || rawDamage <= 0f)
                return new DamageResult(0f, 0f);

            if (armor == null || armor.IsBroken)
                return new DamageResult(rawDamage, 0f);

            var reduction = Clamp01(armor.DamageReduction) * Clamp01(effectiveness);
            if (reduction <= 0f)
                return new DamageResult(rawDamage, 0f);

            var absorbed = rawDamage * reduction;
            armor.Wear(absorbed);
            return new DamageResult(rawDamage - absorbed, absorbed);
        }

        /// <summary>
        /// Patlama hasarı: merkeze yakın küçük çekirdekte tam, yarıçap sınırında 0 olacak şekilde doğrusal azalır.
        /// </summary>
        public static float ComputeExplosionDamage(float maxDamage, float radius, float distance)
        {
            if (maxDamage <= 0f || radius <= 0f || float.IsNaN(distance))
                return 0f;

            if (distance < 0f)
                distance = 0f;
            if (distance >= radius)
                return 0f;

            var core = radius * ExplosionFullDamageRadiusFraction;
            if (distance <= core)
                return maxDamage;

            var t = (distance - core) / (radius - core);
            return maxDamage * (1f - t);
        }

        /// <summary>12 m/s altındaki çarpmalar zararsız; üstünde (v - 12) * 7.5.</summary>
        public static float ComputeFallDamage(float impactSpeed)
        {
            if (float.IsNaN(impactSpeed))
                return 0f;

            var speed = Math.Abs(impactSpeed);
            if (speed < FallDamageThreshold)
                return 0f;

            return (speed - FallDamageThreshold) * FallDamagePerMeterPerSecond;
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }

        private static float Sanitize(float value, float fallback)
        {
            return float.IsNaN(value) || value < 0f ? fallback : value;
        }
    }
}
