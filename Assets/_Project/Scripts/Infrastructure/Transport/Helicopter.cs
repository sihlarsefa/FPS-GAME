using System;
using System.Collections.Generic;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Vfx;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.Transport
{
    /// <summary>
    /// T-70 (Black Hawk benzeri) intikal helikopteri. Plan başlangıcında, verilen irtifada (arazi üstü, AGL) bekler;
    /// Begin() ile hafif kavisli bir rotada yatış yaparak iniş bölgesine (LZ) uçar, yaklaşmada yavaşlar ve süzülür,
    /// LZ üzerinde dikey alçalıp ~1 m'de askıda kalır, kızaklarını yere koyar → Arrived. ReleasePassengers'tan 4 sn sonra
    /// kalkar, haritanın dışına doğru uçar → Departed; 40 sn sonra yok edilir.
    /// <para>
    /// Rota ve irtifa profili oluşturma anında bir kez hesaplanır (sırt/ağaç/yapı engellerinin üstünde güvenli irtifa,
    /// tırmanma/alçalma eğim sınırları, LZ'ye süzülüş eğimi). Uçuş sırasında karede tahsis yapılmaz.
    /// </para>
    /// </summary>
    public sealed class Helicopter : TransportVehicle
    {
        /// <summary>Varsayılan seyir irtifası (arazi üstü, m).</summary>
        public const float DefaultAltitude = 50f;

        public const float CruiseSpeed = 32f;
        public const float DepartSpeed = 42f;

        private const float Acceleration = 4.5f;
        private const float Deceleration = 3.4f;
        private const float ClimbRate = 10f;
        private const float DescentRate = 7.5f;
        private const float VerticalAcceleration = 5f;
        private const float HoverHeight = 1f;
        private const float GlideSlope = 0.4f;
        private const float ObstacleClearance = 25f;
        private const float ApproachClearance = 9f;
        private const float MinimumTerrainClearance = 5f;
        private const float PathSpacing = 6f;
        private const float RotorDegreesPerSecond = 1150f;
        private const float TailRotorFactor = 4.6f;
        private const float HoverHoldSeconds = 1.1f;
        private const float TouchdownSpeed = 0.75f;
        private const float ReleaseToLiftOffSeconds = 4f;
        private const float LiftOffHeight = 14f;
        private const float DustHeight = 20f;
        private const float LandingFootprint = 7.5f;
        private const float LandingSearchRadius = 70f;
        private const float MaxBankDegrees = 28f;
        private const float PivotHeight = 1.6f;
        private const float LandingPhaseDistance = 150f;

        private static readonly Vector3 PivotLocal = new Vector3(0f, PivotHeight, 0f);

        private enum FlightState
        {
            Holding,
            Cruise,
            Descend,
            Hover,
            Touchdown,
            Grounded,
            LiftOff,
            Depart
        }

        private FlightState _state = FlightState.Holding;
        private TransportPath _path;
        private float[] _profile;
        private float _altitude = DefaultAltitude;

        private float _s;
        private float _speed;
        private float _previousSpeed;
        private float _y;
        private float _verticalSpeed;
        private float _yaw;
        private float _yawVelocity;
        private float _pitch;
        private float _roll;
        private float _finalYaw;
        private float _groundPitch;
        private float _groundRoll;
        private float _stateTime;
        private float _bobPhase;

        private float _departYaw;
        private float _departTargetY;
        private float _departSampleTimer;

        private Vector3 _horizontal;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private float _dustTimer;
        private bool _dustFailed;

        private float _rotorSpin = 1f;
        private float _rotorAngle;
        private float _tailAngle;
        private Transform _model;
        private Transform _mainRotor;
        private Transform _tailRotor;
        private Renderer _rotorDisc;
        private GameObject _overrideRoot;
        private Renderer[] _bladeRenderers;
        private bool _blurShown;
        private NavMeshObstacle _obstacle;

        public override string DisplayName => Project.Application.Catalogs.NameProfile.Get(Project.Application.Catalogs.NameProfile.VehicleHeli, "T-70 Helikopteri");

        /// <summary>Seyir irtifası (arazi üstü, m).</summary>
        public float CruiseAltitude => _altitude;

        /// <summary>Kalan rota uzunluğu (m).</summary>
        public float RemainingRouteDistance => _path != null ? Mathf.Max(0f, _path.Length - _s) : DistanceToLandingZone;

        public override float EstimatedSecondsToArrival
        {
            get
            {
                if (HasArrived)
                    return 0f;

                var remaining = RemainingRouteDistance;
                var cruise = remaining / Mathf.Max(CruiseSpeed * 0.65f, _speed);
                var descent = Mathf.Max(0f, _y - LandingZone.y - HoverHeight) / (DescentRate * 0.55f);
                return cruise + descent + HoverHoldSeconds + HoverHeight / TouchdownSpeed;
            }
        }

        // ------------------------------------------------------------------ creation

        /// <summary>Plan için T-70 helikopteri kurar (yolcu koltukları hazır, Begin bekler).</summary>
        public static Helicopter Create(TeamInsertion plan, float altitude)
        {
            var go = new GameObject("T-70 Helikopteri - Tim " + (plan.Team + 1));
            go.layer = GameLayers.Vehicle;
            var helicopter = go.AddComponent<Helicopter>();
            helicopter.Setup(plan, altitude);
            return helicopter;
        }

        private void Setup(TeamInsertion plan, float altitude)
        {
            Method = InsertionMethod.Helicopter;
            Team = plan.Team;
            _altitude = altitude > 1f && !float.IsNaN(altitude) && !float.IsInfinity(altitude)
                ? Mathf.Clamp(altitude, 15f, 400f)
                : DefaultAltitude;

            // Koltuklar önce: model kurulumu başarısız olsa bile yolcu API'si çalışır.
            HelicopterModel.GetSeatLayout(out var seats, out var seatYaws, out var views, out var viewEulers, out var disembark, out var disembarkYaws);
            CreateSeats(seats, seatYaws, views, viewEulers);
            DisembarkLocal = disembark;
            DisembarkYawLocal = disembarkYaws;

            try
            {
                BuildRoute(plan);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                BuildFallbackRoute(plan);
            }

            try
            {
                BuildModel();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            // Başlangıç pozu: rota başında, profil irtifasında, LZ'ye dönük.
            var start = _path.Start;
            _horizontal = start;
            _y = _path.SampleTable(_profile, 0f);
            _yaw = YawOf(_path.DirectionAt(0f, PathSpacing));
            StartPosition = new Vector3(start.x, _y, start.z);
            ApplyPose();
            _lastPosition = transform.position;
            _hasLastPosition = true;

            RequestLoopAudio(SoundId.HelicopterRotor, 1f, 450f);
        }

        private void BuildRoute(TeamInsertion plan)
        {
            var start = ToVector3(plan.Start);
            var planned = ToVector3(plan.LandingZone);
            start.y = 0f;
            planned.y = 0f;

            // Bozuk plan (başlangıç = LZ): LZ'den harita dışına doğru geri çekilmiş bir başlangıç uydur.
            var flat = planned - start;
            if (flat.sqrMagnitude < 40f * 40f)
            {
                var center = TransportGround.MapCenter;
                var outward = new Vector3(planned.x - center.x, 0f, planned.z - center.y);
                if (outward.sqrMagnitude < 1f)
                    outward = Vector3.back;
                start = planned + outward.normalized * 350f;
            }

            StartPosition = start;
            PlannedLandingZone = new Vector3(planned.x, TransportGround.TerrainHeight(planned), planned.z);

            var site = TransportGround.FindLandingSite(planned, LandingFootprint, LandingSearchRadius, 1.2f, 6f);
            site.y = 0f;

            // Hafif kavisli rota (yatış için): kontrol noktası başlangıca yakın, yana kaydırılmış.
            var toSite = site - start;
            toSite.y = 0f;
            var distance = Mathf.Max(1f, toSite.magnitude);
            var forward = toSite / distance;
            var right = new Vector3(forward.z, 0f, -forward.x);
            var sign = (plan.Team & 1) == 0 ? 1f : -1f;
            var offset = distance * Mathf.Lerp(0.1f, 0.2f, Hash01(plan.Team)) * sign;
            var control = start + forward * (distance * 0.45f) + right * offset;

            var points = new List<Vector3>(128);
            TransportPath.AppendBezier(points, start, control, site, Mathf.Clamp(Mathf.CeilToInt(distance / 5f), 12, 160), true);
            _path = TransportPath.Resample(points, PathSpacing);

            _finalYaw = YawOf(_path.DirectionAt(_path.Length, PathSpacing));
            ComputeTouchdown(site, _finalYaw);
            BuildProfile();
        }

        /// <summary>Arazi sorguları başarısız olursa: düz rota, sabit irtifa, LZ'de dikey iniş.</summary>
        private void BuildFallbackRoute(TeamInsertion plan)
        {
            var start = ToVector3(plan.Start);
            var planned = ToVector3(plan.LandingZone);
            start.y = 0f;
            planned.y = 0f;
            if ((planned - start).sqrMagnitude < 1f)
                start = planned + Vector3.back * 300f;

            _path = TransportPath.Resample(new List<Vector3>(2) { start, planned }, PathSpacing);
            var groundY = SafeTerrainHeight(planned);
            StartPosition = start;
            PlannedLandingZone = new Vector3(planned.x, groundY, planned.z);
            LandingZone = PlannedLandingZone;
            _finalYaw = YawOf(planned - start);
            _groundPitch = 0f;
            _groundRoll = 0f;
            _profile = new float[_path.Count];
            for (var i = 0; i < _profile.Length; i++)
                _profile[i] = Mathf.Max(groundY, SafeTerrainHeight(_path.PointAt(i))) + _altitude;
        }

        private static float SafeTerrainHeight(Vector3 point)
        {
            try
            {
                return TransportGround.TerrainHeight(point);
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// <summary>Kızak uçlarının zemin yüksekliklerinden iniş yüksekliği ve eğim duruşu.</summary>
        private void ComputeTouchdown(Vector3 site, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var frontLeft = LandingGround(site + rotation * new Vector3(-1.25f, 0f, 2.3f));
            var frontRight = LandingGround(site + rotation * new Vector3(1.25f, 0f, 2.3f));
            var rearLeft = LandingGround(site + rotation * new Vector3(-1.25f, 0f, -1.7f));
            var rearRight = LandingGround(site + rotation * new Vector3(1.25f, 0f, -1.7f));

            var front = (frontLeft + frontRight) * 0.5f;
            var rear = (rearLeft + rearRight) * 0.5f;
            var left = (frontLeft + rearLeft) * 0.5f;
            var rightSide = (frontRight + rearRight) * 0.5f;

            _groundPitch = Mathf.Clamp(Mathf.Atan2(rear - front, 4f) * Mathf.Rad2Deg, -8f, 8f);
            _groundRoll = Mathf.Clamp(Mathf.Atan2(rightSide - left, 2.5f) * Mathf.Rad2Deg, -8f, 8f);
            var groundY = (frontLeft + frontRight + rearLeft + rearRight) * 0.25f + 0.02f;
            LandingZone = new Vector3(site.x, groundY, site.z);
        }

        private static float LandingGround(Vector3 point)
        {
            var terrain = TransportGround.TerrainHeight(point);
            var surface = TransportGround.SurfaceHeight(point);
            return surface > terrain - 0.5f && surface <= terrain + 0.6f ? Mathf.Max(terrain, surface) : terrain;
        }

        /// <summary>Rota boyunca güvenli irtifa profili (yalnızca yükselten eğim sınırlarıyla yumuşatılır).</summary>
        private void BuildProfile()
        {
            var count = _path.Count;
            var raw = new float[count];
            for (var i = 0; i < count; i++)
            {
                var point = _path.PointAt(i);
                var direction = _path.DirectionAt(_path.DistanceAt(i), PathSpacing);
                var side = new Vector3(direction.z, 0f, -direction.x) * 7f;
                raw[i] = Mathf.Max(ObstacleHeight(point), Mathf.Max(ObstacleHeight(point + side), ObstacleHeight(point - side)));
            }

            var back = 6;
            var ahead = Mathf.CeilToInt(140f / PathSpacing);
            _profile = new float[count];
            for (var i = 0; i < count; i++)
            {
                var windowMax = float.MinValue;
                for (var j = Mathf.Max(0, i - back); j <= Mathf.Min(count - 1, i + ahead); j++)
                    windowMax = Mathf.Max(windowMax, raw[j]);

                var nearMax = float.MinValue;
                for (var j = Mathf.Max(0, i - 2); j <= Mathf.Min(count - 1, i + 3); j++)
                    nearMax = Mathf.Max(nearMax, raw[j]);

                var remaining = _path.Length - _path.DistanceAt(i);
                var cruise = Mathf.Max(raw[i] + _altitude, windowMax + ObstacleClearance);
                var glide = LandingZone.y + HoverHeight + 2f + remaining * GlideSlope;
                _profile[i] = Mathf.Max(Mathf.Min(cruise, glide), nearMax + ApproachClearance);
            }

            const float climbSlope = 0.35f;
            const float descentSlope = 0.55f;
            for (var i = count - 2; i >= 0; i--)
                _profile[i] = Mathf.Max(_profile[i], _profile[i + 1] - climbSlope * PathSpacing);
            for (var i = 1; i < count; i++)
                _profile[i] = Mathf.Max(_profile[i], _profile[i - 1] - descentSlope * PathSpacing);
        }

        /// <summary>Arazi + üzerindeki engel (ağaç/yapı/kaya) yüksekliği; aşırı uç değerler sınırlandırılır.</summary>
        private static float ObstacleHeight(Vector3 point)
        {
            var terrain = TransportGround.TerrainHeight(point);
            var surface = TransportGround.SurfaceHeight(point);
            return Mathf.Max(terrain, Mathf.Min(surface, terrain + 40f));
        }

        private void BuildModel()
        {
            _model = new GameObject("Model").transform;
            _model.gameObject.layer = GameLayers.Vehicle;
            _model.SetParent(transform, false);

            _dustFailed = IsHeadless;
            if (!IsHeadless)
                BuildVisuals();

            // Mermiyi durduran basit gövde çarpıştırıcıları (Vehicle katmanı, kinematik).
            var body = CreateKinematicBody(transform);
            CreateBoxCollider(body, new Vector3(0f, 1.65f, 0.2f), new Vector3(2.35f, 1.95f, 4.5f), "Kabin");
            CreateBoxCollider(body, new Vector3(0f, 1.3f, 3.6f), new Vector3(1.9f, 1.1f, 2.3f), "Burun");
            CreateBoxCollider(body, new Vector3(0f, 2.92f, 0.1f), new Vector3(1.4f, 0.55f, 3.8f), "Motor");
            CreateBoxCollider(body, new Vector3(0f, 2.3f, -6.2f), new Vector3(0.7f, 0.6f, 5.6f), "KuyrukKirisi");
            CreateBoxCollider(body, new Vector3(0f, 3.1f, -9.15f), new Vector3(0.2f, 1.7f, 0.9f), "Dikme");

            _obstacle = CreateParkingObstacle(new Vector3(0f, 1.4f, 0.9f), new Vector3(2.6f, 2.8f, 6.6f));
        }

        private void BuildVisuals()
        {
            GameObject overrideVisual = null;
            if (Project.Infrastructure.Content.ContentOverrides.TryGetHelicopter(out var prefab))
                overrideVisual = InstantiateVisualOverride(prefab, _model, "GovdeHazir");

            if (overrideVisual != null)
            {
                _overrideRoot = overrideVisual;
                _mainRotor = VehicleSockets.Find(overrideVisual, Project.Application.Services.VehicleSocketRules.MainRotorNames());
                _tailRotor = VehicleSockets.Find(overrideVisual, Project.Application.Services.VehicleSocketRules.TailRotorNames());
                VehicleSockets.SnapSeats(overrideVisual, Seats);
                VehicleSockets.SetLights(overrideVisual, true);
                SetupOverrideBlur(overrideVisual);
                if (_mainRotor != null && _tailRotor != null)
                    return;
            }
            else
            {
                CreateVisual("Govde", _model, HelicopterModel.Body, HelicopterModel.BodyMaterials(), true);
            }

            if (_mainRotor == null)
            {
                _mainRotor = new GameObject("AnaRotor").transform;
                _mainRotor.gameObject.layer = GameLayers.Vehicle;
                _mainRotor.SetParent(_model, false);
                _mainRotor.localPosition = HelicopterModel.MainRotorHub;
                CreateVisual("Pervane", _mainRotor, HelicopterModel.MainRotor, HelicopterModel.RotorMaterials(), true);

                var disc = CreateVisual("RotorDiski", _model, HelicopterModel.RotorDisc, HelicopterModel.DiscMaterials(), false);
                disc.transform.localPosition = HelicopterModel.MainRotorHub + Vector3.up * 0.03f;
                disc.receiveShadows = false;
                _rotorDisc = disc;
            }

            if (_tailRotor == null)
            {
                _tailRotor = new GameObject("KuyrukRotor").transform;
                _tailRotor.gameObject.layer = GameLayers.Vehicle;
                _tailRotor.SetParent(_model, false);
                _tailRotor.localPosition = HelicopterModel.TailRotorHub;
                CreateVisual("KuyrukPervane", _tailRotor, HelicopterModel.TailRotor, HelicopterModel.RotorMaterials(), false);
            }
        }

        // ------------------------------------------------------------------ TransportVehicle

        public override Transform GetSeat(int index) => SeatAt(index);

        public override Transform PassengerViewPoint(int index) => ViewPointAt(index);

        public override Vector3 GetDisembarkPoint(int index)
        {
            var local = DisembarkLocal != null && DisembarkLocal.Length > 0 ? DisembarkLocal[NormalizeSeatIndex(index)] : Vector3.right * 3.3f;
            return ResolveDisembarkPoint(index, new Vector3(local.x >= 0f ? 1f : -1f, 0f, 0f));
        }

        public override void Begin()
        {
            if (HasBegun || _path == null)
                return;

            MarkBegun();
            SetState(FlightState.Cruise);
            _speed = Mathf.Max(_speed, 2f);
        }

        public override void ReleasePassengers()
        {
            RequestRelease();
        }

        // ------------------------------------------------------------------ simulation

        private void Update()
        {
            if (_path == null)
                return;

            var dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (dt <= 0f)
                return;

            _stateTime += dt;
            switch (_state)
            {
                case FlightState.Holding:
                    TickHolding(dt);
                    break;
                case FlightState.Cruise:
                    TickCruise(dt);
                    break;
                case FlightState.Descend:
                    TickDescend(dt);
                    break;
                case FlightState.Hover:
                    TickHover(dt);
                    break;
                case FlightState.Touchdown:
                    TickTouchdown(dt);
                    break;
                case FlightState.Grounded:
                    TickGrounded(dt);
                    break;
                case FlightState.LiftOff:
                    TickLiftOff(dt);
                    break;
                case FlightState.Depart:
                    TickDepart(dt);
                    break;
            }

            ApplyPose();
            UpdateRotors(dt);
            UpdateAudio(dt);
            UpdateDust(dt);
            UpdateRotorWash(dt);

            var position = transform.position;
            if (_hasLastPosition)
                Velocity = (position - _lastPosition) / dt;
            _lastPosition = position;
            _hasLastPosition = true;

            if (IsDeparting)
                TickDestroyTimer();
        }

        private void SetState(FlightState state)
        {
            _state = state;
            _stateTime = 0f;
        }

        private void TickHolding(float dt)
        {
            _bobPhase += dt;
            _horizontal = _path.Start;
            var targetY = _path.SampleTable(_profile, 0f) + Mathf.Sin(_bobPhase * 0.9f) * 0.35f;
            _y = Mathf.Lerp(_y, targetY, 1f - Mathf.Exp(-2f * dt));
            _yaw = Mathf.SmoothDampAngle(_yaw, YawOf(_path.DirectionAt(0f, PathSpacing)), ref _yawVelocity, 1f);
            Settle(Mathf.Sin(_bobPhase * 0.6f) * 1.2f, Mathf.Sin(_bobPhase * 0.43f) * 1.5f, dt, 2f);
        }

        private void TickCruise(float dt)
        {
            var remaining = _path.Length - _s;
            if (remaining < LandingPhaseDistance && Phase == TransportPhase.EnRoute)
                Phase = TransportPhase.Landing;

            // Hız: seyir, durma eğrisi ve önümüzdeki alçalma ihtiyacı.
            var target = CruiseSpeed;
            target = Mathf.Min(target, Mathf.Sqrt(2f * Deceleration * Mathf.Max(0f, remaining - 0.5f)) + 0.6f);
            var aheadDistance = 40f;
            var drop = _y - _path.SampleTable(_profile, _s + aheadDistance);
            if (drop > 2f)
                target = Mathf.Min(target, Mathf.Max(8f, aheadDistance * DescentRate / drop));

            _previousSpeed = _speed;
            var rate = target > _speed ? Acceleration : Deceleration * 1.35f;
            _speed = Mathf.MoveTowards(_speed, target, rate * dt);
            _s = Mathf.Min(_path.Length, _s + _speed * dt);
            _horizontal = _path.PositionAt(_s);

            // Yön + yatış (dönüş hızından) + burun (ivmeden).
            var look = Mathf.Clamp(_speed * 0.6f, 3f, 14f);
            var desiredYaw = YawOf(_path.DirectionAt(_s + look, 4f));
            _yaw = Mathf.SmoothDampAngle(_yaw, desiredYaw, ref _yawVelocity, 0.55f, 90f, dt);
            var bank = -Mathf.Atan(_speed * _yawVelocity * Mathf.Deg2Rad / 9.81f) * Mathf.Rad2Deg;
            bank = Mathf.Clamp(bank, -MaxBankDegrees, MaxBankDegrees);
            var acceleration = (_speed - _previousSpeed) / dt;
            var pitch = Mathf.Clamp(acceleration * 2.2f + _speed / CruiseSpeed * 4f, -14f, 12f);
            Settle(pitch, bank, dt, 2.5f);

            // Dikey: profil + ileri bakış; arazi tabanı.
            var lookAhead = _s + Mathf.Max(10f, _speed * 1.2f);
            var targetY = Mathf.Max(_path.SampleTable(_profile, _s), _path.SampleTable(_profile, lookAhead));
            TrackAltitude(targetY, dt, ClimbRate, DescentRate);
            ApplyTerrainFloor();

            if (_s >= _path.Length - 0.05f)
            {
                _s = _path.Length;
                _speed = 0f;
                _horizontal = _path.End;
                Phase = TransportPhase.Landing;
                SetState(FlightState.Descend);
            }
        }

        private void TickDescend(float dt)
        {
            _horizontal = new Vector3(LandingZone.x, 0f, LandingZone.z);
            var hoverY = LandingZone.y + HoverHeight;
            var gap = _y - hoverY;
            var desired = -Mathf.Clamp(gap * 0.7f, 0.6f, DescentRate);
            _verticalSpeed = Mathf.MoveTowards(_verticalSpeed, desired, VerticalAcceleration * dt);
            _y += _verticalSpeed * dt;

            _yaw = Mathf.SmoothDampAngle(_yaw, _finalYaw, ref _yawVelocity, 0.9f, 60f, dt);
            Settle(-2f * Mathf.Clamp01(gap / 10f), 0f, dt, 1.6f);

            if (_y <= hoverY + 0.03f)
            {
                _y = hoverY;
                _verticalSpeed = 0f;
                SetState(FlightState.Hover);
            }
        }

        private void TickHover(float dt)
        {
            _bobPhase += dt;
            var hoverY = LandingZone.y + HoverHeight;
            _y = hoverY + Mathf.Sin(_bobPhase * 2.1f) * 0.06f;
            _yaw = Mathf.SmoothDampAngle(_yaw, _finalYaw, ref _yawVelocity, 0.6f, 60f, dt);
            Settle(_groundPitch * 0.5f, _groundRoll * 0.5f, dt, 2f);

            if (_stateTime >= HoverHoldSeconds)
                SetState(FlightState.Touchdown);
        }

        private void TickTouchdown(float dt)
        {
            _y = Mathf.MoveTowards(_y, LandingZone.y, TouchdownSpeed * dt);
            _yaw = Mathf.SmoothDampAngle(_yaw, _finalYaw, ref _yawVelocity, 0.4f, 60f, dt);
            Settle(_groundPitch, _groundRoll, dt, 4f);

            if (_y <= LandingZone.y + 0.001f)
            {
                _y = LandingZone.y;
                _yaw = _finalYaw;
                _pitch = _groundPitch;
                _roll = _groundRoll;
                _yawVelocity = 0f;
                SetState(FlightState.Grounded);
                SetObstacle(true);
                SafeDust(new Vector3(LandingZone.x, LandingZone.y + 0.1f, LandingZone.z), 2.2f);
                MarkArrived();
            }
        }

        private void TickGrounded(float dt)
        {
            TickUnloading(dt);
            var sinceRelease = SecondsSinceRelease;
            if (sinceRelease >= ReleaseToLiftOffSeconds)
                BeginDeparture();
        }

        private void BeginDeparture()
        {
            SetObstacle(false);

            var center = TransportGround.MapCenter;
            var outward = new Vector3(LandingZone.x - center.x, 0f, LandingZone.z - center.y);
            if (outward.sqrMagnitude < 50f * 50f)
                outward = -(Quaternion.Euler(0f, _finalYaw, 0f) * Vector3.forward);
            _departYaw = YawOf(outward);
            _departTargetY = LandingZone.y + _altitude;
            _departSampleTimer = 0f;
            _speed = 0f;
            _verticalSpeed = 0f;

            MarkDeparting();
            SetState(FlightState.LiftOff);
        }

        private void TickLiftOff(float dt)
        {
            var agl = _y - LandingZone.y;
            _verticalSpeed = Mathf.MoveTowards(_verticalSpeed, agl < 2f ? 2.5f : 5f, VerticalAcceleration * dt);
            _y += _verticalSpeed * dt;

            if (agl > 4f)
                _yaw = Mathf.MoveTowardsAngle(_yaw, _departYaw, 30f * dt);

            var yawError = Mathf.DeltaAngle(_yaw, _departYaw);
            Settle(agl > 4f ? 2f : _groundPitch * Mathf.Clamp01(1f - agl), agl > 4f ? Mathf.Clamp(-yawError * 0.15f, -10f, 10f) : _groundRoll * Mathf.Clamp01(1f - agl), dt, 2f);

            if (agl >= LiftOffHeight)
                SetState(FlightState.Depart);
        }

        private void TickDepart(float dt)
        {
            _previousSpeed = _speed;
            _speed = Mathf.MoveTowards(_speed, DepartSpeed, Acceleration * dt);

            _yaw = Mathf.SmoothDampAngle(_yaw, _departYaw, ref _yawVelocity, 1.2f, 35f, dt);
            var forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            _horizontal += forward * (_speed * dt);
            _horizontal.y = 0f;

            var bank = Mathf.Clamp(-Mathf.Atan(_speed * _yawVelocity * Mathf.Deg2Rad / 9.81f) * Mathf.Rad2Deg, -MaxBankDegrees, MaxBankDegrees);
            var acceleration = (_speed - _previousSpeed) / dt;
            Settle(Mathf.Clamp(acceleration * 2.2f + _speed / CruiseSpeed * 4f, -10f, 12f), bank, dt, 2f);

            _departSampleTimer -= dt;
            if (_departSampleTimer <= 0f)
            {
                _departSampleTimer = 0.3f;
                var here = TransportGround.TerrainHeight(_horizontal);
                var near = TransportGround.TerrainHeight(_horizontal + forward * 60f);
                var far = TransportGround.TerrainHeight(_horizontal + forward * 140f);
                _departTargetY = Mathf.Max(here, Mathf.Max(near, far)) + _altitude;
            }

            TrackAltitude(_departTargetY, dt, ClimbRate, DescentRate * 0.5f);
            ApplyTerrainFloor();
        }

        private void TrackAltitude(float targetY, float dt, float climb, float descent)
        {
            var desired = Mathf.Clamp((targetY - _y) * 0.9f, -descent, climb);
            _verticalSpeed = Mathf.MoveTowards(_verticalSpeed, desired, VerticalAcceleration * dt);
            _y += _verticalSpeed * dt;
        }

        private void ApplyTerrainFloor()
        {
            var floor = TransportGround.TerrainHeight(_horizontal) + MinimumTerrainClearance;
            if (_y < floor)
            {
                _y = floor;
                if (_verticalSpeed < 0f)
                    _verticalSpeed = 0f;
            }
        }

        private void Settle(float pitch, float roll, float dt, float sharpness)
        {
            var t = 1f - Mathf.Exp(-sharpness * dt);
            _pitch = Mathf.Lerp(_pitch, pitch, t);
            _roll = Mathf.Lerp(_roll, roll, t);
        }

        /// <summary>Gövdeyi kabin ortasındaki pivot etrafında döndürerek konumlar (kökte kızak altı).</summary>
        private void ApplyPose()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, _roll);
            var pivot = new Vector3(_horizontal.x, _y + PivotHeight, _horizontal.z);
            transform.SetPositionAndRotation(pivot - rotation * PivotLocal, rotation);
        }

        private void SetObstacle(bool enabled)
        {
            if (_obstacle != null)
                _obstacle.enabled = enabled;
        }

        // ------------------------------------------------------------------ presentation

        /// <summary>Override Rotor_Blur diski varsa bulanık disk geçişi için bağlar (kanatlar yüksek devirde gizlenir).</summary>
        private void SetupOverrideBlur(GameObject root)
        {
            var blurTransform = VehicleSockets.Find(root, Project.Application.Services.VehicleSocketRules.BlurNames());
            var blur = blurTransform != null ? blurTransform.GetComponent<Renderer>() : null;
            if (blur == null || _mainRotor == null)
                return;
            _rotorDisc = blur;
            blur.enabled = false;
            _bladeRenderers = VehicleSockets.BladeRenderers(_mainRotor, blur);
        }

        /// <summary>Override araç materyal durumu (temiz/kirli/yanmış); prosedürel modelde etkisizdir.</summary>
        public void SetCondition(Project.Application.Services.VehicleCondition condition)
        {
            VehicleSockets.ApplyCondition(_overrideRoot, condition);
        }

        private void UpdateRotors(float dt)
        {
            if (_mainRotor == null)
                return;

            _rotorAngle = Mathf.Repeat(_rotorAngle + RotorDegreesPerSecond * _rotorSpin * dt, 360f);
            _tailAngle = Mathf.Repeat(_tailAngle + RotorDegreesPerSecond * TailRotorFactor * _rotorSpin * dt, 360f);
            _mainRotor.localRotation = Quaternion.Euler(0f, _rotorAngle, 0f);
            if (_tailRotor != null)
                _tailRotor.localRotation = Quaternion.Euler(_tailAngle, 0f, 0f);

            if (_rotorDisc != null)
            {
                var show = _bladeRenderers != null
                    ? Project.Application.Services.VehicleSocketRules.BlurVisible(_rotorSpin, _blurShown)
                    : _rotorSpin > 0.6f;
                if (_rotorDisc.enabled != show)
                    _rotorDisc.enabled = show;
                if (_bladeRenderers != null && _blurShown != show)
                {
                    _blurShown = show;
                    for (var i = 0; i < _bladeRenderers.Length; i++)
                        if (_bladeRenderers[i] != null)
                            _bladeRenderers[i].enabled = !show;
                }
            }
        }

        private void UpdateAudio(float dt)
        {
            var load = Mathf.Clamp(_verticalSpeed / ClimbRate, -1f, 1f) * 0.06f;
            var speed = Mathf.Clamp01(_speed / CruiseSpeed) * 0.05f;
            var pitch = _state == FlightState.Grounded ? 0.94f : 1f + load + speed;
            UpdateLoopAudio(pitch, dt);
        }

        private float _washTimer;

        /// <summary>Zemine 15 m'den yakınken rotor akışı halkası (kısılmış, GPU VFX önce).</summary>
        private void UpdateRotorWash(float dt)
        {
            if (_dustFailed || _rotorSpin < 0.5f)
                return;
            _washTimer -= dt;
            if (_washTimer > 0f)
                return;
            _washTimer = 0.4f;
            try
            {
                var ground = TransportGround.TerrainHeight(_horizontal);
                if (_y - ground >= 15f || _y - ground < -2f)
                    return;
                GameVfx.RotorWash(new Vector3(_horizontal.x, ground + 0.1f, _horizontal.z), 7f);
            }
            catch (Exception e)
            {
                _dustFailed = true;
                Debug.LogException(e, this);
            }
        }

        private void UpdateDust(float dt)
        {
            if (_dustFailed || _rotorSpin < 0.5f)
                return;

            var ground = TransportGround.TerrainHeight(_horizontal);
            var agl = _y - ground;
            if (agl > DustHeight || agl < -2f)
                return;

            _dustTimer -= dt;
            if (_dustTimer > 0f)
                return;

            var t = Mathf.Clamp01(agl / DustHeight);
            _dustTimer = _state == FlightState.Grounded ? 0.35f : Mathf.Lerp(0.09f, 0.3f, t);

            var angle = UnityEngine.Random.value * Mathf.PI * 2f;
            var radius = UnityEngine.Random.Range(2.5f, 7.5f);
            var point = _horizontal + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            point.y = TransportGround.TerrainHeight(point) + 0.1f;
            SafeDust(point, Mathf.Lerp(1.8f, 0.5f, t));
        }

        private void SafeDust(Vector3 position, float scale)
        {
            if (_dustFailed)
                return;

            try
            {
                GameVfx.Dust(position, scale);
            }
            catch (Exception exception)
            {
                _dustFailed = true;
                Debug.LogException(exception, this);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static float YawOf(Vector3 direction)
        {
            return direction.sqrMagnitude > 1e-8f ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg : 0f;
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                var h = (uint)(value * 73856093) ^ 0x9E3779B9u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}
