using System;

namespace Project.Infrastructure.Vfx.Impacts
{
    /// <summary>
    /// Çıkartma ömrü ve havuz kotası (saf). Yoğun çatışmada ömür kısalır (basınç), böylece havuz eski izleri
    /// taşımadan temiz kalır; son <c>fade</c> saniyede alfa 1'den 0'a iner.
    /// </summary>
    public static class ImpactDecalLifetime
    {
        /// <summary>Havuz doluluğu 0..1; 0.6'dan sonra ömür doğrusal kısalır, tam doluda %35.</summary>
        public static float PressureFactor(int used, int capacity)
        {
            if (capacity <= 0) return 0.35f;
            var fill = Math.Min(1f, Math.Max(0f, used / (float)capacity));
            if (fill <= 0.6f) return 1f;
            return 1f - 0.65f * ((fill - 0.6f) / 0.4f);
        }

        public static float EffectiveLifetime(SurfaceKind surface, int used, int capacity)
        {
            var life = ImpactSurfaceTable.Get(surface).DecalLifetime;
            return life <= 0f ? 0f : life * PressureFactor(used, capacity);
        }

        /// <summary>Yaşa göre alfa (0..1). Ömür 0 ise hiç solmaz.</summary>
        public static float Alpha(SurfaceKind surface, float age, float lifetime)
        {
            if (lifetime <= 0f) return 1f;
            if (age < 0f) return 1f;
            if (age >= lifetime) return 0f;
            var fade = Math.Min(ImpactSurfaceTable.Get(surface).DecalFade, lifetime * 0.5f);
            var remain = lifetime - age;
            return remain >= fade || fade <= 0f ? 1f : remain / fade;
        }

        public static bool IsExpired(float age, float lifetime)
        {
            return lifetime > 0f && age >= lifetime;
        }

        /// <summary>Kaliteye göre çıkartma havuz boyutu.</summary>
        public static int PoolCapacity(int qualityLevel)
        {
            switch (qualityLevel)
            {
                case 0: return 64;
                case 1: return 128;
                case 2: return 256;
                default: return 400;
            }
        }
    }
}
