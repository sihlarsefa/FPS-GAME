using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Hareket (NavMeshAgent ya da NavMesh yoksa ışınla zemine oturan doğrudan yürüyüş), duruş ve bakış/nişan.
    /// Beyin (Brain) her karede niyet bildirir (MoveTo / StopMoving / Look*); bu kısım niyeti uygular:
    ///  • Yol istekleri <see cref="BotDirector.TryConsumePathRequest"/> bütçesiyle kısılır, hedef belirgin değişmedikçe
    ///    yeni yol istenmez; geçersiz/kısmi yol ve takılma (2.5 sn ilerleyememe) <c>_moveFailed</c> ile beyne bildirilir.
    ///  • Gövde yönü (yaw) bakış yönüdür (ajan döndürmez); dönüş hızı çatışmada zorluk profilinden gelir.
    ///  • Çatışma nişanı: hedef bölgesi (baş/gövde), hedefin hızına göre öndelik, mermi düşüşü telafisi ve
    ///    profil/mesafe/hareket/irkilmeye göre salınan nişan hatası.
    /// Kare başına bellek ayırmaz.
    /// </summary>
    public sealed partial class BotController
    {
        /// <summary>Düzen içinde temkinli yürüyüş hızı (m/sn).</summary>
        public const float WalkSpeed = 2.4f;

        /// <summary>Hafif koşu (m/sn).</summary>
        public const float JogSpeed = 3.6f;

        /// <summary>Depar üst sınırı (koşan oyuncu komutana yetişebilmek için).</summary>
        public const float MaxSprintSpeed = 6.9f;

        private const float CasualTurnSpeed = 320f;
        private const float IdleTurnSpeed = 140f;
        private const float RepathMinInterval = 0.3f;
        private const float StuckSeconds = 2.5f;
        private const float NavMeshRetryInterval = 2f;
        private const float StanceMinHoldSeconds = 0.35f;
        private const float FallbackProbeHeight = 0.9f;
        private const float FallbackGroundProbe = 1.6f;
        private const float MaxPitch = 75f;

        private enum MoveSpeed
        {
            Walk = 0,
            Jog = 1,
            Run = 2,
            Sprint = 3
        }

        private enum LookMode
        {
            Movement,
            Yaw,
            Point,
            Target
        }

        // hareket
        private float _runSpeed = 5.6f;
        private bool _useNavMesh;
        private Vector3 _velocity;
        private bool _wantsMove;
        private Vector3 _moveDestination;
        private MoveSpeed _moveSpeed;
        private float _moveStoppingDistance = 0.5f;
        private bool _moveFailed;
        private bool _hasRequestedDestination;
        private Vector3 _requestedDestination;
        private float _lastPathRequestTime = -999f;
        private float _stuckTimer;
        private float _nextNavMeshRetry;
        private float _fallbackSide = 1f;

        // duruş
        private Stance _desiredStance = Stance.Standing;
        private float _lastStanceChange = -999f;
        private Stance _colliderStance = Stance.Standing;

        // bakış / nişan
        private float _aimYaw;
        private float _aimPitch; // + yukarı
        private float _anchorYaw; // gözetleme (ScanYaw) için sabit taban yön
        private LookMode _lookMode = LookMode.Movement;
        private float _lookYaw;
        private float _lookPitch;
        private Vector3 _lookPoint;
        private float _nextScanTime;
        private float _scanOffset;

        private Combatant _aimTarget;
        private BodyPart _aimPart = BodyPart.Torso;
        private float _aimSettle = 1f;
        private float _aimErrorYaw;
        private float _aimErrorPitch;
        private float _aimErrorTargetYaw;
        private float _aimErrorTargetPitch;
        private float _nextAimErrorTime;
        private bool _aimOnTarget;
        private float _aimOffYaw;
        private float _aimDistance;

        /// <summary>Bakış yönü (göz → nişan).</summary>
        public Vector3 AimForward => Quaternion.Euler(-_aimPitch, _aimYaw, 0f) * Vector3.forward;

        /// <summary>Bakış açısı (derece, Y ekseni).</summary>
        public float AimYaw => _aimYaw;

        /// <summary>Nişan yükselişi (derece, pozitif = yukarı).</summary>
        public float AimPitch => _aimPitch;

        /// <summary>Bot NavMesh ile mi hareket ediyor (false = doğrudan yürüyüş yedeği).</summary>
        public bool UsesNavMesh => _useNavMesh;

        // ------------------------------------------------------------------ niyet (Brain çağırır)

        private void MoveTo(Vector3 destination, MoveSpeed speed, float stoppingDistance)
        {
            _wantsMove = true;
            _moveDestination = destination;
            _moveSpeed = speed;
            _moveStoppingDistance = Mathf.Max(0.15f, stoppingDistance);
        }

        private void StopMoving()
        {
            _wantsMove = false;
        }

        private void LookYaw(float yaw)
        {
            _lookMode = LookMode.Yaw;
            _lookYaw = yaw;
            _lookPitch = -2f;
        }

        private void LookAt(Vector3 point)
        {
            _lookMode = LookMode.Point;
            _lookPoint = point;
        }

        private void LookMovement()
        {
            _lookMode = LookMode.Movement;
        }

        private void LookAtTarget()
        {
            _lookMode = LookMode.Target;
        }

        /// <summary>NavMesh (yoksa zemin) üzerinde hedef noktası. Bulunamazsa false.</summary>
        private bool SampleDestination(Vector3 point, float radius, out Vector3 result)
        {
            if (_useNavMesh)
                return BotTactics.SampleNavMesh(point, Mathf.Max(0.5f, radius), out result);

            if (BotTactics.TryGroundPoint(point, out result))
                return true;

            result = point;
            return true;
        }

        // ------------------------------------------------------------------ duruş

        private void ApplyStance()
        {
            var combatant = Combatant;
            if (combatant == null)
                return;

            var desired = _desiredStance;
            if (_wantsMove || Time.time < _evadeUntil)
            {
                if (_moveSpeed >= MoveSpeed.Run || Time.time < _evadeUntil)
                    desired = Stance.Standing;
                else if (desired == Stance.Prone)
                    desired = Stance.Crouching;
            }

            if (combatant.Stance == desired)
                return;

            var now = Time.time;
            if (now - _lastStanceChange < StanceMinHoldSeconds)
                return;

            _lastStanceChange = now;
            combatant.Stance = desired;
            UpdateColliderForStance(desired);
        }

        private void UpdateColliderForStance(Stance stance)
        {
            if (_collider == null || stance == _colliderStance)
                return;

            _colliderStance = stance;
            var height = stance == Stance.Crouching ? 1.2f : stance == Stance.Prone ? CapsuleRadius * 2f + 0.05f : CapsuleHeight;
            _collider.height = height;
            _collider.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private static float StanceSpeedFactor(Stance stance)
        {
            switch (stance)
            {
                case Stance.Crouching: return 0.6f;
                case Stance.Prone: return 0.25f;
                default: return 1f;
            }
        }

        private float SpeedFor(MoveSpeed speed)
        {
            switch (speed)
            {
                case MoveSpeed.Walk: return WalkSpeed;
                case MoveSpeed.Jog: return JogSpeed;
                case MoveSpeed.Run: return _runSpeed;
                default: return Mathf.Min(MaxSprintSpeed, _runSpeed * 1.18f);
            }
        }

        // ------------------------------------------------------------------ hareket

        private void UpdateLocomotion(float dt, float now)
        {
            var combatant = Combatant;

            if (now >= _nextDangerCheck)
                CheckDangers(now);

            if (now < _evadeUntil)
            {
                if (FlatDistance(transform.position, _evadePoint) < 1.2f)
                {
                    _evadeUntil = 0f;
                }
                else
                {
                    _wantsMove = true;
                    _moveDestination = _evadePoint;
                    _moveSpeed = MoveSpeed.Sprint;
                    _moveStoppingDistance = 0.6f;
                }
            }

            // Ajan NavMesh'ten düştüyse (NavMesh kaldırıldı / ışınlandı): doğrudan yürüyüşe geç, sonra yeniden dene.
            if (_useNavMesh && !AgentReady())
            {
                if (_agent != null && _agent.enabled)
                    _agent.enabled = false;

                _useNavMesh = false;
                _hasRequestedDestination = false;
                _nextNavMeshRetry = now + 1f;
            }

            if (!_useNavMesh && now >= _nextNavMeshRetry)
            {
                _nextNavMeshRetry = now + NavMeshRetryInterval;
                TryAcquireNavMesh();
            }

            var speed = SpeedFor(_moveSpeed) * StanceSpeedFactor(combatant.Stance) * combatant.MovementSpeedMultiplier;
            if (speed < 0.2f)
                speed = 0.2f;

            if (AgentReady())
                UpdateAgentMove(dt, now, speed);
            else
                UpdateDirectMove(dt, speed);

            combatant.Velocity = _velocity;
        }

        private bool AgentReady()
        {
            return _useNavMesh && _agent != null && _agent.enabled && _agent.isOnNavMesh;
        }

        private void UpdateAgentMove(float dt, float now, float speed)
        {
            var agent = _agent;
            var position = transform.position;

            if (!_wantsMove)
            {
                if (!agent.isStopped)
                    agent.isStopped = true;

                _stuckTimer = 0f;
                _velocity = agent.velocity;
                return;
            }

            agent.speed = speed;
            if (!Mathf.Approximately(agent.stoppingDistance, _moveStoppingDistance))
                agent.stoppingDistance = _moveStoppingDistance;
            if (agent.isStopped)
                agent.isStopped = false;

            var remaining = FlatDistance(position, _moveDestination);
            var canRequest = now - _lastPathRequestTime >= RepathMinInterval;
            var needRequest = false;
            if (!_hasRequestedDestination)
            {
                needRequest = canRequest;
            }
            else if (canRequest)
            {
                var dx = _moveDestination.x - _requestedDestination.x;
                var dz = _moveDestination.z - _requestedDestination.z;
                var tolerance = Mathf.Clamp(remaining * 0.08f, 0.6f, 6f);
                if (dx * dx + dz * dz > tolerance * tolerance)
                    needRequest = true;
                else if (!agent.pathPending && !agent.hasPath && remaining > _moveStoppingDistance + 0.6f &&
                         now - _lastPathRequestTime > 1f)
                    needRequest = true;
            }

            if (needRequest && (_director == null || _director.TryConsumePathRequest()))
            {
                _lastPathRequestTime = now;
                _hasRequestedDestination = true;
                _requestedDestination = _moveDestination;
                if (!agent.SetDestination(_moveDestination))
                {
                    _moveFailed = true;
                    _hasRequestedDestination = false;
                }
            }

            if (_hasRequestedDestination && !agent.pathPending)
            {
                var status = agent.pathStatus;
                if (status == NavMeshPathStatus.PathInvalid)
                {
                    _moveFailed = true;
                }
                else if (status == NavMeshPathStatus.PathPartial && agent.remainingDistance <= agent.stoppingDistance + 0.4f &&
                         remaining > _moveStoppingDistance + 2.5f)
                {
                    _moveFailed = true;
                }
            }

            _velocity = agent.velocity;
            UpdateStuck(dt, remaining, agent.pathPending);
        }

        /// <summary>NavMesh yoksa: hedefe düz yürü, önündeki engelden yana kay, zemine oturt.</summary>
        private void UpdateDirectMove(float dt, float speed)
        {
            var position = transform.position;
            var desired = Vector3.zero;
            var remaining = 0f;

            if (_wantsMove)
            {
                var to = _moveDestination - position;
                to.y = 0f;
                remaining = to.magnitude;
                if (remaining > _moveStoppingDistance && remaining > 0.01f)
                {
                    var dir = to / remaining;
                    if (IsDirectionBlocked(position, dir))
                    {
                        var left = Quaternion.Euler(0f, -55f * _fallbackSide, 0f) * dir;
                        if (!IsDirectionBlocked(position, left))
                        {
                            dir = left;
                        }
                        else
                        {
                            var right = Quaternion.Euler(0f, 55f * _fallbackSide, 0f) * dir;
                            if (!IsDirectionBlocked(position, right))
                            {
                                dir = right;
                            }
                            else
                            {
                                dir = Vector3.zero;
                                _fallbackSide = -_fallbackSide;
                            }
                        }
                    }

                    // Varışa yakın yavaşla.
                    var arrive = Mathf.Clamp01((remaining - _moveStoppingDistance) / 1.5f);
                    desired = dir * (speed * Mathf.Max(0.35f, arrive));
                }
            }

            var horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            horizontal = Vector3.MoveTowards(horizontal, desired, AgentAcceleration * dt);
            var next = position + horizontal * dt;

            var origin = next + Vector3.up * FallbackGroundProbe;
            if (Physics.Raycast(origin, Vector3.down, out var hit, FallbackGroundProbe + 3f, GameLayers.GroundMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (hit.point.y - position.y > 0.95f)
                {
                    // Tırmanılamayacak basamak: yerinde kal.
                    next = position;
                    horizontal = Vector3.zero;
                }
                else
                {
                    next.y = hit.point.y;
                }
            }

            transform.position = next;
            _velocity = dt > 0f ? new Vector3(horizontal.x, (next.y - position.y) / dt, horizontal.z) : horizontal;
            UpdateStuck(dt, remaining, false);
        }

        private static bool IsDirectionBlocked(Vector3 position, Vector3 direction)
        {
            var origin = position + Vector3.up * FallbackProbeHeight;
            if (!Physics.Raycast(origin, direction, out var hit, CapsuleRadius + 0.7f, GameLayers.LineOfSightMask,
                    QueryTriggerInteraction.Ignore))
                return false;

            // Yürünebilir eğim engel sayılmaz.
            return hit.normal.y < 0.6f;
        }

        private void UpdateStuck(float dt, float remaining, bool waitingForPath)
        {
            var flatSpeedSqr = _velocity.x * _velocity.x + _velocity.z * _velocity.z;
            if (_wantsMove && !waitingForPath && remaining > _moveStoppingDistance + 0.8f && flatSpeedSqr < 0.09f)
            {
                _stuckTimer += dt;
                if (_stuckTimer >= StuckSeconds)
                {
                    _stuckTimer = 0f;
                    _moveFailed = true;
                    _hasRequestedDestination = false;
                    _fallbackSide = -_fallbackSide;
                    if (AgentReady())
                        _agent.ResetPath();
                }
            }
            else if (_stuckTimer > 0f)
            {
                _stuckTimer = Mathf.Max(0f, _stuckTimer - dt * 2f);
            }
        }

        /// <summary>NavMesh sonradan yüklendiyse (ör. geç bake) ajana geç.</summary>
        private void TryAcquireNavMesh()
        {
            if (_agent == null || _seated || _dead || !HasLanded)
                return;

            if (!BotTactics.SampleNavMesh(transform.position, 4f, out var onMesh))
                return;

            transform.position = onMesh;
            _useNavMesh = true;
            EnableAgentIfPossible();
        }

        // ------------------------------------------------------------------ bakış / nişan

        private void UpdateAim(float dt, float now)
        {
            var eye = EyePosition;
            float targetYaw;
            float targetPitch;
            var turnSpeed = CasualTurnSpeed;
            var combatAim = false;

            switch (_lookMode)
            {
                case LookMode.Target:
                {
                    var target = _perception.Target;
                    if (target == null && _perception.LastSeenEnemy != null && _perception.LastSeenEnemy.IsAlive &&
                        now - _perception.LastSeenTime < 0.8f)
                        target = _perception.LastSeenEnemy;

                    if (target != null)
                    {
                        ComputeCombatAim(target, eye, dt, now, out targetYaw, out targetPitch);
                        turnSpeed = Profile.TurnSpeedDegreesPerSecond;
                        combatAim = true;
                    }
                    else if (_perception.HasLastKnownEnemyPosition)
                    {
                        DirectionAngles(_perception.LastKnownEnemyPosition + Vector3.up * 1.3f - eye, out targetYaw, out targetPitch);
                        turnSpeed = Profile.TurnSpeedDegreesPerSecond;
                    }
                    else
                    {
                        targetYaw = _aimYaw;
                        targetPitch = 0f;
                    }

                    break;
                }

                case LookMode.Point:
                    DirectionAngles(_lookPoint - eye, out targetYaw, out targetPitch);
                    break;

                case LookMode.Yaw:
                    targetYaw = _lookYaw;
                    targetPitch = _lookPitch;
                    turnSpeed = IdleTurnSpeed;
                    break;

                default:
                    MovementAngles(out targetYaw, out targetPitch);
                    break;
            }

            // Hasar / yakın silah sesi: kaynağa dön (depar atmıyorsa).
            var alerted = false;
            if (!combatAim && now < _perception.AlertUntil && !(_wantsMove && _moveSpeed == MoveSpeed.Sprint))
            {
                DirectionAngles(_perception.AlertPosition + Vector3.up * 1.3f - eye, out targetYaw, out targetPitch);
                turnSpeed = Mathf.Max(CasualTurnSpeed, Profile.TurnSpeedDegreesPerSecond);
                alerted = true;
            }

            if (!combatAim)
                _aimTarget = null;

            targetPitch = Mathf.Clamp(targetPitch, -MaxPitch, MaxPitch);
            var yawDelta = Mathf.DeltaAngle(_aimYaw, targetYaw);
            var step = turnSpeed * dt;

            // Hedefe yaklaşınca yavaşlayan dönüş (insansı), ama küçük açıları da izleyebilecek kadar hızlı.
            var ease = Mathf.Clamp(Mathf.Abs(yawDelta) / 20f, 0.35f, 1f);
            _aimYaw = Mathf.Repeat(_aimYaw + Mathf.Clamp(yawDelta, -step * ease, step * ease), 360f);
            _aimPitch = Mathf.MoveTowards(_aimPitch, targetPitch, step * 0.75f * Mathf.Clamp(Mathf.Abs(targetPitch - _aimPitch) / 15f, 0.35f, 1f));
            _aimPitch = Mathf.Clamp(_aimPitch, -MaxPitch, MaxPitch);

            transform.rotation = Quaternion.Euler(0f, _aimYaw, 0f);

            // Gözetleme tabanı: serbest bakışta (hareket/nokta/hedef/irkilme) son yön tutulur.
            if (_lookMode != LookMode.Yaw || alerted)
                _anchorYaw = _aimYaw;

            if (combatAim)
            {
                var offYaw = Mathf.Abs(Mathf.DeltaAngle(_aimYaw, targetYaw));
                var offPitch = Mathf.Abs(_aimPitch - targetPitch);
                var tolerance = Mathf.Max(1.4f, Mathf.Atan2(0.45f, Mathf.Max(1f, _aimDistance)) * Mathf.Rad2Deg);
                _aimOffYaw = offYaw;
                _aimOnTarget = offYaw <= tolerance && offPitch <= tolerance * 1.4f;
            }
            else
            {
                _aimOnTarget = false;
                _aimOffYaw = 180f;
            }
        }

        private void MovementAngles(out float yaw, out float pitch)
        {
            pitch = -3f;
            yaw = _aimYaw;

            Vector3 direction;
            if (AgentReady() && _wantsMove && _agent.hasPath)
            {
                direction = _agent.steeringTarget - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.04f)
                    direction = _agent.desiredVelocity;
            }
            else
            {
                direction = _velocity;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude > 0.04f && (_wantsMove || direction.sqrMagnitude > 0.5f))
                yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        private static void DirectionAngles(Vector3 direction, out float yaw, out float pitch)
        {
            var flat = Mathf.Sqrt(direction.x * direction.x + direction.z * direction.z);
            yaw = flat > 1e-4f ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg : 0f;
            pitch = Mathf.Atan2(direction.y, Mathf.Max(1e-4f, flat)) * Mathf.Rad2Deg;
        }

        /// <summary>Hedefe nişan: vücut bölgesi + öndelik + düşüş telafisi + salınan hata.</summary>
        private void ComputeCombatAim(Combatant target, Vector3 eye, float dt, float now, out float yaw, out float pitch)
        {
            if (!ReferenceEquals(target, _aimTarget))
            {
                _aimTarget = target;
                _aimSettle = 1.7f;
                _aimPart = RollAimPart(FlatDistance(transform.position, target.transform.position));
                var initial = AimErrorMagnitude(target, FlatDistance(transform.position, target.transform.position), now);
                _aimErrorYaw = Gaussian() * initial * 0.75f;
                _aimErrorPitch = Gaussian() * initial * 0.45f;
                _nextAimErrorTime = 0f;
            }

            _aimSettle = Mathf.MoveTowards(_aimSettle, 0.65f, dt * 0.6f);

            var point = target.GetAimPosition(_aimPart);
            var toTarget = point - eye;
            var distance = toTarget.magnitude;
            _aimDistance = distance;

            // Öndelik ve düşüş telafisi.
            var inventory = Combatant.Inventory;
            var weapon = inventory != null ? inventory.ActiveWeapon : null;
            var definition = weapon != null ? weapon.Definition : null;
            if (definition != null && distance > 8f)
            {
                var muzzleVelocity = definition.MuzzleVelocity > 50f ? definition.MuzzleVelocity : 700f;
                var flight = distance / muzzleVelocity;
                var skill = LeadSkill;
                var v = target.Velocity;
                point.x += v.x * flight * skill;
                point.z += v.z * flight * skill;

                var gravityScale = BallisticsSystem.Instance != null ? BallisticsSystem.Instance.GravityScale : 1f;
                var g = Mathf.Abs(Physics.gravity.y) * gravityScale;
                point.y += 0.5f * g * flight * flight * Mathf.Lerp(0.75f, 1f, skill);
            }

            if (now >= _nextAimErrorTime)
            {
                _nextAimErrorTime = now + Range(0.3f, 0.6f);
                var magnitude = AimErrorMagnitude(target, distance, now);
                _aimErrorTargetYaw = Gaussian() * magnitude * 0.6f;
                _aimErrorTargetPitch = Gaussian() * magnitude * 0.4f;
            }

            var drift = Mathf.Max(0.5f, Profile.AimErrorDegrees) * 2.5f * dt;
            _aimErrorYaw = Mathf.MoveTowards(_aimErrorYaw, _aimErrorTargetYaw, drift);
            _aimErrorPitch = Mathf.MoveTowards(_aimErrorPitch, _aimErrorTargetPitch, drift);

            DirectionAngles(point - eye, out yaw, out pitch);
            yaw += _aimErrorYaw;
            pitch += _aimErrorPitch;
        }

        /// <summary>Nişan hatası yarı açısı (derece): profil × hedef hareketi × kendi hareketi × mesafe × duruş × irkilme × hedefe yerleşme (ilk görüşte yüksek, izledikçe azalır).</summary>
        private float AimErrorMagnitude(Combatant target, float distance, float now)
        {
            var magnitude = Mathf.Max(0.2f, Profile.AimErrorDegrees);

            var tv = target.Velocity;
            if (tv.x * tv.x + tv.z * tv.z > 1f)
                magnitude *= Mathf.Max(1f, Profile.MovingTargetErrorMultiplier);

            var selfSpeed = Mathf.Sqrt(_velocity.x * _velocity.x + _velocity.z * _velocity.z);
            if (selfSpeed > 0.8f)
                magnitude *= 1f + Mathf.Min(1f, selfSpeed / 5f) * 0.8f;

            magnitude *= 0.8f + Mathf.Clamp01(distance / 150f) * 0.5f;

            var stance = Combatant.Stance;
            if (stance == Stance.Crouching)
                magnitude *= 0.85f;
            else if (stance == Stance.Prone)
                magnitude *= 0.7f;

            if (now - _perception.LastEnemyDamageTime < 0.8f)
                magnitude *= 1.5f;

            return magnitude * _aimSettle;
        }

        private BodyPart RollAimPart(float distance)
        {
            var headChance = Profile.HeadshotChance;
            if (distance > 80f)
                headChance *= 0.5f;
            else if (distance < 15f)
                headChance *= 1.3f;

            return Rand() < headChance ? BodyPart.Head : BodyPart.Torso;
        }

        /// <summary>Öndelik becerisi (zorluk).</summary>
        private float LeadSkill
        {
            get
            {
                switch (Profile.Difficulty)
                {
                    case BotDifficulty.Easy: return 0.5f;
                    case BotDifficulty.Hard: return 1f;
                    default: return 0.8f;
                }
            }
        }

        /// <summary>Standart normal dağılım (Box-Muller, ±3 ile sınırlı).</summary>
        private float Gaussian()
        {
            var u1 = 1.0 - _rng.NextDouble();
            var u2 = _rng.NextDouble();
            var value = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
            return Mathf.Clamp(value, -3f, 3f);
        }
    }
}
