using System;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Sekme (ricochet) saf kuralları: açı kapısı, tohumdan deterministik şans, hasar düşüşü ve sapma yönü.
    /// Unity nesnesi/Random kullanmaz; aynı atış tohumu sunucuda ve istemcide aynı sonucu verir.
    /// </summary>
    public static class RicochetRules
    {
        /// <summary>Bu sıyırma açısının (derece) altındaki isabetler sekebilir.</summary>
        public const float MaxAngleDeg = 25f;

        /// <summary>Sert yüzeyde, açı kapısını geçen her isabetin sekme olasılığı.</summary>
        public const float BaseChance = 0.20f;

        /// <summary>Sıfır açıda kalan hasar oranı; MaxAngle'da <see cref="DamageKeepAtMax"/>.</summary>
        public const float DamageKeepAtZero = 0.5f;
        public const float DamageKeepAtMax = 0.3f;

        /// <summary>Sert yüzey: metal, beton/kaya (kaya sınıflandırıcıda beton sayılır).</summary>
        public static bool IsHardSurface(SurfaceKind surface) =>
            surface == SurfaceKind.Metal || surface == SurfaceKind.Concrete;

        public static bool PassesAngleGate(float grazingAngleDeg) =>
            grazingAngleDeg >= 0f && grazingAngleDeg < MaxAngleDeg;

        /// <summary>Kalan hasar oranı (açı küçüldükçe daha çok enerji korunur).</summary>
        public static float DamageKeep(float grazingAngleDeg)
        {
            var t = Mathf.Clamp01(grazingAngleDeg / MaxAngleDeg);
            return Mathf.Lerp(DamageKeepAtZero, DamageKeepAtMax, t);
        }

        /// <summary>Atış tohumu: kaynak ve isabet noktası milimetreye yuvarlanır, sekme sırası katılır.</summary>
        public static uint ShotSeed(Vector3 origin, Vector3 point, int ricochetIndex)
        {
            unchecked
            {
                var h = 2166136261u;
                h = Mix(h, Q(origin.x)); h = Mix(h, Q(origin.y)); h = Mix(h, Q(origin.z));
                h = Mix(h, Q(point.x)); h = Mix(h, Q(point.y)); h = Mix(h, Q(point.z));
                h = Mix(h, (uint)ricochetIndex);
                return Finalize(h);
            }
        }

        /// <summary>Tohum ve kanal için [0,1) değeri (deterministik).</summary>
        public static float Roll01(uint seed, int channel = 0)
        {
            unchecked
            {
                var h = Finalize(seed ^ ((uint)channel * 0x9E3779B1u));
                return (h >> 8) * (1f / 16777216f);
            }
        }

        public static bool ShouldRicochet(SurfaceKind surface, float grazingAngleDeg, uint seed) =>
            IsHardSurface(surface) && PassesAngleGate(grazingAngleDeg) && Roll01(seed, 0) < BaseChance;

        /// <summary>Yansıma yönü + tohumdan türeyen küçük saçılma; yüzeyin içine gömülmez. Birim vektör döner.</summary>
        public static Vector3 Deflect(Vector3 direction, Vector3 normal, uint seed)
        {
            var r = Vector3.Reflect(direction, normal);
            var jitter = new Vector3(Roll01(seed, 1) - 0.5f, Roll01(seed, 2) - 0.5f, Roll01(seed, 3) - 0.5f) * 0.12f;
            r += jitter;
            var d = Vector3.Dot(r, normal);
            if (d < 0.02f)
                r += normal * (0.02f - d);
            return r.sqrMagnitude < 1e-8f ? normal : r.normalized;
        }

        private static uint Q(float v) => unchecked((uint)(int)Mathf.Round(Mathf.Clamp(v, -1e6f, 1e6f) * 1000f));

        private static uint Mix(uint h, uint v)
        {
            unchecked
            {
                h ^= v;
                h *= 16777619u;
                return h;
            }
        }

        private static uint Finalize(uint h)
        {
            unchecked
            {
                h ^= h >> 16; h *= 0x85EBCA6Bu;
                h ^= h >> 13; h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }
    }

    /// <summary>Yüzey isabet sesi saf kuralları: varyasyon seçimi, mesafe kovası, kesim frekansı, perde.</summary>
    public static class ImpactSoundRules
    {
        public const int Variations = 4;
        public const int DistanceBuckets = 3;

        /// <summary>Yüzeyin ses ailesi (benzer yüzeyler aynı aileyi paylaşır).</summary>
        public static SurfaceKind Family(SurfaceKind s)
        {
            switch (s)
            {
                case SurfaceKind.Metal:
                case SurfaceKind.Concrete:
                case SurfaceKind.Wood:
                case SurfaceKind.Dirt:
                case SurfaceKind.Water:
                case SurfaceKind.Snow:
                case SurfaceKind.Foliage:
                case SurfaceKind.Flesh:
                    return s;
                default:
                    return SurfaceKind.Concrete;
            }
        }

        public static int VariationCount(SurfaceKind s) => Variations;

        public static int VariantFor(uint seed, SurfaceKind s)
        {
            var n = VariationCount(s);
            var v = (int)(RicochetRules.Roll01(seed, 7) * n);
            return Math.Min(Math.Max(v, 0), n - 1);
        }

        /// <summary>0 yakın (filtresiz), 1 orta, 2 uzak.</summary>
        public static int DistanceBucket(float distance)
        {
            if (!(distance > 30f)) return 0;
            return distance <= 80f ? 1 : 2;
        }

        public static float BucketDistance(int bucket) => bucket <= 0 ? 0f : bucket == 1 ? 55f : 130f;

        /// <summary>Kovanın alçak geçiren kesimi (Hz); yakın kova filtresiz (0).</summary>
        public static float CutoffHz(int bucket)
        {
            if (bucket <= 0) return 0f;
            // Küçük darbe sesleri silah raporundan hızlı sönümlenir: etkin mesafe 3 kat.
            return AcousticsMath.CutoffHz(CaliberClass.Rifle, BucketDistance(bucket) * 3f);
        }

        public static float Pitch(uint seed) => 0.93f + 0.14f * RicochetRules.Roll01(seed, 11);
    }
}
