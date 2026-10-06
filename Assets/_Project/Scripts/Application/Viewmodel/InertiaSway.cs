using System;
using Project.Application.Services;

namespace Project.Application.Viewmodel
{
    /// <summary>
    /// Fare ataleti sallantısı: bakış hızı hedef gecikmeyi (ViewmodelMotionMath.TurnLagTarget) belirler, silah bu
    /// hedefi kritik-altı yayla izler (hafif aşım = ağırlık hissi). Ağır silahta daha yumuşak yay. ADS'de azalır.
    /// </summary>
    public sealed class InertiaSway
    {
        private SpringAxis3 _yaw, _pitch, _roll;
        private float _stiffness = 130f;
        private float _damping = 0.62f;

        public float YawDeg => _yaw.Position;
        public float PitchDeg => _pitch.Position;
        public float RollDeg => _roll.Position;
        /// <summary>Hafif yan konum kayması (m): yaw gecikmesinin ~1 cm / 3 derece'si.</summary>
        public float OffsetX => -_yaw.Position * 0.0033f;
        public float OffsetY => -_pitch.Position * 0.0033f;

        /// <summary>Silah ağırlığına göre yay: ağır = düşük sertlik (daha uzun, yavaş salınım).</summary>
        public void SetWeight(float weightMul)
        {
            var w = weightMul < 0.5f ? 0.5f : (weightMul > 2f ? 2f : weightMul);
            _stiffness = 130f / w;
            _damping = 0.62f + 0.06f * (w - 1f);
        }

        /// <summary>
        /// yawDelta/pitchDelta: bu karedeki bakış değişimi (derece). scale: ADS sallanma çarpanı.
        /// </summary>
        public void Step(float yawDelta, float pitchDelta, float strafe, float scale, float dt)
        {
            if (dt <= 0f)
                return;
            var s = scale < 0f ? 0f : (scale > 1.5f ? 1.5f : scale);
            var yawRate = yawDelta / dt;
            var pitchRate = pitchDelta / dt;
            // Bakış sağa -> silah sola gecikir (negatif); yukarı -> aşağı gecikir.
            var ty = -ViewmodelMotionMath.TurnLagTarget(yawRate) * s;
            var tp = -ViewmodelMotionMath.TurnLagTarget(pitchRate) * s * 0.8f;
            var tr = ViewmodelMotionMath.StrafeRollTarget(strafe) * s + ty * 0.35f;
            _yaw.Step(ty, _stiffness, _damping, dt);
            _pitch.Step(tp, _stiffness, _damping, dt);
            _roll.Step(tr, _stiffness * 0.7f, _damping + 0.1f, dt);
        }

        public void Reset()
        {
            _yaw = default; _pitch = default; _roll = default;
        }
    }
}
