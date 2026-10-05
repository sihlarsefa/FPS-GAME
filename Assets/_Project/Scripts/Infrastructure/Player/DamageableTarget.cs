using System;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Characters;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.Player
{
    /// <summary>
    /// Atış poligonu eğitim hedefi (manken). Bir <see cref="Combat.Combatant"/> olarak kurulur: tim 99 (herkese düşman),
    /// IsBot = true, DropLootOnDeath = false; baş/gövde/kol/bacak vuruş kutuları vardır (bölge hasarı ve kafa vuruşu
    /// çalışır). Can bitince manken geriye devrilir, <see cref="RespawnSeconds"/> sonra kalkar ve canı dolar
    /// (Combatant.Revive → Health.ResetToFull). Hareketli hedef iki nokta arasında gidip gelir.
    /// Konum zemin noktasıdır; verilen yükseklik yakındaki zemine oturtulur (eski çağıranlar y=1 verir).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageableTarget : MonoBehaviour
    {
        /// <summary>Eğitim hedeflerinin tim numarası (tüm timlere düşman).</summary>
        public const int DummyTeam = 99;

        private const float KnockDownSeconds = 0.35f;
        private const float RiseSeconds = 0.5f;
        private const float TurnSpeed = 140f;

        private enum TargetState
        {
            Up,
            Falling,
            Down,
            Rising
        }

        private TrainingDummyModel _model;
        private CapsuleCollider _blocker;
        private TargetState _state = TargetState.Up;
        private float _stateTime;
        private float _wobbleX;
        private float _wobbleXVel;
        private float _wobbleZ;
        private float _wobbleZVel;

        // Devriye
        private bool _moving;
        private Vector3 _pointA;
        private Vector3 _pointB;
        private float _patrolT;
        private float _patrolDir = 1f;
        private float _pathLength;

        /// <summary>Hedefin savaşan bileşeni (kimlik, can, vuruş kutuları).</summary>
        public Combatant Combatant { get; private set; }

        /// <summary>Devrildikten sonra kalkma süresi (saniye). 0 veya negatif → hiç kalkmaz.</summary>
        public float RespawnSeconds { get; set; } = 3f;

        /// <summary>Oyuncuya doğru dönsün mü (ön yüz ve hedef halkaları görünür kalsın).</summary>
        public bool FacePlayer { get; set; } = true;

        /// <summary>Hareketli hedef hızı (m/s).</summary>
        public float PatrolSpeed { get; set; } = 2f;

        public bool IsMoving => _moving;
        public bool IsDown => _state != TargetState.Up;
        public TrainingDummyModel Model => _model;

        /// <summary>Toplam isabet sayısı ve hasarı (eğitim istatistiği).</summary>
        public int HitCount { get; private set; }
        public int HeadshotCount { get; private set; }
        public float TotalDamage { get; private set; }
        public int KnockDownCount { get; private set; }

        // Eski API ile uyum (salt okunur geçişler — hasar Combatant üzerinden işler).
        public PlayerId OwnerId => Combatant != null ? Combatant.Id : PlayerId.Invalid;
        public HealthState State => Combatant != null ? Combatant.State : new HealthState(0f, 100f);
        public bool IsAlive => Combatant != null && Combatant.IsAlive;

        public event Action<DamageableTarget, DamageInfo> Hit;
        public event Action<DamageableTarget> KnockedDown;
        public event Action<DamageableTarget> Respawned;

        // ------------------------------------------------------------------ Fabrika

        /// <summary>Sabit eğitim mankeni. position zemin noktası (yakındaki zemine oturtulur).</summary>
        public static DamageableTarget CreateDummy(Vector3 position, IEventBus eventBus, IDamageableRegistry registry, int id, float health = 100f)
        {
            var ground = SnapToGround(position);
            var go = new GameObject("Hedef_" + id);
            go.layer = GameLayers.Bot;
            go.transform.SetPositionAndRotation(ground, Quaternion.identity);

            var combatant = go.AddComponent<Combatant>();
            combatant.DropLootOnDeath = false;
            combatant.Initialize(new PlayerId(id), "Hedef Mankeni " + id, false, true, DummyTeam, TeamRole.Rifleman, eventBus, registry,
                health > 0f ? health : 100f);
            combatant.DropLootOnDeath = false;
            combatant.Stance = Stance.Standing;
            combatant.DropState = DropState.Landed;

            var target = go.AddComponent<DamageableTarget>();
            target.Setup(combatant);
            return target;
        }

        /// <summary>a ile b arasında speed (m/s) hızla gidip gelen eğitim mankeni.</summary>
        public static DamageableTarget CreateMoving(Vector3 a, Vector3 b, float speed, IEventBus eventBus, IDamageableRegistry registry, int id)
        {
            var target = CreateDummy(a, eventBus, registry, id);
            target.StartPatrol(a, b, speed);
            return target;
        }

        /// <summary>Hedefi iki nokta arasında devriyeye başlatır (noktalar zemine oturtulur).</summary>
        public void StartPatrol(Vector3 a, Vector3 b, float speed)
        {
            _pointA = SnapToGround(a);
            _pointB = SnapToGround(b);
            _pathLength = Vector3.Distance(_pointA, _pointB);
            PatrolSpeed = Mathf.Max(0.1f, speed);
            _patrolT = 0f;
            _patrolDir = 1f;
            _moving = _pathLength > 0.05f;
            transform.position = _pointA;

            if (_moving && !TryGetComponent<Rigidbody>(out _))
            {
                var body = gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
            }
        }

        public void StopPatrol()
        {
            _moving = false;
            if (Combatant != null)
                Combatant.Velocity = Vector3.zero;
        }

        private void Setup(Combatant combatant)
        {
            Combatant = combatant;
            _model = TrainingDummyModel.Build(transform, combatant, true, GameLayers.Bot);
            combatant.EyePoint = _model.EyePoint;
            combatant.AimPoint = _model.AimPoint;

            // Oyuncunun içinden geçmemesi için hareket engeli (Bot katmanı: mermiler geçer, vuruş kutuları isabet alır).
            _blocker = gameObject.AddComponent<CapsuleCollider>();
            _blocker.center = new Vector3(0f, 0.9f, 0f);
            _blocker.radius = 0.28f;
            _blocker.height = 1.8f;
            _blocker.direction = 1;

            combatant.Damaged += OnDamaged;
            combatant.Died += OnDied;

            FaceLocalPlayer(1000f);
            _model.SetPose(0f, 0f, 0f);
        }

        // ------------------------------------------------------------------ Uyum geçişleri

        /// <summary>Doğrudan hasar (CombatService/vuruş kutusu kullanmayan eski çağıranlar için).</summary>
        public void ApplyDamage(DamageInfo damage) => Combatant?.ApplyDamage(damage);

        public void Heal(float amount) => Combatant?.Heal(amount);

        /// <summary>Hedefi hemen ayağa kaldırır ve canını doldurur.</summary>
        public void ResetTarget()
        {
            if (Combatant == null)
                return;

            if (!Combatant.IsAlive || _state != TargetState.Up)
                Combatant.Revive();

            _state = TargetState.Up;
            _stateTime = 0f;
            _model.SetHitboxesEnabled(true);
            if (_blocker != null)
                _blocker.enabled = true;
            _model.SetPose(0f, 0f, 0f);
        }

        /// <summary>İstatistikleri sıfırlar.</summary>
        public void ResetStats()
        {
            HitCount = 0;
            HeadshotCount = 0;
            TotalDamage = 0f;
            KnockDownCount = 0;
        }

        // ------------------------------------------------------------------ Olaylar

        private void OnDamaged(Combatant combatant, DamageInfo damage)
        {
            HitCount++;
            TotalDamage += damage.Amount;
            if (damage.IsHeadshot)
                HeadshotCount++;

            // İsabet sallanması: önden gelen vuruş geriye iter, yandan gelen yana yatırır.
            var push = Mathf.Clamp(damage.Amount * 0.6f, 3f, 25f);
            var dirLocal = Vector3.back;
            if (damage.HasSourcePosition)
            {
                var source = new Vector3(damage.SourcePosition.X, damage.SourcePosition.Y, damage.SourcePosition.Z);
                var away = transform.position - source;
                away.y = 0f;
                if (away.sqrMagnitude > 1e-4f)
                    dirLocal = transform.InverseTransformDirection(away.normalized);
            }

            _wobbleXVel += dirLocal.z * push * 12f;
            _wobbleZVel += -dirLocal.x * push * 12f;

            Raise(Hit, damage);
        }

        private void OnDied(Combatant combatant, DamageInfo damage)
        {
            if (_state == TargetState.Falling || _state == TargetState.Down)
                return;

            KnockDownCount++;
            _state = TargetState.Falling;
            _stateTime = 0f;
            _model.SetHitboxesEnabled(false);
            if (Combatant != null)
                Combatant.Velocity = Vector3.zero;

            var handler = KnockedDown;
            if (handler != null)
            {
                try
                {
                    handler(this);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        private void Raise(Action<DamageableTarget, DamageInfo> handler, DamageInfo damage)
        {
            if (handler == null)
                return;

            try
            {
                handler(this, damage);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        // ------------------------------------------------------------------ Güncelleme

        private void Update()
        {
            if (Combatant == null || _model == null)
                return;

            var dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            UpdateWobble(dt);

            switch (_state)
            {
                case TargetState.Up:
                    if (_moving)
                        UpdatePatrol(dt);
                    if (FacePlayer)
                        FaceLocalPlayer(TurnSpeed * dt);
                    _model.SetPose(0f, _wobbleX, _wobbleZ);
                    break;

                case TargetState.Falling:
                {
                    _stateTime += dt;
                    var f = Mathf.Clamp01(_stateTime / KnockDownSeconds);
                    _model.SetPose(f * f, _wobbleX * (1f - f), _wobbleZ * (1f - f));
                    if (f >= 1f)
                    {
                        _state = TargetState.Down;
                        _stateTime = 0f;
                        if (_blocker != null)
                            _blocker.enabled = false;
                        try
                        {
                            GameAudio.Play(SoundId.BulletImpactMetal, transform.position + Vector3.up * 0.2f, 0.7f, 0.65f, 40f);
                        }
                        catch (Exception)
                        {
                            // Ses sistemi isteğe bağlıdır.
                        }
                    }

                    break;
                }

                case TargetState.Down:
                    _stateTime += dt;
                    _model.SetPose(1f, 0f, 0f);
                    if (RespawnSeconds > 0f && _stateTime >= RespawnSeconds && GameContext.HasAuthority)
                    {
                        _state = TargetState.Rising;
                        _stateTime = 0f;
                    }

                    break;

                case TargetState.Rising:
                {
                    _stateTime += dt;
                    var f = Mathf.Clamp01(_stateTime / RiseSeconds);
                    var e = 1f - (1f - f) * (1f - f);
                    _model.SetPose(1f - e, 0f, 0f);
                    if (f >= 1f)
                        FinishRespawn();
                    break;
                }
            }
        }

        private void FinishRespawn()
        {
            _state = TargetState.Up;
            _stateTime = 0f;
            _wobbleX = _wobbleZ = _wobbleXVel = _wobbleZVel = 0f;
            Combatant.Revive();
            _model.SetHitboxesEnabled(true);
            if (_blocker != null)
                _blocker.enabled = true;
            _model.SetPose(0f, 0f, 0f);

            var handler = Respawned;
            if (handler == null)
                return;

            try
            {
                handler(this);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void UpdateWobble(float dt)
        {
            // Sönümlü yay (≈ 2,5 Hz).
            const float stiffness = 240f;
            const float damping = 9f;
            _wobbleXVel += (-stiffness * _wobbleX - damping * _wobbleXVel) * dt;
            _wobbleZVel += (-stiffness * _wobbleZ - damping * _wobbleZVel) * dt;
            _wobbleX = Mathf.Clamp(_wobbleX + _wobbleXVel * dt, -25f, 25f);
            _wobbleZ = Mathf.Clamp(_wobbleZ + _wobbleZVel * dt, -20f, 20f);
        }

        private void UpdatePatrol(float dt)
        {
            if (_pathLength <= 0.05f)
            {
                Combatant.Velocity = Vector3.zero;
                return;
            }

            var previous = transform.position;
            _patrolT += _patrolDir * PatrolSpeed * dt / _pathLength;
            if (_patrolT >= 1f)
            {
                _patrolT = 1f;
                _patrolDir = -1f;
            }
            else if (_patrolT <= 0f)
            {
                _patrolT = 0f;
                _patrolDir = 1f;
            }

            // Uçlarda yumuşak yavaşlama.
            var eased = _patrolT * _patrolT * (3f - 2f * _patrolT);
            var t = Mathf.Lerp(_patrolT, eased, 0.35f);
            var position = Vector3.Lerp(_pointA, _pointB, t);
            transform.position = position;
            Combatant.Velocity = (position - previous) / dt;
        }

        private void FaceLocalPlayer(float maxDegrees)
        {
            var player = CombatantRegistry.LocalPlayer;
            if (player == null)
                return;

            var to = player.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f)
                return;

            var desired = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, maxDegrees);
        }

        private static Vector3 SnapToGround(Vector3 position)
        {
            var origin = position + Vector3.up * 2f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 40f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return position;
        }

        private void OnDestroy()
        {
            if (Combatant != null)
            {
                Combatant.Damaged -= OnDamaged;
                Combatant.Died -= OnDied;
            }

            Hit = null;
            KnockedDown = null;
            Respawned = null;
        }
    }
}
