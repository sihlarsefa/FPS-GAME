namespace Project.Application.Services
{
    /// <summary>Kamera tepmesi (ayrı yay) ölçekleme matematiği; Unity bağımlılığı yok.</summary>
    public static class CameraKickMath
    {
        public const float BasePitchPerShot = 0.22f;
        public const float BaseYawPerShot = 0.10f;
        public const float AdsFactor = 0.55f;
        public const float MaxIntensity = 1.5f;

        /// <summary>Ayar şiddeti (0-1.5) ve nişan durumuna göre atış başına tepme (derece).</summary>
        public static float Scale(float intensity, bool aiming, float kickScale = 1f)
        {
            if (float.IsNaN(intensity) || intensity <= 0f || float.IsNaN(kickScale) || kickScale <= 0f)
                return 0f;
            if (intensity > MaxIntensity)
                intensity = MaxIntensity;
            return intensity * kickScale * (aiming ? AdsFactor : 1f);
        }

        /// <summary>Çıktı pitch/yaw darbesi; rastgele -1..1 yaw yönü verilir.</summary>
        public static void Shot(float intensity, bool aiming, float yawRandom, float kickScale, out float pitch, out float yaw)
        {
            var s = Scale(intensity, aiming, kickScale);
            pitch = BasePitchPerShot * s;
            if (yawRandom > 1f) yawRandom = 1f;
            if (yawRandom < -1f) yawRandom = -1f;
            yaw = BaseYawPerShot * s * yawRandom;
        }
    }
}
