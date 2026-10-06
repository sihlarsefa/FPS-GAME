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

        /// <param name="armorCoverage">
        /// Zırh bölgesi çarpanı (ArmorZones): 1 = vuruş zırhın tam kapladığı yerde (varsayılan, eski davranış), 0 = zırhı atlar
        /// (omuz/yan, miğfer yüzü), arası kısmi (yumuşak zırh, miğfer kenarı). Zırhın etkinliğini çarpar; 0'da zırh aşınmaz.
        /// </param>
        public static DamageResult ComputeBulletDamage(WeaponDefinitionData weapon, BodyPart part, float distance, ArmorPiece armor,
            float damageScale = 1f, float armorCoverage = 1f)
        {
            if (weapon == null || weapon.Damage <= 0f)
                return new DamageResult(0f, 0f);

            var raw = weapon.Damage * BodyPartMultiplier(weapon, part) * DistanceFactor(weapon, distance) *
                      (damageScale < 0f ? 0f : damageScale);
            // Zırh sınıfı vs kalibre: Sv.1 yelek 7.62'ye karşı zayıf, 9 mm'ye karşı güçlü (PenetrationRules.ArmorEffectiveness);
            // bölge kapsaması (plaka/omuz/yan, miğfer kabuk/kenar/yüz) etkinliği ayrıca ölçekler.
            var effectiveness = armor != null
                ? PenetrationRules.ArmorEffectiveness(weapon.AmmoType, armor.Level) * Clamp01(armorCoverage)
                : 1f;
            return ApplyArmor(raw, armor, effectiveness);
        }

        /// <summary>
        /// Zırhsız/zırhlı (aşınmadan, ilk atış zırhıyla) tek mermi hasarı — saf hesap, zırh dayanıklılığı değişmez.
        /// Saçma silahlarda tek peletin hasarıdır.
        /// </summary>
        public static float PredictBulletDamage(WeaponDefinitionData weapon, BodyPart part, float distance, int armorLevel,
            float armorReduction)
        {
            if (weapon == null || weapon.Damage <= 0f)
                return 0f;

            var raw = weapon.Damage * BodyPartMultiplier(weapon, part) * DistanceFactor(weapon, distance);
            if (armorLevel <= 0 && armorReduction <= 0f)
                return raw;
            var reduction = Clamp01(armorReduction) * PenetrationRules.ArmorEffectiveness(weapon.AmmoType, armorLevel);
            return raw * (1f - reduction);
        }

        /// <summary>Verilen sağlığı bitirmek için gereken isabetli atış (saçmada: tüm peletler isabetli sayılır).</summary>
        public static int ShotsToKill(WeaponDefinitionData weapon, BodyPart part, float distance, int armorLevel = 0,
            float armorReduction = 0f, float health = 100f)
        {
            var perPellet = PredictBulletDamage(weapon, part, distance, armorLevel, armorReduction);
            if (perPellet <= 0f || health <= 0f)
                return 0;

            var perShot = perPellet * (weapon.PelletCount > 1 ? weapon.PelletCount : 1);
            return (int)Math.Ceiling(health / perShot - 1e-4f);
        }

        /// <summary>İlk atıştan ölüme saniye: (atış-1) × atış aralığı (ilk mermi anında çıkar).</summary>
        public static float TimeToKill(WeaponDefinitionData weapon, BodyPart part, float distance, int armorLevel = 0,
            float armorReduction = 0f, float health = 100f)
        {
            var shots = ShotsToKill(weapon, part, distance, armorLevel, armorReduction, health);
            return shots <= 1 ? 0f : (shots - 1) * weapon.FireIntervalSeconds;
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
