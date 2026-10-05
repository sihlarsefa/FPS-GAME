using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Characters;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Loot;
using Project.Infrastructure.Transport;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Tim yapay zekâsı (bir asker). <see cref="Create"/> eksiksiz bir bot kurar: kapsül çarpıştırıcı (Bot katmanı),
    /// NavMeshAgent, <see cref="Combatant"/> (rütbe: RankCatalog.RankForTeamSlot), <see cref="SoldierModel"/> (tim görünümü +
    /// vuruş kutuları), ayak sesi, görev teçhizatı (LoadoutCatalog), MatchService / ChainOfCommandService kaydı.
    /// <para>
    /// İntikal: Transport verilmişse koltuğa oturur (ajan kapalı, hedef alınamaz); araç varınca/indirince (Arrived,
    /// IsUnloading) koltuk sırasıyla iner, iniş noktası NavMesh'e oturtulur ve ajan açılır (HasLanded).
    /// </para>
    /// <para>
    /// Döngü (yalnızca otoritede): algı 0.2-0.3 sn, karar ~0.5 sn (<see cref="BotDecisionService.Decide"/>), durum
    /// yürütme / nişan / atış / hareket her kare. Tim: lider = komuta zinciri komutanı (ya da SetSquadLeader), takipçiler
    /// kama düzeni; insan komutanın SquadOrderService emirleri (takip, mevzi, taarruz, toplan) uygulanır.
    /// Ölümde ajan ve çarpıştırıcı kapanır, ceset kalır.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class BotController : MonoBehaviour
    {
        public const float CapsuleRadius = 0.35f;
        public const float CapsuleHeight = 1.8f;
        public const float AgentAngularSpeed = 540f;
        public const float AgentAcceleration = 14f;

        private const float StandingEyeHeight = 1.62f;
        private const float CrouchingEyeHeight = 1.1f;
        private const float ProneEyeHeight = 0.35f;
        private const float GroundSpawnSearchRadius = 25f;
        private const float DisembarkSearchRadius = 12f;

        private static readonly List<BotController> AllBots = new(64);

        private BotDirector _director;
        private BotPerception _perception;
        private System.Random _rng;
        private CapsuleCollider _collider;
        private NavMeshAgent _agent;
        private Transform _eye;
        private Transform _aimPointTransform;
        private Transform _homeParent;

        private int _team;
        private TeamRole _role;
        private int _slot;
        private int _seed;
        private bool _initialized;
        private bool _dead;
        private bool _equipmentDirty = true;
        private float _eyeHeight = StandingEyeHeight;
        private WeaponDefinitionData _heldWeapon;
        private bool _heldWeaponSet;

        // intikal
        private TransportVehicle _transport;
        private int _seat;
        private bool _seated;
        private float _disembarkAt = -1f;
        private float _landedTime = -999f;
        private Vector3 _disembarkAwayDirection;

        // tim
        private Combatant _explicitLeader;
        private Combatant _leader;
        private bool _isCommander;

        // hata günlüğü
        private float _nextErrorLog;

        /// <summary>Sahnedeki canlı/aktif botlar.</summary>
        public static IReadOnlyList<BotController> All => AllBots;

        public Combatant Combatant { get; private set; }
        public BotState State { get; private set; } = BotState.Idle;
        public bool HasLanded { get; private set; }

        public int Team => _team;
        public TeamRole Role => _role;

        /// <summary>Timdeki sıra (0 = komutan) — rütbe ve teçhizat çeşidi için.</summary>
        public int Slot => _slot;

        public BotDifficultyProfile Profile { get; private set; }
        public SoldierModel Model { get; private set; }
        public NavMeshAgent Agent => _agent;
        public BotPerception Perception => _perception;

        /// <summary>Takip edilen tim lideri (komutansa null).</summary>
        public Combatant SquadLeader => _leader;

        /// <summary>Bu bot timinin komutanı mı (kimseyi takip etmiyor).</summary>
        public bool IsCommander => _isCommander;

        public bool IsSeated => _seated;
        public TransportVehicle Transport => _transport;
        public Combatant CurrentTarget => _perception != null ? _perception.Target : null;

        /// <summary>Durumun Türkçe adı (HUD / hata ayıklama).</summary>
        public string StateName => _dead ? "Şehit" : _seated ? "Araçta" : BotDecisionService.GetStateName(State);

        /// <summary>Kama düzenindeki sıra (koordinatör atar; 0 = komutan).</summary>
        public int FormationIndex { get; internal set; }

        /// <summary>Göz konumu (atış başlangıcı).</summary>
        public Vector3 EyePosition => _eye != null ? _eye.position : transform.position + Vector3.up * _eyeHeight;

        // ------------------------------------------------------------------ oluşturma

        public static BotController Create(BotSpawnArgs args)
        {
            if (args == null)
            {
                Debug.LogError("[Bot] BotController.Create: BotSpawnArgs null.");
                return null;
            }

            var name = string.IsNullOrWhiteSpace(args.Name) ? "Asker " + Mathf.Max(0, args.Id.Value) : args.Name.Trim();
            var seed = args.Seed != 0 ? args.Seed : args.Id.Value * 7919 + args.Team * 104729 + 17;
            var slot = args.Slot >= 0 ? args.Slot : SlotForRole(args.Role);

            MilitaryRank rank;
            if (args.Rank.HasValue)
            {
                rank = args.Rank.Value;
            }
            else
            {
                try
                {
                    rank = RankCatalog.RankForTeamSlot(slot, new SeededRandom(seed));
                }
                catch (Exception)
                {
                    rank = MilitaryRank.SozlesmeliEr;
                }
            }

            var go = new GameObject("Bot_" + name);
            go.SetActive(false); // bileşenler yapılandırılana kadar Awake/NavMeshAgent oluşturma ertelensin
            go.layer = GameLayers.Bot;
            if (args.Parent != null)
                go.transform.SetParent(args.Parent, false);

            go.transform.SetPositionAndRotation(args.GroundPosition, Quaternion.Euler(0f, args.GroundYaw, 0f));

            var bot = go.AddComponent<BotController>();
            try
            {
                bot.Setup(args, name, seed, slot, rank);
            }
            catch (Exception e)
            {
                Debug.LogException(e, go);
            }

            go.SetActive(true);

            try
            {
                bot.AfterActivate();
            }
            catch (Exception e)
            {
                Debug.LogException(e, go);
            }

            return bot;
        }

        /// <summary>Tim liderini elle atar (null → komuta zinciri komutanı).</summary>
        public void SetSquadLeader(Combatant leader)
        {
            _explicitLeader = leader;
            RequestDecision();
        }

        /// <summary>Bir sonraki karede yeni karar üretilmesini ister (emir, komuta devri, hasar).</summary>
        public void RequestDecision()
        {
            _forceDecision = true;
        }

        private static int SlotForRole(TeamRole role)
        {
            switch (role)
            {
                case TeamRole.Leader: return 0;
                case TeamRole.Marksman: return 1;
                case TeamRole.MachineGunner: return 2;
                case TeamRole.Medic: return 3;
                case TeamRole.Radioman: return 4;
                case TeamRole.Grenadier: return 5;
                default: return 6;
            }
        }

        private void Setup(BotSpawnArgs args, string name, int seed, int slot, MilitaryRank rank)
        {
            _seed = seed;
            _rng = new System.Random(seed);
            _team = args.Team;
            _role = args.Role;
            _slot = slot;
            _homeParent = args.Parent;
            _explicitLeader = args.SquadLeader;

            Profile = BotDifficultyProfile.For(args.Difficulty);
            // Bireysel farklılık: aynı zorluktaki botlar birebir aynı davranmasın.
            Profile.AimErrorDegrees *= Range(0.85f, 1.15f);
            Profile.ReactionSeconds *= Range(0.85f, 1.2f);
            Profile.TurnSpeedDegreesPerSecond *= Range(0.9f, 1.1f);
            if (_role == TeamRole.Marksman)
                Profile.ViewDistance *= 1.2f;

            _runSpeed = Range(5.4f, 6f);
            _lootFilter = IsUsefulLoot;

            // Fizik gövdesi
            _collider = gameObject.AddComponent<CapsuleCollider>();
            _collider.radius = CapsuleRadius;
            _collider.height = CapsuleHeight;
            _collider.center = new Vector3(0f, CapsuleHeight * 0.5f, 0f);
            _collider.direction = 1;

            // Göz ve nişan noktaları
            _eye = new GameObject("Eye").transform;
            _eye.SetParent(transform, false);
            _eye.localPosition = new Vector3(0f, StandingEyeHeight, 0f);
            _aimPointTransform = new GameObject("AimPoint").transform;
            _aimPointTransform.SetParent(transform, false);
            _aimPointTransform.localPosition = new Vector3(0f, StandingEyeHeight * 0.76f, 0f);

            // Savaşan
            IEventBus bus = null;
            IDamageableRegistry registry = null;
            GameContext.TryGet(out bus);
            GameContext.TryGet(out registry);

            Combatant = gameObject.AddComponent<Combatant>();
            Combatant.Initialize(args.Id, name, false, true, args.Team, args.Role, bus, registry, 100f);
            Combatant.Rank = rank;
            Combatant.EyePoint = _eye;
            Combatant.AimPoint = _aimPointTransform;
            Combatant.DropLootOnDeath = args.DropLootOnDeath;
            Combatant.Stance = Stance.Standing;
            Combatant.DropState = DropState.Landed;

            // Görünüm + vuruş kutuları
            try
            {
                Model = SoldierModel.Build(transform, SoldierLook.ForTeam(args.Team, new System.Random(seed)), Combatant, true, GameLayers.Bot);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            // Ayak sesi
            try
            {
                if (GetComponent<FootstepEmitter>() == null)
                    gameObject.AddComponent<FootstepEmitter>();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            // NavMesh ajanı (yerleşim sonrası açılır)
            _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.radius = CapsuleRadius;
            _agent.height = CapsuleHeight;
            _agent.baseOffset = 0f;
            _agent.speed = WalkSpeed;
            _agent.angularSpeed = AgentAngularSpeed;
            _agent.acceleration = AgentAcceleration;
            _agent.autoBraking = true;
            _agent.autoRepath = true;
            _agent.stoppingDistance = 0.25f;
            _agent.updateRotation = false;
            _agent.updateUpAxis = true;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            _agent.avoidancePriority = 30 + _rng.Next(0, 40);
            _agent.areaMask = NavMesh.AllAreas;
            _agent.enabled = false;

            // Teçhizat
            var inventory = Combatant.Inventory;
            if (inventory != null)
            {
                if (args.GiveLoadout)
                    ApplyLoadout(inventory);

                inventory.Changed += OnInventoryChanged;
            }

            Combatant.Damaged += OnDamaged;
            Combatant.Died += OnDied;

            _perception = new BotPerception(seed ^ 0x5bd1e995);
            _perception.Bind(Combatant);

            if (args.RegisterWithMatch)
                RegisterWithServices(args.Id, name, rank);

            _director = BotDirector.GetOrCreate();

            // Yerleşim
            if (args.Transport != null && !args.SpawnOnGround)
                BoardTransport(args.Transport, args.Seat);
            else
                LandAt(args.GroundPosition, args.GroundYaw, GroundSpawnSearchRadius);
        }

        private void AfterActivate()
        {
            if (_agent != null && _agent.enabled)
            {
                if (!_agent.isOnNavMesh)
                    _agent.Warp(transform.position);

                if (!_agent.isOnNavMesh)
                {
                    _agent.enabled = false;
                    _useNavMesh = false;
                }
            }

            if (!AllBots.Contains(this))
                AllBots.Add(this);

            _director?.Register(this);

            var now = Time.time;
            _nextPerception = now + Range(0f, 0.3f);
            _nextDecision = now + Range(0.05f, 0.5f);
            _nextLootSearch = now + Range(0.5f, 2f);
            _aimYaw = transform.eulerAngles.y;
            _initialized = true;
        }

        private void ApplyLoadout(InventoryService inventory)
        {
            try
            {
                var loadout = LoadoutCatalog.For(_role, _slot < 0 ? 0 : _slot);
                inventory.ApplyLoadout(loadout, true);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                try
                {
                    // Yedek: en azından bir tüfek ve mermi.
                    inventory.GiveWeapon(WeaponIds.Mpt55, true);
                    inventory.GiveItem(ItemIds.Ammo556, 120);
                    inventory.GiveItem(ItemIds.Bandage, 3);
                }
                catch (Exception)
                {
                    // katalog yok — silahsız başlar
                }
            }

            try
            {
                inventory.SelectBestWeapon();
            }
            catch (Exception)
            {
                // silah yok
            }

            _equipmentDirty = true;
        }

        private void RegisterWithServices(PlayerId id, string name, MilitaryRank rank)
        {
            if (GameContext.TryGet<IMatchService>(out var match))
            {
                try
                {
                    match.RegisterCombatant(id, RankCatalog.FormatName(rank, name), false, _team, _role);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }

            if (GameContext.TryGet<ChainOfCommandService>(out var chain))
            {
                try
                {
                    chain.Register(id, _team, rank);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        // ------------------------------------------------------------------ intikal

        private void BoardTransport(TransportVehicle transport, int seat)
        {
            _transport = transport;
            _seat = Mathf.Max(0, seat);

            Transform seatTransform = null;
            try
            {
                seatTransform = transport.GetSeat(_seat);
            }
            catch (Exception)
            {
                seatTransform = null;
            }

            transform.SetParent(seatTransform != null ? seatTransform : transport.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            _seated = true;
            _useNavMesh = false;
            HasLanded = false;
            _disembarkAt = -1f;
            if (_collider != null)
                _collider.enabled = false;
            if (_agent != null)
                _agent.enabled = false;

            Combatant.DropState = DropState.InTransport;
            Combatant.IsTargetable = false;
            Combatant.Stance = Stance.Standing;
            Combatant.Velocity = Vector3.zero;

            try
            {
                Model?.SetSeated(true);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            transport.Arrived += OnTransportArrived;
        }

        private void OnTransportArrived()
        {
            if (_seated && _disembarkAt < 0f)
                _disembarkAt = Time.time + 0.4f + _seat * 0.3f;
        }

        private void UpdateSeated(float now)
        {
            Combatant.Velocity = Vector3.zero;

            if (_transport == null)
            {
                // Araç yok edildi: bulunulan yerde in.
                Disembark();
                return;
            }

            if (_disembarkAt < 0f && (_transport.IsUnloading || _transport.HasArrived))
                _disembarkAt = now + 0.25f + _seat * 0.25f;

            if (_transport.IsDeparting && _disembarkAt > now)
                _disembarkAt = now;

            if (_disembarkAt >= 0f && now >= _disembarkAt)
                Disembark();
        }

        private void Disembark()
        {
            var point = transform.position;
            var away = transform.forward;
            var transport = _transport;
            if (transport != null)
            {
                transport.Arrived -= OnTransportArrived;
                try
                {
                    point = transport.GetDisembarkPoint(_seat);
                }
                catch (Exception)
                {
                    point = transport.transform.position + transport.transform.right * (2.5f + _seat * 0.6f);
                }

                away = point - transport.transform.position;
            }

            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = transform.forward;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;

            transform.SetParent(_homeParent, true);
            _seated = false;
            _disembarkAt = -1f;
            _disembarkAwayDirection = away;
            _landedTime = Time.time;

            LandAt(point, Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg, DisembarkSearchRadius);
            if (gameObject.activeInHierarchy)
                EnableAgentIfPossible();

            try
            {
                Model?.SetSeated(false);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            _transport = null;
            RequestDecision();
        }

        /// <summary>Konumu NavMesh'e (yoksa zemine) oturtur, gövdeyi açar ve iniş durumuna geçer.</summary>
        private void LandAt(Vector3 point, float yaw, float searchRadius)
        {
            var position = point;
            if (BotTactics.SampleNavMesh(point, searchRadius, out var onMesh))
            {
                position = onMesh;
                _useNavMesh = true;
            }
            else
            {
                _useNavMesh = false;
                if (BotTactics.TryGroundPoint(point, out var ground))
                    position = ground;
            }

            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _aimYaw = yaw;
            _aimPitch = 0f;

            if (_collider != null)
                _collider.enabled = true;
            if (_agent != null)
                _agent.enabled = _useNavMesh;

            Combatant.DropState = DropState.Landed;
            Combatant.IsTargetable = true;
            Combatant.Stance = Stance.Standing;
            HasLanded = true;
        }

        private void EnableAgentIfPossible()
        {
            if (_agent == null)
                return;

            if (!_useNavMesh)
            {
                _agent.enabled = false;
                return;
            }

            _agent.enabled = true;
            if (!_agent.isOnNavMesh)
                _agent.Warp(transform.position);

            if (!_agent.isOnNavMesh)
            {
                _agent.enabled = false;
                _useNavMesh = false;
            }

            _hasRequestedDestination = false;
        }

        // ------------------------------------------------------------------ döngü

        private void Update()
        {
            if (!_initialized || _dead)
                return;

            var combatant = Combatant;
            if (combatant == null)
                return;

            if (!combatant.IsAlive)
            {
                HandleDeath(combatant.LastDamageSource);
                return;
            }

            var dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            var now = Time.time;
            try
            {
                if (_seated)
                {
                    UpdateSeated(now);
                    UpdateBody(dt);
                    return;
                }

                if (!GameContext.HasAuthority)
                {
                    // İstemci: yalnızca görsel (otorite durumu ağdan gelir).
                    _velocity = combatant.Velocity;
                    UpdateBody(dt);
                    return;
                }

                Think(dt, now);
            }
            catch (Exception e)
            {
                if (now >= _nextErrorLog)
                {
                    _nextErrorLog = now + 5f;
                    Debug.LogException(e, this);
                }
            }
        }

        private void Think(float dt, float now)
        {
            TickWeapon(dt);

            if (now >= _nextPerception)
            {
                _nextPerception = now + Range(0.2f, 0.3f);
                Perceive(now);
            }

            if (now >= _nextDecision || (_forceDecision && now >= _lastDecisionTime + 0.15f))
                Decide(now);

            ExecuteState(now);
            UpdateLocomotion(dt, now);
            UpdateAim(dt, now);
            UpdateTrigger(now);
            UpdateBody(dt);
        }

        /// <summary>Göz yüksekliği, model hareket/nişan, elde tutulan silah ve teçhizat görünümü.</summary>
        private void UpdateBody(float dt)
        {
            var combatant = Combatant;
            var stance = combatant.Stance;
            var targetEye = stance == Stance.Crouching ? CrouchingEyeHeight : stance == Stance.Prone ? ProneEyeHeight : StandingEyeHeight;
            _eyeHeight = Mathf.MoveTowards(_eyeHeight, targetEye, 3.5f * dt);
            if (_eye != null)
            {
                _eye.localPosition = new Vector3(0f, _eyeHeight, 0f);
                _eye.localRotation = Quaternion.Euler(-_aimPitch, 0f, 0f);
            }

            if (_aimPointTransform != null)
                _aimPointTransform.localPosition = new Vector3(0f, _eyeHeight * 0.76f, 0f);

            var model = Model;
            if (model == null)
                return;

            model.SetLocomotion(_seated ? Vector3.zero : _velocity, stance, !_seated);
            model.SetAimPitch(_aimPitch);

            var inventory = combatant.Inventory;
            var weapon = inventory != null ? inventory.ActiveWeapon : null;
            var definition = weapon != null ? weapon.Definition : null;
            if (!_heldWeaponSet || !ReferenceEquals(definition, _heldWeapon))
            {
                _heldWeaponSet = true;
                _heldWeapon = definition;
                model.HoldWeapon(definition);
            }

            if (_equipmentDirty && inventory != null)
            {
                _equipmentDirty = false;
                var helmet = inventory.Helmet;
                var vest = inventory.Vest;
                model.SetEquipment(helmet != null && !helmet.IsBroken ? helmet.Level : 0,
                    vest != null && !vest.IsBroken ? vest.Level : 0,
                    inventory.BackpackLevel);
            }
        }

        // ------------------------------------------------------------------ olaylar

        private void OnInventoryChanged()
        {
            _equipmentDirty = true;
        }

        private void OnDamaged(Combatant combatant, DamageInfo damage)
        {
            if (_dead || _perception == null)
                return;

            var now = Time.time;
            _perception.OnDamaged(damage, transform.position, _director != null ? _director.Relations : null, _director, now);
            if (now >= _nextDamageDecision)
            {
                _nextDamageDecision = now + 0.35f;
                RequestDecision();
            }
        }

        private void OnDied(Combatant combatant, DamageInfo damage)
        {
            var source = damage.HasSourcePosition
                ? new Vector3(damage.SourcePosition.X, damage.SourcePosition.Y, damage.SourcePosition.Z)
                : combatant != null ? combatant.LastDamageSource : transform.position;
            HandleDeath(source);
        }

        private void HandleDeath(Vector3 damageSource)
        {
            if (_dead)
                return;

            _dead = true;
            _burstRemaining = 0;
            _triggerWasHeld = false;
            State = BotState.Idle;

            if (_director != null)
            {
                _director.ReleaseClaims(this);
                _director.Unregister(this);
            }

            AllBots.Remove(this);

            if (_seated)
            {
                if (_transport != null)
                    _transport.Arrived -= OnTransportArrived;

                transform.SetParent(_homeParent, true);
                _seated = false;
                _transport = null;
            }

            if (_agent != null && _agent.enabled)
            {
                if (_agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.ResetPath();
                }

                _agent.enabled = false;
            }

            if (_collider != null)
                _collider.enabled = false;

            if (Combatant != null)
                Combatant.Velocity = Vector3.zero;

            _velocity = Vector3.zero;

            var hitDirection = transform.position - damageSource;
            hitDirection.y = 0f;
            hitDirection = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : -transform.forward;

            try
            {
                if (Model != null)
                {
                    Model.SetLocomotion(Vector3.zero, Stance.Standing, true);
                    Model.PlayDeath(hitDirection);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            // Ceset kalır; yapay zekâ döngüsü durur.
            enabled = false;
        }

        private void OnDestroy()
        {
            if (Combatant != null)
            {
                Combatant.Damaged -= OnDamaged;
                Combatant.Died -= OnDied;
                if (Combatant.Inventory != null)
                    Combatant.Inventory.Changed -= OnInventoryChanged;
            }

            if (_transport != null)
                _transport.Arrived -= OnTransportArrived;

            if (_director != null)
            {
                _director.ReleaseClaims(this);
                _director.Unregister(this);
            }

            AllBots.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AllBots.Clear();
        }

        // ------------------------------------------------------------------ yardımcılar

        private float Range(float min, float max) => min + (max - min) * (float)_rng.NextDouble();

        private float Rand() => (float)_rng.NextDouble();

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static Vector3 ToVector3(Float3 f) => new(f.X, f.Y, f.Z);
    }
}
