using System;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Config;
using Project.Infrastructure.Input;
using Project.Infrastructure.Player;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Transport;
using Project.Infrastructure.Vehicles;
using Project.Infrastructure.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Presentation.Player
{
    /// <summary>
    /// Yerel oyuncu (tim komutanı). <see cref="Create"/> tüm bileşenleri kurar: CharacterController + motor, girdi okuyucu,
    /// kamera pivotu + CameraRig + FPP kamera, silah görünümü, Combatant (+ duruşu izleyen vuruş kutuları), ayak sesleri,
    /// görev teçhizatı, maç/komuta zinciri kaydı.
    /// <para>
    /// Her kare: girdi → <see cref="PlayerCommand"/> (sunucu-otoriteli modele hazır; varsa IPlayerCommandSink'e gönderilir)
    /// → yerel simülasyon (hareket, bakış, silah, etkileşim, eşya, bomba, tim emirleri, topçu).
    /// İntikal aracında koltuğa bağlıdır (kamera yolcu bakış noktasında, yalnız bakış serbest), araç varınca F ile ya da
    /// 3 sn sonra otomatik iner. Sürülebilir araçta WASD aracı sürer, F iner. Düşme hasarı motorun Landed olayından.
    /// Ölünce girdi kapanır, kamera yere düşer (ölüm kamerası) ve <see cref="Died"/> yayınlanır.
    /// </para>
    /// Hiyerarşi: Player (yaw, CharacterController) → CameraPivot (pitch/göz) → CameraOffset (ölüm kamerası) → CameraRig →
    /// WeaponViewModel; Player → Hitboxes.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerController : MonoBehaviour, IPlayerHudSource
    {
        public const float DefaultFieldOfView = 80f;
        public const float ReferenceStandingHeight = 1.8f;
        public const float AutoDisembarkSeconds = 3f;
        public const float ArtilleryMaxRange = 600f;

        private const float CapsuleRadius = 0.3f;
        private const float DefaultEyeHeight = 1.62f;
        private const float SafeFallSpeed = 11f;
        private const float FallDamagePerMetrePerSecond = 7.5f;
        private const float DisembarkFallGraceSeconds = 1.2f;
        private const float VehicleExitFallGraceSeconds = 0.8f;
        private const float DeathCamSeconds = 1.4f;
        private const float DeathCamGroundOffset = 0.28f;
        private const float DeathCamRollDegrees = 72f;
        private const float AdsMoveSpeedFactor = 0.65f;

        private enum Mode
        {
            None,
            OnFoot,
            Transport,
            Driving,
            Dead
        }

        private static PlayerMovementConfig _defaultConfig;

        // ---------------------------------------------------------------- bileşenler
        private CharacterController _characterController;
        private CharacterControllerMotor _motor;
        private FirstPersonCameraController _camera;
        private CameraRig _rig;
        private Transform _pitchPivot;
        private Transform _cameraOffset;
        private WeaponViewModel _viewModel;
        private UnityInputReader _input;
        private Combatant _combatant;
        private FootstepEmitter _footsteps;
        private PlayerHitboxRig _hitboxes;

        private PlayerWeaponHandler _weapons;
        private PlayerInteraction _interaction;
        private SquadCommandInput _squad;

        // ---------------------------------------------------------------- servisler (isteğe bağlı)
        private Core.Interfaces.IServiceProvider _servicesFor;
        private bool _servicesResolved;
        private CombatService _combat;
        private ChainOfCommandService _chain;
        private SquadOrderService _orders;
        private ArtilleryService _artillery;
        private SettingsService _settingsService;
        private IPlayerCommandSink _commandSink;
        private ISimulationClock _clock;

        // ---------------------------------------------------------------- durum
        private Mode _mode = Mode.None;
        private bool _initialized;
        private bool _inputEnabled = true;
        private GameSettings _settings = new GameSettings();
        private float _baseSensitivity = 0.12f;
        private float _adsSensitivityMultiplier = 0.8f;
        private float _appliedSensitivity = -1f;
        private uint _localTick;
        private float _fallGraceUntil;
        private PlayerCommand _lastCommand;
        private Vector3 _lastPosition;

        // intikal
        private TransportVehicle _transport;
        private int _seat;
        private Transform _seatTransform;
        private float _transportArrivedAt = -1f;
        private bool _transportHooked;

        // sürüş
        private DrivableVehicle _vehicle;

        // ölüm kamerası
        private float _deathTime;
        private Vector3 _deathCamStartPos;
        private Quaternion _deathCamStartRot;
        private Vector3 _deathCamEndPos;
        private Quaternion _deathCamEndRot;

        // bildirim
        private string _notification;
        private float _notificationUntil;

        /// <summary>Sahnedeki yerel oyuncu (yoksa null).</summary>
        public static PlayerController Local { get; private set; }

        // ================================================================ IPlayerHudSource
        public Combatant Combatant => _combatant;
        public InventoryService Inventory => _combatant != null ? _combatant.Inventory : null;
        public WeaponRuntimeService ActiveWeapon => Inventory?.ActiveWeapon;
        public bool IsAiming => _weapons != null && _weapons.IsAiming;
        public bool IsScoped => _weapons != null && _weapons.IsScoped;
        public float ScopeZoom => _weapons != null ? _weapons.CurrentZoom : 1f;
        public float SpreadAngle => _weapons != null ? _weapons.SpreadAngle : 0f;

        /// <summary>"[F] ..." istemi; kısa bilgi mesajı varken o mesaj.</summary>
        public string InteractionPrompt
        {
            get
            {
                if (_notification != null && Time.time < _notificationUntil)
                    return _notification;

                return _interaction != null && _mode != Mode.Dead ? _interaction.Prompt : string.Empty;
            }
        }

        public DropState DropState => _combatant != null ? _combatant.DropState : DropState.Landed;
        public ItemUseService ItemUse => _combatant != null ? _combatant.ItemUse : null;

        /// <summary>Bakış yönü (derece, dünya). Araçta serbest bakış dahil.</summary>
        public float Yaw
        {
            get
            {
                var t = _pitchPivot != null ? _pitchPivot : transform;
                var forward = t.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 1e-6f)
                    return transform.eulerAngles.y;

                return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            }
        }

        public Vector3 Position => transform.position;
        public bool IsDead => _mode == Mode.Dead || _combatant == null || !_combatant.IsAlive;
        public FirstPersonCameraController CameraController => _camera;
        public SquadOrder CurrentOrder => _squad != null ? _squad.CurrentOrder : SquadOrder.Follow;
        public float ArtilleryCooldown => _squad != null ? _squad.ArtilleryCooldown : 0f;
        public bool IsInVehicle => _mode == Mode.Driving || _mode == Mode.Transport;

        // ================================================================ ek genel API
        /// <summary>
        /// false iken oynanış girdileri yok sayılır (menü/harita/envanter açık). Girdi okuyucunun GameplayEnabled'ı
        /// yalnızca bu değer değişince yazılır (harita katmanı onu ayrıca kapatabilir). İmleç kilidi de buna uyar.
        /// </summary>
        public bool InputEnabled
        {
            get => _inputEnabled;
            set
            {
                if (_inputEnabled == value)
                    return;

                _inputEnabled = value;
                if (_input != null)
                    _input.GameplayEnabled = value && _mode != Mode.Dead;

                if (_mode != Mode.Dead && _initialized)
                {
                    Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
                    Cursor.visible = !value;
                }
            }
        }

        /// <summary>Oynanış girdisi şu an etkin mi (InputEnabled, hayatta ve okuyucu kapalı değil)?</summary>
        public bool GameplayInputActive => _inputEnabled && _mode != Mode.Dead && (_input == null || _input.GameplayEnabled);

        public UnityInputReader Input => _input;
        public CharacterControllerMotor Motor => _motor;
        public CameraRig CameraRig => _rig;
        public WeaponViewModel ViewModel => _viewModel;
        public PlayerWeaponHandler Weapons => _weapons;
        public PlayerInteraction Interaction => _interaction;
        public SquadCommandInput Squad => _squad;
        public TransportVehicle Transport => _transport;
        public int Seat => _seat;
        public DrivableVehicle Vehicle => _vehicle;
        public bool IsDriving => _mode == Mode.Driving;
        public bool IsInTransport => _mode == Mode.Transport;
        public PlayerCommand LastCommand => _lastCommand;
        public GameSettings Settings => _settings;

        /// <summary>İntikal aracı vardı ve inmeye hazır (F ile ya da 3 sn sonra otomatik).</summary>
        public bool CanDisembark => _mode == Mode.Transport
                                    && (_transport == null || _transport.HasArrived || _transport.IsUnloading || _transport.IsDeparting);

        /// <summary>Otomatik inişe kalan süre (araç vardıysa), yoksa -1.</summary>
        public float AutoDisembarkRemaining => _mode == Mode.Transport && _transportArrivedAt >= 0f
            ? Mathf.Max(0f, _transportArrivedAt + AutoDisembarkSeconds - Time.time)
            : -1f;

        /// <summary>Oyuncu timinin komutanı mı (komuta zinciri yoksa rolüne göre)?</summary>
        public bool IsCommander => _squad != null && _squad.IsCommander;

        /// <summary>V ile topçu desteği isteyebilir mi (komutan ya da timde yaşayan telsizci)?</summary>
        public bool CanCallArtillery => _squad != null && _squad.HasArtilleryAccess;

        /// <summary>Harita işareti (topçu/taarruz hedefi yedeği). FullMapView.PointMarked buraya bağlanabilir.</summary>
        public Vector3? MapMarker { get; private set; }

        /// <summary>Etkin kısa bilgi mesajı (yoksa null).</summary>
        public string NotificationText => _notification != null && Time.time < _notificationUntil ? _notification : null;

        public event Action Died;

        /// <summary>Kısa bilgi mesajı (metin, süre) — HUD isterse merkez mesajı olarak gösterebilir.</summary>
        public event Action<string, float> Notification;

        // ================================================================ oluşturma
        public static PlayerController Create(PlayerSpawnArgs args)
        {
            args ??= new PlayerSpawnArgs();

            var settings = ResolveSettings(args.Settings);
            var transport = args.Transport;
            var seatTransform = transport != null ? SafeSeat(transport, args.Seat) : null;
            if (transport != null && seatTransform == null)
                seatTransform = transport.transform;

            var spawnPosition = seatTransform != null ? seatTransform.position : SnapToGround(args.GroundPosition);
            var spawnYaw = seatTransform != null ? seatTransform.eulerAngles.y : args.GroundYaw;

            var go = new GameObject("Player");
            go.layer = GameLayers.Player;
            go.transform.SetPositionAndRotation(spawnPosition, Quaternion.Euler(0f, spawnYaw, 0f));

            // --- hareket (motor CharacterController ayarlarını kendi yapılandırmasından yazar)
            var cc = go.AddComponent<CharacterController>();
            cc.radius = CapsuleRadius;
            cc.height = ReferenceStandingHeight;
            cc.center = new Vector3(0f, ReferenceStandingHeight * 0.5f, 0f);
            cc.minMoveDistance = 0f;

            var config = LoadMovementConfig();
            var motor = go.AddComponent<CharacterControllerMotor>();
            SafeRun(() => motor.Configure(config), go);

            var input = go.AddComponent<UnityInputReader>();

            // --- kamera: Player → CameraPivot (pitch) → CameraOffset (ölüm kamerası) → CameraRig
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(0f, DefaultEyeHeight, 0f);
            pivot.localRotation = Quaternion.identity;

            var offset = new GameObject("CameraOffset").transform;
            offset.SetParent(pivot, false);

            var fov = Mathf.Clamp(settings.FieldOfView, SettingsService.MinFieldOfView, SettingsService.MaxFieldOfView);
            CameraRig rig = null;
            SafeRun(() => rig = CameraRig.Create(offset, fov, true), go);

            var camera = go.AddComponent<FirstPersonCameraController>();
            SafeRun(() => camera.Configure(pivot, config, rig), go);

            WeaponViewModel viewModel = null;
            var viewParent = rig != null ? rig.transform : offset;
            SafeRun(() => viewModel = WeaponViewModel.Create(viewParent, GameLayers.Viewmodel), go);

            // --- savaşan
            var rank = ResolveRank(args);
            var combatant = go.AddComponent<Combatant>();
            combatant.Rank = rank;
            combatant.DropLootOnDeath = true;

            GameContext.TryGet<IEventBus>(out var eventBus);
            GameContext.TryGet<IDamageableRegistry>(out var registry);
            var displayName = string.IsNullOrWhiteSpace(args.Name) ? settings.PlayerName : args.Name.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = SettingsService.DefaultPlayerName;

            combatant.Initialize(args.Id, displayName, true, false, args.Team, args.Role, eventBus, registry,
                args.MaxHealth > 0f ? args.MaxHealth : 100f);
            combatant.EyePoint = pivot;
            combatant.DropState = transport != null ? DropState.InTransport : DropState.Landed;
            CombatantRegistry.LocalPlayer = combatant;

            var hitboxes = PlayerHitboxRig.Build(go.transform, combatant);
            combatant.AimPoint = hitboxes.AimPoint;

            // --- teçhizat (botlarla aynı: LoadoutCatalog rol teçhizatı)
            if (combatant.Inventory != null)
            {
                SafeRun(() =>
                {
                    if (args.ApplyLoadout)
                        combatant.Inventory.ApplyLoadout(args.Loadout ?? LoadoutCatalog.For(args.Role), true);
                    if (args.InfiniteAmmo)
                        combatant.Inventory.InfiniteAmmo = true;
                }, go);
            }

            var footsteps = go.AddComponent<FootstepEmitter>();

            var controller = go.AddComponent<PlayerController>();
            controller.Setup(cc, motor, input, pivot, offset, rig, camera, viewModel, combatant, footsteps, hitboxes, settings);

            if (args.RegisterInMatch)
                controller.RegisterInMatch(displayName, rank, args.Team, args.Role);

            if (transport != null)
                controller.EnterTransport(transport, args.Seat);
            else
                controller.EnterOnFoot(spawnPosition, spawnYaw);

            return controller;
        }

        private void Setup(CharacterController cc, CharacterControllerMotor motor, UnityInputReader input, Transform pivot,
            Transform offset, CameraRig rig, FirstPersonCameraController camera, WeaponViewModel viewModel, Combatant combatant,
            FootstepEmitter footsteps, PlayerHitboxRig hitboxes, GameSettings settings)
        {
            _characterController = cc;
            _motor = motor;
            _input = input;
            _pitchPivot = pivot;
            _cameraOffset = offset;
            _rig = rig;
            _camera = camera;
            _viewModel = viewModel;
            _combatant = combatant;
            _footsteps = footsteps;
            _hitboxes = hitboxes;

            ResolveServices();

            _weapons = new PlayerWeaponHandler(this);
            _interaction = new PlayerInteraction(this);
            _squad = new SquadCommandInput(this);

            if (_combatant != null)
            {
                _combatant.Died += OnCombatantDied;
                _combatant.Damaged += OnCombatantDamaged;
            }

            if (_motor != null)
                _motor.Landed += OnMotorLanded;

            ApplySettings(settings);

            if (_input != null)
                _input.GameplayEnabled = _inputEnabled;

            Local = this;
            _lastPosition = transform.position;
            _initialized = true;

            if (_inputEnabled)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void RegisterInMatch(string displayName, MilitaryRank rank, int team, TeamRole role)
        {
            var id = _combatant != null ? _combatant.Id : PlayerId.Invalid;
            if (!id.IsValid)
                return;

            try
            {
                if (GameContext.TryGet<IMatchService>(out var match))
                    match.RegisterCombatant(id, RankCatalog.FormatName(rank, displayName), true, team, role);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            try
            {
                _chain?.Register(id, team, rank);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        // ================================================================ ayarlar
        /// <summary>Hassasiyet, nişan hassasiyeti çarpanı, FOV ve ters Y'yi uygular (SettingsService değişince otomatik).</summary>
        public void ApplySettings(GameSettings settings)
        {
            if (settings == null)
                return;

            _settings = settings;
            _baseSensitivity = Mathf.Clamp(settings.MouseSensitivity, SettingsService.MinMouseSensitivity, SettingsService.MaxMouseSensitivity);
            _adsSensitivityMultiplier = Mathf.Clamp(settings.AdsSensitivityMultiplier, SettingsService.MinAdsMultiplier, SettingsService.MaxAdsMultiplier);
            _appliedSensitivity = -1f;

            if (_camera == null)
                return;

            var fov = Mathf.Clamp(settings.FieldOfView, SettingsService.MinFieldOfView, SettingsService.MaxFieldOfView);
            try
            {
                _camera.InvertY = settings.InvertY;
                _camera.BaseFieldOfView = fov;
                _camera.SetFieldOfView(fov);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            UpdateSensitivity();
        }

        private void OnSettingsChanged(GameSettings settings) => ApplySettings(settings);

        // ================================================================ kare döngüsü
        private void Update()
        {
            if (!_initialized)
                return;

            var dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            ResolveServicesIfChanged();

            // 1) Girdi → komut (okuyucu kapalıysa sıfır döner)
            MovementInputState movement;
            LookInputState look;
            CombatInputState combatInput;
            if (_input != null && _inputEnabled && _mode != Mode.Dead)
            {
                movement = _input.ReadMovement();
                look = _input.ReadLook();
                combatInput = _input.ReadCombat();
            }
            else
            {
                movement = MovementInputState.Zero;
                look = LookInputState.Zero;
                combatInput = CombatInputState.Zero;
            }

            if (_mode != Mode.Dead)
                ApplyLook(look);

            var tick = _clock != null ? _clock.CurrentTick : ++_localTick;
            var command = PlayerCommand.From(tick, movement, combatInput, transform.eulerAngles.y, CurrentPitch());
            _lastCommand = command;

            if (_commandSink != null && _combatant != null)
            {
                try
                {
                    _commandSink.Submit(_combatant.Id, command);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                    _commandSink = null;
                }
            }

            // 2) Yerel simülasyon (çevrimdışı otorite / istemci tahmini)
            Simulate(command, look, dt);
        }

        private void LateUpdate()
        {
            if (!_initialized)
                return;

            switch (_mode)
            {
                case Mode.Transport:
                    if (_transport != null && transform.parent == null)
                    {
                        // Araç yolcuyu bıraktı (ayrılış): iniş akışını tamamla.
                        Disembark();
                        break;
                    }

                    AlignSeat(_transport != null ? SafeViewPoint(_transport, _seat) : null);
                    break;
                case Mode.Driving:
                    AlignSeat(_vehicle != null ? _vehicle.DriverViewPoint : null);
                    break;
                case Mode.Dead:
                    UpdateDeathCam();
                    break;
            }

            if (_combatant != null && _mode != Mode.OnFoot)
            {
                var dt = Time.deltaTime;
                if (dt > 0f)
                    _combatant.Velocity = _mode == Mode.Dead ? Vector3.zero : (transform.position - _lastPosition) / dt;
            }

            _lastPosition = transform.position;
        }

        private void Simulate(PlayerCommand command, LookInputState look, float dt)
        {
            switch (_mode)
            {
                case Mode.OnFoot:
                    SimulateOnFoot(command, look, dt);
                    break;
                case Mode.Transport:
                    SimulateTransport(command, dt);
                    break;
                case Mode.Driving:
                    SimulateDriving(command, dt);
                    break;
                default:
                    return;
            }

            if (_mode != Mode.Dead)
                _squad.Tick(dt);
        }

        private void SimulateOnFoot(PlayerCommand command, LookInputState look, float dt)
        {
            var movement = command.ToMovement();
            var combatInput = command.ToCombat();

            // Zıplama eşya kullanımını keser.
            var itemUse = ItemUse;
            if (movement.Jump && itemUse != null && itemUse.IsUsing)
                itemUse.Cancel();

            // Nişan/ateş koşuyu bastırır.
            if (movement.Sprint && _weapons.WantsSprintSuppressed(combatInput))
            {
                movement = new MovementInputState(movement.Forward, movement.Right, false, movement.Jump, movement.Crouch,
                    movement.CrouchToggle, movement.ProneToggle, movement.LeanLeft, movement.LeanRight);
            }

            if (_motor != null)
            {
                var speedMultiplier = _combatant != null ? _combatant.MovementSpeedMultiplier : 1f;
                if (_weapons.IsAiming)
                    speedMultiplier *= AdsMoveSpeedFactor;

                _motor.SpeedMultiplier = speedMultiplier;
                _motor.ApplyMovement(movement, dt);
            }

            SyncCombatantFromMotor();
            SyncCameraFromMotor();
            if (_motor != null)
                _hitboxes?.Follow(_motor.CurrentStance, _motor.CurrentHeight, false);

            _weapons.Tick(combatInput, look, dt);
            UpdateSensitivity();
            _interaction.Tick(combatInput, dt);
        }

        private void SimulateTransport(PlayerCommand command, float dt)
        {
            if (_transport == null || transform.parent == null)
            {
                // Araç yok oldu ya da yolcuyu bıraktı: bulunduğu/iniş noktasında in.
                Disembark();
                return;
            }

            if (_transportArrivedAt < 0f && (_transport.HasArrived || _transport.IsUnloading))
                OnTransportArrived();

            if (_transport.IsDeparting)
            {
                Disembark();
                return;
            }

            SeatedTick(command, dt);

            if (_mode == Mode.Transport && _transportArrivedAt >= 0f && Time.time >= _transportArrivedAt + AutoDisembarkSeconds)
                Disembark();
        }

        private void SimulateDriving(PlayerCommand command, float dt)
        {
            var vehicle = _vehicle;
            if (vehicle == null || !vehicle.isActiveAndEnabled || vehicle.Health <= 0f
                || (vehicle.Driver != null && vehicle.Driver != _combatant))
            {
                ExitVehicle(false);
                return;
            }

            var brake = false;
            if (GameplayInputActive)
            {
                var keyboard = Keyboard.current;
                brake = keyboard != null && keyboard.spaceKey.isPressed;
            }

            try
            {
                vehicle.SetInput(command.MoveForward, command.MoveRight, brake);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            SeatedTick(command, dt);
        }

        private void SeatedTick(PlayerCommand command, float dt)
        {
            if (_camera != null)
            {
                _camera.SetBob(0f, true);
                _camera.SetLean(0f);
            }

            _hitboxes?.Follow(Stance.Crouching, ReferenceStandingHeight, true);
            if (_combatant != null)
                _combatant.Stance = Stance.Crouching;

            _weapons.TickPassive(dt);
            UpdateSensitivity();
            _interaction.Tick(command.ToCombat(), dt);
        }

        // ================================================================ bakış
        private void ApplyLook(LookInputState look)
        {
            if (_camera == null)
                return;

            if (Mathf.Abs(look.PitchDelta) < 1e-6f && Mathf.Abs(look.YawDelta) < 1e-6f)
                return;

            // Kamera pitch'i (hassasiyet, ters Y, dürbün ölçeği) uygular ve yaw'ı dereceye çevirir; gövdeyi motor döndürür.
            _camera.ApplyLook(look.PitchDelta, look.YawDelta);
            var yawDegrees = _camera.LastYawDegrees;
            if (_motor != null)
                _motor.ApplyRotation(yawDegrees);
            else
                transform.Rotate(0f, yawDegrees, 0f, Space.Self);
        }

        private float CurrentPitch() => _camera != null ? _camera.Pitch : 0f;

        /// <summary>Ayar hassasiyeti (nişan alırken ADS çarpanıyla). Dürbün FOV ölçeklemesini kamera yapar.</summary>
        public float CurrentSensitivity
        {
            get
            {
                var sensitivity = _baseSensitivity;
                if (_weapons != null && _weapons.IsAiming)
                    sensitivity *= _adsSensitivityMultiplier;

                return sensitivity;
            }
        }

        private void UpdateSensitivity()
        {
            if (_camera == null)
                return;

            var sensitivity = CurrentSensitivity;
            if (Mathf.Abs(sensitivity - _appliedSensitivity) < 1e-6f)
                return;

            _appliedSensitivity = sensitivity;
            _camera.SetSensitivity(sensitivity);
        }

        private void SyncCombatantFromMotor()
        {
            if (_combatant == null || _motor == null)
                return;

            _combatant.Stance = _motor.CurrentStance;
            _combatant.Velocity = _motor.Velocity;
        }

        private void SyncCameraFromMotor()
        {
            if (_camera == null || _motor == null)
                return;

            _camera.SetEyeHeight(_motor.EyeHeight);
            _camera.SetLean(_motor.Lean);
            _camera.SetBob(_motor.SpeedNormalized, _motor.IsGrounded);
        }

        // ================================================================ yerde başlama / ışınlama
        private void EnterOnFoot(Vector3 position, float yaw)
        {
            _mode = Mode.OnFoot;
            TeleportMotor(position, yaw);
            SetPhysicalBody(true);
            if (_combatant != null)
                _combatant.DropState = DropState.Landed;

            _fallGraceUntil = Time.time + DisembarkFallGraceSeconds;
            _weapons.SetViewModelVisible(true);
        }

        /// <summary>Oyuncuyu (yerdeyse) belirtilen noktaya ışınlar.</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            if (_mode != Mode.OnFoot)
                return;

            TeleportMotor(position, yaw);
            _fallGraceUntil = Time.time + DisembarkFallGraceSeconds;
        }

        /// <summary>Antrenman: canlandırır ve verilen noktaya koyar.</summary>
        public void Respawn(Vector3 position, float yaw)
        {
            if (_combatant == null)
                return;

            if (_transport != null || _vehicle != null)
                LeaveVehicleImmediate();

            _combatant.Revive();
            _combatant.DropState = DropState.Landed;
            try
            {
                _chain?.Revive(_combatant.Id);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            _mode = Mode.OnFoot;
            if (_camera != null)
                _camera.enabled = true;
            ResetCameraOffset();
            TeleportMotor(SnapToGround(position), yaw);
            SetPhysicalBody(true);
            _fallGraceUntil = Time.time + DisembarkFallGraceSeconds;
            _weapons.ResetState();
            _weapons.SetViewModelVisible(true);
            if (_input != null)
                _input.GameplayEnabled = _inputEnabled;
        }

        private void TeleportMotor(Vector3 position, float yaw)
        {
            if (_motor != null)
            {
                _motor.Teleport(position, yaw);
                _lastPosition = transform.position;
                return;
            }

            var ccEnabled = _characterController != null && _characterController.enabled;
            if (ccEnabled)
                _characterController.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            if (ccEnabled)
                _characterController.enabled = true;
            _lastPosition = transform.position;
        }

        // ================================================================ intikal aracı
        private void EnterTransport(TransportVehicle transport, int seat)
        {
            if (transport == null)
            {
                EnterOnFoot(transform.position, transform.eulerAngles.y);
                return;
            }

            _transport = transport;
            _seat = Mathf.Max(0, seat);
            _transportArrivedAt = -1f;
            _mode = Mode.Transport;

            if (!_transportHooked)
            {
                transport.Arrived += OnTransportArrived;
                _transportHooked = true;
            }

            SetPhysicalBody(false);
            var seatTransform = SafeSeat(transport, _seat);
            _seatTransform = seatTransform != null ? seatTransform : transport.transform;
            transform.SetParent(_seatTransform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            _lastPosition = transform.position;

            if (_combatant != null)
            {
                _combatant.DropState = DropState.InTransport;
                _combatant.Stance = Stance.Crouching;
            }

            _weapons.OnLeaveFoot();
            _weapons.SetViewModelVisible(false);
            AlignSeat(SafeViewPoint(transport, _seat));

            if (transport.HasArrived || transport.IsUnloading)
                OnTransportArrived();
        }

        private void OnTransportArrived()
        {
            if (_mode != Mode.Transport || _transportArrivedAt >= 0f)
                return;

            _transportArrivedAt = Time.time;
            Notify("İniş bölgesine varıldı — [F] araçtan in", AutoDisembarkSeconds);
        }

        /// <summary>İntikal aracından iner (araç vardıysa ya da zorunluysa): iniş noktasına konur, kontrol açılır.</summary>
        public void Disembark()
        {
            if (_mode != Mode.Transport)
                return;

            var transport = _transport;
            var point = transform.position;
            if (transport != null)
            {
                try
                {
                    point = transport.GetDisembarkPoint(_seat);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }

            UnhookTransport();

            var yaw = Yaw;
            transform.SetParent(null, true);

            _mode = Mode.OnFoot;
            TeleportMotor(SnapToGround(point), yaw);
            SetPhysicalBody(true);
            _fallGraceUntil = Time.time + DisembarkFallGraceSeconds;

            if (_combatant != null)
                _combatant.DropState = DropState.Landed;

            _weapons.SetViewModelVisible(true);
            PlaySound2D(SoundId.VehicleDoor, 0.6f);
        }

        private void UnhookTransport()
        {
            if (_transport != null && _transportHooked)
                _transport.Arrived -= OnTransportArrived;

            _transportHooked = false;
            _transport = null;
            _seatTransform = null;
            _transportArrivedAt = -1f;
        }

        // ================================================================ sürülebilir araç
        internal bool TryEnterVehicle(DrivableVehicle vehicle)
        {
            if (_mode != Mode.OnFoot || vehicle == null || _combatant == null || vehicle.HasDriver)
                return false;

            bool entered;
            try
            {
                entered = vehicle.TryEnter(_combatant);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                entered = false;
            }

            if (!entered)
                return false;

            _weapons.OnLeaveFoot();
            _vehicle = vehicle;
            _mode = Mode.Driving;

            SetPhysicalBody(false);
            transform.SetParent(vehicle.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            _lastPosition = transform.position;

            if (_combatant != null)
                _combatant.Stance = Stance.Crouching;

            _weapons.SetViewModelVisible(false);
            AlignSeat(vehicle.DriverViewPoint);
            PlaySound2D(SoundId.VehicleDoor, 0.7f);
            return true;
        }

        /// <summary>Sürülen araçtan iner. notifyVehicle=false: araç zaten bırakmış/yok olmuş olabilir.</summary>
        internal void ExitVehicle(bool notifyVehicle = true)
        {
            if (_mode != Mode.Driving)
                return;

            var vehicle = _vehicle;
            _vehicle = null;

            if (vehicle != null && (notifyVehicle || vehicle.Driver == _combatant))
            {
                try
                {
                    vehicle.SetInput(0f, 0f, true);
                    vehicle.Exit();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }

            var yaw = Yaw;
            transform.SetParent(null, true);

            var exitPoint = vehicle != null ? FindVehicleExitPoint(vehicle.transform) : transform.position;
            _mode = Mode.OnFoot;
            TeleportMotor(exitPoint, yaw);
            SetPhysicalBody(true);
            _fallGraceUntil = Time.time + VehicleExitFallGraceSeconds;
            _weapons.SetViewModelVisible(true);
            PlaySound2D(SoundId.VehicleDoor, 0.7f);
        }

        private static Vector3 FindVehicleExitPoint(Transform vehicle)
        {
            var origin = vehicle.position;
            var right = vehicle.right;
            var forward = vehicle.forward;

            for (var i = 0; i < 4; i++)
            {
                Vector3 candidate;
                switch (i)
                {
                    case 0: candidate = origin - right * 2.4f; break;
                    case 1: candidate = origin + right * 2.4f; break;
                    case 2: candidate = origin - forward * 4f; break;
                    default: candidate = origin + forward * 4f; break;
                }

                var p = SnapToGround(candidate + Vector3.up * 1.5f);
                var bottom = p + Vector3.up * (CapsuleRadius + 0.05f);
                var top = p + Vector3.up * (ReferenceStandingHeight - CapsuleRadius);
                if (!Physics.CheckCapsule(bottom, top, CapsuleRadius, GameLayers.MovementBlockMask, QueryTriggerInteraction.Ignore))
                    return p;
            }

            // Hepsi kapalı: aracın üstü.
            return origin + Vector3.up * 3f;
        }

        /// <summary>
        /// Oturma pozu: kök koltuk zemininde, bakış noktasının altında; göz yüksekliği bakış noktasına eşitlenir
        /// (kamera denetleyicisi yumuşatarak uygular). Bakış (yaw/pitch) serbest kalır.
        /// </summary>
        private void AlignSeat(Transform viewPoint)
        {
            var seat = transform.parent;
            if (viewPoint == null || seat == null)
                return;

            var local = seat.InverseTransformPoint(viewPoint.position);
            var target = new Vector3(local.x, 0f, local.z);
            if ((transform.localPosition - target).sqrMagnitude > 1e-8f)
                transform.localPosition = target;

            var scaleY = seat.lossyScale.y;
            var eye = local.y * (Mathf.Abs(scaleY) > 1e-4f ? scaleY : 1f);
            _camera?.SetEyeHeight(Mathf.Clamp(eye, 0.2f, 4f));
        }

        private void LeaveVehicleImmediate()
        {
            if (_vehicle != null)
            {
                try
                {
                    if (_vehicle.Driver == _combatant)
                    {
                        _vehicle.SetInput(0f, 0f, true);
                        _vehicle.Exit();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }

                _vehicle = null;
            }

            UnhookTransport();
            var yaw = Yaw;
            transform.SetParent(null, true);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ================================================================ hasar / düşme / ölüm
        private void OnMotorLanded(float impactSpeed)
        {
            if (_mode != Mode.OnFoot || _combatant == null || !_combatant.IsAlive)
                return;

            if (impactSpeed > 7f)
                _camera?.Shake(Mathf.Clamp01((impactSpeed - 7f) / 14f), 0.3f);

            if (impactSpeed <= SafeFallSpeed || Time.time < _fallGraceUntil)
                return;

            if (_combatant.DropState != DropState.Landed || !GameContext.HasAuthority)
                return;

            var damage = (impactSpeed - SafeFallSpeed) * FallDamagePerMetrePerSecond;
            if (damage <= 0.5f)
                return;

            try
            {
                if (_combat != null)
                    _combat.ApplyEnvironmentalDamage(_combatant.Id, DamageSourceIds.Fall, damage);
                else
                    _combatant.ApplyDamage(new DamageInfo(damage, PlayerId.Invalid, DamageSourceIds.Fall));
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            PlaySound2D(SoundId.Land, 0.9f);
        }

        private void OnCombatantDamaged(Combatant combatant, DamageInfo damage)
        {
            if (_mode == Mode.Dead || _camera == null)
                return;

            _camera.Shake(Mathf.Clamp(damage.Amount / 60f, 0.1f, 0.8f), 0.22f);
        }

        private void OnCombatantDied(Combatant combatant, DamageInfo damage)
        {
            if (_mode == Mode.Dead)
                return;

            if (_mode == Mode.Driving || _mode == Mode.Transport)
                LeaveVehicleImmediate();

            _mode = Mode.Dead;
            if (_input != null)
                _input.GameplayEnabled = false;

            try
            {
                _weapons?.OnDeath();
                _interaction?.Clear();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            SetPhysicalBody(false);
            _hitboxes?.SetActive(false);
            _weapons?.SetViewModelVisible(false);
            BeginDeathCam(damage);

            try
            {
                Died?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void BeginDeathCam(DamageInfo damage)
        {
            _deathTime = Time.time;
            if (_cameraOffset == null)
                return;

            _deathCamStartPos = _cameraOffset.position;
            _deathCamStartRot = _cameraOffset.rotation;

            // Kamera denetleyicisi kapatılır: ölüm kamerası pozu tamamen buradan yazılır (sarsıntı/sallantı sıfırlanır).
            if (_camera != null)
            {
                _camera.SetZoom(1f);
                _camera.enabled = false;
            }

            var groundY = _deathCamStartPos.y - 1.4f;
            if (Physics.Raycast(_deathCamStartPos, Vector3.down, out var hit, 60f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                groundY = hit.point.y;

            // Darbe yönünden uzağa doğru hafifçe devril.
            var fallDirection = transform.forward;
            if (damage.HasSourcePosition)
            {
                var source = new Vector3(damage.SourcePosition.X, damage.SourcePosition.Y, damage.SourcePosition.Z);
                var away = _deathCamStartPos - source;
                away.y = 0f;
                if (away.sqrMagnitude > 0.01f)
                    fallDirection = away.normalized;
            }

            fallDirection.y = 0f;
            if (fallDirection.sqrMagnitude < 1e-4f)
                fallDirection = Vector3.forward;
            fallDirection.Normalize();

            var end = _deathCamStartPos + fallDirection * 0.45f;
            end.y = groundY + DeathCamGroundOffset;
            if (Physics.Linecast(_deathCamStartPos, end, out var block, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
                end = block.point + block.normal * 0.15f;

            _deathCamEndPos = end;
            var yaw = _deathCamStartRot.eulerAngles.y;
            var roll = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * DeathCamRollDegrees;
            _deathCamEndRot = Quaternion.Euler(-8f, yaw, roll);
        }

        private void UpdateDeathCam()
        {
            if (_cameraOffset == null)
                return;

            var t = Mathf.Clamp01((Time.time - _deathTime) / DeathCamSeconds);
            // Yerçekimi hissi: hızlanarak düş; dönüş yumuşak biter.
            var fall = t * t;
            var rotT = 1f - (1f - t) * (1f - t);
            var pos = Vector3.LerpUnclamped(_deathCamStartPos, _deathCamEndPos, fall);
            _cameraOffset.SetPositionAndRotation(pos, Quaternion.Slerp(_deathCamStartRot, _deathCamEndRot, rotT));
        }

        private void ResetCameraOffset()
        {
            if (_cameraOffset == null)
                return;

            _cameraOffset.localPosition = Vector3.zero;
            _cameraOffset.localRotation = Quaternion.identity;
        }

        // ================================================================ fizik gövdesi
        /// <summary>Yaya: motor kontrolü (CharacterController'ı da açar), ayak sesi, vuruş kutuları. Araçta/ölüde kapalı.</summary>
        private void SetPhysicalBody(bool onFoot)
        {
            if (_motor != null)
                _motor.ControlEnabled = onFoot;
            else if (_characterController != null)
                _characterController.enabled = onFoot;

            if (_footsteps != null)
                _footsteps.enabled = onFoot;

            if (onFoot)
                _hitboxes?.SetActive(true);
        }

        // ================================================================ alt modüller için yardımcılar
        internal ChainOfCommandService Chain => _chain;
        internal SquadOrderService Orders => _orders;
        internal ArtilleryService Artillery => _artillery;

        /// <summary>Nişan başlangıcı (göz + eğilme; kamera denetleyicisi yoksa pivot).</summary>
        public Vector3 AimOrigin
        {
            get
            {
                if (_camera != null && _camera.enabled)
                    return _camera.AimOrigin;

                if (_rig != null)
                    return _rig.transform.position;
                return _pitchPivot != null ? _pitchPivot.position : transform.position + Vector3.up * DefaultEyeHeight;
            }
        }

        /// <summary>Nişan yönü (gövde yaw + bakış + sekme).</summary>
        public Vector3 AimForward
        {
            get
            {
                if (_camera != null && _camera.enabled)
                {
                    var forward = _camera.AimForward;
                    if (forward.sqrMagnitude > 1e-6f)
                        return forward.normalized;
                }

                if (_rig != null)
                    return _rig.transform.forward;
                return _pitchPivot != null ? _pitchPivot.forward : transform.forward;
            }
        }

        /// <summary>Nişan noktasını (arazi/yapı/araç) bulur; bulamazsa false ve azami menzildeki nokta.</summary>
        public bool TryGetAimPoint(float maxDistance, out Vector3 point)
        {
            var origin = AimOrigin;
            var forward = AimForward;
            if (Physics.Raycast(origin, forward, out var hit, maxDistance, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            point = origin + forward * maxDistance;
            return false;
        }

        public void SetMapMarker(Vector3 worldPoint)
        {
            MapMarker = worldPoint;
        }

        public void ClearMapMarker()
        {
            MapMarker = null;
        }

        /// <summary>Kısa süreli bilgi mesajı gösterir (InteractionPrompt üzerinden + Notification olayı).</summary>
        public void Notify(string text, float seconds = 2f)
        {
            if (string.IsNullOrEmpty(text))
                return;

            _notification = text;
            _notificationUntil = Time.time + Mathf.Max(0.25f, seconds);

            var handler = Notification;
            if (handler == null)
                return;

            try
            {
                handler(text, seconds);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        internal static void PlaySound2D(SoundId id, float volume)
        {
            try
            {
                GameAudio.Play2D(id, volume);
            }
            catch (Exception)
            {
                // ses sistemi isteğe bağlı
            }
        }

        // ================================================================ servisler
        private void ResolveServicesIfChanged()
        {
            if (!_servicesResolved || !ReferenceEquals(GameContext.Services, _servicesFor))
                ResolveServices();
        }

        private void ResolveServices()
        {
            var services = GameContext.Services;
            if (_settingsService != null)
                _settingsService.Changed -= OnSettingsChanged;

            _servicesFor = services;
            _servicesResolved = services != null;
            _combat = null;
            _chain = null;
            _orders = null;
            _artillery = null;
            _settingsService = null;
            _commandSink = null;
            _clock = null;

            if (services == null)
                return;

            try
            {
                services.TryResolve(out _combat);
                services.TryResolve(out _chain);
                services.TryResolve(out _orders);
                services.TryResolve(out _artillery);
                services.TryResolve(out _settingsService);
                services.TryResolve(out _commandSink);
                if (!services.TryResolve(out _clock) && services.TryResolve<SimulationClock>(out var clock))
                    _clock = clock;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            if (_settingsService != null)
                _settingsService.Changed += OnSettingsChanged;
        }

        // ================================================================ statik yardımcılar
        private static GameSettings ResolveSettings(GameSettings settings)
        {
            if (settings != null)
                return settings;

            if (GameContext.TryGet<SettingsService>(out var service) && service.Current != null)
                return service.Current;

            return new GameSettings();
        }

        /// <summary>
        /// Rütbe: açıkça verilmişse o; değilse kariyer rütbesi (yoksa Yüzbaşı). Tim komutanı en az Yüzbaşı olur ki
        /// komuta zinciri oyuncuyla başlasın (kariyer daha yüksekse o kullanılır).
        /// </summary>
        private static MilitaryRank ResolveRank(PlayerSpawnArgs args)
        {
            if (args.Rank.HasValue)
                return args.Rank.Value;

            var rank = MilitaryRank.Yuzbasi;
            if (GameContext.TryGet<CareerStatsService>(out var career) && career.Current != null)
                rank = career.Current.Rank;

            if (args.Role == TeamRole.Leader && rank < MilitaryRank.Yuzbasi)
                rank = MilitaryRank.Yuzbasi;

            return rank;
        }

        private static PlayerMovementConfig LoadMovementConfig()
        {
            if (_defaultConfig != null)
                return _defaultConfig;

            try
            {
                _defaultConfig = Resources.Load<PlayerMovementConfig>("PlayerMovementConfig");
            }
            catch (Exception)
            {
                _defaultConfig = null;
            }

            if (_defaultConfig == null)
            {
                _defaultConfig = ScriptableObject.CreateInstance<PlayerMovementConfig>();
                _defaultConfig.name = "PlayerMovementConfig (varsayılan)";
                _defaultConfig.hideFlags = HideFlags.DontSave;
            }

            return _defaultConfig;
        }

        private static Vector3 SnapToGround(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out var hit, 80f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;

            return position;
        }

        private static Transform SafeSeat(TransportVehicle transport, int seat)
        {
            try
            {
                return transport.GetSeat(seat);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Transform SafeViewPoint(TransportVehicle transport, int seat)
        {
            try
            {
                return transport.PassengerViewPoint(seat);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void SafeRun(Action action, UnityEngine.Object context)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e, context);
            }
        }

        // ================================================================ yaşam döngüsü
        private void OnDisable()
        {
            if (_vehicle != null && _mode == Mode.Driving)
            {
                try
                {
                    _vehicle.SetInput(0f, 0f, true);
                }
                catch (Exception)
                {
                    // yok sayılır
                }
            }
        }

        private void OnDestroy()
        {
            if (_combatant != null)
            {
                _combatant.Died -= OnCombatantDied;
                _combatant.Damaged -= OnCombatantDamaged;
            }

            if (_motor != null)
                _motor.Landed -= OnMotorLanded;

            if (_settingsService != null)
                _settingsService.Changed -= OnSettingsChanged;

            if (_mode == Mode.Driving && _vehicle != null)
            {
                try
                {
                    if (_vehicle.Driver == _combatant)
                        _vehicle.Exit();
                }
                catch (Exception)
                {
                    // sahne kapanıyor olabilir
                }
            }

            UnhookTransport();
            _weapons?.Dispose();
            _squad?.Dispose();

            if (Local == this)
                Local = null;

            if (_combatant != null && CombatantRegistry.LocalPlayer == _combatant)
                CombatantRegistry.LocalPlayer = null;

            Died = null;
            Notification = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Local = null;
            _defaultConfig = null;
        }
    }
}
