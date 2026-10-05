using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Infrastructure.DI;
using Project.Infrastructure.Loot;
using Project.Infrastructure.Player;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using Project.Presentation.Player;
using UnityEngine;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// Atış poligonu (TrainingRange) kompozisyon kökü. Harekâtla aynı altyapı (servisler, balistik, topçu, HUD, harita,
    /// envanter, duraklatma menüsü) ama bölge ve rakip tim yok: oyuncu yerde, üç silah + sınırsız mermi ile başlar; silah
    /// raflarında tüm Türk silahları ve teçhizat bulunur; atış hattı boyunca (25-300 m) sabit hedefler ve hareketli hedefler
    /// kurulur. Oyuncu ölürse kısa süre sonra başlangıç noktasında yeniden doğar. Esc menüsünden ana menüye dönülür.
    /// Adanmış sunucuda poligon anlamsızdır: sahne kurulmaz, harekât sahnesine geçilir.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class TrainingBootstrap : MonoBehaviour
    {
        private const int TargetIdBase = 1000;

        private static readonly float[] LaneDistances = { 25f, 50f, 100f, 200f, 300f };
        private static readonly float[] LaneHealth = { 100f, 100f, 150f, 150f, 200f };

        [Tooltip("Sahnede WorldMetadata yoksa poligon çalışma zamanında kurulur.")]
        [SerializeField] private bool buildRangeIfMissing = true;

        [Tooltip("Hedeflerin yeniden kalkma süresi (sn).")]
        [SerializeField] private float targetRespawnSeconds = 3f;

        [Tooltip("Oyuncu ölünce yeniden doğma gecikmesi (sn).")]
        [SerializeField] private float reloadAfterDeathSeconds = 4f;

        [Tooltip("Poligonda topçu bekleme süresi (sn).")]
        [SerializeField] private float artilleryCooldownSeconds = 25f;

        private readonly List<DamageableTarget> _targets = new(16);

        private ServiceContainer _container;
        private MatchConfig _config;
        private IEventBus _eventBus;
        private MatchService _match;
        private CombatService _combat;
        private ArtilleryService _artillery;
        private DamageableRegistry _damageables;
        private SettingsService _settings;
        private WorldMetadata _world;
        private PlayerController _player;
        private Combatant _playerCombatant;
        private GameplayUiController _ui;
        private GameLoopPresenter _loop;
        private Transform _runtimeRoot;
        private Action<GameSettings> _onSettingsChanged;

        private Vector3 _spawnPoint;
        private Vector3 _rangeForward = Vector3.forward;
        private float _reloadAt = -1f;
        private bool _setupComplete;
        private bool _disposed;
        private bool _redirectToOperation;

        public static PlayerId LocalPlayerId => GameCompositionRoot.DefaultLocalPlayerId;

        public ServiceContainer Services => _container;
        public PlayerController Player => _player;
        public GameplayUiController Ui => _ui;
        public IReadOnlyList<DamageableTarget> Targets => _targets;
        public bool IsReady => _setupComplete;

        private void Awake()
        {
            GameSession.EnsureInitialized();
            if (ServerRuntime.IsDedicatedServer)
            {
                Debug.LogWarning("[Sunucu] Atış poligonu sunucuda çalıştırılmaz — harekât sahnesine geçiliyor.");
                _redirectToOperation = true;
                return;
            }

            GameSession.Mode = GameMode.Training;
            BootstrapUtility.PrepareScene();

            _settings = GameSession.Settings;
            _config = CreateTrainingConfig();
            _container = GameCompositionRoot.Build(_config, _settings, GameSession.Career, LocalPlayerId);
            GameContext.Set(_container);

            _container.TryResolve(out _eventBus);
            _container.TryResolve(out _match);
            _container.TryResolve(out _combat);
            _container.TryResolve(out _artillery);
            _container.TryResolve(out _damageables);

            if (_container.TryResolve<KillFeedService>(out var killFeed))
                killFeed.LocalTeamOverride = 0;

            var quality = _settings != null ? _settings.Current.QualityLevel : 2;
            BootstrapUtility.InitializeEngineSystems(PostProcessing.Look.Gameplay, quality, true);
        }

        private void Start()
        {
            if (_redirectToOperation)
            {
                GameSession.StartOperation();
                return;
            }

            if (_container == null)
                return;

            var sceneCameras = Camera.allCameras;
            _runtimeRoot = new GameObject("[Atış Poligonu]").transform;

            BootstrapUtility.Try(SetupWorld, "Poligon kurulumu");
            BootstrapUtility.Try(SetupCombatSystems, "Çatışma sistemleri");
            BootstrapUtility.Try(ResolveSpawn, "Başlangıç noktası");
            BootstrapUtility.Try(SpawnPlayer, "Oyuncu");
            BootstrapUtility.Try(SpawnRacks, "Silah rafları");
            BootstrapUtility.Try(SpawnTargets, "Hedefler");
            BootstrapUtility.Try(() => SetupPresentation(sceneCameras), "Arayüz");
            BootstrapUtility.Try(SetupLoop, "Simülasyon döngüsü");

            // Poligonda geri sayım/intikal yok: doğrudan çatışma fazı (bölge başlamaz, kayıtlı tim olmadığından maç bitmez).
            if (_match != null)
                BootstrapUtility.Try(() => _match.TransitionTo(MatchPhase.InMatch), "MatchService.TransitionTo");
            BootstrapUtility.Try(() => GameAudio.SetAmbience(SoundId.Wind, 0.4f), "GameAudio.SetAmbience");

            if (_ui != null)
                _ui.ShowMessage("ATIŞ POLİGONU — Mermi sınırsız. Raflardan silah alabilirsin.", 6f);

            _setupComplete = true;
            GameSession.HideLoading();
        }

        private void Update()
        {
            if (!_setupComplete || _disposed)
                return;

            if (_reloadAt < 0f && _playerCombatant != null && _playerCombatant.IsInitialized && !_playerCombatant.IsAlive)
                OnLocalPlayerDied();

            if (_reloadAt >= 0f && Time.time >= _reloadAt)
            {
                _reloadAt = -1f;
                RespawnPlayer();
            }
        }

        private void OnDestroy()
        {
            Teardown();
        }

        // ------------------------------------------------------------------ Kurulum

        private MatchConfig CreateTrainingConfig()
        {
            var config = new MatchConfig().WithTeams(2, 1);
            config.MapName = "Atış Poligonu";
            config.MapHalfSize = 150f;
            config.InitialZoneRadius = 1000f;
            config.PreMatchDurationSeconds = 0f;
            config.ArtilleryCooldownSeconds = artilleryCooldownSeconds;
            config.FriendlyFire = false;
            config.RandomSeed = Environment.TickCount & 0x7fffffff;
            if (_settings != null)
                config.Difficulty = _settings.Current.Difficulty;
            return config;
        }

        private void SetupWorld()
        {
            _world = WorldMetadata.Instance != null ? WorldMetadata.Instance : FindAnyObjectByType<WorldMetadata>();
            if (_world == null && buildRangeIfMissing)
            {
                Debug.Log("[TrainingBootstrap] Sahnede poligon yok — çalışma zamanında kuruluyor.");
                var parent = new GameObject("[Poligon]").transform;
                _world = BootstrapUtility.Try(() => TrainingRangeBuilder.Build(parent), "TrainingRangeBuilder.Build");
            }

            if (_world != null)
            {
                var navMesh = _world.NavMesh;
                if (navMesh != null)
                    BootstrapUtility.Try(() => NavMeshBaker.EnsureLoaded(navMesh), "NavMeshBaker.EnsureLoaded");
            }
            else
            {
                BootstrapUtility.CreateFallbackGround(_config.MapHalfSize).transform.SetParent(_runtimeRoot, true);
            }

            BootstrapUtility.EnsureSun();
        }

        private void SetupCombatSystems()
        {
            if (_combat != null)
                BootstrapUtility.Try(() => BallisticsSystem.Create(_combat, _eventBus), "BallisticsSystem.Create");

            if (_artillery != null)
                BootstrapUtility.Try(() => ArtilleryExecutor.Create(_artillery), "ArtilleryExecutor.Create");
        }

        private void ResolveSpawn()
        {
            var spawn = Vector3.zero;
            if (_world != null && _world.GroundSpawnPoints != null && _world.GroundSpawnPoints.Count > 0)
                spawn = _world.GroundSpawnPoints[0];

            // Atış hattı yönü: başlangıçtan poligon merkezine doğru (merkezdeyse +Z).
            var center = _world != null ? new Vector3(_world.MapCenter.x, 0f, _world.MapCenter.y) : Vector3.zero;
            var toCenter = center - new Vector3(spawn.x, 0f, spawn.z);
            _rangeForward = toCenter.sqrMagnitude > 400f ? toCenter.normalized : Vector3.forward;
            _spawnPoint = BootstrapUtility.GroundPoint(_world, spawn) + Vector3.up * 0.05f;
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
                Transport = null,
                Seat = 0,
                GroundPosition = _spawnPoint,
                GroundYaw = Mathf.Atan2(_rangeForward.x, _rangeForward.z) * Mathf.Rad2Deg,
                Settings = settings,
                ApplyLoadout = false,
                InfiniteAmmo = true,
                RegisterInMatch = false
            };

            _player = BootstrapUtility.Try(() => PlayerController.Create(args), "PlayerController.Create");
            if (_player == null)
                return;

            _playerCombatant = _player.Combatant;
            _player.Died += OnLocalPlayerDied;
            if (_playerCombatant != null)
                _playerCombatant.DropLootOnDeath = false;   // poligonda ölünce teçhizat yere saçılmasın

            GiveTrainingKit(_playerCombatant != null ? _playerCombatant.Inventory : null);
        }

        /// <summary>Üç silah (piyade tüfeği, keskin nişancı, tabanca), zırh, büyük çanta, bombalar ve sağlık malzemesi.</summary>
        private static void GiveTrainingKit(InventoryService inventory)
        {
            if (inventory == null)
                return;

            BootstrapUtility.Try(() => inventory.InfiniteAmmo = true, "InventoryService.InfiniteAmmo");
            BootstrapUtility.Try(() => inventory.EquipBackpack(3), "InventoryService.EquipBackpack");
            BootstrapUtility.Try(() => inventory.EquipArmor(ItemIds.Vest2), "InventoryService.EquipArmor");
            BootstrapUtility.Try(() => inventory.EquipArmor(ItemIds.Helmet2), "InventoryService.EquipArmor");
            BootstrapUtility.Try(() => inventory.GiveWeapon(WeaponIds.Mpt76, true), "InventoryService.GiveWeapon");
            BootstrapUtility.Try(() => inventory.GiveWeapon(WeaponIds.Jng90, true), "InventoryService.GiveWeapon");
            BootstrapUtility.Try(() => inventory.GiveWeapon(WeaponIds.Sar9, true), "InventoryService.GiveWeapon");
            BootstrapUtility.Try(() => inventory.GiveItem(ItemIds.FragGrenade, 6), "InventoryService.GiveItem");
            BootstrapUtility.Try(() => inventory.GiveItem(ItemIds.SmokeGrenade, 3), "InventoryService.GiveItem");
            BootstrapUtility.Try(() => inventory.GiveItem(ItemIds.Bandage, 10), "InventoryService.GiveItem");
            BootstrapUtility.Try(() => inventory.GiveItem(ItemIds.FirstAid, 3), "InventoryService.GiveItem");
            BootstrapUtility.Try(() => inventory.GiveItem(ItemIds.MedKit, 1), "InventoryService.GiveItem");
            BootstrapUtility.Try(() => inventory.GiveItem(ItemIds.EnergyDrink, 3), "InventoryService.GiveItem");
            BootstrapUtility.Try(() => inventory.SetActiveSlot(InventoryService.PrimarySlotA), "InventoryService.SetActiveSlot");
        }

        /// <summary>
        /// Silah rafları: ilk ganimet noktalarına tüm silahlar (katalog sırasıyla), kalanlara bomba/sağlık/zırh. Ganimet noktası
        /// yoksa başlangıç noktasının yanında bir sıra halinde dizilir.
        /// </summary>
        private void SpawnRacks()
        {
            var items = new List<LootItemData>(24);
            var weapons = WeaponCatalog.All;
            for (var i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] != null)
                    AddLoot(items, () => ItemCatalog.CreateWeaponLoot(weapons[i].WeaponId));
            }

            AddLoot(items, () => ItemCatalog.CreateLoot(ItemIds.FragGrenade, 5));
            AddLoot(items, () => ItemCatalog.CreateLoot(ItemIds.SmokeGrenade, 5));
            AddLoot(items, () => ItemCatalog.CreateLoot(ItemIds.FirstAid, 3));
            AddLoot(items, () => ItemCatalog.CreateLoot(ItemIds.MedKit, 2));
            AddLoot(items, () => ItemCatalog.CreateLoot(ItemIds.Painkiller, 2));
            AddLoot(items, () => ItemCatalog.CreateArmorLoot(ItemIds.Vest3));
            AddLoot(items, () => ItemCatalog.CreateArmorLoot(ItemIds.Helmet3));

            var points = _world != null ? _world.LootPoints : null;
            var right = new Vector3(_rangeForward.z, 0f, -_rangeForward.x);
            for (var i = 0; i < items.Count; i++)
            {
                Vector3 position;
                if (points != null && i < points.Count)
                    position = points[i].Position;
                else
                {
                    var column = i % 12;
                    var row = i / 12;
                    position = BootstrapUtility.GroundPoint(_world, _spawnPoint + right * (4f + column * 1.4f) - _rangeForward * (3f + row * 1.6f));
                }

                var item = items[i];
                var yaw = Mathf.Atan2(_rangeForward.x, _rangeForward.z) * Mathf.Rad2Deg;
                BootstrapUtility.Try(() => LootPickupComponent.Spawn(item, position, yaw), "LootPickupComponent.Spawn");
            }
        }

        private static void AddLoot(List<LootItemData> items, Func<LootItemData> create)
        {
            try
            {
                var item = create();
                if (item.IsValid)
                    items.Add(item);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Atış hattı boyunca 25/50/100/200/300 m'de sabit hedefler, 75 ve 150 m'de yatay hareket eden hedefler.</summary>
        private void SpawnTargets()
        {
            if (_eventBus == null || _damageables == null)
                return;

            var limit = (_world != null ? _world.MapHalfSize : _config.MapHalfSize) - 6f;
            var right = new Vector3(_rangeForward.z, 0f, -_rangeForward.x);
            var origin = new Vector3(_spawnPoint.x, 0f, _spawnPoint.z);
            var id = TargetIdBase;

            for (var lane = 0; lane < LaneDistances.Length; lane++)
            {
                var lateral = (lane - LaneDistances.Length / 2) * 7f;
                var position = ClampToRange(origin + _rangeForward * LaneDistances[lane] + right * lateral, limit);
                var ground = BootstrapUtility.GroundPoint(_world, position);
                var health = LaneHealth[lane];
                var targetId = id++;
                var target = BootstrapUtility.Try(
                    () => DamageableTarget.CreateDummy(ground, _eventBus, _damageables, targetId, health),
                    "DamageableTarget.CreateDummy");
                Register(target);

                // Yakın mesafede ikinci hedef (çoklu hedef geçişi için).
                if (lane < 2)
                {
                    var side = BootstrapUtility.GroundPoint(_world, ClampToRange(position + right * 3.5f, limit));
                    var sideId = id++;
                    Register(BootstrapUtility.Try(
                        () => DamageableTarget.CreateDummy(side, _eventBus, _damageables, sideId),
                        "DamageableTarget.CreateDummy"));
                }
            }

            SpawnMover(origin, right, 75f, 3.2f, limit, id++);
            SpawnMover(origin, right, 150f, 4.5f, limit, id);
        }

        private void SpawnMover(Vector3 origin, Vector3 right, float distance, float speed, float limit, int targetId)
        {
            var center = origin + _rangeForward * distance;
            var a = BootstrapUtility.GroundPoint(_world, ClampToRange(center - right * 18f, limit));
            var b = BootstrapUtility.GroundPoint(_world, ClampToRange(center + right * 18f, limit));
            Register(BootstrapUtility.Try(
                () => DamageableTarget.CreateMoving(a, b, speed, _eventBus, _damageables, targetId),
                "DamageableTarget.CreateMoving"));
        }

        private void Register(DamageableTarget target)
        {
            if (target == null)
                return;

            target.RespawnSeconds = targetRespawnSeconds;
            if (_runtimeRoot != null && target.transform.parent == null)
                target.transform.SetParent(_runtimeRoot, true);
            _targets.Add(target);
        }

        private static Vector3 ClampToRange(Vector3 position, float limit)
        {
            if (limit <= 0f)
                return position;

            position.x = Mathf.Clamp(position.x, -limit, limit);
            position.z = Mathf.Clamp(position.z, -limit, limit);
            return position;
        }

        private void SetupPresentation(Camera[] sceneCameras)
        {
            _ui = GameplayUiController.Create(_runtimeRoot, _player, _settings, GameSession.ReturnToMainMenu);

            var replaced = BootstrapUtility.DisableSceneCamerasIfReplaced(sceneCameras);
            if (!replaced && Camera.allCamerasCount == 0)
                BootstrapUtility.CreateObserverCamera(_spawnPoint + _rangeForward * 40f);

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

        // ------------------------------------------------------------------ Akış

        private void OnLocalPlayerDied()
        {
            if (_reloadAt >= 0f || _disposed)
                return;

            _reloadAt = Time.time + reloadAfterDeathSeconds;
            if (_ui != null)
                _ui.ShowMessage("VURULDUN — başlangıç noktasında yeniden doğacaksın", reloadAfterDeathSeconds);
        }

        /// <summary>Oyuncuyu başlangıç noktasında canlandırır ve teçhizatı yeniler; olmazsa poligonu yeniden yükler.</summary>
        private void RespawnPlayer()
        {
            if (_player == null)
            {
                GameSession.LoadScene(SceneNames.Training);
                return;
            }

            var yaw = Mathf.Atan2(_rangeForward.x, _rangeForward.z) * Mathf.Rad2Deg;
            if (!BootstrapUtility.Try(() => _player.Respawn(_spawnPoint, yaw), "PlayerController.Respawn")
                || _playerCombatant == null || !_playerCombatant.IsAlive)
            {
                GameSession.LoadScene(SceneNames.Training);
                return;
            }

            var inventory = _playerCombatant.Inventory;
            if (inventory != null && !inventory.HasAnyWeapon)
                GiveTrainingKit(inventory);
            else if (inventory != null)
                BootstrapUtility.Try(() => inventory.InfiniteAmmo = true, "InventoryService.InfiniteAmmo");

            if (_ui != null)
            {
                _ui.SetHudVisible(true);
                _ui.ShowMessage("ATIŞ POLİGONU — yeniden hazırsın", 2.5f);
            }
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            if (_ui != null)
                _ui.ApplySettings(settings);
        }

        private void Teardown()
        {
            if (_disposed)
                return;

            _disposed = true;
            Time.timeScale = 1f;

            if (_settings != null && _onSettingsChanged != null)
                _settings.Changed -= _onSettingsChanged;

            if (_player != null)
                _player.Died -= OnLocalPlayerDied;

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
