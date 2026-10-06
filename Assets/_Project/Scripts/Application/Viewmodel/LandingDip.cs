using System;

namespace Project.Application.Viewmodel
{
    /// <summary>
    /// İniş çökmesi: düşme hızına göre silah aşağı çöker (m) ve namlu öne eğilir, sonra yayla toparlanır.
    /// 3 m/s altı: tepki yok; 14 m/s üstü: azami. Kamera çökmesi ayrı (kamera payı CameraDipM).
    /// </summary>
    public sealed class LandingDip
    {
        public const float MinFallSpeed = 3f;
        public const float MaxFallSpeed = 14f;
        public const float MinDepthM = 0.012f;
        public const float MaxDepthM = 0.075f;
        public const float Stiffness = 380f;
        public const float DampingRatio = 0.62f;

        private SpringAxis3 _y, _pitch;

        /// <summary>Silah dikey ofseti (m, - aşağı).</summary>
        public float OffsetY => _y.Position;
        /// <summary>Namlu öne eğim (derece, + öne/aşağı).</summary>
        public float PitchDeg => _pitch.Position;
        /// <summary>Kameraya devredilen çökme (m, - aşağı): modelinkinin %40'ı.</summary>
        public float CameraDipM => _y.Position * 0.4f;

        /// <summary>Çökme derinliği (m, pozitif) - düşme hızından, 0 altında yok.</summary>
        public static float DepthFor(float fallSpeed)
        {
            if (float.IsNaN(fallSpeed) || fallSpeed < MinFallSpeed) return 0f;
            var t = (fallSpeed - MinFallSpeed) / (MaxFallSpeed - MinFallSpeed);
            t = t > 1f ? 1f : t;
            return MinDepthM + (MaxDepthM - MinDepthM) * t;
        }

        public void Land(float fallSpeed, float weightMul)
        {
            var d = DepthFor(fallSpeed);
            if (d <= 0f)
                return;
            var w = weightMul < 0.5f ? 0.5f : (weightMul > 2f ? 2f : weightMul);
            _y.Velocity -= SpringImpulse.VelocityForPeak(d * w, Stiffness, DampingRatio);
            _pitch.Velocity += SpringImpulse.VelocityForPeak(d * 90f * w, Stiffness, DampingRatio);
        }

        public void Step(float dt)
        {
            if (dt <= 0f)
                return;
            _y.Step(0f, Stiffness, DampingRatio, dt);
            _pitch.Step(0f, Stiffness, DampingRatio, dt);
        }

        public void Reset()
        {
            _y = default; _pitch = default;
        }
    }
}
