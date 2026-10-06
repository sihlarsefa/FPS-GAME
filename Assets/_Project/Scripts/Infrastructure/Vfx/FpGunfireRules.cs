using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>Namlu ucu cihazı: alev şeklini belirler.</summary>
    public enum MuzzleDevice
    {
        None = 0,
        FlashHider = 1,
        Suppressor = 2
    }

    /// <summary>
    /// Birinci şahıs ateş cilası için saf kurallar (Unity nesnesi yok, EditMode test edilir):
    /// 1-2 karelik alev süresi, cihaz şekli, ısı birikimi, duman lifi, duvar ışık sızması, iz başlangıç ofseti.
    /// </summary>
    public static class FpGunfireRules
    {
        public const float WispLifetime = 2f;
        public const float HeatPerShot = 0.07f;
        public const float HeatDecayPerSecond = 0.3f;
        public const float HeatVisibleThreshold = 0.35f;
        public const float BurstGap = 0.18f;
        public const int BurstMinShots = 3;
        public const float TracerMinCameraDistance = 0.7f;

        /// <summary>Alev ışığı/kartı ömrü: yüksek FPS'te 1, normalde ~2 kare (sn).</summary>
        public static float FlashDuration(float frameTime, MuzzleDevice device)
        {
            var frames = device == MuzzleDevice.Suppressor ? 1f : 2f;
            var ft = Mathf.Clamp(float.IsNaN(frameTime) ? 1f / 60f : frameTime, 1f / 240f, 1f / 20f);
            return Mathf.Clamp(frames * ft, 0.008f, 0.04f);
        }

        /// <summary>Cihaza göre alev yaprak sayısı: yivli kesici yıldız (5), susturucu yok (0), yoksa 3.</summary>
        public static int FlashPetals(MuzzleDevice device)
        {
            return device == MuzzleDevice.FlashHider ? 5 : device == MuzzleDevice.Suppressor ? 0 : 3;
        }

        /// <summary>Alev ölçeği çarpanı: susturucuda çok küçük, flash hider'da biraz yanal-dar.</summary>
        public static float FlashScale(MuzzleDevice device)
        {
            return device == MuzzleDevice.Suppressor ? 0.35f : device == MuzzleDevice.FlashHider ? 0.85f : 1f;
        }

        /// <summary>Susturucuda alev yerine kısa bir duman üfürmesi (ölçek); diğerlerinde 0.</summary>
        public static float SuppressorPuffScale(MuzzleDevice device)
        {
            return device == MuzzleDevice.Suppressor ? 0.7f : 0f;
        }

        /// <summary>Isı birikimi: atışta artar, zamanla söner. 0..1.</summary>
        public static float Heat(float heat, int shots, float deltaTime)
        {
            if (float.IsNaN(heat)) heat = 0f;
            heat += Mathf.Max(0, shots) * HeatPerShot;
            heat -= Mathf.Max(0f, deltaTime) * HeatDecayPerSecond;
            return Mathf.Clamp01(heat);
        }

        /// <summary>Isı sisi alfa'sı: eşik altında 0, üstünde yumuşak artar. Düşük kademede hep 0.</summary>
        public static float ShimmerAlpha(float heat, VfxTier tier)
        {
            if (tier == VfxTier.Low) return 0f;
            var t = Mathf.InverseLerp(HeatVisibleThreshold, 1f, heat);
            var a = t * t * (3f - 2f * t) * 0.16f;
            return tier == VfxTier.Medium ? a * 0.7f : a;
        }

        /// <summary>Seri bitti mi: yeterli atış sonrası boşluk geçti.</summary>
        public static bool BurstEnded(int burstShots, float timeSinceLastShot)
        {
            return burstShots >= BurstMinShots && timeSinceLastShot >= BurstGap;
        }

        /// <summary>Eşzamanlı duman lifi sınırı.</summary>
        public static int WispCap(VfxTier tier)
        {
            return tier == VfxTier.High ? 4 : tier == VfxTier.Medium ? 2 : 0;
        }

        /// <summary>Parlayan kovan işareti sınırı.</summary>
        public static int GlintCap(VfxTier tier)
        {
            return tier == VfxTier.High ? 6 : tier == VfxTier.Medium ? 3 : 0;
        }

        /// <summary>Lif ilerleme 0..1 için alfa: hızlı belirir, uzun söner.</summary>
        public static float WispAlpha(float t)
        {
            t = Mathf.Clamp01(t);
            var fadeIn = Mathf.Clamp01(t / 0.1f);
            return fadeIn * (1f - t) * (1f - t) * 0.32f;
        }

        /// <summary>Duvara yakın ateşte ışık sızması şiddeti 0..1 (0.3 m'de en yüksek, 4 m'de sıfır).</summary>
        public static float WallSpill(float wallDistance)
        {
            if (float.IsNaN(wallDistance) || wallDistance < 0f) return 0f;
            return 1f - Mathf.Clamp01((wallDistance - 0.3f) / 3.7f);
        }

        /// <summary>
        /// İz başlangıcı kameranın içinde doğmasın: kameraya min mesafeden yakınsa başlangıç ileri itilir
        /// (hedefi geçmez).
        /// </summary>
        public static Vector3 TracerStart(Vector3 from, Vector3 to, Vector3 cameraPosition, float minDistance)
        {
            var delta = to - from;
            var len = delta.magnitude;
            if (len < 1e-4f) return from;
            var dir = delta / len;
            var along = Vector3.Dot(cameraPosition - from, dir);
            var closest = from + dir * Mathf.Clamp(along, 0f, len);
            if ((closest - cameraPosition).sqrMagnitude < minDistance * minDistance
                || (from - cameraPosition).sqrMagnitude < minDistance * minDistance)
            {
                var push = Mathf.Max(0f, Vector3.Dot(cameraPosition - from, dir)) + minDistance;
                push = Mathf.Min(push, len * 0.9f);
                return from + dir * push;
            }
            return from;
        }

        /// <summary>Kovan ışıltısı konumu (balistik yay): p0 + v t + g t²/2.</summary>
        public static Vector3 GlintPosition(Vector3 p0, Vector3 v, float t)
        {
            return p0 + v * t + 0.5f * t * t * Physics.gravity;
        }

        /// <summary>Kovan ışıltısı parlaklığı: dönerek yanıp söner, ömür sonunda söner.</summary>
        public static float GlintBrightness(float t, float life, float spin)
        {
            if (life <= 0f) return 0f;
            var k = Mathf.Clamp01(t / life);
            var s = Mathf.Max(0f, Mathf.Sin(t * spin));
            return s * s * s * (1f - k);
        }
    }
}
