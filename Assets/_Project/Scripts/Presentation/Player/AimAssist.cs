using System;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Presentation.Player
{
    /// <summary>Nişan yardımı ve erişilebilirlik girdi dönüşümlerinin saf matematiği (test edilebilir).</summary>
    public static class AimAssistMath
    {
        public const float SlowdownConeDegrees = 5f;
        public const float MagnetConeDegrees = 3.5f;
        public const float MaxSlowdown = 0.55f;
        public const float MagnetGain = 4f;
        public const float MagnetMaxDegPerSecond = 14f;

        /// <summary>Ayar (0-100) → 0-1.</summary>
        public static float Strength01(int strength) => Mathf.Clamp01(strength / 100f);

        /// <summary>Gamepad nişan yardımı yalnızca gamepad girdisi varken ve güç &gt; 0 iken geçerlidir.</summary>
        public static bool IsActive(bool gamepadInput, int strength) => gamepadInput && strength > 0;

        /// <summary>Hedefe açı (derece) küçüldükçe bakış çarpanı 1'den (1 - MaxSlowdown*güç)'e düşer.</summary>
        public static float SlowdownFactor(float strength01, float angleDegrees)
        {
            if (strength01 <= 0f || angleDegrees < 0f || angleDegrees >= SlowdownConeDegrees)
                return 1f;

            var t = 1f - angleDegrees / SlowdownConeDegrees;
            return 1f - MaxSlowdown * Mathf.Clamp01(strength01) * t;
        }

        /// <summary>Mıknatıs: hata açısına (derece, işaretli) doğru bu karede dönülecek derece (aşmaz, üst sınırlı).</summary>
        public static float MagnetStep(float errorDegrees, float strength01, float dt)
        {
            if (strength01 <= 0f || dt <= 0f || Mathf.Abs(errorDegrees) > MagnetConeDegrees)
                return 0f;

            var cap = MagnetMaxDegPerSecond * strength01 * dt;
            var step = Mathf.Clamp(errorDegrees * MagnetGain * strength01 * dt, -cap, cap);
            return Mathf.Abs(step) > Mathf.Abs(errorDegrees) ? errorDegrees : step;
        }

        /// <summary>Aç/kapa tuşu: basılmanın yükselen kenarında durumu çevirir.</summary>
        public static bool Latch(bool latched, bool prevHeld, bool held) => held && !prevHeld ? !latched : latched;
    }

    /// <summary>
    /// Gamepad nişan yardımı: düşman vuruş kutusu yakınında bakış yavaşlar, ADS'de hafif mıknatıs hedefe çeker.
    /// Fare girdisinde (veya güç 0 iken) hiçbir şey yapmaz. PlayerWeaponHandler.ModifyLook üzerinden çağrılır.
    /// </summary>
    public sealed class AimAssist
    {
        private const float Range = 70f;
        private const float CastRadius = 0.7f;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        /// <summary>Son karedeki hedef açısı (derece); hedef yoksa -1. Hata ayıklama/test için.</summary>
        public float LastAngle { get; private set; } = -1f;

        /// <summary>Şu an gamepad ile bakılıyor mu (sağ çubuk hareketli, fare hareketsiz)?</summary>
        public static bool GamepadLookActive()
        {
            var pad = Gamepad.current;
            if (pad == null || pad.rightStick.ReadValue().sqrMagnitude < 0.04f)
                return false;

            var mouse = Mouse.current;
            return mouse == null || mouse.delta.ReadValue().sqrMagnitude < 0.01f;
        }

        public LookInputState Modify(LookInputState look, Transform cam, Combatant self, bool aiming,
            float sensitivity, bool invertY, int strengthSetting, bool gamepadInput, float dt)
        {
            LastAngle = -1f;
            if (cam == null || !AimAssistMath.IsActive(gamepadInput, strengthSetting))
                return look;

            var strength = AimAssistMath.Strength01(strengthSetting);
            if (!TryFindTarget(cam, self, out var yawErr, out var pitchErr, out var angle))
                return look;

            LastAngle = angle;
            var factor = AimAssistMath.SlowdownFactor(strength, angle);
            var pitch = look.PitchDelta * factor;
            var yaw = look.YawDelta * factor;

            if (aiming && sensitivity > 1e-5f)
            {
                // Derece → bakış birimi (kamera: derece = birim * hassasiyet; ters Y pitch'i çevirir).
                yaw += AimAssistMath.MagnetStep(yawErr, strength, dt) / sensitivity;
                pitch += AimAssistMath.MagnetStep(pitchErr, strength, dt) / sensitivity * (invertY ? -1f : 1f);
            }

            return new LookInputState(pitch, yaw);
        }

        private bool TryFindTarget(Transform cam, Combatant self, out float yawErr, out float pitchErr, out float angle)
        {
            yawErr = pitchErr = 0f;
            angle = float.MaxValue;
            var count = Physics.SphereCastNonAlloc(cam.position, CastRadius, cam.forward, _hits, Range,
                1 << GameLayers.Hitbox, QueryTriggerInteraction.Collide);
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var hitbox = _hits[i].collider != null ? _hits[i].collider.GetComponent<Hitbox>() : null;
                var owner = hitbox != null ? hitbox.Owner : null;
                if (owner == null || owner == self || !owner.IsAlive || (self != null && self.IsAllyOf(owner)))
                    continue;

                var center = hitbox.WorldCenter;
                var local = cam.InverseTransformPoint(center);
                if (local.z <= 0.5f)
                    continue;

                var yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                var pitch = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
                var a = Mathf.Sqrt(yaw * yaw + pitch * pitch);
                if (a >= angle)
                    continue;

                if (Physics.Linecast(cam.position, center, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
                    continue;

                angle = a;
                yawErr = yaw;
                pitchErr = pitch;
                found = true;
            }

            return found;
        }
    }

    /// <summary>Aç/kapa nişan ve aç/kapa eğilme için girdi dönüştürücü (ayar kapalıyken girdiyi olduğu gibi geçirir).</summary>
    public sealed class ToggleInputAdapter
    {
        /// <summary>Nişan basışı, nişan geçici olarak mümkün değilken (koşu/şarjör) bu kadar saniye tamponlanır.</summary>
        public const float AimBufferSeconds = 0.3f;

        private bool _adsLatched;
        private bool _adsPrev;
        private bool _crouchPrev;
        private float _pendingPress;

        public bool AdsLatched => _adsLatched;
        public bool PendingPress => _pendingPress > 0f;
        public bool AimHeldPrev => _adsPrev;

        public bool ResolveAim(bool held, bool toggleMode, bool canAim) => ResolveAim(held, toggleMode, canAim, 0f);

        /// <summary>Basış kenarı girdiden geliyorsa (kareler arası tık) kaçırılmaz.</summary>
        public bool ResolveAim(bool held, bool pressedEdge, bool toggleMode, bool canAim, float dt)
        {
            if (pressedEdge && !held)
                _adsPrev = false;
            return ResolveAim(held || pressedEdge, toggleMode, canAim, dt);
        }

        /// <summary>
        /// Nişan tuşu → etkin nişan isteği. Aç-Kapa: yükselen kenar çevirir; nişan imkânsızken basış kısa süre tamponlanır
        /// (koşudan çıkınca nişan alır), mandal ise iptal edilir.
        /// </summary>
        public bool ResolveAim(bool held, bool toggleMode, bool canAim, float dt)
        {
            var edge = held && !_adsPrev;
            _adsPrev = held;
            if (!toggleMode)
            {
                _adsLatched = false;
                _pendingPress = 0f;
                return held;
            }

            if (dt > 0f && _pendingPress > 0f)
                _pendingPress = Math.Max(0f, _pendingPress - dt);

            if (!canAim)
            {
                _adsLatched = false;
                if (edge)
                    _pendingPress = AimBufferSeconds;
                return false;
            }

            if (_pendingPress > 0f)
            {
                _pendingPress = 0f;
                edge = true;
                _adsLatched = false;
            }

            if (edge)
                _adsLatched = !_adsLatched;
            return _adsLatched;
        }

        /// <summary>Eğilme tutma tuşunu aç/kapa komutuna çevirir.</summary>
        public MovementInputState AdaptMovement(MovementInputState m, bool toggleMode)
        {
            var edge = m.Crouch && !_crouchPrev;
            _crouchPrev = m.Crouch;
            if (!toggleMode)
                return m;

            return new MovementInputState(m.Forward, m.Right, m.Sprint, m.Jump, false,
                m.CrouchToggle || edge, m.ProneToggle, m.LeanLeft, m.LeanRight);
        }

        public void ResetAim()
        {
            _adsLatched = false;
            _pendingPress = 0f;
            _adsPrev = false;
        }
    }
}
