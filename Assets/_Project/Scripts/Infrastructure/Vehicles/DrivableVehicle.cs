using System;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Vehicles
{
    /// <summary>
    /// Sürülebilir Kirpi MRAP: raycast süspansiyon, binme/inme, hasar ve yol ezmesi.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DrivableVehicle : MonoBehaviour
    {
        public const float MaxHealth = 1200f; // Kirpi varsayılanı (geri uyumluluk); araç başına değer Config.MaxHealth
        public const float RoadkillSpeedKmh = 25f;

        public VehicleConfig Config { get; private set; } = VehicleConfig.Kirpi;
        private Vector3[] _wheelLocal;
        private float MaxSpeedKmh => Config.MaxSpeedKmh;
        private float SpringForce => Config.SpringForce;
        private float DamperForce => Config.DamperForce;
        private float SuspensionRest => Config.SuspensionRest;
        private float WheelRadius => Config.WheelRadius;
        private float DriveForce => Config.DriveForce;
        private float BrakeForce => Config.BrakeForce;
        private float SteerAngle => Config.SteerAngle;
        private float LateralGrip => Config.LateralGrip;
        private float AntiRoll => Config.AntiRoll;

        private Rigidbody _rb;
        private KirpiModelBuilder.Result _model;
        private AudioSource _engine;
        private ParticleSystem _dust;
        private readonly float[] _suspension = new float[4];
        private readonly float[] _wheelSpin = new float[4];
        private float _throttle;
        private float _steer;
        private bool _brake;
        private float _steerVisual;
        private bool _exploded;
        private float _roadkillCooldown;
        private Combatant _driver;
        private Combatant[] _passengers = new Combatant[9];

        public bool HasDriver => _driver != null;
        public Combatant Driver => _driver;
        public Transform DriverViewPoint => _model != null ? _model.DriverView : transform;
        public KirpiTurret Turret { get; private set; }
        public VehicleFeel Feel { get; private set; }

        /// <summary>Tekerlek süspansiyon sıkışması 0..1 (görsel yatış/sarsıntı için).</summary>
        internal float WheelCompression(int i) => i >= 0 && i < _suspension.Length ? _suspension[i] : 0f;
        internal Transform WheelVisual(int i) => _model?.WheelVisuals != null && i >= 0 && i < _model.WheelVisuals.Length ? _model.WheelVisuals[i] : null;
        internal bool WheelGrounded(int i) => i >= 0 && i < _suspension.Length && _suspension[i] > 0.001f;

        /// <summary>Oyuncunun şu an baktığı koltuk noktası: nişancıysa taret, değilse sürücü.</summary>
        public Transform ActiveViewPoint =>
            Turret != null && Turret.PlayerGunner && Turret.GunnerView != null ? Turret.GunnerView : DriverViewPoint;

        /// <summary>Bu savaşan araçta (sürücü ya da yolcu) mı? Kendi aracına mermi/hasar uygulanmasın diye.</summary>
        public bool IsOccupant(PlayerId id)
        {
            if (!id.IsValid)
                return false;
            if (_driver != null && _driver.Id == id)
                return true;
            for (var i = 0; i < _passengers.Length; i++)
                if (_passengers[i] != null && _passengers[i].Id == id)
                    return true;
            return false;
        }

        public Combatant GetPassenger(int index) => index >= 0 && index < _passengers.Length ? _passengers[index] : null;
        public int SeatCount => _passengers.Length;

        public float SpeedKmh { get; private set; }
        public float Health { get; private set; } = 1200f;

        public int PassengerCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _passengers.Length; i++)
                    if (_passengers[i] != null) n++;
                return n;
            }
        }

        public Transform GetPassengerSeat(int index)
        {
            if (_model == null || _model.PassengerSeats == null || index < 0 || index >= _model.PassengerSeats.Length)
                return transform;
            return _model.PassengerSeats[index];
        }

        public Transform GetPassengerView(int index)
        {
            if (_model == null || _model.PassengerViews == null || index < 0 || index >= _model.PassengerViews.Length)
                return DriverViewPoint;
            return _model.PassengerViews[index];
        }

        public static DrivableVehicle Spawn(Vector3 position, float yaw) => Spawn(position, yaw, VehicleConfig.Kirpi);

        public static DrivableVehicle Spawn(Vector3 position, float yaw, VehicleConfig config)
        {
            config ??= VehicleConfig.Kirpi;
            var go = new GameObject(config.ObjectName);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.layer = GameLayers.Vehicle;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = config.MassKg;
            rb.linearDamping = 0.15f;
            rb.angularDamping = 1.8f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.centerOfMass = config.CenterOfMass;

            var vehicle = go.AddComponent<DrivableVehicle>();
            vehicle._rb = rb;
            vehicle.Config = config;
            vehicle.Health = config.MaxHealth;
            vehicle._passengers = new Combatant[config.SeatCount];
            vehicle._wheelLocal = BuildWheelLocal(config);
            vehicle._model = config.Kind == VehicleKind.Cobra
                ? CobraModelBuilder.Build(go.transform)
                : KirpiModelBuilder.Build(go.transform);
            vehicle.SetupAudioAndDust();
            vehicle.Turret = go.AddComponent<KirpiTurret>();
            vehicle.Turret.Setup(vehicle._model, config);
            go.AddComponent<KirpiCrew>();
            vehicle.Feel = VehicleFeel.Attach(vehicle, vehicle._model?.Root != null ? vehicle._model.Root.transform : null,
                vehicle._model != null ? vehicle._model.GunnerView : null);
            VehicleRegistry.Register(vehicle);
            return vehicle;
        }

        private static Vector3[] BuildWheelLocal(VehicleConfig c) => new[]
        {
            new Vector3(-c.TrackX, c.WheelY, c.WheelBaseZ),
            new Vector3(c.TrackX, c.WheelY, c.WheelBaseZ),
            new Vector3(-c.TrackX, c.WheelY, -c.WheelBaseZ),
            new Vector3(c.TrackX, c.WheelY, -c.WheelBaseZ)
        };

        private void Awake()
        {
            _wheelLocal ??= BuildWheelLocal(Config);
            if (_rb == null)
                _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            VehicleRegistry.Register(this);
            try { Project.Infrastructure.World.GrassSystem.AddInteractor(transform, 2.5f); } catch (Exception) { /* yok sayılır */ }
        }

        private void OnDisable()
        {
            VehicleRegistry.Unregister(this);
            try { Project.Infrastructure.World.GrassSystem.RemoveInteractor(transform); } catch (Exception) { /* yok sayılır */ }
        }

        private void OnDestroy()
        {
            VehicleRegistry.Unregister(this);
            try { ExplosionSystem.Exploded -= OnNearbyExplosion; } catch { /* ignore */ }
        }

        private void Start()
        {
            try { ExplosionSystem.Exploded += OnNearbyExplosion; } catch { /* ignore */ }
        }

        public bool TryEnter(Combatant combatant)
        {
            if (combatant == null || !Project.Application.Services.DownedRules.CanBoardVehicle(combatant.IsAlive, combatant.IsDowned) || _exploded || Health <= 0f)
                return false;
            if (_driver != null)
                return false;

            _driver = combatant;
            if (Turret != null)
                Turret.PlayerGunner = false;
            combatant.IsTargetable = false;
            return true;
        }

        /// <summary>Yolcu koltuğuna binme (ateş edemez — Presentation engeller).</summary>
        public bool TryEnterPassenger(Combatant combatant, out int seatIndex)
        {
            seatIndex = -1;
            if (combatant == null || !Project.Application.Services.DownedRules.CanBoardVehicle(combatant.IsAlive, combatant.IsDowned) || _exploded || Health <= 0f)
                return false;

            for (var i = 0; i < _passengers.Length; i++)
            {
                if (_passengers[i] != null)
                    continue;
                _passengers[i] = combatant;
                combatant.IsTargetable = false;
                seatIndex = i;
                return true;
            }

            return false;
        }

        public void Exit()
        {
            if (_driver != null)
            {
                PlaceBeside(_driver);
                _driver.IsTargetable = true;
                _driver = null;
                if (Turret != null)
                    Turret.PlayerGunner = false;
            }

            _throttle = 0f;
            _steer = 0f;
            _brake = true;
        }

        public void ExitPassenger(int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= _passengers.Length)
                return;
            var c = _passengers[seatIndex];
            if (c == null)
                return;
            PlaceBeside(c);
            c.IsTargetable = true;
            _passengers[seatIndex] = null;
        }

        public void SetInput(float throttle, float steer, bool brake)
        {
            _throttle = Mathf.Clamp(throttle, -1f, 1f);
            _steer = Mathf.Clamp(steer, -1f, 1f);
            _brake = brake;
        }

        public void ApplyDamage(float amount, PlayerId attackerId = default)
        {
            if (_exploded || amount <= 0f)
                return;

            Health = Mathf.Max(0f, Health - amount);
            if (Health <= 0f)
                Explode(attackerId);
            RefreshCondition();
        }

        private Project.Application.Services.VehicleCondition _condition = Project.Application.Services.VehicleCondition.Clean;

        /// <summary>Can/yıkım durumuna göre temiz/kirli/yanmış görünüm (MaterialPropertyBlock; değişimde bir kez).</summary>
        private void RefreshCondition()
        {
            try
            {
                var max = Mathf.Max(1f, Config != null ? Config.MaxHealth : MaxHealth);
                var next = Project.Application.Services.VehicleSocketRules.ConditionFor(Health / max, _exploded || Health <= 0f);
                if (next == _condition)
                    return;
                _condition = next;
                if (_model != null && _model.Root != null)
                    Project.Infrastructure.Transport.VehicleSockets.ApplyCondition(_model.Root, next);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DrivableVehicle] Condition: " + e.Message);
            }
        }

        private void FixedUpdate()
        {
            if (_rb == null || _exploded)
                return;

            var dt = Time.fixedDeltaTime;
            SpeedKmh = _rb.linearVelocity.magnitude * 3.6f;
            _roadkillCooldown = Mathf.Max(0f, _roadkillCooldown - dt);
            _steerVisual = Mathf.MoveTowards(_steerVisual, _steer * SteerAngle, 120f * dt);

            var grounded = 0;
            for (var i = 0; i < 4; i++)
            {
                if (SimulateWheel(i, dt))
                    grounded++;
            }

            if (grounded >= 2)
            {
                ApplyAntiRoll(0, 1);
                ApplyAntiRoll(2, 3);
            }

            UpdateVisualWheels();
            UpdateEngineAudio();
            UpdateDust(grounded >= 2);
        }

        private bool SimulateWheel(int index, float dt)
        {
            var local = _wheelLocal[index];
            var origin = transform.TransformPoint(local + Vector3.up * 0.2f);
            var rayLen = SuspensionRest + WheelRadius + 0.2f;
            var hitGround = Physics.Raycast(origin, -transform.up, out var hit, rayLen, GameLayers.GroundMask,
                QueryTriggerInteraction.Ignore);

            if (!hitGround)
            {
                _suspension[index] = 0f;
                return false;
            }

            var compression = 1f - Mathf.Clamp01((hit.distance - WheelRadius) / SuspensionRest);
            var prev = _suspension[index];
            var compressionVel = (compression - prev) / Mathf.Max(dt, 0.0001f);
            _suspension[index] = compression;

            var spring = compression * SpringForce;
            var damper = compressionVel * DamperForce;
            _rb.AddForceAtPosition(transform.up * (spring + damper), hit.point, ForceMode.Force);

            var wheelVel = _rb.GetPointVelocity(hit.point);
            var forward = transform.forward;
            if (index < 2)
                forward = Quaternion.AngleAxis(_steerVisual, transform.up) * forward;
            forward = Vector3.ProjectOnPlane(forward, hit.normal).normalized;
            var right = Vector3.Cross(hit.normal, forward).normalized;

            var latSpeed = Vector3.Dot(wheelVel, right);
            _rb.AddForceAtPosition(-right * latSpeed * LateralGrip, hit.point, ForceMode.Force);

            var forwardSpeed = Vector3.Dot(wheelVel, forward);
            var drive = 0f;
            if (_brake || Mathf.Abs(_throttle) < 0.01f)
            {
                drive = -Mathf.Sign(forwardSpeed) * BrakeForce * (_brake ? 0.45f : 0.2f);
            }
            else if (SpeedKmh < MaxSpeedKmh)
            {
                var headroom = 1f - Mathf.Clamp01(SpeedKmh / MaxSpeedKmh);
                drive = _throttle * DriveForce * (0.35f + 0.65f * headroom);
            }

            _rb.AddForceAtPosition(forward * drive, hit.point, ForceMode.Force);

            var spin = forwardSpeed / Mathf.Max(WheelRadius, 0.1f) * Mathf.Rad2Deg * dt;
            _wheelSpin[index] += spin;

            if (_model != null && _model.WheelVisuals != null && index < _model.WheelVisuals.Length && _model.WheelVisuals[index] != null)
            {
                var t = _model.WheelVisuals[index];
                var y = hit.point.y + WheelRadius - transform.position.y;
                var lp = t.localPosition;
                lp.y = Mathf.Lerp(lp.y, y, 0.35f);
                t.localPosition = lp;
            }

            return true;
        }

        private void ApplyAntiRoll(int left, int right)
        {
            var force = (_suspension[left] - _suspension[right]) * AntiRoll;
            var posL = transform.TransformPoint(_wheelLocal[left]);
            var posR = transform.TransformPoint(_wheelLocal[right]);
            _rb.AddForceAtPosition(transform.up * -force, posL, ForceMode.Force);
            _rb.AddForceAtPosition(transform.up * force, posR, ForceMode.Force);
        }

        private void UpdateVisualWheels()
        {
            if (_model?.WheelVisuals == null)
                return;

            for (var i = 0; i < _model.WheelVisuals.Length; i++)
            {
                var t = _model.WheelVisuals[i];
                if (t == null)
                    continue;
                var steer = i < 2 ? _steerVisual : 0f;
                t.localRotation = Quaternion.Euler(_wheelSpin[i], steer, 90f);
            }
        }

        private void UpdateEngineAudio()
        {
            if (_engine == null)
                return;
            var speed01 = Mathf.Clamp01(SpeedKmh / MaxSpeedKmh);
            // Vites basamaklı devir perdesi (VehicleFeel); yoksa eski doğrusal formül.
            _engine.pitch = Feel != null
                ? Feel.EnginePitch(Mathf.Abs(_throttle))
                : 0.75f + speed01 * 0.9f + Mathf.Abs(_throttle) * 0.15f;
            _engine.volume = HasDriver ? 0.25f + speed01 * 0.45f : 0.05f;
        }

        private void UpdateDust(bool grounded)
        {
            if (_dust == null)
                return;
            var emit = grounded && SpeedKmh > 12f && HasDriver;
            var emission = _dust.emission;
            emission.enabled = emit;
            if (emit)
            {
                var main = _dust.main;
                main.startSpeed = 1.5f + SpeedKmh * 0.03f;
            }
        }

        private void OnCollisionEnter(Collision collision) => HandleCollision(collision);
        private void OnCollisionStay(Collision collision) => HandleCollision(collision);

        private void HandleCollision(Collision collision)
        {
            if (_exploded || _roadkillCooldown > 0f || SpeedKmh < Config.RoadkillSpeedKmh)
                return;
            if (collision.collider == null)
                return;

            var combatant = collision.collider.GetComponentInParent<Combatant>();
            if (combatant == null || !combatant.IsAlive || combatant == _driver)
                return;
            for (var i = 0; i < _passengers.Length; i++)
                if (_passengers[i] == combatant)
                    return;
            // Dost ateşi kapalıyken sürücünün timine ezme hasarı yok (bot takım arkadaşları yoldan kaçmaz).
            if (_driver != null && combatant.Team == _driver.Team && !CombatContext.FriendlyFire)
                return;

            _roadkillCooldown = 0.4f;
            var damage = Mathf.Lerp(25f, 120f, Mathf.Clamp01((SpeedKmh - Config.RoadkillSpeedKmh) / 60f));
            var combat = CombatContext.Combat;
            if (combat != null)
                combat.ApplyEnvironmentalDamage(combatant.Id, DamageSourceIds.Vehicle, damage);
            else
                combatant.ApplyDamage(new DamageInfo(damage, PlayerId.Invalid, DamageSourceIds.Vehicle));
        }

        private void OnNearbyExplosion(Vector3 position, float radius)
        {
            if (_exploded || !GameContext.HasAuthority)
                return;
            var d = Vector3.Distance(transform.position, position);
            if (d > radius * 1.2f)
                return;
            var t = 1f - Mathf.Clamp01(d / Mathf.Max(radius, 0.1f));
            ApplyDamage(220f * t * t);
        }

        private void Explode(PlayerId attackerId)
        {
            if (_exploded)
                return;
            _exploded = true;
            Health = 0f;

            var driver = _driver;

            if (_rb != null)
            {
                _rb.linearVelocity *= 0.2f;
                _rb.isKinematic = true;
            }

            try
            {
                ExplosionSystem.Explode(transform.position + Vector3.up, Config.ExplosionRadius, Config.ExplosionDamage,
                    attackerId.IsValid ? attackerId : (driver != null ? driver.Id : PlayerId.Invalid),
                    DamageSourceIds.Vehicle);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            // Mürettebat patlama bitince fırlatılır: koltukta hedef alınamaz oldukları için kendi araçlarının patlamasından korunur.
            Exit();
            for (var i = 0; i < _passengers.Length; i++)
                ExitPassenger(i);

            if (_model?.Root != null)
            {
                foreach (var r in _model.Root.GetComponentsInChildren<Renderer>())
                {
                    if (r != null)
                        r.sharedMaterial = MaterialLibrary.Get(MaterialId.Rust);
                }
            }

            RefreshCondition();
            Destroy(gameObject, 12f);
        }

        private void PlaceBeside(Combatant combatant)
        {
            if (combatant == null)
                return;

            var side = transform.right * 2.2f;
            var candidate = transform.position + side + Vector3.up * 0.5f;
            if (Physics.Raycast(candidate + Vector3.up * 3f, Vector3.down, out var hit, 8f, GameLayers.GroundMask,
                    QueryTriggerInteraction.Ignore))
                candidate = hit.point;

            combatant.transform.SetParent(null, true);
            combatant.transform.position = candidate;
        }

        private void SetupAudioAndDust()
        {
            _engine = gameObject.AddComponent<AudioSource>();
            Project.Infrastructure.Audio.HdrMix.MixerRouting.Route(_engine, Project.Infrastructure.Audio.HdrMix.MixChannel.Arac); // mikser Arac grubu (yoksa no-op)
            _engine.loop = true;
            _engine.spatialBlend = 1f;
            _engine.rolloffMode = AudioRolloffMode.Linear;
            _engine.maxDistance = 45f;
            _engine.volume = 0.05f;
            try
            {
                var clip = GameAudio.GetClip(SoundId.VehicleEngine);
                if (clip != null)
                {
                    _engine.clip = clip;
                    _engine.Play();
                }
            }
            catch
            {
                // ses yoksa sessiz devam
            }

            var dustGo = new GameObject("Toz");
            dustGo.transform.SetParent(transform, false);
            dustGo.transform.localPosition = Config.DustOffset;
            _dust = dustGo.AddComponent<ParticleSystem>();
            var main = _dust.main;
            main.startLifetime = 1.2f;
            main.startSize = 0.6f;
            main.startColor = new Color(0.55f, 0.48f, 0.35f, 0.35f);
            main.maxParticles = 40;
            var emission = _dust.emission;
            emission.rateOverTime = 18f;
            emission.enabled = false;
            var shape = _dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.6f, 0.1f, 0.8f);
        }
    }
}
