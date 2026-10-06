using System;
using System.Collections.Generic;
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
using Project.Infrastructure.Input;
using Project.Infrastructure.Loot;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Transport;
using Project.Infrastructure.World;
using Project.Presentation.DevTools;
using Project.Presentation.Player;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// "REHİNE KURTARMA" modu: Kuzgun Köyü / Yıkık Köy'deki bir binada 2 rehine tutulur, 10 kişilik savunma timi korur.
    /// Oyuncu timi (saldıran) binaya girer, rehineleri bulup F ile serbest bırakır, takip ettirerek tahliye noktasına götürür;
    /// ilk rehine çözülünce T-70 gelir ve iner. 8 dk süre, yeniden doğuş yok, rehine ölürse görev başarısız.
    /// Kural mantığı <see cref="HostageRules"/>'ta; kurulum <see cref="SkirmishBootstrap"/> ile aynı parçaları kullanır.
    /// <see cref="MatchBootstrap"/> bu modda kendini devre dışı bırakıp bu bileşeni ekler.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HostageBootstrap : MonoBehaviour
    {
        private enum Flow { Setup, Running, Ended, EndScreen }

        private const int AttackerTeam = 0;
        private const int DefenderTeam = 1;
        private const int AllyCount = 3;
        private const int DefenderCount = 10;
        private const int HostageIdOffset = 300;
        private const float EndScreenDelaySeconds = 3.5f;
        private const float FallbackCallSeconds = 150f;
        private const string MenuLabel = "REHİNE KURTARMA";

        private ServiceContainer _container;
        private MatchConfig _config;
        private IEventBus _eventBus;
        private MatchService _match;
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
        private Transform _runtimeRoot;
        private HostageRules _rules;

        private readonly List<HostageNpc> _hostages = new List<HostageNpc>(2);
        private Helicopter _heli;
        private GameObject _lzMarker;
        private Vector3 _plannedLz;
        private bool _heliCalled;

        private Vector3 _attackBase;
        private float _attackYaw;
        private Vector3 _buildingCenter;
        private float _buildingRadius = 25f;
        private string _locationName = "Kuzgun Köyü";

        private Action<MatchEndedEvent> _onEnded;
        private Action<GameSettings> _onSettingsChanged;

        private Flow _flow = Flow.Setup;
        private bool _disposed;
        private bool _resultRecorded;
        private bool _endForced;
        private float _endScreenAt = -1f;
        private System.Random _rng;

        private Canvas _hudCanvas;
        private Text _status;
        private Text _clock;
        private Text _sub;
        private Text _prompt;
        private string _lastStatus, _lastClock, _lastSub, _lastPrompt;

        public HostageRules Rules => _rules;
        public PlayerController Player => _player;

        // ------------------------------------------------------------------ Menü

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterMenuButton()
        {
            var list = MainMenuController.ExtraButtons;
            for (var i = 0; i < list.Count; i++)
                if (list[i].label == MenuLabel)
                    return;
            list.Add((MenuLabel, _ => GameSession.StartHostageRescue()));
        }

        // ------------------------------------------------------------------ Yaşam döngüsü

        private void Awake()
        {
            GameSession.EnsureInitialized();
            GameSession.Mode = GameMode.HostageRescue;
            BootstrapUtility.PrepareScene();

            _settings = GameSession.Settings;
            _config = GameSession.AcquireMatchConfig();
            _config.WithTeams(2, 10);
            GameSession.AlignConfigToMap(_config, MapCatalog.Kuzgun);
            _config.MapName = MapCatalog.KuzgunName;
            _config.PlayerInsertion = InsertionMethod.Helicopter;
            _config.PreMatchDurationSeconds = 4f;
            _config.MatchDurationSeconds = HostageRules.DefaultDurationSeconds + 30f;
            _rng = new System.Random(_config.RandomSeed != 0 ? _config.RandomSeed : Environment.TickCount);
            _rules = new HostageRules();

            _container = GameCompositionRoot.Build(_config, _settings, GameSession.Career, MatchBootstrap.LocalPlayerId);
            GameContext.Set(_container);

            _container.TryResolve(out _eventBus);
            _container.TryResolve(out _match);
            _container.TryResolve(out _combat);
            _container.TryResolve(out _artillery);
            _container.TryResolve(out _chain);
            _container.TryResolve(out _stats);
            _container.TryResolve(out _killFeed);
            _container.TryResolve(out _lootSpawn);
            if (_killFeed != null)
                _killFeed.LocalTeamOverride = AttackerTeam;
            if (_match != null)
                _match.AutoEndOnElimination = false; // bitiş kuralı: HostageRules

            var quality = _settings != null ? _settings.Current.QualityLevel : 2;
            BootstrapUtility.InitializeEngineSystems(PostProcessing.Look.Gameplay, quality, true);
        }

        private void Start()
        {
            if (_container == null)
                return;

            var sceneCameras = Camera.allCameras;
            _runtimeRoot = new GameObject("[Rehine Kurtarma]").transform;

            BootstrapUtility.Try(SetupWorld, "Rehine: dünya");
            BootstrapUtility.Try(SetupCombat, "Rehine: sistemler");
            BootstrapUtility.Try(PickSite, "Rehine: bölge seçimi");
            BootstrapUtility.Try(SpawnLoot, "Rehine: ganimet");
            BootstrapUtility.Try(SpawnAttackers, "Rehine: saldıran tim");
            BootstrapUtility.Try(SpawnDefenders, "Rehine: savunma timi");
            BootstrapUtility.Try(SpawnHostages, "Rehine: rehineler");
            BootstrapUtility.Try(SetupHelicopter, "Rehine: T-70");
            BootstrapUtility.Try(() => SetupPresentation(sceneCameras), "Rehine: arayüz");
            BootstrapUtility.Try(() => Project.Infrastructure.Rendering.Atmosphere.Apply(_config), "Rehine: atmosfer");
            BootstrapUtility.Try(SetupLoop, "Rehine: döngü");
            BootstrapUtility.Try(Subscribe, "Rehine: olaylar");

            _flow = Flow.Running;
            BootstrapUtility.Try(() => GameAudio.SetAmbience(SoundId.Ambience, 0.55f), "GameAudio.SetAmbience");
            if (_match != null)
                BootstrapUtility.Try(_match.Begin, "MatchService.Begin");

            BootstrapUtility.Try(() => DevConsole.Ensure(), "DevConsole.Ensure");
            GameSession.RaiseMatchStarting(_config);
            GameSession.HideLoading();

            if (_ui != null)
                _ui.ShowMessage("REHİNE KURTARMA — " + _locationName.ToUpperInvariant() + " · 2 rehineyi bul", 5f);
        }

        private void Update()
        {
            if (_disposed || _flow == Flow.Setup || _match == null)
                return;

            // İntikal yok: önhazırlıktan sonra doğrudan maç fazı.
            if (_match.CurrentPhase == MatchPhase.Insertion)
                _match.NotifyDropComplete();

            if (_flow == Flow.Running && _match.CurrentPhase == MatchPhase.InMatch)
            {
                _rules.Tick(Time.deltaTime);
                if (!_rules.IsOver)
                {
                    TickHostages();
                    TickInteraction();
                    TickExtraction();
                    TickPlayerAlive();
                    if (!_heliCalled && _rules.Elapsed >= FallbackCallSeconds)
                        CallHelicopter();
                }

                if (_rules.IsOver && !_endForced)
                {
                    _endForced = true;
                    if (_rules.IsSuccess && _heli != null)
                        _heli.ReleasePassengers();
                    _match.ForceEnd(_rules.IsSuccess ? AttackerTeam : DefenderTeam);
                }
            }

            if (_flow == Flow.Ended && _endScreenAt >= 0f && Time.time >= _endScreenAt)
                ShowEndScreen();

            RefreshHud();
        }

        private void OnDestroy() => Teardown();

        private void OnApplicationQuit() => Time.timeScale = 1f;

        // ------------------------------------------------------------------ Kurulum

        private void SetupWorld()
        {
            _world = WorldMetadata.Instance != null ? WorldMetadata.Instance : FindAnyObjectByType<WorldMetadata>();
            if (_world == null)
            {
                var parent = new GameObject("[Dünya]").transform;
                var options = new WorldGenerationOptions
                {
                    Parent = parent,
                    MapId = _config.MapName,
                    BakeNavMesh = true,
                    GenerateMinimap = true,
                    MinimapSize = 1024
                };
                _world = BootstrapUtility.Try(() => WorldGenerator.Generate(options), "WorldGenerator.Generate");
            }

            if (_world != null && _world.NavMesh != null)
            {
                var navMesh = _world.NavMesh;
                BootstrapUtility.Try(() => NavMeshBaker.EnsureLoaded(navMesh), "NavMeshBaker.EnsureLoaded");
            }
            else if (_world == null)
            {
                BootstrapUtility.CreateFallbackGround(_config.MapHalfSize).transform.SetParent(_runtimeRoot, true);
            }

            BootstrapUtility.EnsureSun();
        }

        private void SetupCombat()
        {
            if (_combat != null)
                BootstrapUtility.Try(() => BallisticsSystem.Create(_combat, _eventBus), "BallisticsSystem.Create");
            if (_artillery != null)
                BootstrapUtility.Try(() => ArtilleryExecutor.Create(_artillery), "ArtilleryExecutor.Create");
        }

        private void SpawnLoot()
        {
            if (_world == null || _lootSpawn == null || _world.LootPoints == null || _world.LootPoints.Count == 0)
                return;

            var random = new SeededRandom(GameCompositionRoot.DeriveSeed(_config.RandomSeed, 0x1007));
            LootSpawner.SpawnWorldLoot(_world.LootPoints, _lootSpawn, random);
        }

        /// <summary>Kuzgun Köyü / Yıkık Köy'den birini seçer; en büyük yapı rehine binası, saldıran üssü bölgenin dışında.</summary>
        private void PickSite()
        {
            Vector2 center = _world != null ? _world.MapCenter : Vector2.zero;
            var radius = 70f;

            if (_world != null && _world.Locations != null && _world.Locations.Count > 0)
            {
                var pool = new List<NamedLocation>();
                for (var i = 0; i < _world.Locations.Count; i++)
                {
                    var l = _world.Locations[i];
                    if (l != null && (l.Name == "Kuzgun Köyü" || l.Name == "Yıkık Köy"))
                        pool.Add(l);
                }

                if (pool.Count == 0)
                    for (var i = 0; i < _world.Locations.Count; i++)
                        if (_world.Locations[i] != null && _world.Locations[i].IsMajor)
                            pool.Add(_world.Locations[i]);
                if (pool.Count == 0)
                    pool.AddRange(_world.Locations);

                var pick = pool[_rng.Next(pool.Count)];
                _locationName = string.IsNullOrEmpty(pick.Name) ? _locationName : pick.Name;
                center = pick.Center;
                radius = Mathf.Max(pick.Radius, 40f);
            }

            var c = new Vector3(center.x, 0f, center.y);
            _buildingCenter = c;
            _buildingRadius = 22f;

            // Bölge içindeki en büyük yapı.
            if (_world != null && _world.StructureBounds != null)
            {
                var best = -1f;
                for (var i = 0; i < _world.StructureBounds.Count; i++)
                {
                    var b = _world.StructureBounds[i];
                    var d = new Vector2(b.center.x - center.x, b.center.z - center.y).magnitude;
                    if (d > radius)
                        continue;
                    var area = b.size.x * b.size.z;
                    if (area > best)
                    {
                        best = area;
                        _buildingCenter = new Vector3(b.center.x, 0f, b.center.z);
                        _buildingRadius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) + 12f, 16f, 40f);
                    }
                }
            }

            _buildingCenter = SnapToNav(BootstrapUtility.GroundPoint(_world, _buildingCenter));

            var angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            var distance = Mathf.Clamp(radius + 40f, 90f, 150f);
            _attackBase = SnapToNav(BootstrapUtility.GroundPoint(_world, _buildingCenter + dir * distance));
            var facing = -dir;
            _attackYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;

            // Tahliye noktası: üssün yanında, binadan uzakta.
            var side = new Vector3(dir.z, 0f, -dir.x);
            _plannedLz = SnapToNav(BootstrapUtility.GroundPoint(_world, _attackBase + dir * 25f + side * 30f));
        }

        private void SpawnAttackers()
        {
            var settings = _settings != null ? _settings.Current : new GameSettings();
            var args = new PlayerSpawnArgs
            {
                Id = MatchBootstrap.LocalPlayerId,
                Name = settings.PlayerName,
                Team = AttackerTeam,
                Role = TeamRole.Leader,
                Transport = null,
                GroundPosition = AttackerSlot(0),
                GroundYaw = _attackYaw,
                Settings = settings
            };

            _player = BootstrapUtility.Try(() => PlayerController.Create(args), "PlayerController.Create");
            if (_player != null)
            {
                _playerCombatant = _player.Combatant;
                EnsureRegistered(_playerCombatant, true, true);
            }

            var nameRandom = new SeededRandom(GameCompositionRoot.DeriveSeed(_config.RandomSeed, 0x4A3E));
            var names = NameRoster.CreateUnique(AllyCount + DefenderCount, nameRandom);

            for (var slot = 1; slot <= AllyCount; slot++)
            {
                var id = new PlayerId(MatchBootstrap.LocalPlayerId.Value + slot);
                var leader = _playerCombatant != null && _playerCombatant.IsAlive ? _playerCombatant : null;
                SpawnBot(AttackerTeam, slot, names[slot - 1], id, AttackerSlot(slot), _attackYaw, leader);
            }

            _defenderNames = names;
        }

        private IReadOnlyList<string> _defenderNames;

        private void SpawnDefenders()
        {
            Combatant leader = null;
            for (var slot = 0; slot < DefenderCount; slot++)
            {
                // Binanın çevresinde halka; yarısı içeride/yakında, yarısı dışta.
                var a = (slot / (float)DefenderCount) * Mathf.PI * 2f + 0.4f;
                var r = slot % 2 == 0 ? _buildingRadius * 0.45f : _buildingRadius;
                var p = SnapToNav(BootstrapUtility.GroundPoint(_world, _buildingCenter + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r))
                        + Vector3.up * 0.05f;
                var yaw = a * Mathf.Rad2Deg;
                var id = new PlayerId(MatchBootstrap.LocalPlayerId.Value + 100 + slot);
                var name = _defenderNames != null && AllyCount + slot < _defenderNames.Count ? _defenderNames[AllyCount + slot] : "Savunma " + (slot + 1);
                var bot = SpawnBot(DefenderTeam, slot, name, id, p, yaw, leader);
                if (slot == 0 && bot != null)
                    leader = bot.Bot.Combatant;
            }
        }

        private void SpawnHostages()
        {
            var names = new[] { "Rehine Ayşe", "Rehine Mehmet" };
            for (var i = 0; i < HostageRules.DefaultHostageCount; i++)
            {
                var offset = new Vector3(i == 0 ? -2.2f : 2.2f, 0f, i == 0 ? 1.2f : -1.2f);
                var p = SnapToNav(BootstrapUtility.GroundPoint(_world, _buildingCenter + offset)) + Vector3.up * 0.05f;
                var id = new PlayerId(MatchBootstrap.LocalPlayerId.Value + HostageIdOffset + i);
                var npc = HostageNpc.Create(p, _rng.Next(0, 360), id, names[i], AttackerTeam, _config.RandomSeed + i * 31, _runtimeRoot);
                if (npc == null)
                    continue;

                var index = _hostages.Count;
                _hostages.Add(npc);
                npc.SetTarget(_player != null ? _player.transform : null);
                npc.Died += _ => _rules.MarkDead(IndexOf(npc));
                EnsureRegistered(npc.Combatant, false, false);
            }
        }

        private int IndexOf(HostageNpc npc) => _hostages.IndexOf(npc);

        private void SetupHelicopter()
        {
            var away = _plannedLz - _buildingCenter;
            away.y = 0f;
            if (away.sqrMagnitude < 1f)
                away = Vector3.forward;
            var start = _plannedLz + away.normalized * 380f;
            var plan = new TeamInsertion(AttackerTeam, InsertionMethod.Helicopter,
                new Float3(start.x, 0f, start.z), new Float3(_plannedLz.x, _plannedLz.y, _plannedLz.z));
            _heli = Helicopter.Create(plan, Helicopter.DefaultAltitude);
            if (_heli == null)
                return;

            _heli.transform.SetParent(_runtimeRoot, true);
            _heli.AutoReleaseSeconds = 0f;                 // oyuncu rehineleri getirene kadar bekle
            _heli.EmptyArrivalReleaseSeconds = 100000f;
            _heli.DestroyDelaySeconds = 60f;

            // Tahliye noktası işareti (çarpışmasız yeşil sütun).
            _lzMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _lzMarker.name = "TahliyeIsareti";
            _lzMarker.transform.SetParent(_runtimeRoot, true);
            _lzMarker.transform.position = _plannedLz + Vector3.up * 15f;
            _lzMarker.transform.localScale = new Vector3(1.2f, 15f, 1.2f);
            var col = _lzMarker.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            var rend = _lzMarker.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = new Material(rend.sharedMaterial) { color = new Color(0.2f, 0.9f, 0.3f, 1f) };
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void CallHelicopter()
        {
            if (_heliCalled)
                return;

            _heliCalled = true;
            if (_heli != null)
                _heli.Begin();
            if (_ui != null)
                _ui.ShowMessage("T-70 YOLDA — tahliye noktasına ilerle", 4f);
        }

        private Member SpawnBotInternal(int team, int slot, string name, PlayerId id, Vector3 position, float yaw, Combatant leader)
        {
            var args = new BotSpawnArgs
            {
                Id = id,
                Name = name,
                Team = team,
                Role = LoadoutCatalog.RoleForSlot(slot),
                Difficulty = _config.Difficulty,
                Transport = null,
                SpawnOnGround = true,
                GroundPosition = position,
                GroundYaw = yaw,
                Slot = slot,
                SquadLeader = slot == 0 ? null : leader,
                Seed = GameCompositionRoot.DeriveSeed(_config.RandomSeed, id.Value),
                Parent = _runtimeRoot
            };

            var bot = BootstrapUtility.Try(() => BotController.Create(args), "BotController.Create");
            if (bot == null)
                return null;

            EnsureRegistered(bot.Combatant, false, true);
            return new Member { Bot = bot };
        }

        private sealed class Member
        {
            public BotController Bot;
        }

        private Member SpawnBot(int team, int slot, string name, PlayerId id, Vector3 position, float yaw, Combatant leader) =>
            SpawnBotInternal(team, slot, name, id, position, yaw, leader);

        private void EnsureRegistered(Combatant combatant, bool isLocal, bool inChain)
        {
            if (combatant == null || !combatant.Id.IsValid)
                return;

            try
            {
                if (_match != null && !_match.IsRegistered(combatant.Id))
                    _match.RegisterCombatant(combatant.Id, combatant.RankedName, isLocal, combatant.Team, combatant.Role);
                if (inChain && _chain != null && !_chain.IsRegistered(combatant.Id))
                    _chain.Register(combatant.Id, combatant.Team, combatant.Rank);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private Vector3 AttackerSlot(int slot)
        {
            var yaw = _attackYaw * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var right = new Vector3(forward.z, 0f, -forward.x);
            var offset = right * (slot - 1.5f) * 2.5f;
            return SnapToNav(BootstrapUtility.GroundPoint(_world, _attackBase + offset)) + Vector3.up * 0.05f;
        }

        private static Vector3 SnapToNav(Vector3 p)
        {
            return NavMesh.SamplePosition(p, out var hit, 12f, NavMesh.AllAreas) ? hit.position : p;
        }

        private void SetupPresentation(Camera[] sceneCameras)
        {
            BootstrapUtility.InstallGrass(_world, _settings != null ? _settings.Current.QualityLevel : 2);
            _ui = GameplayUiController.Create(_runtimeRoot, _player, _settings, GameSession.ReturnToMainMenu);
            BuildHud();

            var replaced = BootstrapUtility.DisableSceneCamerasIfReplaced(sceneCameras);
            if (!replaced && Camera.allCamerasCount == 0)
                BootstrapUtility.CreateObserverCamera(BootstrapUtility.GroundPoint(_world, _attackBase));

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

        private void Subscribe()
        {
            if (_eventBus == null)
                return;

            _onEnded = _ => OnMatchEndedInternal();
            _eventBus.Subscribe(_onEnded);
        }

        // ------------------------------------------------------------------ Kurallar

        private void TickHostages()
        {
            for (var i = 0; i < _hostages.Count; i++)
            {
                var h = _hostages[i];
                if (h == null)
                {
                    _rules.MarkDead(i);
                    continue;
                }

                if (h.IsDead)
                    _rules.MarkDead(i);
            }
        }

        private int _nearHostage = -1;

        private void TickInteraction()
        {
            _nearHostage = -1;
            if (_player == null || _playerCombatant == null || !_playerCombatant.IsAlive)
                return;

            var best = HostageRules.InteractRange;
            for (var i = 0; i < _hostages.Count; i++)
            {
                var h = _hostages[i];
                var s = _rules.GetState(i);
                if (h == null || s == HostageState.Dead || s == HostageState.Extracted)
                    continue;

                var d = FlatDistance(h.transform.position, _player.transform.position);
                if (d <= best)
                {
                    best = d;
                    _nearHostage = i;
                }
            }

            if (_nearHostage < 0 || !InputBindings.Pressed(BindAction.Interact))
                return;

            var newState = _rules.Interact(_nearHostage);
            _hostages[_nearHostage].SetState(newState);
            if (_ui != null)
                _ui.ShowMessage(newState == HostageState.Following ? "REHİNE SENİ TAKİP EDİYOR" : "REHİNE BEKLİYOR", 2f);

            if (newState == HostageState.Following)
                CallHelicopter();
        }

        private void TickExtraction()
        {
            if (_heli == null || !_heli.HasArrived || _heli.IsDeparting)
                return;

            var lz = _heli.LandingZone;
            for (var i = 0; i < _hostages.Count; i++)
            {
                var s = _rules.GetState(i);
                if (s != HostageState.Following && s != HostageState.Holding)
                    continue;

                var h = _hostages[i];
                if (h == null || !HostageRules.CanExtract(FlatDistance(h.transform.position, lz), true))
                    continue;

                _rules.MarkExtracted(i);
                h.Evacuate();
                if (_ui != null && !_rules.IsOver)
                    _ui.ShowMessage("REHİNE TAHLİYE EDİLDİ (" + _rules.CountIn(HostageState.Extracted) + "/" + _rules.HostageCount + ")", 2.5f);
            }
        }

        private void TickPlayerAlive()
        {
            if (_playerCombatant != null && _playerCombatant.IsInitialized && !_playerCombatant.IsAlive)
                _rules.SquadLost();
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ------------------------------------------------------------------ Maç sonu

        private void ShowEndScreen()
        {
            if (_flow == Flow.EndScreen)
                return;

            _flow = Flow.EndScreen;
            var result = BuildResult();
            if (!_resultRecorded)
            {
                _resultRecorded = true;
                GameSession.LastResult = result;
            }

            if (_hudCanvas != null)
                _hudCanvas.enabled = false;
            if (_ui != null)
                _ui.SetEndScreenActive(true);

            var shown = BootstrapUtility.Try(() => EndScreen.Show(result, GameSession.Restart, GameSession.ReturnToMainMenu), "EndScreen.Show");
            if (shown == null)
                GameSession.ReturnToMainMenu();
        }

        private MatchResult BuildResult()
        {
            var won = _rules.IsSuccess;
            var teamName = SafeTeamName(AttackerTeam);
            var total = AllyCount + 1 + DefenderCount;
            var elapsed = _rules.Elapsed;
            var saved = _rules.CountIn(HostageState.Extracted);
            try
            {
                if (_stats != null)
                {
                    var b = _stats.BuildResult(MatchBootstrap.LocalPlayerId);
                    return new MatchResult(won, won ? 1 : 2, total, b.Kills, b.Headshots, b.DamageDealt, elapsed, b.Accuracy,
                        b.KillerName, won ? 1 : 2, 2, teamName, saved);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            return new MatchResult(won, won ? 1 : 2, total, 0, 0, 0f, elapsed, 0f, null, won ? 1 : 2, 2, teamName, saved);
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
                // yedek ad aşağıda
            }

            return (team + 1) + ". Tim";
        }

        private void OnMatchEndedInternal()
        {
            if (_flow != Flow.Running)
                return;

            _flow = Flow.Ended;
            _endScreenAt = Time.time + EndScreenDelaySeconds;
            if (_ui != null)
                _ui.ShowMessage(HostageRules.OutcomeText(_rules.Outcome), EndScreenDelaySeconds);
        }

        // ------------------------------------------------------------------ HUD

        private void BuildHud()
        {
            var canvas = UiFactory.CreateCanvas("HostageCanvas", 60);
            canvas.transform.SetParent(_runtimeRoot, false);
            _hudCanvas = canvas;

            var panel = UiFactory.Panel(canvas.transform, UiTheme.PanelDark);
            UiFactory.Anchor(panel, UiAnchor.Top, new Vector2(0f, -14f), new Vector2(620f, 84f));

            _status = UiFactory.Label(panel, "", UiTheme.FontMedium, TextAnchor.MiddleCenter, UiTheme.Khaki, FontStyle.Bold);
            UiFactory.SetRect(_status, new Vector2(0f, 0.3f), new Vector2(0.62f, 1f), new Vector2(6f, 0f), new Vector2(-6f, -4f));
            _clock = UiFactory.Label(panel, "", UiTheme.FontLarge, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(_clock, new Vector2(0.62f, 0.3f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, -4f));
            _sub = UiFactory.Label(panel, "", UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.TextMuted);
            UiFactory.SetRect(_sub, new Vector2(0f, 0f), new Vector2(1f, 0.3f), new Vector2(6f, 2f), new Vector2(-6f, 0f));

            _prompt = UiFactory.Label(canvas.transform, "", UiTheme.FontMedium, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
            UiFactory.Anchor(_prompt, UiAnchor.Bottom, new Vector2(0f, 190f), new Vector2(600f, 40f));
        }

        private void RefreshHud()
        {
            if (_status == null || _rules == null)
                return;

            var status = "REHİNE " + _rules.CountIn(HostageState.Extracted) + "/" + _rules.HostageCount + " KURTARILDI";
            var t = Mathf.CeilToInt(_rules.TimeRemaining);
            var clock = (t / 60).ToString("00") + ":" + (t % 60).ToString("00");
            string sub;
            if (_rules.AnyFreed)
                sub = _heli != null && _heli.HasArrived ? "T-70 İNDİ — rehineleri yeşil işarete götür" : "Rehineleri tahliye noktasına götür · " + _locationName;
            else
                sub = "Rehineleri bul (" + _locationName + ") · F ile serbest bırak";

            var prompt = string.Empty;
            if (_nearHostage >= 0 && !_rules.IsOver)
            {
                var tag = InputBindings.Bracket(BindAction.Interact);
                prompt = _rules.GetState(_nearHostage) == HostageState.Following ? tag + " Rehine beklesin" : tag + " Rehineyi çöz / takip ettir";
            }

            if (status != _lastStatus) { _status.text = _lastStatus = status; }
            if (clock != _lastClock) { _clock.text = _lastClock = clock; }
            if (sub != _lastSub) { _sub.text = _lastSub = sub; }
            if (prompt != _lastPrompt) { _prompt.text = _lastPrompt = prompt; }
        }

        // ------------------------------------------------------------------ Kapanış

        private void Teardown()
        {
            if (_disposed)
                return;

            _disposed = true;
            Time.timeScale = 1f;

            if (_eventBus != null && _onEnded != null)
                _eventBus.Unsubscribe(_onEnded);
            if (_settings != null && _onSettingsChanged != null)
                _settings.Changed -= _onSettingsChanged;

            if (_match != null)
                _match.AutoEndOnElimination = true;

            BootstrapUtility.Try(() => GameAudio.SetAmbience(SoundId.None, 0f), "GameAudio.SetAmbience");
            BootstrapUtility.ClearStaticRegistries();

            if (_container != null)
            {
                if (ReferenceEquals(GameContext.Services, _container))
                    GameContext.Clear();
                BootstrapUtility.Try(_container.DisposeAll, "ServiceContainer.DisposeAll");
                _container = null;
            }

            BootstrapUtility.ReleaseCursor();
        }
    }
}
