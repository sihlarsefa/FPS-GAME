using UnityEngine;

namespace Project.Infrastructure.World
{
    public enum DoorState { Closed, Open, Ajar }

    /// <summary>Ahşap kapı saf mantığı: hedef açı, yumuşak yaylanma, hangi yöne açılacağı, çarpma eşiği. Test edilebilir.</summary>
    public static class WoodenDoorMath
    {
        public const float OpenAngle = 98f;
        public const float AjarAngle = 16f;
        public const float SlamOvershoot = 112f;
        public const float SprintSpeed = 4.6f;
        public const float SmoothTime = 0.22f;
        public const float SlamSmoothTime = 0.08f;
        public const float SettleEpsilon = 0.05f;

        /// <summary>Hedef açı (işaretli: sign * derece).</summary>
        public static float TargetAngle(DoorState state, float sign)
        {
            switch (state)
            {
                case DoorState.Open: return sign * OpenAngle;
                case DoorState.Ajar: return sign * AjarAngle;
                default: return 0f;
            }
        }

        /// <summary>Kapı, oyuncudan uzağa açılır: oyuncu +Z yüzündeyse kanat -Z tarafına döner. localZ = oyuncunun kapı yerelindeki Z'si.</summary>
        public static float OpenSign(float actorLocalZ, bool hingeOnRight)
        {
            var tipZ = actorLocalZ >= 0f ? -1f : 1f; // kanat ucunun gideceği Z yönü
            // +Y dönüşü: menteşe solda (kanat +X) ise uç -Z'ye, menteşe sağda (kanat -X) ise +Z'ye gider.
            return hingeOnRight ? tipZ : -tipZ;
        }

        public static bool IsSlam(float horizontalSpeed) => horizontalSpeed >= SprintSpeed;

        /// <summary>Kritik sönümlü yaylanma (SmoothDamp eşdeğeri). vel referansla taşınır.</summary>
        public static float Step(float current, float target, ref float vel, float smoothTime, float dt)
        {
            smoothTime = Mathf.Max(0.0001f, smoothTime);
            var omega = 2f / smoothTime;
            var x = omega * dt;
            var exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            var change = current - target;
            var temp = (vel + omega * change) * dt;
            vel = (vel - omega * temp) * exp;
            var result = target + (change + temp) * exp;
            if ((target - current > 0f) == (result > target))
            {
                result = target;
                vel = 0f;
            }

            return result;
        }

        public static bool Settled(float angle, float target, float vel) =>
            Mathf.Abs(angle - target) < SettleEpsilon && Mathf.Abs(vel) < 0.5f;

        /// <summary>Menteşe yerel X konumu (kapı merkezine göre): sağ → +width/2, sol → -width/2.</summary>
        public static float HingeX(float width, bool hingeOnRight) => hingeOnRight ? width * 0.5f : -width * 0.5f;

        /// <summary>Kanat merkezinin menteşeye göre yerel X ofseti (kanat menteşeden kapının içine doğru uzanır).</summary>
        public static float LeafOffsetX(float width, bool hingeOnRight) => hingeOnRight ? -width * 0.5f : width * 0.5f;
    }
}
