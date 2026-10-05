using System;
using Project.Core.Domain;

namespace Project.Application.Services
{
    /// <summary>
    /// Serbest düşüş + paraşüt fiziği (saf, sunucuda da çalışır). Yer yüksekliği AutoOpenHeight'ın altına inince
    /// paraşüt otomatik açılır. Step() bu karedeki yer değiştirmeyi döndürür (dünya ekseninde).
    /// Serbest düşüş: dikey ~-48 m/sn (ileri dalışta -62), yatay en fazla ~30 m/sn.
    /// Paraşüt: dikey ~-6 m/sn (ileri basınca -8, geri -4.5), yatay en fazla ~13 m/sn.
    /// Yere yükseklik LandingHeight'ın altına inince (ya da bu adım zemini geçecekse) otomatik iner: dikey yer
    /// değiştirme zemine kadar kırpılır ve durum Landed olur.
    /// </summary>
    public sealed class SkydiveSimulator
    {
        public const float AutoOpenHeight = 110f;
        public const float LandingHeight = 1.2f;

        public const float FreefallVerticalSpeed = 48f;
        public const float FreefallDiveVerticalSpeed = 62f;
        public const float FreefallMaxHorizontalSpeed = 30f;
        public const float FreefallBackwardSpeed = 12f;
        public const float FreefallStrafeSpeed = 20f;
        public const float FreefallHorizontalAcceleration = 14f;
        public const float FreefallVerticalAcceleration = 25f;

        public const float ParachuteVerticalSpeed = 6f;
        public const float ParachuteForwardVerticalSpeed = 8f;
        public const float ParachuteBackwardVerticalSpeed = 4.5f;
        public const float ParachuteMaxHorizontalSpeed = 13f;
        public const float ParachuteGlideSpeed = 4f;
        public const float ParachuteStrafeSpeed = 8f;
        public const float ParachuteHorizontalAcceleration = 8f;
        public const float ParachuteVerticalAcceleration = 35f;

        private const float DegToRad = (float)(Math.PI / 180.0);

        private DropState _state = DropState.InTransport;
        private Float3 _velocity;

        public DropState State => _state;
        public Float3 Velocity => _velocity;

        /// <summary>İnişteki dikey hız (m/sn, pozitif) — düşme hasarı/efekt için.</summary>
        public float LandingSpeed { get; private set; }

        public bool IsAirborne => _state == DropState.Freefall || _state == DropState.Parachute;

        /// <summary>Durum değiştiğinde (paraşüt açıldı, indi...) çağrılır.</summary>
        public event Action<DropState> StateChanged;

        public void BeginFreefall(Float3 initialVelocity)
        {
            _velocity = IsFinite(initialVelocity) ? initialVelocity : Float3.Zero;
            LandingSpeed = 0f;
            SetState(DropState.Freefall);
        }

        public void RequestOpenParachute()
        {
            if (_state == DropState.Freefall)
                SetState(DropState.Parachute);
        }

