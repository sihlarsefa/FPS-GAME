using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.AI;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Infrastructure.DI;
using Project.Infrastructure.Player;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Vehicles;
using Project.Infrastructure.World;
using Project.Presentation.Bootstrap;
using Project.Presentation.DevTools;
using Project.Presentation.Player;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Project.Presentation.Benchmark
{
    /// <summary>
    /// "AAA_Benchmark" sahnesi kompozisyon kökü. 150×150 m'lik küçük bir parça: çamur + kaya + çim bölgeleri, küçük orman,
    /// girilebilir köy evi, Kirpi, nöbetçi asker, çelik hedefler + beton duvar (dekal/VFX), gündüz + hafif sis.
    /// Oyuncu MPT-76 ile doğar (TrainingBootstrap ile aynı oyuncu yolu). İki mod: Oyna / Vitrin (bkz. <see cref="AaaBenchmarkVitrin"/>).
    /// Tüm G4 AAA özellikleri Ultra kademede <see cref="AaaBenchmarkFeatureInstaller"/> ile açılır. Hedef: bu küçük sahne
    /// "gerçekten çok iyi" olmadan büyük haritalara geçilmez (bkz. Docs/CURSOR_AAA_BENCHMARK.md).
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class AaaBenchmarkBootstrap : MonoBehaviour
    {
        public const string MenuLabel = "AAA TEST SAHNESİ";
        public const string MenuLabelShowcase = "AAA VİTRİN";
        public const int BenchmarkTier = AaaBenchmarkFeatureInstaller.UltraTier;
        private const int Seed = 20261006;
        private const int SoldierId = 700;
        private const int TargetIdBase = 6000;

        /// <summary>Sonraki yüklemede Vitrin modu ile başla (menü düğmesi ayarlar, sahne tüketir).</summary>
        public static bool StartInVitrin { get; set; }

        [Tooltip("İşaretliyse sahne açılışında Vitrin modu ile başlar.")]
        [SerializeField] private bool startInShowcase;

        [Tooltip("Sahnede WorldMetadata yoksa parça çalışma zamanında kurulur.")]
        [SerializeField] private bool buildIfMissing = true;

        private readonly List<DamageableTarget> _targets = new List<DamageableTarget>(8);
        private ServiceContainer _container;
        private MatchConfig _config;
        private IEventBus _eventBus;
        private MatchService _match;
        private CombatService _combat;
        private DamageableRegistry _damageables;
        private SettingsService _settings;
        private WorldMetadata _world;
        private PlayerController _player;
        private GameplayUiController _ui;
        private GameLoopPresenter _loop;
        private AaaBenchmarkVitrin _vitrin;
        private Transform _runtimeRoot;
        private Terrain _terrain;
        private Action<GameSettings> _onSettingsChanged;
        private BuildingResult _house;
        private BotController _soldier;
        private AaaBenchmarkShooter _shooter;
        private DrivableVehicle _vehicle;
        private bool _setupComplete;
        private bool _disposed;
        private bool _redirect;
        private Vector3 _spawnPoint;
        private float _spawnYaw;
        private Vector3 _housePosition;
        private float _houseYaw;
        private Vector3 _vehiclePosition;
        private Vector3 _soldierPosition;
        private Vector3 _targetsCenter;
        private Vector3 _forestPoint;

        public static PlayerId LocalPlayerId => GameCompositionRoot.DefaultLocalPlayerId;

        public bool IsReady => _setupComplete;
        public PlayerController Player => _player;
        public Terrain Terrain => _terrain;

        // ------------------------------------------------------------------ Menü

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            AddButton(MenuLabel, false);
            AddButton(MenuLabelShowcase, true);
        }

        private static void AddButton(string label, bool vitrin)
        {
            var list = MainMenuController.ExtraButtons;
            for (var i = 0; i < list.Count; i++)
                if (list[i].label == label)
                    return;
            list.Add((label, _ => Begin(vitrin)));
        }

        /// <summary>Benchmark sahnesini yükler.</summary>
        public static void Begin(bool vitrin)
        {
            StartInVitrin = vitrin;
            GameSession.LoadScene(SceneNames.AaaBenchmark);
        }

        // ------------------------------------------------------------------ Yaşam döngüsü

        private void Awake()
        {
            GameSession.EnsureInitialized();
            if (ServerRuntime.IsDedicatedServer)
            {
                _redirect = true;
                return;
            }

            GameSession.Mode = GameMode.Training;
            BootstrapUtility.PrepareScene();

            _settings = GameSession.Settings;
            _config = new MatchConfig().WithTeams(2, 1);
            _config.MapName = "AAA Benchmark";
            _config.MapHalfSize = BenchmarkLayout.HalfSize;
            _config.InitialZoneRadius = 1000f;
            _config.PreMatchDurationSeconds = 0f;
            _config.FriendlyFire = false;
            _config.RandomSeed = Seed;
            _config.TimeOfDay = TimeOfDay.Gunduz;
            _config.Weather = WeatherKind.Acik;

            _container = GameCompositionRoot.Build(_config, _settings, GameSession.Career, LocalPlayerId);
            GameContext.Set(_container);
            _container.TryResolve(out _eventBus);
            _container.TryResolve(out _match);
            _container.TryResolve(out _combat);
            _container.TryResolve(out _damageables);
            if (_container.TryResolve<KillFeedService>(out var killFeed))
                killFeed.LocalTeamOverride = 0;

            // Ultra kademe: post-process + kalite tablosu doğrudan 3 ile kurulur (kayıtlı ayara dokunulmaz).
            BootstrapUtility.InitializeEngineSystems(PostProcessing.Look.Gameplay, BenchmarkTier, true);
        }

        private void Start()
        {
            if (_redirect)
            {
                GameSession.StartOperation();
                return;
            }

            if (_container == null)
                return;

            var sceneCameras = Camera.allCameras;
            _runtimeRoot = new GameObject("[AAA Benchmark]").transform;

            BootstrapUtility.Try(SetupWorld, "Benchmark arazisi");
            BootstrapUtility.Try(SetupProps, "Benchmark nesneleri");
            BootstrapUtility.Try(BakeNavMesh, "NavMesh");
            BootstrapUtility.Try(SetupCombatSystems, "Çatışma sistemleri");
            BootstrapUtility.Try(SpawnPlayer, "Oyuncu");
            BootstrapUtility.Try(SpawnTargets, "Hedefler");
            BootstrapUtility.Try(SpawnVehicle, "Kirpi");
            BootstrapUtility.Try(SpawnSoldier, "Nöbetçi asker");
            BootstrapUtility.Try(SpawnShooter, "Vitrin askeri");
            BootstrapUtility.Try(() => SetupPresentation(sceneCameras), "Arayüz");
            BootstrapUtility.Try(SetupLoop, "Simülasyon döngüsü");

            if (_match != null)
                BootstrapUtility.Try(() => _match.TransitionTo(MatchPhase.InMatch), "MatchService.TransitionTo");

            BootstrapUtility.Try(SetupAtmosphere, "Atmosfer");
            BootstrapUtility.Try(SetupReflectionProbe, "Yansıma probu");
            BootstrapUtility.Try(() => GameAudio.SetAmbience(SoundId.Wind, 0.35f), "GameAudio.SetAmbience");
            BootstrapUtility.Try(InstallFeatures, "G4 özellikleri");
            BootstrapUtility.Try(SetupVitrin, "Vitrin");
            BootstrapUtility.Try(() => DevConsole.Ensure(), "DevConsole.Ensure");

            _setupComplete = true;
            GameSession.HideLoading();
        }

        private void Update()
        {
            if (!_setupComplete || _disposed || _player == null)
                return;

            // Oyuncu ölürse başlangıçta canlanır (benchmark kesintisiz kalsın).
            if (_player.Combatant != null && _player.Combatant.IsInitialized && !_player.Combatant.IsAlive && !_respawning)
            {
                _respawning = true;
                Invoke(nameof(RespawnPlayer), 2f);
            }
        }

        private bool _respawning;

        private void RespawnPlayer()
        {
            _respawning = false;
            if (_player == null || _disposed)
                return;
            BootstrapUtility.Try(() => _player.Respawn(_spawnPoint, _spawnYaw), "PlayerController.Respawn");
            var inventory = _player.Combatant != null ? _player.Combatant.Inventory : null;
            if (inventory != null && !inventory.HasAnyWeapon)
                GiveKit(inventory);
        }

        private void OnDestroy()
        {
            Teardown();
        }

        // ------------------------------------------------------------------ Kurulum

        private void SetupWorld()
        {
            _world = WorldMetadata.Instance != null ? WorldMetadata.Instance : FindAnyObjectByType<WorldMetadata>();
            if (_world != null || !buildIfMissing)
            {
                BootstrapUtility.EnsureSun();
                return;
            }

            var terrain = AaaBenchmarkTerrain.Build(_runtimeRoot, Seed, BenchmarkTier);
            _terrain = terrain.Terrain;

            var meta = terrain.Root.AddComponent<WorldMetadata>();
            meta.Terrain = _terrain;
            meta.MapHalfSize = BenchmarkLayout.HalfSize;
            meta.WaterLevel = -50f;
            meta.MaxHeight = BenchmarkLayout.TerrainHeight + 10f;
            meta.MapCenter = Vector2.zero;
            meta.Locations = new List<NamedLocation>
            {
                new NamedLocation { Name = "AAA Benchmark", Center = Vector2.zero, Radius = BenchmarkLayout.HalfSize, IsMajor = true }
            };

            var spawn = BenchmarkLayout.SpawnPoint;
            meta.GroundSpawnPoints = new List<Vector3> { spawn };
            meta.LandingZones = new List<Vector3> { spawn };
            meta.LootPoints = new List<LootSpawnPointData>();
            meta.VehicleSpawns = new List<VehicleSpawnData>();
            meta.StructureBounds = new List<Bounds>();
            _world = meta;

            BootstrapUtility.EnsureSun();
            Debug.Log("[AAA Benchmark] Arazi hazır: " + terrain.TreeCount + " ağaç, " + terrain.RockCount + " kaya.");
        }

        private Vector3 Ground(float x, float z) => BootstrapUtility.GroundPoint(_world, new Vector3(x, 0f, z));

        private void SetupProps()
        {
            _spawnPoint = Ground(BenchmarkLayout.SpawnPoint.x, BenchmarkLayout.SpawnPoint.z) + Vector3.up * 0.05f;
            _spawnYaw = 0f; // +Z: atış hattına bakar

            // Ev: kapı oyuncu doğuş noktasına bakar (cephe 0 = yerel +Z).
            var house = BenchmarkLayout.HousePoint;
            _housePosition = Ground(house.x, house.z);
            var toSpawn = new Vector3(_spawnPoint.x - _housePosition.x, 0f, _spawnPoint.z - _housePosition.z);
            _houseYaw = Mathf.Atan2(toSpawn.x, toSpawn.z) * Mathf.Rad2Deg;
            _house = BootstrapUtility.Try(() => BuildingGenerator.Build(new BuildingSpec
            {
                Name = "Benchmark Köy Evi",
                Style = BuildingStyle.VillageHouse,
                Position = _housePosition,
                Yaw = _houseYaw,
                Width = 9f,
                Depth = 7.5f,
                Floors = 1,
                Seed = 7,
                Tier = LootTier.Low
            }, _runtimeRoot), "BuildingGenerator.Build");
            if (_house != null && _world != null)
                _world.StructureBounds.Add(_house.Bounds);

            // Atış alanı: beton duvar + çelik plakalar.
            var wall = BenchmarkLayout.WallPoint;
            var wallGround = Ground(wall.x, wall.z);
            var props = new GameObject("Atış Alanı").transform;
            props.SetParent(_runtimeRoot, false);
            StructureKit.CreateBox(props, "Beton Duvar", wallGround + new Vector3(0f, 1.6f, 0f), new Vector3(12f, 3.2f, 0.5f),
                Quaternion.identity, MaterialId.Concrete);
            StructureKit.CreateBox(props, "Duvar Tabanı", wallGround + new Vector3(0f, 0.15f, 0.2f), new Vector3(12.4f, 0.3f, 1f),
                Quaternion.identity, MaterialId.ConcreteDark);

            for (var i = 0; i < 3; i++)
            {
                var x = wall.x - 3.5f + i * 3.5f;
                var z = wall.z - 5f - i * 3.5f;
                var g = Ground(x, z);
                StructureKit.CreateBox(props, "Çelik Direk " + i, g + new Vector3(0f, 0.7f, 0f), new Vector3(0.08f, 1.4f, 0.08f),
                    Quaternion.identity, MaterialId.MetalDark);
                StructureKit.CreateBox(props, "Çelik Hedef " + i, g + new Vector3(0f, 1.45f, 0f), new Vector3(0.6f, 0.8f, 0.04f),
                    Quaternion.Euler(-6f, 0f, 0f), MaterialId.Rust);
            }

            _targetsCenter = Ground(wall.x, wall.z - 8f);
            _vehiclePosition = Ground(BenchmarkLayout.VehiclePoint.x, BenchmarkLayout.VehiclePoint.z) + Vector3.up * 0.3f;
            _soldierPosition = Ground(house.x - 8f, house.z - 13f);
            _forestPoint = Ground(BenchmarkLayout.ForestZone.x + 6f, BenchmarkLayout.ForestZone.y - 10f);
        }

        private void BakeNavMesh()
        {
            if (_world == null || _terrain == null)
                return;
            var bounds = new Bounds(new Vector3(0f, 8f, 0f), new Vector3(BenchmarkLayout.Size, 40f, BenchmarkLayout.Size));
            var data = NavMeshBaker.Bake(bounds, _terrain, GameLayers.WorldMask);
            if (data != null)
            {
                _world.NavMesh = data;
                NavMeshBaker.EnsureLoaded(data);
            }
            else
            {
                Debug.LogWarning("[AAA Benchmark] NavMesh pişirilemedi — asker olduğu yerde nöbet tutar.");
            }
        }

        private void SetupCombatSystems()
        {
            if (_combat != null)
                BootstrapUtility.Try(() => BallisticsSystem.Create(_combat, _eventBus), "BallisticsSystem.Create");
        }

        private void SpawnPlayer()
        {
            var settings = _settings != null ? _settings.Current : new GameSettings();
            var args = new PlayerSpawnArgs
            {
                Id = LocalPlayerId,
                Name = settings.PlayerName,
                Team = 0,
                Role = TeamRole.Leader,
                GroundPosition = _spawnPoint,
                GroundYaw = _spawnYaw,
                Settings = settings,
                ApplyLoadout = false,
                InfiniteAmmo = true,
                RegisterInMatch = false
            };

            _player = BootstrapUtility.Try(() => PlayerController.Create(args), "PlayerController.Create");
            if (_player == null)
                return;

            if (_player.Combatant != null)
                _player.Combatant.DropLootOnDeath = false;
            GiveKit(_player.Combatant != null ? _player.Combatant.Inventory : null);
        }

        private static void GiveKit(InventoryService inventory)
        {
            if (inventory == null)
                return;
            BootstrapUtility.Try(() => inventory.InfiniteAmmo = true, "InventoryService.InfiniteAmmo");
            BootstrapUtility.Try(() => inventory.EquipArmor(ItemIds.Vest2), "InventoryService.EquipArmor");
            BootstrapUtility.Try(() => inventory.EquipArmor(ItemIds.Helmet2), "InventoryService.EquipArmor");
            BootstrapUtility.Try(() => inventory.GiveWeapon(WeaponIds.Mpt76, true), "InventoryService.GiveWeapon");
            BootstrapUtility.Try(() => inventory.SetActiveSlot(InventoryService.PrimarySlotA), "InventoryService.SetActiveSlot");
        }

        private void SpawnTargets()
        {
            if (_eventBus == null || _damageables == null)
                return;
            var wall = BenchmarkLayout.WallPoint;
            for (var i = 0; i < 2; i++)
            {
                var ground = Ground(wall.x - 6.5f + i * 13f, wall.z - 12f);
                var id = TargetIdBase + i;
                var target = BootstrapUtility.Try(
                    () => DamageableTarget.CreateDummy(ground, _eventBus, _damageables, id, 400f), "DamageableTarget.CreateDummy");
                if (target == null)
                    continue;
                target.RespawnSeconds = 3f;
                target.transform.SetParent(_runtimeRoot, true);
                _targets.Add(target);
            }
        }

        private void SpawnVehicle()
        {
            var yaw = 70f;
            _vehicle = BootstrapUtility.Try(() => DrivableVehicle.Spawn(_vehiclePosition, yaw), "DrivableVehicle.Spawn");
        }

        private void SpawnSoldier()
        {
            var args = new BotSpawnArgs
            {
                Id = new PlayerId(SoldierId),
                Name = "Nöbetçi",
                Team = 0,
                Role = TeamRole.Rifleman,
                Difficulty = BotDifficulty.Normal,
                SpawnOnGround = true,
                GroundPosition = _soldierPosition,
                GroundYaw = 200f,
                Slot = 3,
                Seed = 9137,
                RegisterWithMatch = false,
                Parent = _runtimeRoot
            };

            _soldier = BootstrapUtility.Try(() => BotController.Create(args), "BotController.Create");
            if (_soldier == null)
                return;

            var patrol = _soldier.gameObject.AddComponent<AaaBenchmarkPatrol>();
            patrol.Initialize(_soldier.Agent, _soldierPosition, 3f);
        }

        /// <summary>Vitrin askeri: oyuncunun yanında, hedeflere bakan MPT-76'lı görünür asker (Vitrin planları 1 ve 6).</summary>
        private void SpawnShooter()
        {
            if (_shooter != null)
                return;
            var toTargets = _targetsCenter - _spawnPoint;
            var flat = BenchmarkShotPath.FlatDir(toTargets, Vector3.forward);
            var right = Vector3.Cross(Vector3.up, flat);
            var pos = Ground(_spawnPoint.x + right.x * 1.2f, _spawnPoint.z + right.z * 1.2f);
            _shooter = AaaBenchmarkShooter.Create(_runtimeRoot, pos, flat, _targetsCenter + Vector3.up * 1.1f);
        }

        private void SetupPresentation(Camera[] sceneCameras)
        {
            _ui = GameplayUiController.Create(_runtimeRoot, _player, _settings, GameSession.ReturnToMainMenu);
            var replaced = BootstrapUtility.DisableSceneCamerasIfReplaced(sceneCameras);
            if (!replaced && Camera.allCamerasCount == 0)
                BootstrapUtility.CreateObserverCamera(_spawnPoint + Vector3.forward * 20f);

            if (_ui != null)
                _ui.ShowMessage("AAA BENCHMARK — F9: Vitrin/Oyna, F12: ekran görüntüsü", 6f);

            if (_settings != null)
            {
                _onSettingsChanged = s => { if (_ui != null) _ui.ApplySettings(s); };
                _settings.Changed += _onSettingsChanged;
            }
        }

        private void SetupLoop()
        {
            _loop = GetComponent<GameLoopPresenter>();
            if (_loop == null)
                _loop = gameObject.AddComponent<GameLoopPresenter>();
            _container.TryResolve<SimulationClock>(out var clock);
            _container.TryResolve<GameTickCoordinator>(out var tick);
            _loop.Initialize(clock, tick, _match);
        }

        /// <summary>Gündüz + hafif sis: açık hava, sis yoğunluğu hafifçe artırılır (uzak katmanlar belirgin olsun).</summary>
        private void SetupAtmosphere()
        {
            Atmosphere.Apply(TimeOfDay.Gunduz, WeatherKind.Acik, false, _config.MapName);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity, 0.0075f);
        }

        /// <summary>Gerçek zamanlı yansıma probu: bir kez çizilir (ViaScripting), tüm parçayı kaplar.</summary>
        private void SetupReflectionProbe()
        {
            var go = new GameObject("Benchmark Yansıma Probu");
            go.transform.SetParent(_runtimeRoot, false);
            go.transform.position = _spawnPoint + new Vector3(10f, 12f, 40f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 256;
            probe.hdr = true;
            probe.size = new Vector3(BenchmarkLayout.Size, 40f, BenchmarkLayout.Size);
            probe.boxProjection = false;
            probe.RenderProbe();
        }

        private void InstallFeatures()
        {
            Camera camera = null;
            if (_player != null && _player.CameraRig != null)
                camera = _player.CameraRig.WorldCamera;
            if (camera == null)
                camera = Camera.main;

            AaaBenchmarkTerrain.ApplyTier(_terrain, BenchmarkTier);
            BootstrapUtility.InstallGrass(WorldMetadata.Instance, BenchmarkTier);
            var report = AaaBenchmarkFeatureInstaller.InstallAll(camera, _terrain, _runtimeRoot, BenchmarkTier);
            Debug.Log("[AAA Benchmark] G4 özellikleri (Ultra): " + report.Installed.Count + " açıldı, "
                      + report.Skipped.Count + " atlandı, aday " + report.Candidates + ". Açılanlar: "
                      + (report.Installed.Count > 0 ? string.Join(", ", report.Installed) : "(yok)"));
        }

        /// <summary>
        /// Vitrin çapalarını CANLI öznelerden üretir (asker devriye gezer, Kirpi hareket edebilir). Özne yoksa (yok edilmiş/hiç
        /// doğmamış) yeniden doğurur, böylece kamera hiçbir zaman boş araziye bakmaz.
        /// </summary>
        private BenchmarkShotPath.Anchors ResolveAnchors()
        {
            if (_soldier == null)
                BootstrapUtility.Try(SpawnSoldier, "Vitrin: asker yeniden");
            if (_vehicle == null)
                BootstrapUtility.Try(SpawnVehicle, "Vitrin: Kirpi yeniden");

            var anchors = new BenchmarkShotPath.Anchors
            {
                Player = _spawnPoint,
                PlayerForward = Quaternion.Euler(0f, _spawnYaw, 0f) * Vector3.forward,
                Soldier = _soldier != null ? GroundUnder(_soldier.transform.position) : _soldierPosition,
                SoldierForward = _soldier != null ? _soldier.transform.forward : Quaternion.Euler(0f, 200f, 0f) * Vector3.forward,
                Vehicle = _vehicle != null ? _vehicle.transform.position : _vehiclePosition,
                VehicleForward = _vehicle != null ? _vehicle.transform.forward : Quaternion.Euler(0f, 70f, 0f) * Vector3.forward,
                Forest = _forestPoint,
                Targets = _targetsCenter
            };

            if (_shooter == null)
                BootstrapUtility.Try(SpawnShooter, "Vitrin: asker yeniden");
            if (_shooter != null)
            {
                anchors.HasShooter = true;
                anchors.Shooter = _shooter.GroundPosition;
                anchors.ShooterForward = _shooter.Forward;
            }

            var zone = BenchmarkLayout.ForestZone;
            var zoneCenter = Ground(zone.x, zone.y);
            anchors.ForestAlong = BenchmarkShotPath.FlatDir(zoneCenter - _forestPoint, Vector3.forward);

            var fwd = Quaternion.Euler(0f, _houseYaw, 0f) * Vector3.forward;
            var center = _house != null ? _house.Bounds.center : _housePosition;
            anchors.HouseInside = new Vector3(center.x, _housePosition.y + 0.2f, center.z);
            anchors.HouseOutward = fwd;
            anchors.HouseDoor = anchors.HouseInside + fwd * 6.5f;
            anchors.HouseDoor.y = _housePosition.y + 0.2f;
            return anchors;
        }

        private Vector3 GroundUnder(Vector3 p) => Ground(p.x, p.z);

        private void SetupVitrin()
        {
            _vitrin = gameObject.GetComponent<AaaBenchmarkVitrin>();
            if (_vitrin == null)
                _vitrin = gameObject.AddComponent<AaaBenchmarkVitrin>();

            var start = (StartInVitrin || startInShowcase) ? BenchmarkMode.Vitrin : BenchmarkMode.Oyna;
            StartInVitrin = false;
            _vitrin.Initialize(_player, _ui, _spawnPoint, _spawnYaw, ResolveAnchors, start, _shooter);
        }

        private void Teardown()
        {
            if (_disposed)
                return;
            _disposed = true;
            Time.timeScale = 1f;

            if (_settings != null && _onSettingsChanged != null)
                _settings.Changed -= _onSettingsChanged;

            BootstrapUtility.Try(() => GameAudio.SetAmbience(SoundId.None, 0f), "GameAudio.SetAmbience");
            BootstrapUtility.ClearStaticRegistries();

            if (_container != null)
            {
                if (ReferenceEquals(GameContext.Services, _container))
                    GameContext.Clear();
                BootstrapUtility.Try(_container.DisposeAll, "ServiceContainer.DisposeAll");
                _container = null;
            }

            _targets.Clear();
            BootstrapUtility.ReleaseCursor();
        }
    }
}
