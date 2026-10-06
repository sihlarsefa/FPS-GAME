using System;

namespace Project.Infrastructure.Vfx.Impacts
{
    /// <summary>Mesafeye göre isabet efekti ayrıntı kademesi.</summary>
    public enum ImpactLod
    {
        Full = 0,
        Reduced = 1,
        DecalOnly = 2,
        None = 3
    }

    /// <summary>
    /// Mesafe + açı + kalite kurallari (saf). Uzakta kıvılcım/parça atılır, toz kalır (uzaktan okunan tek
    /// sinyal budur); çok uzakta yalnız çıkartma; sonra hiç. Sıyırma açısında efekt yüzey boyunca uzar.
    /// </summary>
    public static class ImpactLodRules
    {
        public const float FullDistance = 35f;
        public const float ReducedDistance = 90f;
        public const float DecalDistance = 140f;

        /// <param name="qualityLevel">0..3 (0 en düşük).</param>
        public static float QualityDistanceScale(int qualityLevel)
        {
            switch (qualityLevel)
            {
                case 0: return 0.6f;
                case 1: return 0.8f;
                case 2: return 1f;
                default: return 1.25f;
            }
        }

        public static ImpactLod Classify(float distance, SurfaceKind surface, int qualityLevel)
        {
            if (float.IsNaN(distance) || distance < 0f)
                return ImpactLod.Full;

            var scale = QualityDistanceScale(qualityLevel) * ImpactSurfaceTable.Get(surface).CullDistanceScale;
            if (distance <= FullDistance * scale) return ImpactLod.Full;
            if (distance <= ReducedDistance * scale) return ImpactLod.Reduced;
            if (distance <= DecalDistance * scale)
                return ImpactSurfaceTable.Get(surface).LeavesDecal ? ImpactLod.DecalOnly : ImpactLod.None;
            return ImpactLod.None;
        }

        /// <summary>Kademeye göre parçacık sayısı çarpanı.</summary>
        public static float ParticleFactor(ImpactLod lod)
        {
            switch (lod)
            {
                case ImpactLod.Full: return 1f;
                case ImpactLod.Reduced: return 0.45f;
                default: return 0f;
            }
        }

        /// <summary>Kıvılcım/parça yalnız tam kademede.</summary>
        public static bool AllowsSecondary(ImpactLod lod)
        {
            return lod == ImpactLod.Full;
        }

        /// <summary>Isabet doğrultusu ile yüzey normali arasından yüzeyle yapılan açı (derece, 0=sıyırma, 90=dik).</summary>
        public static float SurfaceAngleDeg(float dirDotNormal)
        {
            var c = Math.Min(1f, Math.Max(0f, Math.Abs(dirDotNormal)));
            return (float)(Math.Asin(c) * 180.0 / Math.PI);
        }

        /// <summary>Çıkartma uzama çarpanı: dikte 1, sıyırmada profilin <c>GrazingStretch</c> değerine kadar.</summary>
        public static float DecalStretch(float surfaceAngleDeg, float maxStretch)
        {
            var t = 1f - Math.Min(1f, Math.Max(0f, surfaceAngleDeg / 90f));
            return 1f + (Math.Max(1f, maxStretch) - 1f) * t * t;
        }

        public static bool IsRicochetAngle(float surfaceAngleDeg, SurfaceKind surface)
        {
            var limit = ImpactSurfaceTable.Get(surface).RicochetAngleDeg;
            return limit > 0f && surfaceAngleDeg <= limit;
        }
    }
}
