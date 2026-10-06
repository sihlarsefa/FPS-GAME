using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Yeni atılabilirlerin (flaş, molotof, aldatma) ve atış yayı önizlemesinin saf kuralları. Unity sahnesi gerektirmez;
    /// EditMode testleri doğrudan çağırır.
    /// </summary>
    public static class ThrowableRules
    {
        // ---- Flaş
        public const float FlashFuseSeconds = 1.6f;
        public const float FlashRadius = 16f;
        public const float FlashMaxBlindSeconds = 5.5f;
        public const float FlashMinIntensity = 0.04f;
        /// <summary>Tam beyaz ekranın toplam körlüğe oranı (kalanı yumuşak sönme).</summary>
        public const float FlashWhiteHoldFraction = 0.4f;

        // ---- Molotof
        public const float MolotovFuseSeconds = 6f; // yedek: çarpmazsa 6 sn sonra kırılır
        public const float MolotovShatterMinSpeed = 2.5f;
        public const float FireRadius = 4.5f;
        public const float FireDurationSeconds = 9f;
        public const float FireTickSeconds = 0.5f;
        public const float FireTickDamage = 6f;
        public const float FireEdgeDamageFactor = 0.5f;
        public const int FireBurnTicksAfterLeaving = 3;
        public const float FireVerticalReach = 2.2f;
        public const float FireMaxSlopeSpread = 0.45f;
        public const float FireRainThreshold = 0.75f;
        public const string MolotovSourceId = "grenade_molotov";

        // ---- Aldatma
        public const float DecoyFuseSeconds = 1.2f;
        public const float DecoyDurationSeconds = 14f;
        public const float DecoyLoudness = 1.2f;
        public const float DecoyShotSpacing = 0.085f;

        // ---- Atış yayı (PlayerWeaponHandler ile aynı sabitler)
        public const float PlayerThrowSpeed = 17f;
        public const float PlayerThrowUpBoost = 3.2f;
        public const float ThrowLinearDamping = 0.05f;

        public static float DefaultFuse(ThrowableKind kind)
        {
            switch (kind)
            {
                case ThrowableKind.Smoke: return ThrowableProjectile.SmokeFuseSeconds;
                case ThrowableKind.Flash: return FlashFuseSeconds;
                case ThrowableKind.Molotov: return MolotovFuseSeconds;
                case ThrowableKind.Decoy: return DecoyFuseSeconds;
                default: return ThrowableProjectile.FragFuseSeconds;
            }
        }

        public static string ItemIdFor(ThrowableKind kind)
        {
            switch (kind)
            {
                case ThrowableKind.Smoke: return ItemIds.SmokeGrenade;
                case ThrowableKind.Flash: return ItemIds.FlashGrenade;
                case ThrowableKind.Molotov: return ItemIds.MolotovGrenade;
                case ThrowableKind.Decoy: return ItemIds.DecoyGrenade;
                default: return ItemIds.FragGrenade;
            }
        }

        // ------------------------------------------------------------------ flaş

        /// <summary>
        /// Flaş etkisi 0..1. Mesafe (yumuşak düşüş) × bakış açısı (bombaya doğrudan bakış 1, arkaya dönük ~0,12).
        /// Görüş hattı kapalıysa 0.
        /// </summary>
        public static float FlashIntensity(float viewAngleDeg, float distance, float radius, bool lineOfSight)
        {
            if (!lineOfSight || radius <= 0.01f || distance >= radius)
                return 0f;

            var d = Mathf.Clamp01(1f - Mathf.Max(0f, distance) / radius);
            d = d * d * (3f - 2f * d);
            var a = Mathf.Clamp01(Mathf.Abs(viewAngleDeg) / 180f);
            var angle = 1f - 0.88f * a * a * (3f - 2f * a);
            var result = Mathf.Clamp01(d * angle * 1.15f);
            return result < FlashMinIntensity ? 0f : result;
        }

        /// <summary>Toplam körlük süresi (sn).</summary>
        public static float FlashBlindSeconds(float intensity)
        {
            if (intensity < FlashMinIntensity)
                return 0f;
            return Mathf.Lerp(0.6f, FlashMaxBlindSeconds, Mathf.Clamp01(intensity));
        }

        /// <summary>Geçen süreye göre ekranı kaplayan beyaz alfa: ilk kısım tam, sonra yumuşak sönme.</summary>
        public static float FlashAlpha(float elapsed, float blindSeconds, float intensity)
        {
            if (blindSeconds <= 0f || elapsed < 0f || elapsed >= blindSeconds)
                return 0f;
            var hold = blindSeconds * FlashWhiteHoldFraction;
            var peak = Mathf.Clamp01(intensity * 1.1f);
            if (elapsed <= hold)
                return peak;
            var t = (elapsed - hold) / Mathf.Max(0.01f, blindSeconds - hold);
            return peak * (1f - t * t * (3f - 2f * t));
        }

        // ------------------------------------------------------------------ molotof

        /// <summary>Eğimde yangın alanı yarıçapı: eğim arttıkça en fazla %45 yayılır.</summary>
        public static float FireRadiusOnSlope(float baseRadius, float slopeDegrees)
        {
            var s = Mathf.Clamp01(Mathf.Abs(slopeDegrees) / 45f);
            return baseRadius * (1f + FireMaxSlopeSpread * s);
        }

        /// <summary>Alan merkezinden uzaklığa göre tik hasarı: merkez tam, kenar yarısı; dışarısı 0.</summary>
        public static float FireTickDamageAt(float horizontalDistance, float radius)
        {
            if (radius <= 0.01f || horizontalDistance > radius)
                return 0f;
            var t = Mathf.Clamp01(horizontalDistance / radius);
            return Mathf.Lerp(FireTickDamage, FireTickDamage * FireEdgeDamageFactor, t);
        }

        /// <summary>Kalan yangın süresinin yağmurla hızlanan azalışı: yağmur 0 → 1x, 1 → 5x.</summary>
        public static float FireDecayRate(float rain01) => 1f + 4f * Mathf.Clamp01(rain01);

        public static bool RainExtinguishes(float rain01) => rain01 >= FireRainThreshold;

        /// <summary>Yanma durumunda alanı terk edince kalan tik sayısı; alandaysa tam yenilenir.</summary>
        public static int BurnTicksLeft(bool insideZone, int current)
        {
            if (insideZone)
                return FireBurnTicksAfterLeaving;
            return Math.Max(0, current - 1);
        }

        // ------------------------------------------------------------------ aldatma

        /// <summary>
        /// Sahte atış zamanlaması: seri içinde sabit aralık, seri bitince 0,6–2,2 sn boşluk.
        /// <paramref name="burstLength"/> seri başına atış sayısı (3-6).
        /// </summary>
        public static float DecoyNextDelay(int shotIndexInBurst, int burstLength, float random01)
        {
            if (shotIndexInBurst + 1 < burstLength)
                return DecoyShotSpacing;
            return Mathf.Lerp(0.6f, 2.2f, Mathf.Clamp01(random01));
        }

        public static int DecoyBurstLength(float random01) => 3 + Mathf.Clamp((int)(Mathf.Clamp01(random01) * 4f), 0, 3);

        // ------------------------------------------------------------------ atış yayı

        /// <summary>
        /// Basit balistik yörünge (Unity ile aynı: önce ivme, sonra konum; lineer sönüm). Noktaları listeye yazar.
        /// </summary>
        public static void SimulateArc(Vector3 start, Vector3 velocity, float step, int maxSteps, List<Vector3> points)
        {
            points.Clear();
            if (step <= 0f || maxSteps <= 0)
                return;
            var p = start;
            var v = velocity;
            points.Add(p);
            var damp = 1f / (1f + ThrowLinearDamping * step);
            for (var i = 0; i < maxSteps; i++)
            {
                v += Physics.gravity * step;
                v *= damp;
                p += v * step;
                points.Add(p);
            }
        }
    }
}
