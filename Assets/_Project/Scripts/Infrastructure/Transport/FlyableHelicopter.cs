using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Vehicles;
using Project.Infrastructure.Vfx;
using Project.Infrastructure.World;
using UnityEngine;
using Rules = Project.Infrastructure.Vehicles.HelicopterFlightRules;

namespace Project.Infrastructure.Transport
{
    /// <summary>
    /// Oyuncunun uçurabildiği T-70: arcade-gerçekçi uçuş (kolektif W/S, yaw A/D, fareyle eğim/yatış, otomatik havada asılı
    /// kalma yardımı), rotor devri, yer etkisi, iniş takımı teması, sert iniş/rotor çarpması hasarı.
    /// 1 pilot + 9 yolcu + 2 kapı nişancısı (PMT-76, KirpiTurret mantığı yeniden kullanılır).
    /// Giriş/çıkış PlayerController.Heli üzerinden. Görünüm ve koltuk düzeni HelicopterModel'den; insertion Helicopter'a dokunmaz.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FlyableHelicopter : MonoBehaviour
    {
        public const float MaxHealth = 1600f;
        public const float EnterRadius = 5.5f;
        public const float MaxExitAgl = 2.5f;
        public const float MaxExitSpeed = 6f;
        private const float RotorDegPerSecond = 1150f;
        private const float TailFactor = 4.6f;
        private const float DustHeight = 22f;

        private static readonly List<FlyableHelicopter> Active = new(4);
        public static IReadOnlyList<FlyableHelicopter> All => Active;

        // --- durum
        private Rigidbody _rb;
        private Transform _model;
        private Transform _mainRotor;
        private Transform _tailRotor;
        private Renderer _rotorDisc;
        private AudioSource _audio;
        private Transform[] _seats;
        private Transform[] _views;
        private Transform _pilotSeat;
        private Transform _pilotView;
        private readonly Transform[] _gunnerViews = new Transform[Rules.GunnerSeats];
        private readonly KirpiTurret[] _turrets = new KirpiTurret[Rules.GunnerSeats];
        private Vector3[] _disembarkLocal;

        private Combatant _pilot;
        private readonly Combatant[] _passengers = new Combatant[Rules.PassengerSeats];
        private readonly Combatant[] _gunners = new Combatant[Rules.GunnerSeats];

        private bool _engineOn;
        private float _rotor01;
        private float _rotorAngle;
        private float _tailAngle;
        private float _collective;
        private float _collectiveInput;
        private float _yawInput;
        private float _pitchStick;
        private float _rollStick;
        private float _yawTarget;
        private bool _hoverAssist = true;
        private bool _exploded;
        private float _strikeCooldown;
        private float _dustTimer;
        private float _noPilotSince = -1f;
        private bool _wasGrounded = true;
        private float _lastVy;
        private bool _dustFailed;

        private readonly Vector3[] _gearLocal =
        {
            new Vector3(-1.0f, 0.5f, 1.4f), new Vector3(1.0f, 0.5f, 1.4f), new Vector3(0f, 0.5f, -7.6f)
        };

        private readonly float[] _gearShare = { 0.4f, 0.4f, 0.2f };
        private readonly float[] _gearCompression = new float[3];
        private int _gearTouching;

        // --- herkese açık durum
        public float Health { get; private set; } = MaxHealth;
        public bool EngineOn => _engineOn;
        public float Rotor01 => _rotor01;
        public float Collective => _collective;
        public bool HoverAssist => _hoverAssist;
        public bool HasPilot => _pilot != null;
        public Combatant Pilot => _pilot;
        public int PassengerCount => CountOccupied(_passengers);
        public int GunnerCount => CountOccupied(_gunners);
        public float SpeedKmh { get; private set; }
        public float VerticalSpeed { get; private set; }
        public float Agl { get; private set; }
        public bool OnGround => _gearTouching > 0;
        public float TiltDegrees => Vector3.Angle(transform.up, Vector3.up);
        public bool CanExitNow => Agl <= MaxExitAgl && _rb != null && _rb.linearVelocity.magnitude <= MaxExitSpeed;
        public string DisplayName => Project.Application.Catalogs.NameProfile.Get(Project.Application.Catalogs.NameProfile.VehicleHeli, "T-70");

        public KirpiTurret GetTurret(int index) => index >= 0 && index < _turrets.Length ? _turrets[index] : null;
        public Combatant GetPassenger(int index) => index >= 0 && index < _passengers.Length ? _passengers[index] : null;
        public Combatant GetGunner(int index) => index >= 0 && index < _gunners.Length ? _gunners[index] : null;

        public bool IsOccupant(PlayerId id)
        {
            if (!id.IsValid)
                return false;
            if (_pilot != null && _pilot.Id == id)
                return true;
            return Contains(_passengers, id) || Contains(_gunners, id);
        }

        public Transform GetViewPoint(Rules.SeatKind kind, int index)
        {
            switch (kind)
            {
                case Rules.SeatKind.Pilot: return _pilotView != null ? _pilotView : transform;
                case Rules.SeatKind.Gunner:
                    return index >= 0 && index < _gunnerViews.Length && _gunnerViews[index] != null ? _gunnerViews[index] : transform;
                default:
                    return _views != null && index >= 0 && index < _views.Length ? _views[index] : transform;
            }
        }

        // ================================================================ üretim
        public static FlyableHelicopter Spawn(Vector3 position, float yaw)
        {
            var go = new GameObject("T-70 (uçurulabilir)");
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.layer = GameLayers.Vehicle;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 7000f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.centerOfMass = new Vector3(0f, 1.6f, 0.4f);

            var heli = go.AddComponent<FlyableHelicopter>();
            heli._rb = rb;
            heli._yawTarget = yaw;
            heli.Build();
            return heli;
        }

        private void Awake()
        {
            if (_rb == null)
                _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (!Active.Contains(this))
                Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);

        private void OnDestroy()
        {
            Active.Remove(this);
            try { ExplosionSystem.Exploded -= OnNearbyExplosion; } catch { /* sahne kapanıyor */ }
        }

        private void Start()
        {
            try { ExplosionSystem.Exploded += OnNearbyExplosion; } catch { /* yok say */ }
        }

        /// <summary>Verilen yarıçaptaki en yakın sağlam helikopter.</summary>
        public static FlyableHelicopter FindNearest(Vector3 pos, float radius)
        {
            FlyableHelicopter best = null;
            var bestSqr = radius * radius;
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                var h = Active[i];
                if (h == null)
                {
                    Active.RemoveAt(i);
                    continue;
                }

                if (h.Health <= 0f)
                    continue;
                var sqr = (h.transform.position - pos).sqrMagnitude;
                if (sqr > bestSqr)
                    continue;
                bestSqr = sqr;
                best = h;
            }

            return best;
        }

