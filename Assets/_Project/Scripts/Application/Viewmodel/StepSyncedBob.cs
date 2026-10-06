using System;

namespace Project.Application.Viewmodel
{
    /// <summary>
    /// Adım senkronlu bob fazı: faz zamanla değil KAT EDİLEN MESAFEYLE ilerler (ayaklar yerde kayarken silah
    /// sallanmaz). Bir tam döngü = iki adım; yarım döngüde (faz = pi katı) ayak basma olayı üretir.
    /// Durunca genlik üstel söner, faz dondurulmaz (ani sıçrama yok).
    /// </summary>
    public sealed class StepSyncedBob
    {
        public const float TwoPi = 6.2831853f;

        private float _phase;
        private float _amp;
        private SpringAxis3 _heel;

        /// <summary>0..2pi bob fazı; ViewmodelMotionMath.SampleBob'a verilir.</summary>
        public float Phase => _phase;
        /// <summary>Yumuşatılmış genlik 0..1.</summary>
        public float Amplitude => _amp;
        /// <summary>Son Step'te geçilen ayak basma sayısı (ses/toz için).</summary>
        public int FootfallsThisStep { get; private set; }
        /// <summary>Ayak basma topuk darbesi (m, - aşağı); silah y konumuna eklenir.</summary>
        public float HeelStrike => _heel.Position;

        /// <summary>
        /// distance: bu karede kat edilen yatay mesafe (m). grounded değilse faz ilerlemez, genlik söner.
        /// </summary>
        public void Step(float distance, float strideLength, bool grounded, float dt)
        {
            FootfallsThisStep = 0;
            if (dt <= 0f)
                return;
            if (float.IsNaN(distance) || distance < 0f) distance = 0f;
            if (strideLength < 0.2f) strideLength = 0.2f;

            var speed = distance / dt;
            var target = grounded ? Clamp01(speed / 1.2f) : 0f;
            // Başlarken hızlı (0.08 sn), dururken yavaş (0.18 sn) takip.
            var rate = target > _amp ? 1f / 0.08f : 1f / 0.18f;
            _amp += (target - _amp) * ViewmodelFollow(rate, dt);

            if (grounded && distance > 0f)
            {
                var before = _phase;
                _phase += TwoPi * distance / strideLength;
                var steps = (int)Math.Floor(_phase / (TwoPi * 0.5f)) - (int)Math.Floor(before / (TwoPi * 0.5f));
                if (steps > 0)
                {
                    FootfallsThisStep = steps > 3 ? 3 : steps;
                    _heel.Velocity -= 0.12f * Clamp01(speed / 6f) * FootfallsThisStep;
                }

                if (_phase >= TwoPi * 8f)
                    _phase -= TwoPi * 8f;
            }

            _heel.Step(0f, 900f, 0.6f, dt);
        }

        public void Reset()
        {
            _phase = 0f; _amp = 0f; _heel = default; FootfallsThisStep = 0;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static float ViewmodelFollow(float rate, float dt)
            => Project.Application.Services.ViewmodelDynamics.ExpFollow(rate, dt);
    }

    /// <summary>Tek eksen yay (Services.SpringAxis'e ince sarmalayıcı; Viewmodel katmanı kendi adını taşır).</summary>
    public struct SpringAxis3
    {
        public float Position;
        public float Velocity;

        public void Step(float target, float stiffness, float dampingRatio, float dt)
        {
            Project.Application.Services.ViewmodelDynamics.StepSpring(ref Position, ref Velocity, target, stiffness, dampingRatio, dt);
        }
    }
}
