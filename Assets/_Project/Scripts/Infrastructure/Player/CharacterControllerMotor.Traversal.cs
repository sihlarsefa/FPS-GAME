using System;
using UnityEngine;
using Project.Application.Movement;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Config;

namespace Project.Infrastructure.Player
{
    /// <summary>Hareket hissi: koşudan çömelince kayma (momentum) ve ≤1,2 m engellerin üzerinden tırmanma (mantle/vault).</summary>
    public sealed partial class CharacterControllerMotor
    {
        private const float SlideMinSpeed = 5.2f;
        private const float MantleReach = 0.75f;

        private bool _sliding;
        private float _slideTime;
        private float _slideStartSpeed;
        private Vector3 _slideDir;

        private bool _mantling;
        private float _mantleTime;
        private float _mantleDuration;
        private float _mantleHeight;
        private Vector3 _mantleStart;
        private Vector3 _mantleTarget;
        private int _mantleStall;
        private float _slideCooldown;
        private float _mantleExitSpeed = 1.5f;

        /// <summary>Koşudan çömelme sonrası kayma sürüyor mu.</summary>
        public bool IsSliding => _sliding;
        public float SlideProgress => _sliding ? _slideModel.Progress : 0f;
        public bool IsMantling => _mantling;
        /// <summary>Tırmanma ilerlemesi 0..1.</summary>
        public float MantleProgress => _mantling ? Mathf.Clamp01(_mantleTime / Mathf.Max(0.01f, _mantleDuration)) : 0f;
        public float MantleHeight => _mantleHeight;

        /// <summary>Kayma başladı (kamera efekti / ses için).</summary>
        public event Action SlideStarted;
        /// <summary>Tırmanma başladı; parametre engel yüksekliği (m).</summary>
        public event Action<float> MantleStarted;

        private void BeginSlideIfNeeded(Stance previous, bool wasSprinting)
        {
            if (_sliding || !wasSprinting || previous != Stance.Standing || _stance != Stance.Crouching || !_grounded)
                return;

            var speed = HorizontalSpeed;
            if (speed < SlideMinSpeed)
                return;

            // Kayma sınırlı: stamina maliyeti + bekleme (kayma spamı yok).
            if (!MovementRules.CanSlide(_stamina.Current, _slideCooldown) || _stamina.Exhausted)
                return;

            var dir = new Vector3(_planarVelocity.x, 0f, _planarVelocity.z);
            if (dir.sqrMagnitude < 0.01f)
                return;

            _slideDir = dir.normalized;
            _slideModel.Begin(Mathf.Max(speed, config.sprintSpeed), config.sprintSpeed, MovementRules.SlideMaxSpeedFactor,
                MovementRules.LoadSpeedFactor(_burden));
            _slideCooldown = MovementRules.SlideCooldownSeconds;
            SpendStamina(MovementRules.SlideStaminaCost);
            _slideTime = 0f;
            _sliding = true;
            SlideStarted?.Invoke();
        }

        /// <summary>Kayma sürerken yatay hızı sürer; biterse false döner.</summary>
        private void TickSlide(float dt, MovementInputState input)
        {
            if (!_sliding)
                return;

            _slideTime += dt;
            var downhill = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
            var slopeDeg = Vector3.Angle(_groundNormal, Vector3.up);
            var dot = downhill.sqrMagnitude > 0.0001f ? Vector3.Dot(_slideDir, downhill.normalized) : 0f;
            var running = _slideModel.Tick(dt, slopeDeg, dot);
            if (!_grounded || _stance != Stance.Crouching || !running)
            {
                // Kayma bitti: zıplama momentumu korunur, silah kayma çıkış süresiyle kalkar.
                if (_slideModel.Active)
                    _slideModel.Cancel();
                _sliding = false;
                NoteWeaponExit(SprintExitKind.Slide);
                return;
            }

            // Hafif yön düzeltmesi: hız arttıkça direksiyon azalır.
            var steer = transform.right * Sanitize(input.Right) * SlideModel.SteerRate(_slideModel.Speed, config.sprintSpeed);
            _slideDir = (_slideDir + steer * dt).normalized;
            var speed = Mathf.Max(_slideModel.Speed, config.SpeedFor(Stance.Crouching));
            _planarVelocity = _slideDir * speed * _speedMultiplier;
        }