        private void Build()
        {
            _model = new GameObject("Model").transform;
            _model.gameObject.layer = GameLayers.Vehicle;
            _model.SetParent(transform, false);

            HelicopterModel.GetSeatLayout(out var seats, out var seatYaws, out var views, out var viewEulers, out var disembark, out _);
            _disembarkLocal = disembark;
            _seats = new Transform[Rules.PassengerSeats];
            _views = new Transform[Rules.PassengerSeats];
            for (var i = 0; i < Rules.PassengerSeats; i++)
            {
                var seat = new GameObject("Yolcu" + (i + 1)).transform;
                seat.SetParent(transform, false);
                seat.localPosition = seats[i];
                seat.localRotation = Quaternion.Euler(0f, seatYaws[i], 0f);
                _seats[i] = seat;
                var view = new GameObject("YolcuKamera" + (i + 1)).transform;
                view.SetParent(transform, false);
                view.localPosition = views[i];
                view.localRotation = Quaternion.Euler(viewEulers[i]);
                _views[i] = view;
            }

            _pilotSeat = new GameObject("PilotKoltugu").transform;
            _pilotSeat.SetParent(transform, false);
            _pilotSeat.localPosition = new Vector3(-0.45f, HelicopterModel.FloorY + 0.2f, 3.0f);
            _pilotView = new GameObject("PilotKamera").transform;
            _pilotView.SetParent(transform, false);
            _pilotView.localPosition = new Vector3(-0.45f, HelicopterModel.FloorY + 1.35f, 3.1f);

            try
            {
                BuildVisuals();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            BuildColliders();
            BuildDoorGuns();
            BuildAudio();
        }

        private void BuildVisuals()
        {
            if (TransportVehicle.IsHeadless)
            {
                _dustFailed = true;
                return;
            }

            TransportVehicle.CreateVisual("Govde", _model, HelicopterModel.Body, HelicopterModel.BodyMaterials(), true);
            _mainRotor = new GameObject("AnaRotor").transform;
            _mainRotor.gameObject.layer = GameLayers.Vehicle;
            _mainRotor.SetParent(_model, false);
            _mainRotor.localPosition = HelicopterModel.MainRotorHub;
            TransportVehicle.CreateVisual("Pervane", _mainRotor, HelicopterModel.MainRotor, HelicopterModel.RotorMaterials(), true);
            var disc = TransportVehicle.CreateVisual("RotorDiski", _model, HelicopterModel.RotorDisc, HelicopterModel.DiscMaterials(), false);
            disc.transform.localPosition = HelicopterModel.MainRotorHub + Vector3.up * 0.03f;
            disc.receiveShadows = false;
            disc.enabled = false;
            _rotorDisc = disc;
            _tailRotor = new GameObject("KuyrukRotor").transform;
            _tailRotor.gameObject.layer = GameLayers.Vehicle;
            _tailRotor.SetParent(_model, false);
            _tailRotor.localPosition = HelicopterModel.TailRotorHub;
            TransportVehicle.CreateVisual("KuyrukPervane", _tailRotor, HelicopterModel.TailRotor, HelicopterModel.RotorMaterials(), false);
        }

        private void BuildColliders()
        {
            TransportVehicle.CreateBoxCollider(transform, new Vector3(0f, 1.65f, 0.2f), new Vector3(2.35f, 1.95f, 4.5f), "Kabin");
            TransportVehicle.CreateBoxCollider(transform, new Vector3(0f, 1.3f, 3.6f), new Vector3(1.9f, 1.1f, 2.3f), "Burun");
            TransportVehicle.CreateBoxCollider(transform, new Vector3(0f, 2.92f, 0.1f), new Vector3(1.4f, 0.55f, 3.8f), "Motor");
            TransportVehicle.CreateBoxCollider(transform, new Vector3(0f, 2.3f, -6.2f), new Vector3(0.7f, 0.6f, 5.6f), "KuyrukKirisi");
            TransportVehicle.CreateBoxCollider(transform, new Vector3(0f, 3.1f, -9.15f), new Vector3(0.2f, 1.7f, 0.9f), "Dikme");
        }

        private void BuildDoorGuns()
        {
            for (var i = 0; i < Rules.GunnerSeats; i++)
            {
                var side = i == 0 ? -1f : 1f;
                var root = new GameObject(i == 0 ? "KapiMakinelisiSol" : "KapiMakinelisiSag");
                root.layer = GameLayers.Vehicle;
                root.transform.SetParent(transform, false);

                var yaw = new GameObject("Donus").transform;
                yaw.SetParent(root.transform, false);
                yaw.localPosition = new Vector3(side * 1.25f, 1.75f, 1.9f);
                // Dışa açılmış, sınırlı namlu modeli.
                StructureKit.CreateBox(yaw, "Govde", new Vector3(0f, 0f, 0.1f), new Vector3(0.18f, 0.2f, 0.45f),
                    Quaternion.identity, MaterialId.MetalDark, false);
                var pitch = new GameObject("Egim").transform;
                pitch.SetParent(yaw, false);
                pitch.localPosition = new Vector3(0f, 0.05f, 0.2f);
                StructureKit.CreateBox(pitch, "Namlu", new Vector3(0f, 0f, 0.5f), new Vector3(0.07f, 0.07f, 1.0f),
                    Quaternion.identity, MaterialId.MetalDark, false);
                var muzzle = new GameObject("NamluUcu").transform;
                muzzle.SetParent(pitch, false);
                muzzle.localPosition = new Vector3(0f, 0f, 1.05f);
                var view = new GameObject("NisanciKamera").transform;
                view.SetParent(transform, false);
                view.localPosition = new Vector3(side * 0.85f, 2.05f, 1.55f);
                _gunnerViews[i] = view;

                var turret = root.AddComponent<KirpiTurret>();
                turret.Setup(new KirpiModelBuilder.Result
                {
                    TurretYaw = yaw, TurretPitch = pitch, TurretMuzzle = muzzle, GunnerView = view
                }, VehicleConfig.Cobra);
                _turrets[i] = turret;
            }
        }

        private void BuildAudio()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            Project.Infrastructure.Audio.HdrMix.MixerRouting.Route(_audio, Project.Infrastructure.Audio.HdrMix.MixChannel.Arac); // rotor: mikser Arac grubu (yoksa no-op)
            _audio.loop = true;
            _audio.spatialBlend = 1f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.maxDistance = 450f;
            _audio.volume = 0f;
            try
            {
                var clip = GameAudio.GetClip(SoundId.HelicopterRotor);
                if (clip != null)
                {
                    _audio.clip = clip;
                    _audio.Play();
                }
            }
            catch
            {
                // ses yoksa sessiz
            }
        }

