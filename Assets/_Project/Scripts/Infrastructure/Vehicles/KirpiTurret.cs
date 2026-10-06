using System;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.Vehicles
{
    /// <summary>
    /// Kirpi tavan 7.62 mm makineli tüfek taretti. Mermi BallisticsSystem üzerinden gider; hasar/ses PMT-76 tanımıyla.
    /// Oyuncu nişancı koltuğunda (tuş 2) ateş eder; bakış yönüne göre tareti döndürür.
    /// </summary>
    public sealed class KirpiTurret : MonoBehaviour
    {
        public const int BeltSize = 200;
        public const float ReloadSeconds = 4f;
        public const float MinPitch = -8f;
        public const float MaxPitch = 65f;
        public const float SpreadDegrees = 0.9f;

        private Transform _yaw;
        private Transform _pitch;
        private Transform _muzzle;
        private WeaponDefinitionData _definition;
        private float _nextShot;
        private float _reloadUntil;
        private int _belt = BeltSize;
        private float _spread = SpreadDegrees;

        public Transform GunnerView { get; private set; }
        public bool PlayerGunner { get; set; }
        public int Ammo { get; private set; } = BeltSize;
        public bool IsReloading => Time.time < _reloadUntil;

        internal void Setup(KirpiModelBuilder.Result model, VehicleConfig config = null)
        {
            if (config != null)
            {
                _belt = config.TurretBelt;
                _spread = config.TurretSpread;
                Ammo = _belt;
            }

            if (model == null)
                return;
            _yaw = model.TurretYaw;
            _pitch = model.TurretPitch;
            _muzzle = model.TurretMuzzle;
            GunnerView = model.GunnerView;
            WeaponCatalog.TryGet(WeaponIds.Pmt76, out _definition);
        }

        private float _yawDeg;
        private float _pitchDeg;
        private int _lastAimFrame = -1;
        private float _lastAimTime;

        /// <summary>Anlık dönüş hızı (derece/sn): motor uğultusu ve gösterge için.</summary>
        public float YawRate { get; private set; }
        public float PitchRate { get; private set; }
        public float WhineLevel => VehicleFeelMath.WhineLevel(YawRate, PitchRate);
        public Transform YawPivot => _yaw;

        /// <summary>Tareti dünya nişan noktasına çevirir; yaw/pitch hız sınırlıdır (ağır taret hissi).</summary>
        public void AimAt(Vector3 worldPoint)
        {
            if (_yaw == null || _pitch == null)
                return;
            var parent = _yaw.parent;
            if (parent == null)
                return;
            var local = parent.InverseTransformPoint(worldPoint) - _yaw.localPosition;
            var yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            var flat = new Vector2(local.x, local.z).magnitude;
            var pitch = ClampPitch(-Mathf.Atan2(local.y - _pitch.localPosition.y, Mathf.Max(flat, 0.1f)) * Mathf.Rad2Deg);

            // Aynı karede (AimAt + TryFire) ikinci çağrı hızı iki kez saymasın.
            if (_lastAimFrame == Time.frameCount)
                return;
            var dt = _lastAimFrame < 0 ? 0.02f : Mathf.Clamp(Time.time - _lastAimTime, 0.001f, 0.1f);
            _lastAimFrame = Time.frameCount;
            _lastAimTime = Time.time;

            var newYaw = VehicleFeelMath.TraverseStep(_yawDeg, yaw, VehicleFeelMath.YawDegPerSec, dt);
            var newPitch = VehicleFeelMath.TraverseStep(_pitchDeg, pitch, VehicleFeelMath.PitchDegPerSec, dt);
            YawRate = VehicleFeelMath.DeltaAngle(_yawDeg, newYaw) / dt;
            PitchRate = VehicleFeelMath.DeltaAngle(_pitchDeg, newPitch) / dt;
            _yawDeg = newYaw;
            _pitchDeg = newPitch;
            _yaw.localRotation = Quaternion.Euler(0f, _yawDeg, 0f);
            _pitch.localRotation = Quaternion.Euler(_pitchDeg, 0f, 0f);
        }

        private void Update()
        {
            // Nişan verilmiyorsa uğultu sönsün.
            if (_lastAimFrame >= 0 && Time.time - _lastAimTime > 0.15f)
            {
                YawRate = 0f;
                PitchRate = 0f;
            }
        }

        /// <summary>Taretin gerçekten namluyu hedefe çevirdiği (hız sınırı bitti) mi: ateş doğruluğu için.</summary>
        public bool OnTarget(Vector3 worldPoint, float toleranceDeg = 2.5f)
        {
            if (_muzzle == null)
                return true;
            return Vector3.Angle(_muzzle.forward, worldPoint - _muzzle.position) <= toleranceDeg;
        }

        private static readonly RaycastHit[] AimHits = new RaycastHit[16];

        /// <summary>Nişan noktası: kameradan ışın, kendi aracının gövdesi/koltukları atlanarak (çatıya kilitlenmesin).</summary>
        public Vector3 ResolveAimPoint(Vector3 origin, Vector3 forward, float maxDistance)
        {
            var count = Physics.RaycastNonAlloc(origin, forward, AimHits, maxDistance, GameLayers.LineOfSightMask,
                QueryTriggerInteraction.Ignore);
            var best = float.MaxValue;
            var point = origin + forward * maxDistance;
            for (var i = 0; i < count; i++)
            {
                var h = AimHits[i];
                if (h.collider == null || h.collider.transform.IsChildOf(transform) || h.distance >= best)
                    continue;
                best = h.distance;
                point = h.point;
            }

            return point;
        }

        public static float ClampPitch(float pitchDownPositive)
            => Mathf.Clamp(pitchDownPositive, -MaxPitch, -MinPitch);

        /// <summary>Atış dener. Başarılıysa true; mermi/yeniden doldurma/ateş hızı sınırı uygular.</summary>
        public bool TryFire(Combatant shooter, Vector3 aimPoint)
        {
            if (_definition == null || _muzzle == null || shooter == null || !shooter.IsAlive)
                return false;

            var now = Time.time;
            if (now < _reloadUntil || now < _nextShot)
                return false;

            if (Ammo <= 0)
            {
                _reloadUntil = now + ReloadSeconds;
                Ammo = _belt;
                return false;
            }

            AimAt(aimPoint);
            var muzzle = _muzzle.position;
            // Namlu hedefe henüz dönmediyse mermi namlunun gerçek yönüne gider (taret dönüş hız sınırı).
            var dir = OnTarget(aimPoint, 6f) ? aimPoint - muzzle : _muzzle.forward;
            if (dir.sqrMagnitude < 0.01f)
                dir = _muzzle.forward;
            dir = BallisticsSystem.ApplySpread(dir.normalized, _spread);

            var ballistics = BallisticsSystem.Instance;
            if (ballistics == null)
                return false;

            Ammo--;
            _nextShot = now + _definition.FireIntervalSeconds;
            try
            {
                ballistics.SpawnProjectile(shooter.Id, _definition, muzzle, dir, muzzle, true, transform);
                GameAudio.PlayGunshot(_definition, muzzle, shooter.IsLocalPlayer);
                GameVfx.MuzzleFlash(muzzle, dir, 1.1f);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            return true;
        }

        public void ReloadNow()
        {
            if (Ammo >= _belt || IsReloading)
                return;
            _reloadUntil = Time.time + ReloadSeconds;
            Ammo = _belt;
        }
    }
}
