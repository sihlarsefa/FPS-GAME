using System;

namespace Project.Infrastructure.Player
{
    /// <summary>Kamera hissi / kayma / tırmanma için saf matematik (Unity'siz; EditMode testlenebilir).</summary>
    public static class CameraFeelMath
    {
        public const float MantleMinHeight = 0.5f;
        public const float MantleMaxHeight = 1.2f;
        public const float SlideDuration = 0.75f;
        public const float SprintFovKickDegrees = 6f;

        /// <summary>Kayma hızı: başlangıç hızından süre boyunca sönerek çömelme hızına iner.</summary>
        public static float SlideSpeed(float startSpeed, float endSpeed, float elapsed, float duration)
        {
            if (duration <= 0f) return endSpeed;
            var t = Clamp01(elapsed / duration);
            var k = 1f - t;
            return endSpeed + (Math.Max(startSpeed, endSpeed) - endSpeed) * k * k;
        }

        /// <summary>Tırmanma süresi (s): yüksekliğe göre 0,35-0,6.</summary>
        public static float MantleDuration(float height)
        {
            var h = Clamp01((height - MantleMinHeight) / (MantleMaxHeight - MantleMinHeight));
            return 0.35f + 0.25f * h;
        }

        public static bool MantleHeightValid(float height)
        {
            return !float.IsNaN(height) && height >= MantleMinHeight && height <= MantleMaxHeight;
        }

        /// <summary>Tırmanma yükseliş oranı (0..1): ilk %65'te hızlı çıkar (ease-out).</summary>
        public static float MantleRise(float p)
        {
            var t = Clamp01(p / 0.65f);
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>Tırmanma ileri oranı (0..1): %35'ten sonra başlar (smoothstep).</summary>
        public static float MantleForward(float p)
        {
            var t = Clamp01((p - 0.35f) / 0.65f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Tırmanma kamera yayı: ortada öne eğilip toparlanan pitch (derece, + aşağı).</summary>
        public static float MantleCameraPitch(float p)
        {
            var t = Clamp01(p);
            return (float)Math.Sin(t * Math.PI) * 9f;
        }

        /// <summary>Koşu FOV artışı (derece): koşu oranı 0..1 → 0..6.</summary>
        public static float SprintFovOffset(float sprint01) => SprintFovKickDegrees * Clamp01(sprint01);

        /// <summary>İniş çökmesi (m): düşüş hızıyla orantılı, üst sınırlı.</summary>
        public static float LandingDip(float impactSpeed, float perSpeed, float max)
        {
            if (float.IsNaN(impactSpeed) || impactSpeed <= 0f) return 0f;
            return Math.Min(impactSpeed * Math.Max(0f, perSpeed), Math.Max(0f, max));
        }

        /// <summary>
        /// Yönlü darbe: kaynağın oyuncuya göre yerel yönünden (sağ x, ileri z) pitch/yaw/roll yumruğu.
        /// Önden vuruş kamerayı yukarı iter, sağdan vuruş sola savurur. Dönüş: (pitch, yaw, roll) derece, genlik 0..1 ölçekli.
        /// </summary>
        public static void DirectionalPunch(float localX, float localZ, float strength, out float pitch, out float yaw, out float roll)
        {
            var len = (float)Math.Sqrt(localX * localX + localZ * localZ);
            if (len < 1e-4f) { localX = 0f; localZ = 1f; len = 1f; }
            var x = localX / len;
            var z = localZ / len;
            var s = Clamp01(strength);
            pitch = z * 3.5f * s + 0.8f * s;
            yaw = -x * 2.5f * s;
            roll = -x * 3.5f * s;
        }

        /// <summary>Patlama sarsıntı şiddeti 0..1: mesafe ile azalır.</summary>
        public static float ExplosionIntensity(float distance, float radius)
        {
            if (radius <= 0f || distance >= radius) return 0f;
            var t = 1f - Clamp01(distance / radius);
            return t * t;
        }

        /// <summary>Boşta hafif nefes sallanması: (yaw, pitch) derece.</summary>
        public static void IdleSway(float time, float amount, out float pitch, out float yaw)
        {
            pitch = (float)Math.Sin(time * 0.9) * 0.12f * amount;
            yaw = (float)Math.Sin(time * 0.55 + 1.3) * 0.09f * amount;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