        // ================================================================ koltuk yönetimi
        public bool TryEnterPilot(Combatant c)
        {
            if (!CanBoard(c) || _pilot != null)
                return false;
            _pilot = c;
            c.IsTargetable = false;
            _engineOn = true;
            _noPilotSince = -1f;
            _collective = 0f;
            _pitchStick = _rollStick = 0f;
            _yawTarget = transform.eulerAngles.y;
            return true;
        }

        public bool TryEnterPassenger(Combatant c, out int seat)
        {
            seat = -1;
            if (!CanBoard(c))
                return false;
            for (var i = 0; i < _passengers.Length; i++)
            {
                if (_passengers[i] != null)
                    continue;
                _passengers[i] = c;
                c.IsTargetable = false;
                seat = i;
                return true;
            }

            return false;
        }

        public bool TryEnterGunner(Combatant c, int gunnerIndex)
        {
            if (!CanBoard(c) || gunnerIndex < 0 || gunnerIndex >= _gunners.Length || _gunners[gunnerIndex] != null)
                return false;
            _gunners[gunnerIndex] = c;
            c.IsTargetable = false;
            return true;
        }

        /// <summary>Boş ilk koltuğa bindirir (pilot, yolcu, nişancı sırasıyla).</summary>
        public Rules.SeatKind TryEnterAny(Combatant c, out int index)
        {
            index = -1;
            if (!CanBoard(c))
                return Rules.SeatKind.None;
            var kind = Rules.PickSeat(_pilot == null, OccupiedMask(_passengers), OccupiedMask(_gunners), out index);
            switch (kind)
            {
                case Rules.SeatKind.Pilot: return TryEnterPilot(c) ? kind : Rules.SeatKind.None;
                case Rules.SeatKind.Passenger: return TryEnterPassenger(c, out index) ? kind : Rules.SeatKind.None;
                case Rules.SeatKind.Gunner: return TryEnterGunner(c, index) ? kind : Rules.SeatKind.None;
                default: return Rules.SeatKind.None;
            }
        }

