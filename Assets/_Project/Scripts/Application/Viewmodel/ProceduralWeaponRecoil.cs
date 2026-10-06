using System;

namespace Project.Application.Viewmodel
{
    /// <summary>
    /// Prosedürel model tepmesi: geri vuruş (Z) + namlu kalkışı (pitch) + yan sapma (yaw) + roll, hepsi kare bağımsız
    /// salınımlı yaylar. Her atış istenen TEPE değerini verecek hızda darbe ekler (SpringImpulse); yaylar kendiliğinden
    /// toparlanır. Kalkışın bir kısmı kameraya devredilir (CameraShare): model ve kamera tepmesi ayrıdır.
    /// </summary>
    public sealed class ProceduralWeaponRecoil
    {
        public const float MaxPitchDeg = 14f;
        public const float MaxKickbackM = 0.12f;
        /// <summary>Seri atışta birikim penceresi (sn) ve çarpan sınırı.</summary>
        public const float BurstWindow = 0.28f;
        public const float MaxBurstGain = 1.6f;

        private SpringAxis3 _z, _pitch, _yaw, _roll;
        private float _sinceShot = 10f;
        private int _burst;
        private RecoilTuning _tune;

        public float KickbackM => _z.Position;
        public float PitchDeg => _pitch.Position;
        public float YawDeg => _yaw.Position;
        public float RollDeg => _roll.Position;
        public int BurstCount => _burst;

        public void SetTuning(RecoilTuning t) { _tune = t; }

        /// <summary>
        /// Kalkışın kamera/model bölünmesi. Toplam korunur: weapon + camera = total.
        /// </summary>
        public static void SplitRise(float totalDeg, float cameraShare, out float weaponDeg, out float cameraDeg)
        {
            var s = cameraShare < 0f ? 0f : (cameraShare > 1f ? 1f : cameraShare);
            cameraDeg = totalDeg * s;
            weaponDeg = totalDeg - cameraDeg;
        }

        public static float BurstGain(int burstCount)
        {
            var g = 1f + 0.08f * (burstCount < 0 ? 0 : burstCount);
            return g > MaxBurstGain ? MaxBurstGain : g;
        }

        /// <summary>
        /// Bir atış. side01/roll01 -1..1 (çağıran deterministik rastgele verir); ads0to1: nişan ağırlığı
        /// (ADS'de geri vuruş %35, kalkış %20 azalır). Kameraya gidecek kalkış/yan (derece) döner.
        /// </summary>
        public void Fire(float side01, float roll01, float ads0to1, out float cameraPitchDeg, out float cameraYawDeg)
        {
            if (_tune.Stiffness <= 0f)
                _tune = ViewmodelTuning.Recoil(ViewmodelWeaponClass.Rifle);

            _burst = _sinceShot < BurstWindow ? _burst + 1 : 0;
            _sinceShot = 0f;
            var gain = BurstGain(_burst);
            var a = ads0to1 < 0f ? 0f : (ads0to1 > 1f ? 1f : ads0to1);
            var kickMul = 1f - 0.35f * a;
            var riseMul = 1f - 0.20f * a;
            var s = Clamp(side01, -1f, 1f);
            var r = Clamp(roll01, -1f, 1f);

            SplitRise(_tune.RiseDeg * riseMul * gain, _tune.CameraShare, out var weaponRise, out cameraPitchDeg);
            cameraYawDeg = _tune.SideDeg * s * 0.5f;

            var k = _tune.Stiffness;
            var z = _tune.DampingRatio;
            _z.Velocity += SpringImpulse.VelocityForPeak(_tune.KickbackM * kickMul * gain, k, z);
            _pitch.Velocity += SpringImpulse.VelocityForPeak(weaponRise, k, z);
            _yaw.Velocity += SpringImpulse.VelocityForPeak(_tune.SideDeg * s * 0.5f, k, z);
            _roll.Velocity += SpringImpulse.VelocityForPeak(_tune.RollDeg * r, k, z);
            Clamp();
        }

        public void Step(float dt)
        {
            if (dt <= 0f)
                return;
            if (_tune.Stiffness <= 0f)
                _tune = ViewmodelTuning.Recoil(ViewmodelWeaponClass.Rifle);
            _sinceShot += dt;
            var k = _tune.Stiffness;
            var z = _tune.DampingRatio;
            _z.Step(0f, k, z, dt);
            _pitch.Step(0f, k, z, dt);
            // Yan ve roll biraz daha sönümlü: kalkıştan daha hızlı oturur.
            _yaw.Step(0f, k * 0.8f, z + 0.15f, dt);
            _roll.Step(0f, k * 0.8f, z + 0.15f, dt);
            Clamp();
        }

        public void Reset()
        {
            _z = default; _pitch = default; _yaw = default; _roll = default; _sinceShot = 10f; _burst = 0;
        }

        private void Clamp()
        {
            if (_pitch.Position > MaxPitchDeg) _pitch.Position = MaxPitchDeg;
            if (_pitch.Position < -MaxPitchDeg) _pitch.Position = -MaxPitchDeg;
            if (_z.Position > MaxKickbackM) _z.Position = MaxKickbackM;
            if (_z.Position < -MaxKickbackM) _z.Position = -MaxKickbackM;
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
