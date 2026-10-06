using System;

namespace Project.Application.Services
{
    /// <summary>Yıkılabilir nesne türü (cam, ahşap).</summary>
    public enum DestructibleKind
    {
        Glass = 0,
        Wood = 1
    }

    /// <summary>Yıkım hasarı / kırılma kuralları — saf (Unity'siz) hesap.</summary>
    public static class DestructionRules
    {
        public const float GlassHp = 1f;
        public const float VehicleMinSpeed = 3f;
        public const float VehicleDamagePerMps = 35f;

        public static float DefaultHp(DestructibleKind kind) => kind == DestructibleKind.Glass ? GlassHp : 40f;

        /// <summary>Mermi hasarı: cam tek mermide kırılır, ahşap mermi hasarının bir kısmını alır.</summary>
        public static float BulletDamage(DestructibleKind kind, float weaponDamage)
        {
            if (weaponDamage <= 0f)
                return 0f;
            return kind == DestructibleKind.Glass ? GlassHp : Math.Max(1f, weaponDamage * 0.5f);
        }

        /// <summary>Patlama hasarı: yarıçapta doğrusal sönümlenir; dışında 0.</summary>
        public static float ExplosionDamage(float maxDamage, float radius, float distance)
        {
            if (radius <= 0f || distance >= radius)
                return 0f;
            var falloff = 1f - Math.Max(0f, distance) / radius;
            return Math.Max(maxDamage, radius * 20f) * falloff;
        }

        /// <summary>Araç çarpması: yavaş hızda hasar yok; hız ve kütleyle artar.</summary>
        public static float VehicleRamDamage(float relativeSpeed, float mass)
        {
            if (relativeSpeed < VehicleMinSpeed)
                return 0f;
            var massFactor = Math.Min(2f, Math.Max(0.5f, mass / 2000f));
            return relativeSpeed * VehicleDamagePerMps * massFactor;
        }

        /// <summary>Kırılınca üretilecek parça sayısı (boyut ve türe göre, üst sınırlı).</summary>
        public static int ChunkCount(DestructibleKind kind, float volume)
        {
            var baseCount = kind == DestructibleKind.Glass ? 10 : 5;
            var extra = (int)Math.Min(8f, Math.Max(0f, volume) * 6f);
            return Math.Min(18, baseCount + extra);
        }
    }
}