        /// <summary>Koltuğu boşaltır ve savaşçıyı yanına indirir.</summary>
        public void Leave(Combatant c)
        {
            if (c == null)
                return;
            var wasPilot = _pilot == c;
            if (wasPilot)
            {
                _pilot = null;
                _collectiveInput = _yawInput = 0f;
                _noPilotSince = Time.time;
            }

            for (var i = 0; i < _passengers.Length; i++)
                if (_passengers[i] == c) _passengers[i] = null;
            for (var i = 0; i < _gunners.Length; i++)
            {
                if (_gunners[i] != c)
                    continue;
                _gunners[i] = null;
                if (_turrets[i] != null)
                    _turrets[i].PlayerGunner = false;
            }

            c.IsTargetable = true;
        }

        /// <summary>Savaşçıyı bu koltuğa taşımadan kayıttan düşer (PlayerController kendi yerleştirir).</summary>
        public Vector3 GetExitPoint(Combatant c)
        {
            var side = 1f;
            for (var i = 0; i < _passengers.Length; i++)
                if (_passengers[i] == c && _disembarkLocal != null && i < _disembarkLocal.Length)
                    side = _disembarkLocal[i].x >= 0f ? 1f : -1f;
            if (c == _pilot)
                side = -1f;
            for (var i = 0; i < _gunners.Length; i++)
                if (_gunners[i] == c)
                    side = i == 0 ? -1f : 1f;

            var candidate = transform.TransformPoint(new Vector3(side * 3.6f, 0.5f, 0.8f));
            if (Physics.Raycast(candidate + Vector3.up * 4f, Vector3.down, out var hit, 40f, GameLayers.GroundMask,
                    QueryTriggerInteraction.Ignore))
                candidate = hit.point;
            return candidate;
        }