        private bool ProbeMantle(CharacterController controller, out Vector3 target, out float height)
        {
            target = default;
            height = 0f;
            if (_stance != Stance.Standing || _currentHeight < config.standingHeight - 0.08f || _onSteepSlope)
                return false;

            var fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                return false;
            fwd.Normalize();

            var feet = transform.position;
            var r = controller.radius;
            var kneeOrigin = feet + Vector3.up * (config.stepOffset + 0.12f) - fwd * 0.05f;
            if (!PlayerPhysicsQueries.SphereCastOther(kneeOrigin, 0.06f, fwd, MantleReach, GameLayers.MovementBlockMask, transform, out var wall))
                return false;
            if (Mathf.Abs(wall.normal.y) > 0.35f)
                return false;

            // Üst yüzeyi bul: duvarın biraz ilerisinden aşağı ışın.
            var probe = new Vector3(wall.point.x, feet.y + CameraFeelMath.MantleMaxHeight + 0.35f, wall.point.z) + fwd * (r + 0.2f);
            if (!PlayerPhysicsQueries.SphereCastOther(probe, 0.06f, Vector3.down, CameraFeelMath.MantleMaxHeight + 0.35f - 0.2f,
                    GameLayers.MovementBlockMask, transform, out var top))
                return false;
            if (top.normal.y < 0.7f)
                return false;

            height = top.point.y - feet.y;
            if (!CameraFeelMath.MantleHeightValid(height))
                return false;

            var stand = new Vector3(top.point.x, top.point.y + controller.skinWidth + 0.02f, top.point.z);
            var cr = r * 0.92f;
            var capBottom = stand + Vector3.up * (cr + 0.02f);
            var capTop = stand + Vector3.up * (config.standingHeight - cr);
            if (PlayerPhysicsQueries.OverlapsOther(capBottom, capTop, cr, GameLayers.MovementBlockMask, transform))
                return false;

            // Yükselme yolu (aynı xz'de ayak yükseltilmiş) açık olmalı.
            var upBottom = feet + Vector3.up * (height + cr + 0.04f);
            var upTop = feet + Vector3.up * (height + config.standingHeight - cr);
            if (PlayerPhysicsQueries.OverlapsOther(upBottom, upTop, cr, GameLayers.MovementBlockMask, transform))
                return false;

            target = stand;
            return true;
        }

        private bool TryStartMantle(CharacterController controller)
        {
            if (_mantling || !ProbeMantle(controller, out var target, out var height))
                return false;

            var plan = MantleRules.Plan(height, HorizontalSpeed, config.sprintSpeed, _burden, _stamina.Exhausted);
            if (!_stamina.CanAfford(plan.StaminaCost))
                return false;

            SpendStamina(plan.StaminaCost);
            _mantleExitSpeed = plan.ExitSpeed;
            NoteWeaponExit(MantleRules.WeaponExitKind(plan.Kind));
            _mantling = true;
            _sliding = false;
            _mantleStart = transform.position;
            _mantleTarget = target;
            _mantleHeight = height;
            _mantleDuration = plan.Duration;
            _mantleTime = 0f;
            _mantleStall = 0;
            _planarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _jumpBufferTimer = 0f;
            _jumpedSinceGrounded = true;
            MantleStarted?.Invoke(height);
            return true;
        }

        /// <summary>Tırmanma adımı: hedef yolu CharacterController.Move ile izler (çarpışma temiz); takılırsa iptal.</summary>
        private void TickMantle(CharacterController controller, float dt)
        {
            _mantleTime += dt;
            var p = Mathf.Clamp01(_mantleTime / Mathf.Max(0.01f, _mantleDuration));
            var rise = CameraFeelMath.MantleRise(p);
            var fwdT = CameraFeelMath.MantleForward(p);
            var flat = Vector3.Lerp(_mantleStart, _mantleTarget, fwdT);
            var desired = new Vector3(flat.x, Mathf.Lerp(_mantleStart.y, _mantleTarget.y, rise), flat.z);

            var before = transform.position;
            var delta = desired - before;
            controller.Move(delta);
            var moved = transform.position - before;
            _velocity = moved / dt;
            _speedNormalized = Mathf.Clamp01(HorizontalSpeed / Mathf.Max(0.01f, config.sprintSpeed));

            if (delta.sqrMagnitude > 1e-4f && moved.sqrMagnitude < delta.sqrMagnitude * 0.25f)
                _mantleStall++;
            else
                _mantleStall = 0;

            if (_mantleStall >= 3)
            {
                _mantling = false;
                _verticalVelocity = 0f;
                return;
            }

            if (p >= 1f)
            {
                _mantling = false;
                var fwd = transform.forward;
                fwd.y = 0f;
                _planarVelocity = fwd.normalized * _mantleExitSpeed;
                _verticalVelocity = -GroundStickSpeed;
                _airTime = 0f;
                _timeSinceGrounded = 0f;
                _grounded = true;
            }
        }
    }
}
