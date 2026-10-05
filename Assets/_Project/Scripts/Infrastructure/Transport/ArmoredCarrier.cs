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
    /// Kirpi zırhlı personel taşıyıcı (MRAP). Plan başlangıcında rölantide bekler; Begin() ile araziyi izleyerek
    /// (dört teker zemin örneklemesi → yükseklik, yunuslama, yatış) iniş noktasına sürer (~12 m/s; virajda, yokuşta ve
    /// sona yaklaşırken yavaşlar), durur, arka kapıyı açar → Arrived. Bırakmadan sonra kapıyı kapatır, U dönüşü yapıp
    /// geldiği yoldan bölgeden ayrılır → Departed; 40 sn sonra yok edilir.
    /// <para>
    /// Rota oluşturma anında bir kez seçilir: birkaç kavisli aday + (varsa) NavMesh yolu; eğim, su, engel ve harita dışı
    /// maliyetine göre en iyisi. Sürüş kinematiktir (fizik yok) ve karede tahsis yapmaz.
    /// </para>
    /// </summary>
    public sealed class ArmoredCarrier : TransportVehicle
    {
        public const float MaxSpeed = 12f;

        private const float Acceleration = 2.6f;
        private const float Braking = 4.5f;
        private const float StopDeceleration = 2.4f;
        private const float LateralAccelerationLimit = 3.2f;
        private const float DoorOpenSeconds = 1.1f;
        private const float DoorCloseSeconds = 0.9f;
        private const float DepartAfterReleaseSeconds = 3.5f;
        private const float PathSpacing = 1f;
        private const float RouteSampleSpacing = 4f;
        private const float UTurnRadius = 8f;
        private const float LandingFootprint = 4.5f;
        private const float LandingSearchRadius = 40f;
        private const float MaxSteerDegrees = 32f;

        private enum DriveState
        {
            Waiting,
            Driving,
            DoorOpening,
            Parked,
            DoorClosing,
            Leaving
        }

        private DriveState _state = DriveState.Waiting;
        private TransportPath _arrivalPath;
        private TransportPath _path;
        private float _s;
        private float _speed;
        private float _previousSpeed;
        private float _yaw;
        private float _yawVelocity;
        private float _y;
        private float _yVelocity;
        private float _pitch;
        private float _roll;
        private float _steer;
        private float _wheelSpin;
        private float _door01;
        private float _stateTime;
        private float _turretTime;
        private float _dustTimer;
        private bool _dustLeft;
        private bool _dustFailed;
        private bool _tan;
        private Vector3 _horizontal;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;

        private Transform _model;
        private Transform[] _wheels;
        private Transform _door;
        private Transform _turret;
        private NavMeshObstacle _obstacle;

        public override string DisplayName => "Kirpi Zırhlı Aracı";

        /// <summary>Anlık hız (m/s).</summary>
        public float Speed => _speed;

        /// <summary>Arka kapı açıklığı (0 kapalı, 1 açık).</summary>
        public float DoorOpenAmount => _door01;

        public override float EstimatedSecondsToArrival
        {
            get
            {
                if (HasArrived)
                    return 0f;

                var remaining = 0f;
                if (_state == DriveState.Waiting || _state == DriveState.Driving)
                    remaining = _path != null ? Mathf.Max(0f, _path.Length - _s) : DistanceToLandingZone;

                return remaining / Mathf.Max(MaxSpeed * 0.6f, _speed) + DoorOpenSeconds * (1f - _door01);
            }
        }

        // ------------------------------------------------------------------ creation

        /// <summary>Plan için Kirpi kurar (yolcu koltukları hazır, Begin bekler).</summary>
        public static ArmoredCarrier Create(TeamInsertion plan)
        {
            var go = new GameObject("Kirpi Zırhlı Aracı - Tim " + (plan.Team + 1));
            go.layer = GameLayers.Vehicle;
            var carrier = go.AddComponent<ArmoredCarrier>();
            carrier.Setup(plan);
            return carrier;
        }

        private void Setup(TeamInsertion plan)
        {
            Method = InsertionMethod.ArmoredVehicle;
            Team = plan.Team;
            _tan = ((plan.Team % 4) + 4) % 4 == 2;

            KirpiModel.GetSeatLayout(out var seats, out var seatYaws, out var views, out var viewEulers, out var disembark, out var disembarkYaws);
            CreateSeats(seats, seatYaws, views, viewEulers);
            DisembarkLocal = disembark;
            DisembarkYawLocal = disembarkYaws;

            try
            {
                BuildArrivalRoute(plan);
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

            _path = _arrivalPath;
            _s = 0f;
            _horizontal = _path.Start;
            _yaw = YawOf(_path.DirectionAt(0f, 2f));
            _y = TransportGround.TerrainHeight(_horizontal);
            SolveGroundPose(1f, true);
            ApplyPose();
            _lastPosition = transform.position;
            _hasLastPosition = true;

            RequestLoopAudio(SoundId.VehicleEngine, 0.9f, 160f);
        }

        // ------------------------------------------------------------------ route

        private void BuildArrivalRoute(TeamInsertion plan)
        {
            var center = TransportGround.MapCenter;
            var limit = Mathf.Max(20f, TransportGround.MapHalfSize - 6f);

            var start = ToVector3(plan.Start);
            var planned = ToVector3(plan.LandingZone);
            start.y = 0f;
            planned.y = 0f;

            var flat = planned - start;
            if (flat.sqrMagnitude < 15f * 15f)
            {
                var outward = new Vector3(planned.x - center.x, 0f, planned.z - center.y);
                if (outward.sqrMagnitude < 1f)
                    outward = Vector3.back;
                start = planned + outward.normalized * 120f;
            }

            start.x = Mathf.Clamp(start.x, center.x - limit, center.x + limit);
            start.z = Mathf.Clamp(start.z, center.y - limit, center.y + limit);

            // Kenar sırtlarının dik yamacında başlamasın: başlangıcın biraz içinde en yakın düz ve açık yeri seç.
            var inward = planned - start;
            inward.y = 0f;
            if (inward.sqrMagnitude > 1f)
            {
                var inwardDistance = inward.magnitude;
                var startSite = TransportGround.FindLandingSite(start + inward / inwardDistance * Mathf.Min(15f, inwardDistance * 0.2f), 4f, 45f, 2.2f, 3.2f);
                startSite.y = 0f;
                if ((planned - startSite).sqrMagnitude > 20f * 20f)
                    start = startSite;
            }

            StartPosition = new Vector3(start.x, TransportGround.TerrainHeight(start), start.z);
            PlannedLandingZone = new Vector3(planned.x, TransportGround.TerrainHeight(planned), planned.z);

            var site = TransportGround.FindLandingSite(planned, LandingFootprint, LandingSearchRadius, 1.6f, 3.2f);
            site.y = 0f;

            var toSite = site - start;
            toSite.y = 0f;
            var distance = Mathf.Max(1f, toSite.magnitude);
            var forward = toSite / distance;
            var right = new Vector3(forward.z, 0f, -forward.x);
            var segments = Mathf.Clamp(Mathf.CeilToInt(distance / 4f), 8, 200);

            List<Vector3> best = null;
            var bestCost = float.MaxValue;
            var candidate = new List<Vector3>(segments + 2);
            float[] offsets = { 0f, 0.15f, -0.15f, 0.3f, -0.3f, 0.45f, -0.45f };
            for (var i = 0; i < offsets.Length; i++)
            {
                candidate.Clear();
                var control = (start + site) * 0.5f + right * (offsets[i] * distance);
                TransportPath.AppendBezier(candidate, start, control, site, segments, true);
                var cost = EvaluateRoute(candidate) + Mathf.Abs(offsets[i]) * 4f;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = new List<Vector3>(candidate);
                }
            }

            if (TryBuildNavMeshRoute(start, site, candidate))
            {
                var cost = EvaluateRoute(candidate);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = new List<Vector3>(candidate);
                }
            }

            if (best == null || best.Count < 2)
            {
                best = new List<Vector3>(2) { start, site };
            }

            _arrivalPath = TransportPath.Resample(best, PathSpacing);
            var end = _arrivalPath.End;
            LandingZone = new Vector3(end.x, TransportGround.TerrainHeight(end), end.z);
        }

        private void BuildFallbackRoute(TeamInsertion plan)
        {
            var start = ToVector3(plan.Start);
            var planned = ToVector3(plan.LandingZone);
            start.y = 0f;
            planned.y = 0f;
            if ((planned - start).sqrMagnitude < 1f)
                start = planned + Vector3.back * 60f;

            _arrivalPath = TransportPath.Resample(new List<Vector3>(2) { start, planned }, PathSpacing);
            StartPosition = start;
            PlannedLandingZone = planned;
            LandingZone = new Vector3(planned.x, SafeTerrainHeight(planned), planned.z);
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

        /// <summary>NavMesh yolu (varsa, tamamsa) — köşeler yumuşatılır. Başarısızsa false.</summary>
        private static bool TryBuildNavMeshRoute(Vector3 start, Vector3 site, List<Vector3> output)
        {
            output.Clear();
            var startProbe = new Vector3(start.x, TransportGround.TerrainHeight(start), start.z);
            var siteProbe = new Vector3(site.x, TransportGround.TerrainHeight(site), site.z);
            if (!NavMesh.SamplePosition(startProbe, out var a, 15f, NavMesh.AllAreas))
                return false;
            if (!NavMesh.SamplePosition(siteProbe, out var b, 15f, NavMesh.AllAreas))
                return false;

            var navPath = new NavMeshPath();
            if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, navPath) || navPath.status != NavMeshPathStatus.PathComplete)
                return false;

            var corners = navPath.corners;
            if (corners == null || corners.Length < 2)
                return false;

            output.Add(start);
            for (var i = 0; i < corners.Length; i++)
                output.Add(corners[i]);
            output.Add(site);
            TransportPath.RemoveDuplicates(output, 2f);
            TransportPath.Smooth(output, 3);
            return output.Count >= 2;
        }

        /// <summary>Rota maliyeti: uzunluk + dik eğim + su + engel + harita dışı.</summary>
        private static float EvaluateRoute(List<Vector3> points)
        {
            var path = TransportPath.Resample(points, RouteSampleSpacing);
            var cost = path.Length * 0.03f;
            var water = TransportGround.WaterLevel;
            var center = TransportGround.MapCenter;
            var limit = TransportGround.MapHalfSize - 4f;
            var previous = TransportGround.TerrainHeight(path.Start);
            for (var i = 1; i < path.Count; i++)
            {
                var point = path.PointAt(i);
                var height = TransportGround.TerrainHeight(point);
                var grade = Mathf.Abs(height - previous) / Mathf.Max(0.5f, path.Spacing);
                if (grade > 0.18f)
                    cost += (grade - 0.18f) * 60f;
                if (grade > 0.45f)
                    cost += 25f;

                if (height < water + 0.3f)
                    cost += 18f;

                if (Mathf.Abs(point.x - center.x) > limit || Mathf.Abs(point.z - center.y) > limit)
                    cost += 30f;

                var direction = path.DirectionAt(path.DistanceAt(i), RouteSampleSpacing);
                var side = new Vector3(direction.z, 0f, -direction.x) * 1.3f;
                if (IsObstructed(point, height))
                    cost += 5f;
                if (IsObstructed(point + side, TransportGround.TerrainHeight(point + side)))
                    cost += 5f;
                if (IsObstructed(point - side, TransportGround.TerrainHeight(point - side)))
                    cost += 5f;

                previous = height;
            }

            return cost;
        }

        private static bool IsObstructed(Vector3 point, float terrain)
        {
            var surface = TransportGround.SurfaceHeight(point);
            return surface > terrain + 0.6f;
        }

        /// <summary>Ayrılış rotası: LZ'den U dönüşü, geliş yolunun tersinden başlangıca ve harita kenarının dışına.</summary>
        private TransportPath BuildDepartureRoute()
        {
            var lz = new Vector3(LandingZone.x, 0f, LandingZone.z);
            var forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            var right = new Vector3(forward.z, 0f, -forward.x);

            var side = TurnCost(lz, forward, right, 1f) <= TurnCost(lz, forward, right, -1f) ? 1f : -1f;
            var points = new List<Vector3>(256);
            var center = lz + right * (side * UTurnRadius);
            const int arcSteps = 14;
            for (var i = 0; i <= arcSteps; i++)
            {
                var theta = i / (float)arcSteps * Mathf.PI;
                points.Add(center + (-right * (side * Mathf.Cos(theta)) + forward * Mathf.Sin(theta)) * UTurnRadius);
            }

            var arcEnd = lz + right * (side * UTurnRadius * 2f);
            points.Add(arcEnd - forward * 10f);

            var arrival = _arrivalPath;
            if (arrival != null && arrival.Length > 40f)
            {
                for (var s = arrival.Length - 30f; s > 0f; s -= 6f)
                    points.Add(arrival.PositionAt(s));
                points.Add(arrival.Start);

                var exitDirection = arrival.Start - arrival.PositionAt(Mathf.Min(20f, arrival.Length));
                exitDirection.y = 0f;
                if (exitDirection.sqrMagnitude > 0.01f)
                    points.Add(arrival.Start + exitDirection.normalized * 90f);
            }
            else
            {
                var outward = -forward;
                points.Add(arcEnd + outward * 120f);
            }

            TransportPath.RemoveDuplicates(points, 1.5f);
            TransportPath.Smooth(points, 2);
            return TransportPath.Resample(points, PathSpacing);
        }

        private static float TurnCost(Vector3 lz, Vector3 forward, Vector3 right, float side)
        {
            var center = lz + right * (side * UTurnRadius);
            var cost = 0f;
            var water = TransportGround.WaterLevel;
            var previous = TransportGround.TerrainHeight(lz);
            for (var i = 1; i <= 6; i++)
            {
                var theta = i / 6f * Mathf.PI;
                var point = center + (-right * (side * Mathf.Cos(theta)) + forward * Mathf.Sin(theta)) * UTurnRadius;
                var height = TransportGround.TerrainHeight(point);
                cost += Mathf.Abs(height - previous) * 2f;
                if (IsObstructed(point, height))
                    cost += 10f;
                if (height < water + 0.3f)
                    cost += 10f;
                previous = height;
            }

            return cost;
        }

        // ------------------------------------------------------------------ model

        private void BuildModel()
        {
            _model = new GameObject("Model").transform;
            _model.gameObject.layer = GameLayers.Vehicle;
            _model.SetParent(transform, false);

            _dustFailed = IsHeadless;
            if (!IsHeadless)
                BuildVisuals();

            var body = CreateKinematicBody(transform);
            CreateBoxCollider(body, new Vector3(0f, 2.04f, -1.2f), new Vector3(2.5f, 1.68f, 4.2f), "Bolme");
            CreateBoxCollider(body, new Vector3(0f, 2.42f, 1.18f), new Vector3(2.3f, 0.86f, 0.56f), "Kabin");
            CreateBoxCollider(body, new Vector3(0f, 1.6f, 2.42f), new Vector3(2.36f, 0.8f, 1.9f), "Kaput");
            CreateBoxCollider(body, new Vector3(0f, 0.85f, 0.05f), new Vector3(1.7f, 0.75f, 6.5f), "AltGovde");
            CreateBoxCollider(body, new Vector3(-KirpiModel.WheelTrack, KirpiModel.WheelRadius, 0.05f), new Vector3(0.44f, KirpiModel.WheelRadius * 2f, 4.7f), "TekerSol");
            CreateBoxCollider(body, new Vector3(KirpiModel.WheelTrack, KirpiModel.WheelRadius, 0.05f), new Vector3(0.44f, KirpiModel.WheelRadius * 2f, 4.7f), "TekerSag");

            _obstacle = CreateParkingObstacle(new Vector3(0f, 1.4f, 0.05f), new Vector3(2.7f, 2.8f, 7.0f));
        }

        private void BuildVisuals()
        {
            var materials = KirpiModel.Materials(_tan);
            CreateVisual("Govde", _model, KirpiModel.Body, materials, true);

            _wheels = new Transform[KirpiModel.WheelPositions.Length];
            for (var i = 0; i < _wheels.Length; i++)
            {
                var wheel = new GameObject("Teker_" + i).transform;
                wheel.gameObject.layer = GameLayers.Vehicle;
                wheel.SetParent(_model, false);
                wheel.localPosition = KirpiModel.WheelPositions[i];
                CreateVisual("Lastik", wheel, KirpiModel.Wheel, materials, true);
                _wheels[i] = wheel;
            }

            _door = new GameObject("ArkaKapi").transform;
            _door.gameObject.layer = GameLayers.Vehicle;
            _door.SetParent(_model, false);
            _door.localPosition = KirpiModel.DoorHinge;
            CreateVisual("Kapi", _door, KirpiModel.Door, materials, true);

            _turret = new GameObject("Kule").transform;
            _turret.gameObject.layer = GameLayers.Vehicle;
            _turret.SetParent(_model, false);
            _turret.localPosition = KirpiModel.TurretPivot;
            CreateVisual("Silah", _turret, KirpiModel.Turret, materials, true);
        }

        // ------------------------------------------------------------------ TransportVehicle

        public override Transform GetSeat(int index) => SeatAt(index);

        public override Transform PassengerViewPoint(int index) => ViewPointAt(index);

        public override Vector3 GetDisembarkPoint(int index)
        {
            var local = DisembarkLocal != null && DisembarkLocal.Length > 0 ? DisembarkLocal[NormalizeSeatIndex(index)] : new Vector3(0f, 0f, -5f);
            var outward = new Vector3(local.x, 0f, local.z);
            if (outward.sqrMagnitude < 0.01f)
                outward = Vector3.back;
            return ResolveDisembarkPoint(index, outward.normalized);
        }

        public override void Begin()
        {
            if (HasBegun || _path == null)
                return;

            MarkBegun();
            SetState(DriveState.Driving);
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
                case DriveState.Waiting:
                    break;
                case DriveState.Driving:
                    TickDrive(dt, true);
                    break;
                case DriveState.DoorOpening:
                    TickDoorOpening(dt);
                    break;
                case DriveState.Parked:
                    TickParked(dt);
                    break;
                case DriveState.DoorClosing:
                    TickDoorClosing(dt);
                    break;
                case DriveState.Leaving:
                    TickDrive(dt, false);
                    break;
            }

            ApplyPose();
            UpdateWheels(dt);
            UpdateTurret(dt);
            UpdateAudio(dt);
            UpdateDust(dt);

            var position = transform.position;
            if (_hasLastPosition)
                Velocity = (position - _lastPosition) / dt;
            _lastPosition = position;
            _hasLastPosition = true;

            if (IsDeparting)
                TickDestroyTimer();
        }

        private void SetState(DriveState state)
        {
            _state = state;
            _stateTime = 0f;
        }

        private void TickDrive(float dt, bool arriving)
        {
            var remaining = _path.Length - _s;
            if (arriving && remaining < 30f && Phase == TransportPhase.EnRoute)
                Phase = TransportPhase.Landing;

            var target = MaxSpeed;

            // Viraj: 10 m ileride yön değişimi → eğrilik yarıçapı.
            var dirNow = _path.DirectionAt(_s, 2f);
            var dirAhead = _path.DirectionAt(_s + 10f, 2f);
            var turn = Vector3.Angle(dirNow, dirAhead) * Mathf.Deg2Rad;
            if (turn > 0.02f)
                target = Mathf.Min(target, Mathf.Sqrt(LateralAccelerationLimit * (10f / turn)));

            // Eğim: dik yokuş/inişte yavaşla.
            var hereY = TransportGround.TerrainHeight(_horizontal);
            var aheadY = TransportGround.TerrainHeight(_path.PositionAt(_s + 6f));
            var grade = Mathf.Abs(aheadY - hereY) / 6f;
            if (grade > 0.12f)
                target *= Mathf.Lerp(1f, 0.45f, (grade - 0.12f) / 0.35f);

            // Durma eğrisi (yalnızca gelişte tam LZ'de durur).
            target = Mathf.Min(target, Mathf.Sqrt(2f * StopDeceleration * Mathf.Max(0f, remaining - 0.3f)) + 0.4f);
            target = Mathf.Max(target, 0.8f);

            _previousSpeed = _speed;
            var rate = target > _speed ? Acceleration : Braking;
            _speed = Mathf.MoveTowards(_speed, target, rate * dt);
            _s = Mathf.Min(_path.Length, _s + _speed * dt);
            _horizontal = _path.PositionAt(_s);

            // Yön: arka aks → ön aks hattı; direksiyon açısı dönüş hızından.
            var rear = _path.PositionAt(_s + KirpiModel.RearAxleZ);
            var front = _path.PositionAt(_s + KirpiModel.FrontAxleZ);
            var axis = front - rear;
            axis.y = 0f;
            var desiredYaw = axis.sqrMagnitude > 0.01f ? YawOf(axis) : YawOf(dirNow);
            _yaw = Mathf.SmoothDampAngle(_yaw, desiredYaw, ref _yawVelocity, 0.12f, 120f, dt);
            var yawRate = _yawVelocity * Mathf.Deg2Rad;
            var steerTarget = Mathf.Clamp(Mathf.Atan(KirpiModel.Wheelbase * yawRate / Mathf.Max(1f, _speed)) * Mathf.Rad2Deg, -MaxSteerDegrees, MaxSteerDegrees);
            _steer = Mathf.Lerp(_steer, steerTarget, 1f - Mathf.Exp(-8f * dt));

            SolveGroundPose(dt, false);

            if (_s >= _path.Length - 0.02f)
            {
                _s = _path.Length;
                _speed = 0f;
                _yawVelocity = 0f;
                if (arriving)
                {
                    Phase = TransportPhase.Landing;
                    SetState(DriveState.DoorOpening);
                    PlayDoorSound();
                }
                else
                {
                    // Ayrılış rotası bitti: kenarda bekle (yok etme zamanlayıcısı işler).
                    SetState(DriveState.Waiting);
                }
            }
        }

        private void TickDoorOpening(float dt)
        {
            SettleParked(dt);
            _door01 = Mathf.MoveTowards(_door01, 1f, dt / DoorOpenSeconds);
            ApplyDoor();
            if (_door01 >= 1f)
            {
                SetState(DriveState.Parked);
                SetObstacle(true);
                MarkArrived();
            }
        }

        private void TickParked(float dt)
        {
            SettleParked(dt);
            TickUnloading(dt);
            if (SecondsSinceRelease >= DepartAfterReleaseSeconds)
            {
                MarkDeparting();
                SetState(DriveState.DoorClosing);
                PlayDoorSound();
            }
        }

        private void TickDoorClosing(float dt)
        {
            SettleParked(dt);
            _door01 = Mathf.MoveTowards(_door01, 0f, dt / DoorCloseSeconds);
            ApplyDoor();
            if (_door01 > 0f)
                return;

            SetObstacle(false);
            try
            {
                _path = BuildDepartureRoute();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                var forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
                _path = TransportPath.Resample(new List<Vector3>(2) { _horizontal, _horizontal + forward * 150f }, PathSpacing);
            }

            _s = 0f;
            _speed = 0f;
            SetState(DriveState.Leaving);
        }

        private void SettleParked(float dt)
        {
            _speed = 0f;
            _steer = Mathf.Lerp(_steer, 0f, 1f - Mathf.Exp(-3f * dt));
            _pitch = Mathf.Lerp(_pitch, _groundPitch, 1f - Mathf.Exp(-6f * dt));
            _roll = Mathf.Lerp(_roll, _groundRoll, 1f - Mathf.Exp(-6f * dt));
        }

        private float _groundPitch;
        private float _groundRoll;

        /// <summary>Dört teker temas yüksekliğinden gövde yüksekliği, yunuslama ve yatış (+ ivme/viraj gövde hareketi).</summary>
        private void SolveGroundPose(float dt, bool snap)
        {
            var rotation = Quaternion.Euler(0f, _yaw, 0f);
            var frontLeft = WheelGround(_horizontal + rotation * new Vector3(-KirpiModel.WheelTrack, 0f, KirpiModel.FrontAxleZ));
            var frontRight = WheelGround(_horizontal + rotation * new Vector3(KirpiModel.WheelTrack, 0f, KirpiModel.FrontAxleZ));
            var rearLeft = WheelGround(_horizontal + rotation * new Vector3(-KirpiModel.WheelTrack, 0f, KirpiModel.RearAxleZ));
            var rearRight = WheelGround(_horizontal + rotation * new Vector3(KirpiModel.WheelTrack, 0f, KirpiModel.RearAxleZ));

            var front = (frontLeft + frontRight) * 0.5f;
            var rear = (rearLeft + rearRight) * 0.5f;
            var left = (frontLeft + rearLeft) * 0.5f;
            var right = (frontRight + rearRight) * 0.5f;

            _groundPitch = Mathf.Clamp(Mathf.Atan2(rear - front, KirpiModel.Wheelbase) * Mathf.Rad2Deg, -30f, 30f);
            _groundRoll = Mathf.Clamp(Mathf.Atan2(right - left, KirpiModel.WheelTrack * 2f) * Mathf.Rad2Deg, -25f, 25f);
            var targetY = (frontLeft + frontRight + rearLeft + rearRight) * 0.25f;

            if (snap)
            {
                _y = targetY;
                _yVelocity = 0f;
                _pitch = _groundPitch;
                _roll = _groundRoll;
                return;
            }

            _y = Mathf.SmoothDamp(_y, targetY, ref _yVelocity, 0.06f, 30f, dt);
            if (_y < targetY - 0.25f)
                _y = targetY - 0.25f;

            var acceleration = (_speed - _previousSpeed) / Mathf.Max(1e-4f, dt);
            var dive = Mathf.Clamp(-acceleration * 0.35f, -2f, 2f);
            var lean = Mathf.Clamp(_speed * _yawVelocity * Mathf.Deg2Rad * 0.8f, -3f, 3f);
            var t = 1f - Mathf.Exp(-10f * dt);
            _pitch = Mathf.Lerp(_pitch, _groundPitch + dive, t);
            _roll = Mathf.Lerp(_roll, _groundRoll + lean, t);
        }

        /// <summary>Teker altı zemin: üst yüzey (köprü/yol plakası) aracın yakınındaysa o, değilse arazi.</summary>
        private float WheelGround(Vector3 point)
        {
            var origin = new Vector3(point.x, _y + 2.5f, point.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 7f, TransportGround.SurfaceMask, QueryTriggerInteraction.Ignore)
                && hit.point.y <= _y + 1f)
                return hit.point.y;

            return TransportGround.TerrainHeight(point);
        }

        private void ApplyPose()
        {
            transform.SetPositionAndRotation(new Vector3(_horizontal.x, _y, _horizontal.z), Quaternion.Euler(_pitch, _yaw, _roll));
        }

        private void ApplyDoor()
        {
            if (_door == null)
                return;

            var eased = _door01 * _door01 * (3f - 2f * _door01);
            _door.localRotation = Quaternion.Euler(0f, KirpiModel.DoorOpenAngle * eased, 0f);
        }

        private void SetObstacle(bool enabled)
        {
            if (_obstacle != null)
                _obstacle.enabled = enabled;
        }

        // ------------------------------------------------------------------ presentation

        private void UpdateWheels(float dt)
        {
            if (_wheels == null)
                return;

            _wheelSpin = Mathf.Repeat(_wheelSpin + _speed * dt / KirpiModel.WheelRadius * Mathf.Rad2Deg, 360f);
            var spin = Quaternion.Euler(_wheelSpin, 0f, 0f);
            var steer = Quaternion.Euler(0f, _steer, 0f) * spin;
            for (var i = 0; i < _wheels.Length; i++)
            {
                var wheel = _wheels[i];
                if (wheel != null)
                    wheel.localRotation = i < 2 ? steer : spin;
            }
        }

        private void UpdateTurret(float dt)
        {
            if (_turret == null)
                return;

            if (_state == DriveState.Driving || _state == DriveState.Leaving)
                _turretTime += dt;
            else
                _turretTime += dt * 0.35f;

            var yaw = Mathf.Sin(_turretTime * 0.35f) * 40f;
            _turret.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void UpdateAudio(float dt)
        {
            var speed01 = Mathf.Clamp01(_speed / MaxSpeed);
            var load = _speed > _previousSpeed + 0.001f ? 0.08f : 0f;
            UpdateLoopAudio(0.72f + 0.55f * speed01 + load, dt);
        }

        private void UpdateDust(float dt)
        {
            if (_dustFailed || _speed < 2.5f)
                return;

            _dustTimer -= dt;
            if (_dustTimer > 0f)
                return;

            var speed01 = Mathf.Clamp01(_speed / MaxSpeed);
            _dustTimer = Mathf.Lerp(0.3f, 0.12f, speed01);
            _dustLeft = !_dustLeft;
            var local = new Vector3(_dustLeft ? -KirpiModel.WheelTrack : KirpiModel.WheelTrack, 0.15f, KirpiModel.RearAxleZ - 0.7f);
            var point = transform.TransformPoint(local);

            try
            {
                GameVfx.Dust(point, Mathf.Lerp(0.5f, 1.2f, speed01));
            }
            catch (Exception exception)
            {
                _dustFailed = true;
                Debug.LogException(exception, this);
            }
        }

        private void PlayDoorSound()
        {
            if (IsHeadless)
                return;

            try
            {
                if (_door != null)
                    GameAudio.Play(SoundId.VehicleDoor, _door.position, 0.8f, 0.85f, 40f);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private static float YawOf(Vector3 direction)
        {
            return direction.sqrMagnitude > 1e-8f ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg : 0f;
        }
    }
}
