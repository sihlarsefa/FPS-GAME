using System;
using System.Collections.Generic;
using Project.Presentation.DevTools;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.AI;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Infrastructure.DI;
using Project.Infrastructure.Loot;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Transport;
using Project.Infrastructure.Vehicles;
using Project.Infrastructure.World;
using Project.Infrastructure.Zone;
using Project.Presentation.Player;
using Project.Presentation.Spectator;
using Project.Presentation.UI;
using Project.Presentation.World;
using UnityEngine;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// Harekât sahnesinin (Kuzgun Vadisi) kompozisyon kökü ve maç akışı.
    /// Awake: oturum, fizik katmanları, servis kapsayıcısı → GameContext, ses/efekt, post-processing ve atmosfer.
    /// Start (sahnedeki tüm Awake'ler — WorldMetadata NavMesh yüklemesi dahil — bittikten sonra): dünya (yoksa çalışma
    /// zamanında üretilir), balistik/bölge hasarı/topçu sistemleri, yerdeki ganimet, sürülebilir Kirpi'ler, intikal planı ve
    /// araçları, oyuncu (tim 0 komutanı) + 9 bot tim arkadaşı + diğer timler, arayüz, bölge duvarı, sabit tick döngüsü; sonra
    /// maç başlar (PreMatch geri sayımı).
    /// Insertion fazında araçlar yola çıkar ve bölge başlar; tüm araçlar inip yolcuları bırakınca (ya da zaman aşımında)
    /// InMatch'e geçilir. Yerel oyuncu ölünce ya da maç bitince birkaç saniye sonra maç sonu ekranı gösterilir; sonuç
    /// kariyere kaydedilir. Sahne kapanırken kapsayıcı serbest bırakılır ve statik kayıtlar temizlenir.
    /// Adanmış sunucu modunda (<see cref="ServerRuntime"/>, Windows Dedicated Server build'i) arayüz, ses ve yerel oyuncu
    /// kurulmaz: tüm timler bot olarak simüle edilir, maç bitince sonuç günlüğe yazılır ve yeni tohumla yeni maç başlar
    /// (ya da süreç kapanır). Sunucuda takılı kalan maçlar bekçi süresiyle sonlandırılır.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class MatchBootstrap : MonoBehaviour
    {
        private enum FlowState
        {
            Setup,
            Running,
            LocalDead,
            Ended,
            EndScreen
        }

        private sealed class TeamSetup
        {
            public int Team;
            public TeamInsertion Plan;
            public TransportVehicle Transport;
            public Combatant Leader;
            public float ArrivedAt = -1f;
            public bool Released;
        }

        private const int LocalTeam = 0;

        [Header("Dünya")]
        [Tooltip("Sahnede WorldMetadata yoksa Kuzgun Vadisi çalışma zamanında üretilir (NavMesh dahil).")]
        [SerializeField] private bool generateWorldIfMissing = true;

        [Tooltip("Çalışma zamanı dünya üretimi tohumu (0 = varsayılan).")]
        [SerializeField] private int worldSeed;

        [Tooltip("Haritaya konacak sürülebilir Kirpi sayısı.")]
        [SerializeField, Range(0, 16)] private int vehicleCount = 5;

        [Header("İntikal")]
        [Tooltip("Araç indikten sonra yolcular kendiliğinden inmezse (araç boşalınca kendisi ayrılır) en geç kaç sn sonra bırakılır.")]
        [SerializeField] private float aiReleaseTimeoutSeconds = 15f;

        [Tooltip("Oyuncu araçta kalırsa araç en geç kaç sn sonra yolcuları bırakır.")]
        [SerializeField] private float playerReleaseTimeoutSeconds = 12f;

        [Tooltip("Insertion fazının azami süresi; dolunca maç InMatch'e geçer.")]
        [SerializeField] private float dropTimeoutSeconds = 90f;

        [Tooltip("Helikopter yerine en yakın düz iniş bölgesine kaydırma yarıçapı (m).")]
        [SerializeField] private float landingZoneSnapRadius = 180f;

        [Header("Maç sonu")]
        [SerializeField] private float deathScreenDelaySeconds = 4f;
        [SerializeField] private float victoryScreenDelaySeconds = 3f;

        [Header("Adanmış sunucu")]
        [Tooltip("Sunucuda maç bu kadar simülasyon saniyesini aşarsa takılı sayılır ve yeniden başlatılır (0 = MatchDuration × 1.5).")]
        [SerializeField] private float serverMatchTimeoutSeconds;

        [Header("Hata ayıklama")]
        [Tooltip("0'dan büyükse ayarlardaki tim sayısını geçersiz kılar (2..8).")]
        [SerializeField, Range(0, 8)] private int teamCountOverride;

        private readonly List<TeamSetup> _teams = new(8);
        private readonly List<BotController> _bots = new(64);

        private ServiceContainer _container;
        private MatchConfig _config;
        private AchievementTracker _achievementTracker;
        private IEventBus _eventBus;
        private MatchService _match;
        private ZoneService _zone;
        private CombatService _combat;
        private ArtilleryService _artillery;
        private ChainOfCommandService _chain;
        private MatchStatsService _stats;
        private KillFeedService _killFeed;
        private LootSpawnService _lootSpawn;
        private SettingsService _settings;
        private WorldMetadata _world;
        private PlayerController _player;
        private Combatant _playerCombatant;
        private GameplayUiController _ui;
        private GameLoopPresenter _loop;
        private ZoneWallView _zoneWall;
        private Transform _runtimeRoot;

        private Action<MatchPhaseChangedEvent> _onPhaseChanged;
        private Action<MatchEndedEvent> _onMatchEnded;
        private Action<GameSettings> _onSettingsChanged;

        private FlowState _flow = FlowState.Setup;
        private bool _setupComplete;
        private bool _insertionStarted;
        private bool _dropComplete;
        private float _insertionStartedAt;
        private float _endScreenAt = -1f;
        private SpectatorController _spectator;
        private bool _resultRecorded;
        private bool _disposed;
        private bool _server;
        private float _serverActionAt = -1f;
        private bool _serverQuitPending;

        /// <summary>Yerel oyuncunun kimliği (tim 0, slot 0).</summary>
        public static PlayerId LocalPlayerId => GameCompositionRoot.DefaultLocalPlayerId;

        public ServiceContainer Services => _container;
        public MatchConfig Config => _config;
        public PlayerController Player => _player;
        public GameplayUiController Ui => _ui;
        public WorldMetadata World => _world;
        public IReadOnlyList<BotController> Bots => _bots;

        /// <summary>Kurulum tamamlandı ve maç başladı mı?</summary>
        public bool IsReady => _setupComplete;

        /// <summary>Adanmış sunucu modunda mı (arayüz/yerel oyuncu yok)?</summary>
        public bool IsServer => _server;

        // ------------------------------------------------------------------ Yaşam döngüsü

        private void Awake()
        {
            GameSession.EnsureInitialized();
            if (Project.Presentation.Replay.ReplayViewer.TryHandleStart(gameObject)) { enabled = false; return; } // Tekrar izleyici modu

            // Çatışma modu aynı sahneyi kullanır: kurulum SkirmishBootstrap'a devredilir.
            if (GameSession.Mode == GameMode.Skirmish && !ServerRuntime.IsDedicatedServer)
            {
                enabled = false;
                if (GetComponent<SkirmishBootstrap>() == null)
                    gameObject.AddComponent<SkirmishBootstrap>();
                return;
            }

            // Rehine Kurtarma: aynı sahne, kurulum HostageBootstrap'ta.
            if (GameSession.Mode == GameMode.HostageRescue && !ServerRuntime.IsDedicatedServer)
            {
                enabled = false;
                if (GetComponent<HostageBootstrap>() == null)
                    gameObject.AddComponent<HostageBootstrap>();
                return;
            }

            GameSession.Mode = GameMode.BattleRoyale;
            _server = ServerRuntime.IsDedicatedServer;
            BootstrapUtility.PrepareScene();

            _settings = GameSession.Settings;
            _config = GameSession.AcquireMatchConfig();
            GameSession.AlignConfigToMap(_config, SceneNames.MapIdForScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
            if (teamCountOverride >= 2)
                _config.WithTeams(teamCountOverride, _config.TeamSize);

            _container = GameCompositionRoot.Build(_config, _settings, GameSession.Career, LocalPlayerId);
            GameContext.Set(_container);
            ResolveServices();

            // Sunucuda ses/efekt/post-processing kurulmaz (grafik aygıtı yok).
            if (!_server)
            {
                var quality = _settings != null ? _settings.Current.QualityLevel : 2;
                BootstrapUtility.InitializeEngineSystems(PostProcessing.Look.Gameplay, quality, true);
            }
        }

        private void Start()
        {
            if (_container == null)
                return;

            var sceneCameras = Camera.allCameras;
            _runtimeRoot = new GameObject("[Harekât]").transform;
            if (!_server) LoadingScreen.SetContext(_config.MapName, GameMode.BattleRoyale, _config.TeamCount, _config.TeamSize);
            ReportLoad(LoadPhase.WorldGen, 0f);

            // Her adım ayrı korunur: bir modülün hatası diğer adımları durdurmaz.
            BootstrapUtility.Try(SetupWorld, "Dünya kurulumu");
            ReportLoad(LoadPhase.WorldGen, 1f);
            BootstrapUtility.Try(SetupCombatSystems, "Çatışma sistemleri");
            ReportLoad(LoadPhase.WorldContent, 0f);
            BootstrapUtility.Try(SpawnWorldContent, "Ganimet ve araçlar");
            ReportLoad(LoadPhase.WorldContent, 1f);
            var plans = BootstrapUtility.Try(PlanInsertion, "İntikal planı") ?? FallbackPlans(null);
            ReportLoad(LoadPhase.BotSpawn, 0f);
            BootstrapUtility.Try(() => SpawnTeams(plans), "Timlerin oluşturulması");
            BootstrapUtility.Try(() => SetupMatchFlow(plans), "Maç akışı (çıpa, başlangıç kiti, ikmal)");
            ReportLoad(LoadPhase.Finalize, 0f);
            BootstrapUtility.Try(() => SetupPresentation(sceneCameras), "Arayüz");
            BootstrapUtility.Try(() => Project.Infrastructure.Rendering.Atmosphere.Apply(_config, _server), "Atmosfer");
            BootstrapUtility.Try(() => Project.Infrastructure.Rendering.WeatherSystem.Attach(_config, _server), "Dinamik hava");
            BootstrapUtility.Try(SetupLoop, "Simülasyon döngüsü");
            BootstrapUtility.Try(SubscribeEvents, "Olay abonelikleri");
            if (!_server) BootstrapUtility.Try(() => Project.Infrastructure.Replay.ReplayRecorder.Attach(gameObject, _config.MapName, _config.RandomSeed, worldSeed, LocalPlayerId.Value), "Tekrar kaydı");

            _setupComplete = true;
            _flow = FlowState.Running;

            if (_server)
                Debug.Log("[Sunucu] Harekât başlıyor — " + _teams.Count + " tim, " + (_match != null ? _match.TotalPlayers : 0)
                          + " asker, tohum " + _config.RandomSeed + ", zorluk " + _config.Difficulty + ".");
            else
                BootstrapUtility.Try(() => GameAudio.SetAmbience(SoundId.Ambience, 0.55f), "GameAudio.SetAmbience");

            if (_match != null)
                BootstrapUtility.Try(_match.Begin, "MatchService.Begin");

            BootstrapUtility.Try(() => DevConsole.Ensure(), "DevConsole.Ensure");
            GameSession.RaiseMatchStarting(_config);

            GameSession.HideLoading();
        }

        private void Update()
        {
            if (!_setupComplete || _disposed)
                return;

            UpdateInsertion();
            UpdateFlow();
        }

        private void OnDestroy()
        {
            _achievementTracker?.Dispose();
            Teardown();
        }

        private void OnApplicationQuit()
        {
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ Kurulum

        private void ResolveServices()
        {
            _container.TryResolve(out _eventBus);
            _achievementTracker?.Dispose();
            _achievementTracker = AchievementTracker.Create(_eventBus, LocalPlayerId, LocalTeam);
            _container.TryResolve(out _match);
            _container.TryResolve(out _zone);
            _container.TryResolve(out _combat);
            _container.TryResolve(out _artillery);
            _container.TryResolve(out _chain);
            _container.TryResolve(out _stats);
            _container.TryResolve(out _killFeed);
            _container.TryResolve(out _lootSpawn);

            if (_killFeed != null)
                _killFeed.LocalTeamOverride = LocalTeam;
        }

        private void ReportLoad(LoadPhase phase, float fraction)
        {
            if (!_server)
                LoadingScreen.Report(phase, fraction);
        }

        private void SetupWorld()
        {
            _world = WorldMetadata.Instance != null ? WorldMetadata.Instance : FindAnyObjectByType<WorldMetadata>();

            if (_world == null && generateWorldIfMissing)
            {
                Debug.Log("[MatchBootstrap] Sahnede dünya yok — Kuzgun Vadisi çalışma zamanında üretiliyor.");
                var parent = new GameObject("[Dünya]").transform;
                var options = new WorldGenerationOptions
                {
                    Parent = parent,
                    MapId = _config != null ? _config.MapName : null,
                    BakeNavMesh = true,
                    GenerateMinimap = !_server,   // sunucuda harita dokusu gereksiz
                    MinimapSize = 1024
                };
                if (worldSeed != 0)
                    options.Seed = worldSeed;

                _world = BootstrapUtility.Try(() => WorldGenerator.Generate(options), "WorldGenerator.Generate");
            }

            if (_world != null)
            {
                var navMesh = _world.NavMesh;
                if (navMesh != null)
                    BootstrapUtility.Try(() => NavMeshBaker.EnsureLoaded(navMesh), "NavMeshBaker.EnsureLoaded");
            }
            else
            {
                Debug.LogWarning("[MatchBootstrap] Dünya bulunamadı/üretilemedi — düz yedek zemin kullanılıyor.");
                BootstrapUtility.CreateFallbackGround(_config.MapHalfSize).transform.SetParent(_runtimeRoot, true);
            }

            BootstrapUtility.EnsureSun();
        }

        private Action<Project.Core.Events.AirdropEvent> _airdropOpenHandler;
        private Action<ZoneStageChangedEvent> _tipZoneHandler;
        private Action<DownedEvent> _tipDownedHandler;

        /// <summary>Maç içi bağlamsal ipuçları (yağma/nişan/araç kendi içinde; bölge ve yaralı tim arkadaşı olayları buradan).</summary>
        private void SetupTutorialTips()
        {
            if (_runtimeRoot == null || _player == null)
                return;
            var tips = BootstrapUtility.Try(() => Project.Presentation.Tutorial.TutorialTipView.Begin(_runtimeRoot, _eventBus, _player, GameSession.Store), "TutorialTipView.Begin");
            if (tips == null || _eventBus == null)
                return;
            _tipZoneHandler = e =>
            {
                if (e.Stage == ZoneStage.Shrinking)
                    Project.Presentation.Tutorial.TutorialTipView.Notify(Project.Presentation.Tutorial.TutorialTipId.FirstZoneWarning);
            };
            _tipDownedHandler = e =>
            {
                if (e.Team == LocalTeam && !e.VictimId.Equals(LocalPlayerId))
                    Project.Presentation.Tutorial.TutorialTipView.Notify(Project.Presentation.Tutorial.TutorialTipId.FirstDownedTeammate);
            };
            _eventBus.Subscribe(_tipZoneHandler);
            _eventBus.Subscribe(_tipDownedHandler);
        }

        /// <summary>Bölge çıpaları (adlandırılmış konumlar), tim başlangıç kitleri, ikmal sandığı açılış yağması, ikmal düşürme saati.</summary>
        private void SetupMatchFlow(TeamInsertion[] plans)
        {
            if (_zone != null && _world != null && _world.Locations != null && _world.Locations.Count > 0)
            {
                var anchors = new List<ZoneAnchor>(_world.Locations.Count);
                foreach (var loc in _world.Locations)
                    if (loc != null)
                        anchors.Add(new ZoneAnchor(loc.Center.x, loc.Center.y, loc.IsMajor ? 2f : 1f));
                BootstrapUtility.Try(() => _zone.SetAnchors(anchors), "ZoneService.SetAnchors");
            }

            IRandom random = null;
            _container?.TryResolve(out random);
            ILootSpawnService lootService = _lootSpawn;
            if (plans != null)
            {
                foreach (var plan in plans)
                {
                    var landing = ToVector3(plan.LandingZone);
                    BootstrapUtility.Try(() => { MatchFlowLoot.SpawnTeamStartKits(landing, Mathf.Max(1, _config.TeamSize), lootService, random); }, "MatchFlowLoot.SpawnTeamStartKits");
                }
            }

            if (_eventBus != null)
                _airdropOpenHandler = BootstrapUtility.Try(() => MatchFlowLoot.BindAirdropOpen(_eventBus, lootService, random), "MatchFlowLoot.BindAirdropOpen");
        }

        private void SetupCombatSystems()
        {
            if (_combat != null)
                BootstrapUtility.Try(() => BallisticsSystem.Create(_combat, _eventBus), "BallisticsSystem.Create");

            if (_zone != null && _combat != null)
                BootstrapUtility.Try(() => ZoneDamageController.Create(_zone, _combat, _match), "ZoneDamageController.Create");

            if (_artillery != null)
                BootstrapUtility.Try(() => ArtilleryExecutor.Create(_artillery), "ArtilleryExecutor.Create");
        }

        private void SpawnWorldContent()
        {
            if (_world == null)
                return;

            if (_lootSpawn != null && _world.LootPoints != null && _world.LootPoints.Count > 0)
            {
                var lootRandom = new SeededRandom(GameCompositionRoot.DeriveSeed(_config.RandomSeed, 0x1007));
                try
                {
                    var count = LootSpawner.SpawnWorldLoot(_world.LootPoints, _lootSpawn, lootRandom);
                    Debug.Log("[MatchBootstrap] Yerdeki ganimet: " + count + " eşya.");
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }

            SpawnVehicles();
        }

        private void SpawnVehicles()
        {
            var spawns = _world.VehicleSpawns;
            BootstrapUtility.Try(() => SpawnFlyableHelicopters(spawns), "SpawnFlyableHelicopters");
            if (vehicleCount <= 0 || spawns == null || spawns.Count == 0)
                return;

            var order = new List<int>(spawns.Count);
            for (var i = 0; i < spawns.Count; i++)
                if (!spawns[i].IsAir)
                    order.Add(i);

            var random = new SeededRandom(GameCompositionRoot.DeriveSeed(_config.RandomSeed, 0x0EE1));
            for (var i = order.Count - 1; i > 0; i--)
            {
                var j = random.Next(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            var count = Mathf.Min(vehicleCount, order.Count);
            for (var i = 0; i < count; i++)
            {
                var data = spawns[order[i]];
                var kind = VehicleConfig.PickForLocation(data.Position, _config.RandomSeed);
                BootstrapUtility.Try(() => DrivableVehicle.Spawn(data.Position, data.Yaw, VehicleConfig.For(kind)), "DrivableVehicle.Spawn");
            }
        }

        /// <summary>İleri Üs helipadlerindeki IsAir noktalarından en fazla 2 uçurulabilir T-70 üretir (seed'e bağlı seçim).</summary>
        private void SpawnFlyableHelicopters(IReadOnlyList<VehicleSpawnData> spawns)
        {
            if (spawns == null)
                return;
            var pads = new List<VehicleSpawnData>(4);
            for (var i = 0; i < spawns.Count; i++)
                if (spawns[i].IsAir)
                    pads.Add(spawns[i]);
            if (pads.Count == 0)
                return;

            var random = new SeededRandom(GameCompositionRoot.DeriveSeed(_config.RandomSeed, 0x7A70));
            for (var i = pads.Count - 1; i > 0; i--)
            {
                var j = random.Next(0, i + 1);
                (pads[i], pads[j]) = (pads[j], pads[i]);
            }

            var count = Mathf.Min(2, pads.Count);
            for (var i = 0; i < count; i++)
            {
                var data = pads[i];
                BootstrapUtility.Try(() => Project.Infrastructure.Transport.FlyableHelicopter.Spawn(data.Position, data.Yaw), "FlyableHelicopter.Spawn");
            }
        }

        private TeamInsertion[] PlanInsertion()
        {
            var random = new SeededRandom(GameCompositionRoot.DeriveSeed(_config.RandomSeed, 0x1A5E));
            TeamInsertion[] plans;
            try
            {
                plans = InsertionPlanner.Plan(_config.TeamCount, _config.MapHalfSize, random, _config.PlayerInsertion);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                plans = Array.Empty<TeamInsertion>();
            }

            if (plans == null || plans.Length < _config.TeamCount)
                plans = FallbackPlans(plans);

            SnapLandingZones(plans);
            return plans;
        }

        /// <summary>Planlayıcı başarısız olursa timleri haritanın etrafına eşit açılarla dağıtır.</summary>
        private TeamInsertion[] FallbackPlans(TeamInsertion[] partial)
        {
            var count = Mathf.Max(1, _config.TeamCount);
            var result = new TeamInsertion[count];
            var half = _config.MapHalfSize;
            for (var team = 0; team < count; team++)
            {
                if (partial != null && team < partial.Length)
                {
                    result[team] = partial[team];
                    continue;
                }

                var angle = team * Mathf.PI * 2f / count;
                var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                var start = dir * (half + 40f);
                var lz = dir * (half * 0.45f);
                var method = team == 0 ? _config.PlayerInsertion : (team % 2 == 0 ? InsertionMethod.Helicopter : InsertionMethod.ArmoredVehicle);
                if (method == InsertionMethod.ArmoredVehicle)
                    start = dir * (half - 30f);

                result[team] = new TeamInsertion(team, method, ToFloat3(start), ToFloat3(lz));
            }

            return result;
        }

        /// <summary>
        /// Her timin iniş noktasını yakındaki (kullanılmamış) düz iniş bölgesine kaydırır — helikopterler yamaca inmesin.
        /// </summary>
        private void SnapLandingZones(TeamInsertion[] plans)
        {
            if (_world == null || _world.LandingZones == null || _world.LandingZones.Count == 0 || plans == null)
                return;

            var zones = _world.LandingZones;
            var used = new bool[zones.Count];
            var maxSqr = landingZoneSnapRadius * landingZoneSnapRadius;
            for (var p = 0; p < plans.Length; p++)
            {
                var plan = plans[p];
                var lz = ToVector3(plan.LandingZone);
                var best = -1;
                var bestSqr = maxSqr;
                for (var i = 0; i < zones.Count; i++)
                {
                    if (used[i])
                        continue;

                    var dx = zones[i].x - lz.x;
                    var dz = zones[i].z - lz.z;
                    var sqr = dx * dx + dz * dz;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = i;
                    }
                }

                if (best < 0)
                    continue;

                used[best] = true;
                var snapped = zones[best];
                plans[p] = new TeamInsertion(plan.Team, plan.Method, plan.Start, new Float3(snapped.x, 0f, snapped.z));
            }
        }

        private void SpawnTeams(TeamInsertion[] plans)
        {
            var teamCount = plans.Length;
            var teamSize = Mathf.Max(1, _config.TeamSize);
            var nameRandom = new SeededRandom(GameCompositionRoot.DeriveSeed(_config.RandomSeed, 0x4A3E));
            var names = NameRoster.CreateUnique(teamCount * teamSize, nameRandom);
            var nameIndex = 0;

            for (var team = 0; team < teamCount; team++)
            {
                var setup = new TeamSetup { Team = team, Plan = plans[team] };
                setup.Transport = BootstrapUtility.Try(() => TransportFactory.Create(setup.Plan), "TransportFactory.Create");
                if (setup.Transport != null)
                    setup.Transport.Arrived += () => OnTransportArrived(setup);
                _teams.Add(setup);
            }

            for (var t = 0; t < _teams.Count; t++)
            {
                var setup = _teams[t];
                for (var slot = 0; slot < teamSize; slot++)
                {
                    var name = nameIndex < names.Count ? names[nameIndex] : "Asker " + (nameIndex + 1);
                    nameIndex++;

                    // Sunucuda yerel oyuncu yok: tim 0'ın komutanı da bottur.
                    if (setup.Team == LocalTeam && slot == 0 && !_server)
                    {
                        SpawnLocalPlayer(setup);
                        if (_playerCombatant != null)
                        {
                            setup.Leader = _playerCombatant;
                            continue;
                        }

                        Debug.LogWarning("[MatchBootstrap] Oyuncu oluşturulamadı — tim komutanı bot olarak oluşturuluyor.");
                    }

                    var bot = SpawnBot(setup, slot, name);
                    if (slot == 0 && bot != null)
                        setup.Leader = bot.Combatant;
                    ReportLoad(LoadPhase.BotSpawn, nameIndex / (float)(teamCount * teamSize));
                }
            }

            Debug.Log("[MatchBootstrap] " + teamCount + " tim, " + _match.TotalPlayers + " asker kayıtlı.");
        }

        private void SpawnLocalPlayer(TeamSetup setup)
        {
            var settings = _settings != null ? _settings.Current : new GameSettings();
            var transport = setup.Transport;
            var args = new PlayerSpawnArgs
            {
                Id = LocalPlayerId,
                Name = settings.PlayerName,
                Team = setup.Team,
                Role = TeamRole.Leader,
                Transport = transport,
                Seat = 0,
                GroundPosition = GroundSpawnPosition(setup, 0),
                GroundYaw = setup.Plan.HeadingDegrees,
                Settings = settings
            };
            try { args.Loadout = Project.Presentation.UI.LoadoutSelection.Shared.BuildMatchLoadout(); }
            catch (Exception e) { Debug.LogWarning("[MatchBootstrap] Donanım seçimi okunamadı, rol kiti kullanılıyor: " + e.Message); }

            _player = BootstrapUtility.Try(() => PlayerController.Create(args), "PlayerController.Create");
            if (_player == null)
                return;

            _playerCombatant = _player.Combatant;
            if (_playerCombatant != null)
            {
                EnsureRegistered(_playerCombatant, true);
                _playerCombatant.Died += OnLocalCombatantDied;
            }

            _player.Died += OnLocalPlayerDied;
        }

        private BotController SpawnBot(TeamSetup setup, int slot, string name)
        {
            var transport = setup.Transport;
            var seated = transport != null && slot < Mathf.Max(0, transport.SeatCount);
            var id = new PlayerId(LocalPlayerId.Value + setup.Team * Mathf.Max(1, _config.TeamSize) + slot);
            var args = new BotSpawnArgs
            {
                Id = id,
                Name = name,
                Team = setup.Team,
                Role = LoadoutCatalog.RoleForSlot(slot),
                Difficulty = _config.Difficulty,
                Transport = seated ? transport : null,
                Seat = slot,
                Slot = slot,
                SpawnOnGround = !seated,
                GroundPosition = GroundSpawnPosition(setup, slot),
                GroundYaw = setup.Plan.HeadingDegrees,
                SquadLeader = slot == 0 ? null : setup.Leader,
                Seed = GameCompositionRoot.DeriveSeed(_config.RandomSeed, id.Value),
                Parent = _runtimeRoot
            };

            var bot = BootstrapUtility.Try(() => BotController.Create(args), "BotController.Create");
            if (bot == null)
                return null;

            _bots.Add(bot);
            EnsureRegistered(bot.Combatant, false);
            Project.Infrastructure.Characters.WearDriver.Attach(bot.gameObject, bot.Combatant, bot.Model); // savaş yıpranması
            return bot;
        }

        /// <summary>Oyuncu/bot kendi kaydını yapmadıysa maç ve komuta zincirine kaydeder (rütbeli görünen adla).</summary>
        private void EnsureRegistered(Combatant combatant, bool isLocal)
        {
            if (combatant == null || !combatant.Id.IsValid)
                return;

            try
            {
                if (_match != null && !_match.IsRegistered(combatant.Id))
                    _match.RegisterCombatant(combatant.Id, combatant.RankedName, isLocal, combatant.Team, combatant.Role);

                if (_chain != null && !_chain.IsRegistered(combatant.Id))
                    _chain.Register(combatant.Id, combatant.Team, combatant.Rank);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        /// <summary>Araçsız doğuş için iniş noktası çevresinde kama düzeninde zemin noktası.</summary>
        private Vector3 GroundSpawnPosition(TeamSetup setup, int slot)
        {
            var lz = ToVector3(setup.Plan.LandingZone);
            var heading = setup.Plan.HeadingDegrees * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
            var right = new Vector3(forward.z, 0f, -forward.x);

            Vector3 offset;
            if (slot == 0)
                offset = Vector3.zero;
            else
            {
                var rank = (slot + 1) / 2;
                var side = slot % 2 == 1 ? -1f : 1f;
                offset = right * (side * rank * 2.5f) - forward * (rank * 2.5f);
            }

            return BootstrapUtility.GroundPoint(_world, lz + offset) + Vector3.up * 0.05f;
        }

        private void SetupPresentation(Camera[] sceneCameras)
        {
            if (_server)
                return;

            _ui = GameplayUiController.Create(_runtimeRoot, _player, _settings, GameSession.ReturnToMainMenu);
            BootstrapUtility.InstallGrass(_world, _settings != null ? _settings.Current.QualityLevel : 2);
            BootstrapUtility.Try(() => Project.Infrastructure.World.AmbientLife.Install(Camera.main, _world, _settings != null ? _settings.Current.QualityLevel : 2), "AmbientLife.Install");
            SetupTutorialTips();

            if (_zone != null)
                _zoneWall = BootstrapUtility.Try(() => ZoneWallView.Create(_zone, _world, _runtimeRoot), "ZoneWallView.Create");

            // Oyuncu kamerası kurulduysa sahnedeki eski kameraları kapat; hiç kamera yoksa gözlemci kamerası.
            var replaced = BootstrapUtility.DisableSceneCamerasIfReplaced(sceneCameras);
            if (!replaced && Camera.allCamerasCount == 0)
            {
                var lookAt = _teams.Count > 0 ? ToVector3(_teams[0].Plan.LandingZone) : Vector3.zero;
                BootstrapUtility.CreateObserverCamera(BootstrapUtility.GroundPoint(_world, lookAt));
            }

            if (_settings != null)
            {
                _onSettingsChanged = OnSettingsChanged;
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

        private void SubscribeEvents()
        {
            if (_eventBus == null)
                return;

            _onPhaseChanged = OnPhaseChanged;
            _onMatchEnded = OnMatchEnded;
            _eventBus.Subscribe(_onPhaseChanged);
            _eventBus.Subscribe(_onMatchEnded);
        }

        // ------------------------------------------------------------------ İntikal

        private void OnPhaseChanged(MatchPhaseChangedEvent e)
        {
            if (e.CurrentPhase == MatchPhase.Insertion)
                BeginInsertion();
            else if (e.CurrentPhase == MatchPhase.InMatch && !_insertionStarted)
                BeginInsertion();
        }

        private void BeginInsertion()
        {
            if (_insertionStarted)
                return;

            _insertionStarted = true;
            _insertionStartedAt = Time.time;

            for (var i = 0; i < _teams.Count; i++)
            {
                var transport = _teams[i].Transport;
                if (transport != null)
                    BootstrapUtility.Try(transport.Begin, "TransportVehicle.Begin");
            }

            if (_zone != null)
                BootstrapUtility.Try(_zone.Start, "ZoneService.Start");

            if (_server)
                Debug.Log("[Sunucu] İntikal başladı.");
            else
                BootstrapUtility.Try(() => GameAudio.Play2D(SoundId.RadioChatter, 0.6f), "GameAudio.Play2D");

            if (!_server)
                BootstrapUtility.Try(() => MatchIntro.Play(_config.MapName, null, v => { if (_ui != null) _ui.SetHudVisible(v); }), "MatchIntro.Play");
        }

        private void OnTransportArrived(TeamSetup setup)
        {
            if (setup.ArrivedAt < 0f)
                setup.ArrivedAt = Time.time;
        }

        private void UpdateInsertion()
        {
            if (!_insertionStarted)
                return;

            var allReleased = true;
            for (var i = 0; i < _teams.Count; i++)
            {
                var setup = _teams[i];
                if (setup.Released)
                    continue;

                var transport = setup.Transport;
                if (transport == null)
                {
                    setup.Released = true;
                    continue;
                }

                // Araç boşalınca kendisi bırakıp ayrılır (IsDeparting); IsUnloading varışta hemen true olur, yolcular hâlâ
                // inmekte olabilir — bu yüzden bırakılmış sayılmaz.
                if (transport.IsDeparting)
                {
                    setup.Released = true;
                    continue;
                }

                if (!transport.HasArrived)
                {
                    allReleased = false;
                    continue;
                }

                if (setup.ArrivedAt < 0f)
                    setup.ArrivedAt = Time.time;

                var waited = Time.time - setup.ArrivedAt;
                var playerAboard = setup.Team == LocalTeam && IsPlayerInTransport();
                var timeout = playerAboard ? playerReleaseTimeoutSeconds : aiReleaseTimeoutSeconds;
                if (waited < timeout)
                {
                    allReleased = false;
                    continue;
                }

                BootstrapUtility.Try(transport.ReleasePassengers, "TransportVehicle.ReleasePassengers");
                setup.Released = true;
            }

            if (_dropComplete || _match == null || _match.CurrentPhase != MatchPhase.Insertion)
                return;

            var playerReady = !IsPlayerInTransport();
            var timedOut = Time.time - _insertionStartedAt >= dropTimeoutSeconds;
            if ((allReleased && playerReady) || timedOut)
            {
                _dropComplete = true;
                BootstrapUtility.Try(_match.NotifyDropComplete, "MatchService.NotifyDropComplete");
            }
        }

        private bool IsPlayerInTransport()
        {
            if (_playerCombatant == null || !_playerCombatant.IsInitialized || !_playerCombatant.IsAlive)
                return false;

            return _playerCombatant.DropState == DropState.InTransport;
        }

        // ------------------------------------------------------------------ Maç akışı

        private void OnLocalPlayerDied()
        {
            HandleLocalDeath();
        }

        private void OnLocalCombatantDied(Combatant combatant, DamageInfo damage)
        {
            HandleLocalDeath();
        }

        private void HandleLocalDeath()
        {
            if (_flow != FlowState.Running)
                return;

            _flow = FlowState.LocalDead;
            _endScreenAt = Time.time + deathScreenDelaySeconds;
            BootstrapUtility.Try(() => GameAudio.Play2D(SoundId.Death, 0.8f), "GameAudio.Play2D");

            // Killcam -> izleyici; maç sonu ekranı maç bitince ya da oyuncu isteyince açılır.
            if (_playerCombatant != null && !_server)
            {
                _spectator = BootstrapUtility.Try(
                    () => SpectatorController.Begin(_playerCombatant, OnMainMenuRequested, ShowEndScreen),
                    "SpectatorController.Begin");
                if (_spectator != null)
                {
                    _endScreenAt = -1f;
                    if (_ui != null)
                        _ui.SetEndScreenActive(true); // HUD'u kapatır, imleci serbest bırakır
                    return;
                }
            }

            if (_ui != null)
                _ui.ShowMessage("ŞEHİT DÜŞTÜN", deathScreenDelaySeconds);
        }

        private void OnMatchEnded(MatchEndedEvent e)
        {
            if (_flow == FlowState.EndScreen || _flow == FlowState.Ended)
                return;

            if (_server)
            {
                HandleServerMatchEnded(e.WinnerTeam, false);
                return;
            }

            var wasDead = _flow == FlowState.LocalDead;
            _flow = FlowState.Ended;

            var delay = victoryScreenDelaySeconds;
            if (wasDead)
            {
                // Ölüm ekranı sayacı sürüyorsa onu uzatma.
                var remaining = _endScreenAt - Time.time;
                delay = _spectator != null ? 2f : (remaining > 0f ? Mathf.Min(remaining, victoryScreenDelaySeconds) : 0f);
            }

            _endScreenAt = Time.time + delay;

            if (!wasDead)
            {
                string message;
                if (e.WinnerTeam == LocalTeam)
                    message = "ZAFER! " + SafeTeamName(LocalTeam).ToUpperInvariant() + " HAREKÂTI KAZANDI";
                else if (e.WinnerTeam >= 0)
                    message = "HAREKÂT SONA ERDİ — " + SafeTeamName(e.WinnerTeam).ToUpperInvariant() + " KAZANDI";
                else
                    message = "HAREKÂT SONA ERDİ";

                if (_ui != null)
                    _ui.ShowMessage(message, delay + 1f);
            }
        }

        private void UpdateFlow()
        {
            if (_server)
            {
                UpdateServerFlow();
                return;
            }

            // Olay kaçarsa (ör. Died bağlanamadıysa) ölüm durumunu yokla.
            if (_flow == FlowState.Running && _playerCombatant != null && _playerCombatant.IsInitialized && !_playerCombatant.IsAlive)
                HandleLocalDeath();

            if ((_flow == FlowState.LocalDead || _flow == FlowState.Ended) && _endScreenAt >= 0f && Time.time >= _endScreenAt)
                ShowEndScreen();
        }

        private void ShowEndScreen()
        {
            if (_flow == FlowState.EndScreen)
                return;

            _flow = FlowState.EndScreen;
            if (_spectator != null)
            {
                _spectator.Stop();
                _spectator = null;
            }

            var result = BuildResult();
            if (!_resultRecorded)
            {
                _resultRecorded = true;
                GameSession.RecordResult(result);
                GameSession.RaiseMatchFinished(result);
                GameSession.RecordCareerMatch(result, _stats != null ? _stats.GetWeaponStats(LocalPlayerId) : null, _config.MapName);
            }

            if (_ui != null)
                _ui.SetEndScreenActive(true);
            var shown = BootstrapUtility.Try(() => EndScreen.Show(result, OnRestartRequested, OnMainMenuRequested), "EndScreen.Show");
            if (shown == null)
            {
                Debug.LogWarning("[MatchBootstrap] Maç sonu ekranı açılamadı — ana menüye dönülüyor.");
                OnMainMenuRequested();
            }
        }

        // ------------------------------------------------------------------ Adanmış sunucu

        private void UpdateServerFlow()
        {
            if (_flow == FlowState.Running && _match != null)
            {
                var limit = serverMatchTimeoutSeconds > 0f
                    ? serverMatchTimeoutSeconds
                    : Mathf.Max(600f, _config.MatchDurationSeconds * 1.5f);
                if (_match.MatchElapsedSeconds > limit)
                {
                    Debug.LogWarning("[Sunucu] Maç " + limit.ToString("0") + " sn sınırını aştı — sonlandırılıp yeniden başlatılıyor.");
                    HandleServerMatchEnded(-1, true);
                }
            }

            if (_serverActionAt < 0f || Time.unscaledTime < _serverActionAt)
                return;

            _serverActionAt = -1f;
            if (_serverQuitPending)
            {
                ServerRuntime.Quit();
                return;
            }

            GameSession.Restart();
        }

        private void HandleServerMatchEnded(int winnerTeam, bool timedOut)
        {
            _flow = FlowState.Ended;
            var quit = ServerRuntime.RegisterMatchFinished();
            PublishServerSummary(timedOut);

            var elapsed = _match != null ? _match.MatchElapsedSeconds : 0f;
            var winner = winnerTeam >= 0 ? SafeTeamName(winnerTeam) : "yok";
            Debug.Log("[Sunucu] Maç #" + ServerRuntime.MatchesPlayed + " bitti" + (timedOut ? " (zaman aşımı)" : string.Empty)
                      + " — kazanan: " + winner + ", süre " + elapsed.ToString("0") + " sn, hayatta "
                      + (_match != null ? _match.AlivePlayerCount : 0) + "/" + (_match != null ? _match.TotalPlayers : 0) + " asker.");

            if (quit)
            {
                _serverQuitPending = true;
                _serverActionAt = Time.unscaledTime + 2f;
            }
            else if (ServerRuntime.AutoRestart)
            {
                _serverActionAt = Time.unscaledTime + ServerRuntime.RestartDelaySeconds;
                Debug.Log("[Sunucu] Yeni maç " + ServerRuntime.RestartDelaySeconds.ToString("0") + " sn sonra başlayacak.");
            }
            else
            {
                Debug.Log("[Sunucu] Otomatik yeniden başlatma kapalı (-norestart) — boşta bekleniyor.");
            }
        }

        private void PublishServerSummary(bool timedOut)
        {
            try
            {
                if (_stats == null)
                    return;

                var ids = new System.Collections.Generic.List<PlayerId>();
                var all = Project.Infrastructure.Combat.CombatantRegistry.All;
                for (var i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].Id.IsValid)
                        ids.Add(all[i].Id);
                }

                GameSession.RaiseMatchSummary(_stats.BuildSummary(ids, timedOut));
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private MatchResult BuildResult()
        {
            try
            {
                if (_stats != null)
                    return _stats.BuildResult(LocalPlayerId);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            var teamCount = _match != null ? _match.TeamCount : _config.TeamCount;
            var won = _match != null && _match.WinnerTeam == LocalTeam;
            return new MatchResult(won, 1, _match?.TotalPlayers ?? 1, 0, 0, 0f, _match?.MatchElapsedSeconds ?? 0f, 0f, null,
                won ? 1 : Mathf.Max(1, _match?.AliveTeamCount ?? 1), teamCount, SafeTeamName(LocalTeam), 0);
        }

        private void OnRestartRequested()
        {
            GameSession.Restart();
        }

        private void OnMainMenuRequested()
        {
            GameSession.ReturnToMainMenu();
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            if (_ui != null)
                _ui.ApplySettings(settings);
        }

        private string SafeTeamName(int team)
        {
            try
            {
                if (_match != null)
                    return _match.GetTeamName(team);
            }
            catch (Exception)
            {
                // Yedek ad aşağıda.
            }

            return (team + 1) + ". Tim";
        }

        // ------------------------------------------------------------------ Kapanış

        private void Teardown()
        {
            if (_disposed)
                return;

            _disposed = true;
            Time.timeScale = 1f;

            if (_eventBus != null)
            {
                if (_onPhaseChanged != null)
                    _eventBus.Unsubscribe(_onPhaseChanged);
                if (_onMatchEnded != null)
                    _eventBus.Unsubscribe(_onMatchEnded);
                if (_airdropOpenHandler != null)
                    _eventBus.Unsubscribe(_airdropOpenHandler);
                if (_tipZoneHandler != null)
                    _eventBus.Unsubscribe(_tipZoneHandler);
                if (_tipDownedHandler != null)
                    _eventBus.Unsubscribe(_tipDownedHandler);
            }

            if (_settings != null && _onSettingsChanged != null)
                _settings.Changed -= _onSettingsChanged;

            if (_player != null)
                _player.Died -= OnLocalPlayerDied;

            if (_playerCombatant != null)
                _playerCombatant.Died -= OnLocalCombatantDied;

            if (!_server)
                BootstrapUtility.Try(() => GameAudio.SetAmbience(SoundId.None, 0f), "GameAudio.SetAmbience");
            BootstrapUtility.ClearStaticRegistries();

            if (_container != null)
            {
                if (ReferenceEquals(GameContext.Services, _container))
                    GameContext.Clear();

                BootstrapUtility.Try(_container.DisposeAll, "ServiceContainer.DisposeAll");
                _container = null;
            }

            _teams.Clear();
            _bots.Clear();
            if (!_server)
                BootstrapUtility.ReleaseCursor();
        }

        // ------------------------------------------------------------------ Yardımcılar

        private static Vector3 ToVector3(Float3 v) => new(v.X, v.Y, v.Z);
        private static Float3 ToFloat3(Vector3 v) => new(v.x, v.y, v.z);
    }
}