        private bool CanBoard(Combatant c)
            => c != null && !_exploded && Health > 0f
               && Project.Application.Services.DownedRules.CanBoardVehicle(c.IsAlive, c.IsDowned);

        // ================================================================ girdi
        /// <summary>Pilot girdisi (kare başına): W/S kolektif, A/D yaw, fare delta eğim/yatış.</summary>
        public void SetPilotInput(float collective, float yaw, float mouseUp, float mouseRight, float dt)
        {
            _collectiveInput = Mathf.Clamp(collective, -1f, 1f);
            _yawInput = Mathf.Clamp(yaw, -1f, 1f);
            // Fare ileri (yukarı) = burun aşağı (cyclic ileri), sağ = sağa yatış.
            _pitchStick = Rules.StepStick(_pitchStick, mouseUp, dt);
            _rollStick = Rules.StepStick(_rollStick, mouseRight, dt);
        }

        public void ToggleHoverAssist() => _hoverAssist = !_hoverAssist;

        // ================================================================ hasar
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
                var next = Project.Application.Services.VehicleSocketRules.ConditionFor(Health / MaxHealth, _exploded || Health <= 0f);
                if (next == _condition)
                    return;
                _condition = next;
                if (_model != null)
                    VehicleSockets.ApplyCondition(_model.gameObject, next);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FlyableHelicopter] Condition: " + e.Message);
            }
        }

        private void OnNearbyExplosion(Vector3 position, float radius)
        {
            if (_exploded || !GameContext.HasAuthority)
                return;
            var d = Vector3.Distance(transform.position, position);
            if (d > radius * 1.2f)
                return;
            var t = 1f - Mathf.Clamp01(d / Mathf.Max(radius, 0.1f));
            ApplyDamage(260f * t * t);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_exploded || collision.collider == null)
                return;
            if (collision.collider.GetComponentInParent<Combatant>() != null)
                return;
            var speed = collision.relativeVelocity.magnitude;
            var dmg = Rules.CrashDamage(speed, TiltDegrees);
            if (dmg > 0f)
                ApplyDamage(dmg);
        }

        private void Explode(PlayerId attackerId)
        {
            if (_exploded)
                return;
            _exploded = true;
            Health = 0f;
            _engineOn = false;
            var pilotId = _pilot != null ? _pilot.Id : PlayerId.Invalid;

            try
            {
                ExplosionSystem.Explode(transform.position + Vector3.up * 1.5f, 9f, 200f,
                    attackerId.IsValid ? attackerId : pilotId, DamageSourceIds.Vehicle);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            // Mürettebat patlamadan sonra serbest bırakılır (koltukta hedef alınamaz olduğundan patlamadan korunurlar).
            if (_pilot != null)
                Leave(_pilot);
            for (var i = 0; i < _passengers.Length; i++)
                if (_passengers[i] != null) Leave(_passengers[i]);
            for (var i = 0; i < _gunners.Length; i++)
                if (_gunners[i] != null) Leave(_gunners[i]);

            if (_model != null)
            {
                foreach (var r in _model.GetComponentsInChildren<Renderer>())
                    if (r != null && r != _rotorDisc)
                        r.sharedMaterial = MaterialLibrary.Get(MaterialId.Rust);
            }

            RefreshCondition();
            Destroy(gameObject, 25f);
        }

        // ================================================================ simülasyon
        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            dt = Mathf.Min(dt, 0.1f);

            _rotor01 = Rules.StepRotor(_rotor01, _engineOn && !_exploded, dt);
            UpdateRotorVisuals(dt);
            UpdateAudio();
            UpdateDust(dt);
            UpdateRotorWash(dt);

            // Pilotsuz yerde duran araç bir süre sonra motoru kapatır.
            if (_pilot == null && _noPilotSince >= 0f && OnGround && Time.time - _noPilotSince > 4f)
                _engineOn = false;
        }

        private void FixedUpdate()
        {
            if (_rb == null || _exploded)
                return;
            var dt = Time.fixedDeltaTime;
            _strikeCooldown = Mathf.Max(0f, _strikeCooldown - dt);

            var v = _rb.linearVelocity;
            SpeedKmh = v.magnitude * 3.6f;
            VerticalSpeed = v.y;
            Agl = MeasureAgl();

            SimulateGear(dt);
            var grounded = _gearTouching > 0;

            // Touchdown: iniş takımı havadan teması ilk aldığında dikey hıza göre hasar (takım enerjinin bir kısmını emer).
            if (grounded && !_wasGrounded)
            {
                var impact = Mathf.Max(0f, -_lastVy) * 0.85f;
                var dmg = Rules.CrashDamage(impact, TiltDegrees);
                if (dmg > 0f)
                    ApplyDamage(dmg);
            }

            _wasGrounded = grounded;
            _lastVy = v.y;
            if (_exploded)
                return;

            var pilotActive = _pilot != null;
            var climbCmd = pilotActive ? _collectiveInput : 0f;

            // Kolektif
            if (!pilotActive)
            {
                _collective = grounded ? Mathf.MoveTowards(_collective, 0f, 0.5f * dt) : Rules.UnpilotedCollective(v.y);
            }
            else if (_hoverAssist)
            {
                var tiltComp = Mathf.Max(Mathf.Cos(TiltDegrees * Mathf.Deg2Rad), 0.6f);
                var target = Rules.AutoHoverCollective(v.y, climbCmd * 5f) / tiltComp;
                if (grounded && climbCmd <= 0.05f)
                    target = Mathf.Min(target, 0.3f);
                _collective = Mathf.MoveTowards(_collective, Mathf.Clamp01(target), 1.2f * dt);
            }
            else
            {
                _collective = Rules.StepCollective(_collective, climbCmd, dt);
            }

            var lift = Rules.LiftAcceleration(_collective, _rotor01, Agl, HelicopterModel.MainRotorRadius);
            _rb.AddForce(transform.up * lift, ForceMode.Acceleration);

            ApplyAerodynamics(v);
            ApplyAttitude(dt, grounded, pilotActive);
            CheckRotorStrike();
        }

        private void ApplyAerodynamics(Vector3 v)
        {
            var local = transform.InverseTransformDirection(v);
            // Yan sürüklenme güçlü (arcade stabilite), ileri hafif, dikey sönümleme.
            var drag = new Vector3(-local.x * 0.7f, -local.y * 0.35f, -local.z * 0.05f);
            var horizontal = new Vector2(v.x, v.z).magnitude;
            var quad = horizontal * horizontal * 0.0017f;
            var world = transform.TransformDirection(drag);
            if (horizontal > 0.1f)
                world -= new Vector3(v.x, 0f, v.z) / horizontal * quad;
            _rb.AddForce(world * Mathf.Lerp(0.15f, 1f, _rotor01), ForceMode.Acceleration);
        }

        private void ApplyAttitude(float dt, bool grounded, bool pilotActive)
        {
            var authority = Mathf.Clamp01((_rotor01 - 0.3f) / 0.6f);
            var yawIn = pilotActive ? _yawInput : 0f;
            if (!grounded || _collective > 0.25f)
                _yawTarget += yawIn * 55f * authority * dt;
            var actualYaw = transform.eulerAngles.y;
            _yawTarget = actualYaw + Mathf.Clamp(Mathf.DeltaAngle(actualYaw, _yawTarget), -45f, 45f);

            var pitchT = pilotActive ? Rules.TargetPitch(_pitchStick) : 0f;
            var rollT = pilotActive ? Rules.TargetRoll(_rollStick) : 0f;
            // Yerde ya da rotor düşükken gövde düz kalır.
            if (grounded && (_collective < 0.4f || _rotor01 < Rules.MinLiftRotor))
            {
                pitchT = 0f;
                rollT = 0f;
            }

            var desired = Quaternion.Euler(pitchT, _yawTarget, -rollT);
            var delta = desired * Quaternion.Inverse(_rb.rotation);
            delta.ToAngleAxis(out var angle, out var axis);
            if (angle > 180f)
                angle -= 360f;
            if (float.IsNaN(axis.x) || axis.sqrMagnitude < 1e-6f)
                return;

            var torque = axis.normalized * (angle * Mathf.Deg2Rad * 5f) - _rb.angularVelocity * 4f;
            _rb.AddTorque(torque * Mathf.Lerp(0.3f, 1f, authority), ForceMode.Acceleration);
        }

        /// <summary>İniş takımı: üç noktadan ray ile yay-sönümleyici + yer sürtünmesi.</summary>
        private void SimulateGear(float dt)
        {
            const float rest = 0.5f;      // origin→zemin: kızak yere değerken
            const float travel = 0.35f;
            const float spring = 120000f;
            const float damper = 16000f;
            _gearTouching = 0;
            for (var i = 0; i < _gearLocal.Length; i++)
            {
                var origin = transform.TransformPoint(_gearLocal[i]);
                var len = rest + 0.3f;
                if (!Physics.Raycast(origin, Vector3.down, out var hit, len, GameLayers.GroundMask, QueryTriggerInteraction.Ignore)
                    || hit.collider.transform.IsChildOf(transform))
                {
                    _gearCompression[i] = 0f;
                    continue;
                }

                _gearTouching++;
                var c = Mathf.Clamp01((rest - hit.distance) / travel);
                var vel = (c - _gearCompression[i]) / Mathf.Max(dt, 1e-4f);
                _gearCompression[i] = c;
                var force = Mathf.Max(0f, c * spring + vel * damper) * _gearShare[i] * 2.5f;
                if (hit.distance <= rest + 0.05f)
                    _rb.AddForceAtPosition(Vector3.up * force, hit.point, ForceMode.Force);

                // Yer sürtünmesi: rotor düşük/kolektif düşükse yatay hızı keser (kızaklar kayar ama durur).
                if (hit.distance <= rest + 0.05f && _collective < 0.45f)
                {
                    var pv = _rb.GetPointVelocity(hit.point);
                    var flat = new Vector3(pv.x, 0f, pv.z);
                    _rb.AddForceAtPosition(-flat * 2200f * _gearShare[i], hit.point, ForceMode.Force);
                }
            }
        }

        private float MeasureAgl()
        {
            var origin = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 400f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform))
                return Mathf.Max(0f, hit.distance - 0.5f);
            // Kendi gövdesine çarptıysa yer bilinmiyor: yüksekte say.
            return 400f;
        }

        private static readonly Collider[] StrikeBuffer = new Collider[12];

        private void CheckRotorStrike()
        {
            if (_strikeCooldown > 0f || _rotor01 < 0.3f)
                return;
            var hub = transform.TransformPoint(HelicopterModel.MainRotorHub);
            for (var i = 0; i < 4; i++)
            {
                var dir = Quaternion.Euler(0f, i * 90f + 45f, 0f) * transform.forward;
                var p = hub + dir * (HelicopterModel.MainRotorRadius - 0.6f);
                var n = Physics.OverlapSphereNonAlloc(p, 0.55f, StrikeBuffer, GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
                for (var k = 0; k < n; k++)
                {
                    var col = StrikeBuffer[k];
                    if (col == null || col.transform.IsChildOf(transform) || col.GetComponentInParent<Combatant>() != null)
                        continue;
                    _strikeCooldown = 0.5f;
                    ApplyDamage(Rules.RotorStrikeDamage(_rotor01));
                    _rb.AddForce(-dir * 6f, ForceMode.VelocityChange);
                    try { GameVfx.Dust(p, 1.2f); } catch { /* yok say */ }
                    return;
                }
            }
        }

        // ================================================================ görsel/ses
        private void UpdateRotorVisuals(float dt)
        {
            if (_mainRotor == null)
                return;
            _rotorAngle = Mathf.Repeat(_rotorAngle + RotorDegPerSecond * _rotor01 * dt, 360f);
            _tailAngle = Mathf.Repeat(_tailAngle + RotorDegPerSecond * TailFactor * _rotor01 * dt, 360f);
            _mainRotor.localRotation = Quaternion.Euler(0f, _rotorAngle, 0f);
            if (_tailRotor != null)
                _tailRotor.localRotation = Quaternion.Euler(_tailAngle, 0f, 0f);
            if (_rotorDisc != null)
            {
                var show = _rotor01 > 0.6f;
                if (_rotorDisc.enabled != show)
                    _rotorDisc.enabled = show;
            }
        }

        private void UpdateAudio()
        {
            if (_audio == null)
                return;
            _audio.volume = Mathf.Clamp01(_rotor01 * 1.1f) * 0.9f;
            _audio.pitch = 0.55f + 0.5f * _rotor01 + Mathf.Clamp(_collective - 0.5f, -0.3f, 0.5f) * 0.12f;
        }

        private float _washTimer;

        /// <summary>Zemine 15 m'den yakınken rotor akışı halkası (kısılmış, GPU VFX önce).</summary>
        private void UpdateRotorWash(float dt)
        {
            if (_dustFailed || _rotor01 < 0.5f || Agl >= 15f)
                return;
            _washTimer -= dt;
            if (_washTimer > 0f)
                return;
            _washTimer = 0.4f;
            try
            {
                var p = transform.position;
                GameVfx.RotorWash(new Vector3(p.x, p.y - Agl + 0.1f, p.z), 7f);
            }
            catch (Exception e)
            {
                _dustFailed = true;
                Debug.LogException(e, this);
            }
        }

        private void UpdateDust(float dt)
        {
            if (_dustFailed || _rotor01 < 0.5f || Agl > DustHeight)
                return;
            _dustTimer -= dt;
            if (_dustTimer > 0f)
                return;
            var t = Mathf.Clamp01(Agl / DustHeight);
            _dustTimer = Mathf.Lerp(0.09f, 0.3f, t);
            var angle = UnityEngine.Random.value * Mathf.PI * 2f;
            var radius = UnityEngine.Random.Range(2.5f, 7.5f);
            var p = transform.position + new Vector3(Mathf.Cos(angle) * radius, -Agl + 0.1f, Mathf.Sin(angle) * radius);
            try
            {
                GameVfx.Dust(p, Mathf.Lerp(1.8f, 0.5f, t));
            }
            catch (Exception e)
            {
                _dustFailed = true;
                Debug.LogException(e, this);
            }
        }

        // ================================================================ yardımcılar
        private static int CountOccupied(Combatant[] arr)
        {
            var n = 0;
            for (var i = 0; i < arr.Length; i++)
                if (arr[i] != null) n++;
            return n;
        }

        private static bool[] OccupiedMask(Combatant[] arr)
        {
            var mask = new bool[arr.Length];
            for (var i = 0; i < arr.Length; i++)
                mask[i] = arr[i] != null;
            return mask;
        }

        private static bool Contains(Combatant[] arr, PlayerId id)
        {
            for (var i = 0; i < arr.Length; i++)
                if (arr[i] != null && arr[i].Id == id)
                    return true;
            return false;
        }
    }
}
