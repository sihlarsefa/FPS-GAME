using System;

namespace Project.Infrastructure.Vfx.Impacts
{
    /// <summary>Bir yüzey için isabet efekti tarifi (saf veri; Unity gerektirmez).</summary>
    public readonly struct ImpactSurfaceProfile
    {
        public ImpactSurfaceProfile(float particleScale, float dustLinger, float sparkChance, float debrisChance,
            float decalMin, float decalMax, float decalLifetime, float decalFade, float grazingStretch,
            float ricochetAngleDeg, bool leavesDecal, float cullDistanceScale)
        {
            ParticleScale = particleScale;
            DustLinger = dustLinger;
            SparkChance = sparkChance;
            DebrisChance = debrisChance;
            DecalMin = decalMin;
            DecalMax = decalMax;
            DecalLifetime = decalLifetime;
            DecalFade = decalFade;
            GrazingStretch = grazingStretch;
            RicochetAngleDeg = ricochetAngleDeg;
            LeavesDecal = leavesDecal;
            CullDistanceScale = cullDistanceScale;
        }

        /// <summary>Parçacık sayısı çarpanı (1 = taban).</summary>
        public float ParticleScale { get; }
        /// <summary>Toz/buhar bulutunun kalma süresi çarpanı.</summary>
        public float DustLinger { get; }
        /// <summary>Kıvılcım ihtimali 0..1.</summary>
        public float SparkChance { get; }
        /// <summary>Kıymık/parça ihtimali 0..1.</summary>
        public float DebrisChance { get; }
        public float DecalMin { get; }
        public float DecalMax { get; }
        /// <summary>Çıkartma ömrü (sn); 0 = oturum boyunca.</summary>
        public float DecalLifetime { get; }
        /// <summary>Ömrün son kısmında solma süresi (sn).</summary>
        public float DecalFade { get; }
        /// <summary>Sıyırma açısında çıkartmanın uzama çarpanı.</summary>
        public float GrazingStretch { get; }
        /// <summary>Bu açının (yüzeyle yapılan) altında seken-izi efekti.</summary>
        public float RicochetAngleDeg { get; }
        public bool LeavesDecal { get; }
        /// <summary>Genel kırpma mesafesi çarpanı (toz uzaktan da okunur, kıvılcım okunmaz).</summary>
        public float CullDistanceScale { get; }
    }

    /// <summary>
    /// Yüzey -> isabet efekti tablosu. Değerler Battlefield/CoD tarzı yüzey tablolarına dayanır: sert yüzey
    /// (metal/beton) kıvılcım+parça, toprak/kar toz bulutu, ahşap kıymık, su sıçrama, et kan buharı.
    /// Çıkartma ömrü yüzeye göre farklıdır (metal delikleri uzun, kar delikleri çabuk kapanır).
    /// </summary>
    public static class ImpactSurfaceTable
    {
        private static readonly ImpactSurfaceProfile[] Profiles = Build();

        public static ImpactSurfaceProfile Get(SurfaceKind surface)
        {
            var i = (int)surface;
            return i >= 0 && i < Profiles.Length ? Profiles[i] : Profiles[0];
        }

        private static ImpactSurfaceProfile[] Build()
        {
            var count = Enum.GetValues(typeof(SurfaceKind)).Length;
            var table = new ImpactSurfaceProfile[count];
            for (var i = 0; i < count; i++)
                table[i] = Create((SurfaceKind)i);
            return table;
        }

        private static ImpactSurfaceProfile Create(SurfaceKind s)
        {
            //                                         parçacık toz   kıvılcım parça  decalMin max   ömür fade sıyırma seken  decal  uzak
            switch (s)
            {
                case SurfaceKind.Dirt:
                    return new ImpactSurfaceProfile(1.15f, 1.6f, 0f, 0.35f, 0.09f, 0.13f, 90f, 12f, 1.8f, 12f, true, 1.1f);
                case SurfaceKind.Concrete:
                    return new ImpactSurfaceProfile(1.0f, 1.2f, 0.25f, 0.7f, 0.07f, 0.10f, 120f, 15f, 2.0f, 15f, true, 1.0f);
                case SurfaceKind.Metal:
                    return new ImpactSurfaceProfile(0.8f, 0.3f, 0.95f, 0.2f, 0.05f, 0.07f, 180f, 20f, 2.4f, 22f, true, 0.7f);
                case SurfaceKind.Wood:
                    return new ImpactSurfaceProfile(0.9f, 0.5f, 0f, 0.9f, 0.06f, 0.085f, 150f, 15f, 2.2f, 10f, true, 0.85f);
                case SurfaceKind.Flesh:
                    return new ImpactSurfaceProfile(1.0f, 1.0f, 0f, 0f, 0.22f, 0.42f, 60f, 20f, 1.4f, 0f, true, 0.9f);
                case SurfaceKind.Water:
                    return new ImpactSurfaceProfile(1.2f, 0.8f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 8f, false, 0.8f);
                case SurfaceKind.Foliage:
                    return new ImpactSurfaceProfile(0.7f, 0.4f, 0f, 0.6f, 0f, 0f, 0f, 0f, 1f, 0f, false, 0.6f);
                case SurfaceKind.Snow:
                    return new ImpactSurfaceProfile(1.25f, 1.9f, 0f, 0.1f, 0.08f, 0.12f, 45f, 15f, 1.6f, 9f, true, 1.15f);
                default:
                    return new ImpactSurfaceProfile(1.0f, 1.0f, 0.1f, 0.4f, 0.07f, 0.10f, 120f, 15f, 2.0f, 14f, true, 1.0f);
            }
        }
    }
}