        /// <param name="deltaTime">adım süresi (sn)</param>
        /// <param name="steerForward">-1..1 ileri/geri</param>
        /// <param name="steerRight">-1..1 sağ/sol</param>
        /// <param name="headingYawDegrees">bakış yönü (Y ekseni, derece; 0 = +Z)</param>
        /// <param name="heightAboveGround">ayakların zeminden yüksekliği (bilinmiyorsa çok büyük bir değer)</param>
        public Float3 Step(float deltaTime, float steerForward, float steerRight, float headingYawDegrees, float heightAboveGround)
        {
            if (!IsAirborne || !(deltaTime > 0f) || float.IsInfinity(deltaTime))
                return Float3.Zero;

            steerForward = Clamp(steerForward, -1f, 1f);
            steerRight = Clamp(steerRight, -1f, 1f);
            if (float.IsNaN(headingYawDegrees) || float.IsInfinity(headingYawDegrees))
                headingYawDegrees = 0f;
            if (float.IsNaN(heightAboveGround))
                heightAboveGround = float.MaxValue;

            if (_state == DropState.Freefall && heightAboveGround <= AutoOpenHeight)
                SetState(DropState.Parachute);

            var yaw = headingYawDegrees * DegToRad;
            var sin = (float)Math.Sin(yaw);
            var cos = (float)Math.Cos(yaw);

            float forwardSpeed;
            float strafeSpeed;
            float maxHorizontal;
            float targetVertical;
            float horizontalAcceleration;
            float verticalAcceleration;

            if (_state == DropState.Freefall)
            {
                forwardSpeed = steerForward >= 0f ? steerForward * FreefallMaxHorizontalSpeed : steerForward * FreefallBackwardSpeed;
                strafeSpeed = steerRight * FreefallStrafeSpeed;
                maxHorizontal = FreefallMaxHorizontalSpeed;
                targetVertical = -(FreefallVerticalSpeed + (FreefallDiveVerticalSpeed - FreefallVerticalSpeed) * Math.Max(0f, steerForward));
                horizontalAcceleration = FreefallHorizontalAcceleration;
                verticalAcceleration = FreefallVerticalAcceleration;
            }
            else
            {
                forwardSpeed = steerForward >= 0f
                    ? ParachuteGlideSpeed + steerForward * (ParachuteMaxHorizontalSpeed - ParachuteGlideSpeed)
                    : ParachuteGlideSpeed * (1f + steerForward);
                strafeSpeed = steerRight * ParachuteStrafeSpeed;
                maxHorizontal = ParachuteMaxHorizontalSpeed;
                targetVertical = steerForward >= 0f
                    ? -(ParachuteVerticalSpeed + (ParachuteForwardVerticalSpeed - ParachuteVerticalSpeed) * steerForward)
                    : -(ParachuteVerticalSpeed + (ParachuteBackwardVerticalSpeed - ParachuteVerticalSpeed) * -steerForward);
                horizontalAcceleration = ParachuteHorizontalAcceleration;
                verticalAcceleration = ParachuteVerticalAcceleration;
            }

            // İleri = (sin, cos), sağ = (cos, -sin) — Unity sol el koordinatları, yaw 0 = +Z.
            var targetX = sin * forwardSpeed + cos * strafeSpeed;
            var targetZ = cos * forwardSpeed - sin * strafeSpeed;
            var targetMagnitude = (float)Math.Sqrt(targetX * targetX + targetZ * targetZ);
            if (targetMagnitude > maxHorizontal)
            {
                var k = maxHorizontal / targetMagnitude;
                targetX *= k;
                targetZ *= k;
            }

            var vx = _velocity.X;
            var vz = _velocity.Z;
            var dx = targetX - vx;
            var dz = targetZ - vz;
            var deltaMagnitude = (float)Math.Sqrt(dx * dx + dz * dz);
            var maxStep = horizontalAcceleration * deltaTime;

            // Mevcut hız tavanın çok üstündeyse (ör. araçtan atlama) daha hızlı yavaşla.
            var currentHorizontal = (float)Math.Sqrt(vx * vx + vz * vz);
            if (currentHorizontal > maxHorizontal * 1.25f)
                maxStep *= 2f;

            if (deltaMagnitude <= maxStep || deltaMagnitude < 1e-5f)
            {
                vx = targetX;
                vz = targetZ;
            }
            else
            {
                vx += dx / deltaMagnitude * maxStep;
                vz += dz / deltaMagnitude * maxStep;
            }

            var vy = MoveTowards(_velocity.Y, targetVertical, verticalAcceleration * deltaTime);
            _velocity = new Float3(vx, vy, vz);

            var displacement = _velocity * deltaTime;
            if (heightAboveGround <= LandingHeight || heightAboveGround + displacement.Y <= 0f)
            {
                displacement = new Float3(displacement.X, -Math.Max(0f, heightAboveGround), displacement.Z);
                Land();
            }

            return displacement;
        }

        public void Land()
        {
            if (_state == DropState.Landed)
                return;

            LandingSpeed = _velocity.Y < 0f ? -_velocity.Y : 0f;
            _velocity = Float3.Zero;
            SetState(DropState.Landed);
        }

        /// <summary>Araç içi duruma döner (yeniden kullanım için).</summary>
        public void Reset()
        {
            _velocity = Float3.Zero;
            LandingSpeed = 0f;
            SetState(DropState.InTransport);
        }

        private void SetState(DropState state)
        {
            if (_state == state)
                return;

            _state = state;
            StateChanged?.Invoke(state);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            var delta = target - current;
            if (Math.Abs(delta) <= maxDelta)
                return target;

            return current + Math.Sign(delta) * maxDelta;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value))
                return 0f;
            return value < min ? min : value > max ? max : value;
        }

        private static bool IsFinite(Float3 v) =>
            !float.IsNaN(v.X) && !float.IsNaN(v.Y) && !float.IsNaN(v.Z) &&
            !float.IsInfinity(v.X) && !float.IsInfinity(v.Y) && !float.IsInfinity(v.Z);
    }
}
