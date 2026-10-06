using System;

namespace Project.Infrastructure.Characters.WeaponHold
{
    /// <summary>
    /// Skaler hedefi geriden izleyen sönümlü yay (saf mantık). Nişan eğimi ani değişince silah gövdeyle birlikte değil
    /// bir an sonra oturur: ağırlık hissi. Kararlılık için adım 1/60 sn'lik alt adımlara bölünür.
    /// </summary>
    public sealed class WeaponSpring
    {
        private const float MaxSubStep = 1f / 60f;

        public float Value { get; private set; }
        public float Velocity { get; private set; }

        public void Reset(float value)
        {
            Value = value;
            Velocity = 0f;
        }

        /// <summary>Yayı hedefe doğru ilerletir; hz doğal frekans, damping 1 = kritik sönüm.</summary>
        public float Step(float dt, float target, float hz, float damping)
        {
            if (float.IsNaN(target) || float.IsInfinity(target) || dt <= 0f)
                return Value;

            dt = Math.Min(dt, 0.1f);
            hz = Math.Max(0.5f, hz);
            damping = Math.Max(0.1f, damping);
            var omega = 2f * (float)Math.PI * hz;
            var steps = (int)Math.Ceiling(dt / MaxSubStep);
            var h = dt / steps;
            for (var i = 0; i < steps; i++)
            {
                // Sönüm örtük (implicit) alınır: yüksek frekansta bile kararlı.
                Velocity = (Velocity + omega * omega * (target - Value) * h) / (1f + 2f * damping * omega * h);
                Value += Velocity * h;
            }

            return Value;
        }
    }
}
