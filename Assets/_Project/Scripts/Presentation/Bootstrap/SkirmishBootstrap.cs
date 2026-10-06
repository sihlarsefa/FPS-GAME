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
using Project.Infrastructure.Loot;
using Project.Infrastructure.Rendering;
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
    /// "ÇATIŞMA (Hızlı Maç)" modu: Kuzgun Vadisi sahnesinde tek bir bölgede 2 tim × 10 asker. İntikal yok (yerden doğuş),
    /// ölenler her 20 sn'de bir kendi üslerinde takviye dalgasıyla döner, bölge (zone) kapalıdır. 50 öldürmeye ilk ulaşan
    /// ya da 10 dk sonunda önde olan kazanır. Kural mantığı <see cref="SkirmishRules"/>'ta.
    /// <see cref="MatchBootstrap"/> bu modda kendini devre dışı bırakıp bu bileşeni ekler; servis kurulumu aynı
    /// <see cref="GameCompositionRoot"/> ve <see cref="BootstrapUtility"/> parçalarıyla yapılır (kompozisyon).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkirmishBootstrap : MonoBehaviour
    {
        private enum Flow { Setup, Running, Ended, EndScreen }

        private sealed class Member
        {
            public BotController Bot;
            public int Slot;
            public string Name;
        }

        private const int LocalTeam = 0;
        private const int RespawnIdOffset = 200;
        private const float EndScreenDelaySeconds = 3.5f;

        private readonly List<Member>[] _members = { new List<Member>(10), new List<Member>(10) };
        private readonly Vector3[] _bases = new Vector3[2];
        private readonly float[] _yaw = new float[2];
        private readonly Combatant[] _leaders = new Combatant[2];

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
        private SkirmishRules _rules;

        private Action<PlayerDiedEvent> _onDied;
        private Action<MatchEndedEvent> _onEnded;
        private Action<GameSettings> _onSettingsChanged;

        private Flow _flow = Flow.Setup;
        private bool _disposed;
        private bool _resultRecorded;
        private bool _endForced;
        private float _endScreenAt = -1f;
        private int _respawnCounter;
        private string _locationName = "Kuzgun Vadisi";
        private System.Random _rng;

        private Canvas _hudCanvas;
        private Text _scoreLeft;
        private Text _scoreRight;
        private Text _clock;
        private Text _sub;
        private string _lastClock, _lastSub, _lastLeft, _lastRight;
        private float _lastDeadMessageAt;

        public SkirmishRules Rules => _rules;
        public PlayerController Player => _player;
        public IReadOnlyList<Vector3> Bases => _bases;

        // ------------------------------------------------------------------ Yaşam döngüsü

        private void Awake()
        {
            GameSession.EnsureInitialized();
            GameSession.Mode = GameMode.Skirmish;
            BootstrapUtility.PrepareScene();

            _settings = GameSession.Settings;
            _config = GameSession.AcquireMatchConfig();
            _config.WithTeams(SkirmishRules.TeamCount, 10);
            GameSession.AlignConfigToMap(_config, MapCatalog.Kuzgun);
            _config.MapName = MapCatalog.KuzgunName;
            _config.PlayerInsertion = InsertionMethod.Helicopter;
            _config.PreMatchDurationSeconds = 4f;
            _config.MatchDurationSeconds = SkirmishRules.DefaultDurationSeconds;
            _rng = new System.Random(_config.RandomSeed != 0 ? _config.RandomSeed : Environment.TickCount);
            _rules = ConvoyBootstrap.Active
                ? new SkirmishRules(9999, 3600f, Project.Application.Services.ConvoyRules.RespawnWaveSeconds)
                : new SkirmishRules();

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
                _killFeed.LocalTeamOverride = LocalTeam;
            if (_match != null)
                _match.AutoEndOnElimination = false; // bitiş kuralı: skor/süre

            var quality = _settings != null ? _settings.Current.QualityLevel : 2;
            BootstrapUtility.InitializeEngineSystems(PostProcessing.Look.Gameplay, quality, true);
        }

        private void Start()
        {
            if (_container == null)
                return;

            var sceneCameras = Camera.allCameras;
            _runtimeRoot = new GameObject("[Çatışma]").transform;

            BootstrapUtility.Try(SetupWorld, "Çatışma: dünya");
            BootstrapUtility.Try(SetupCombat, "Çatışma: sistemler");
            BootstrapUtility.Try(PickRegion, "Çatışma: bölge seçimi");
            BootstrapUtility.Try(SpawnLoot, "Çatışma: ganimet");
            BootstrapUtility.Try(SpawnTeams, "Çatışma: timler");
            BootstrapUtility.Try(() => SetupPresentation(sceneCameras), "Çatışma: arayüz");
            BootstrapUtility.Try(() => Project.Infrastructure.Rendering.Atmosphere.Apply(_config), "Çatışma: atmosfer");
            BootstrapUtility.Try(SetupLoop, "Çatışma: döngü");
            BootstrapUtility.Try(Subscribe, "Çatışma: olaylar");

            _flow = Flow.Running;
            BootstrapUtility.Try(() => GameAudio.SetAmbience(SoundId.Ambience, 0.55f), "GameAudio.SetAmbience");
            if (_match != null)
                BootstrapUtility.Try(_match.Begin, "MatchService.Begin");

            BootstrapUtility.Try(() => DevConsole.Ensure(), "DevConsole.Ensure");
            GameSession.RaiseMatchStarting(_config);
            GameSession.HideLoading();

            if (ConvoyBootstrap.Active)
                gameObject.AddComponent<ConvoyBootstrap>();

            if (_ui != null && !ConvoyBootstrap.Active)
                _ui.ShowMessage("ÇATIŞMA — " + _locationName.ToUpperInvariant() + " · ilk " + _rules.KillTarget + " öldürme", 4f);
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
                if (_rules.Tick(Time.deltaTime))
                    SpawnWave();

                if (_rules.IsOver && !_endForced)
                {
                    _endForced = true;
                    _match.ForceEnd(_rules.WinnerTeam);
                }

                UpdateLocalDeathNotice();
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
            // Bölge (zone) bilinçli olarak kurulmaz/başlatılmaz.
        }

        private void SpawnLoot()
        {
            if (_world == null || _lootSpawn == null || _world.LootPoints == null || _world.LootPoints.Count == 0)
                return;

            var random = new SeededRandom(GameCompositionRoot.DeriveSeed(_config.RandomSeed, 0x1007));
            LootSpawner.SpawnWorldLoot(_world.LootPoints, _lootSpawn, random);
        }

        /// <summary>Rastgele bir yer adı seçer; iki üssü o bölgenin karşı uçlarına koyar.</summary>
        private void PickRegion()
        {
            if (ConvoyBootstrap.Active && ConvoyBootstrap.TryPlaceBases(_world, _bases, _yaw))
            {
                _locationName = "Ana Yol";
                return;
            }

            Vector2 center = _world != null ? _world.MapCenter : Vector2.zero;
            var radius = 90f;

            if (_world != null && _world.Locations != null && _world.Locations.Count > 0)
            {
                var pool = new List<NamedLocation>();
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

            var distance = Mathf.Clamp(radius * 0.8f, 40f, 85f);
            var angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            var c = new Vector3(center.x, 0f, center.y);

            for (var t = 0; t < 2; t++)
            {
                var sign = t == 0 ? 1f : -1f;
                _bases[t] = SnapToNav(BootstrapUtility.GroundPoint(_world, c + dir * (distance * sign)));
                var facing = -dir * sign; // karşı üsse bak
                _yaw[t] = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            }
        }

        private void SpawnTeams()
        {
            var teamSize = Mathf.Max(1, _config.TeamSize);
            var nameRandom = new SeededRandom(GameCompositionRoot.DeriveSeed(_config.RandomSeed, 0x4A3E));
            var names = NameRoster.CreateUnique(SkirmishRules.TeamCount * teamSize, nameRandom);
            var nameIndex = 0;

            for (var team = 0; team < SkirmishRules.TeamCount; team++)
            {
                for (var slot = 0; slot < teamSize; slot++)
                {
                    var name = nameIndex < names.Count ? names[nameIndex] : "Asker " + (nameIndex + 1);
                    nameIndex++;

                    if (team == LocalTeam && slot == 0)
                    {
                        SpawnLocalPlayer();
                        if (_playerCombatant != null)
                        {
                            _leaders[team] = _playerCombatant;
                            continue;
                        }
                    }

                    var id = new PlayerId(MatchBootstrap.LocalPlayerId.Value + team * teamSize + slot);
                    var member = SpawnBot(team, slot, name, id);
                    if (slot == 0 && member != null)
                        _leaders[team] = member.Bot.Combatant;
                }
            }
        }

        private void SpawnLocalPlayer()
        {
            var settings = _settings != null ? _settings.Current : new GameSettings();
            var args = new PlayerSpawnArgs
            {
                Id = MatchBootstrap.LocalPlayerId,
                Name = settings.PlayerName,
                Team = LocalTeam,
                Role = TeamRole.Leader,
                Transport = null,
                GroundPosition = SlotPosition(LocalTeam, 0),
                GroundYaw = _yaw[LocalTeam],
                Settings = settings
            };

            _player = BootstrapUtility.Try(() => PlayerController.Create(args), "PlayerController.Create");
            if (_player == null)
                return;

            _playerCombatant = _player.Combatant;
            EnsureRegistered(_playerCombatant, true);
        }

        private Member SpawnBot(int team, int slot, string name, PlayerId id)
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
                GroundPosition = SlotPosition(team, slot),
                GroundYaw = _yaw[team],
                Slot = slot,
                SquadLeader = slot == 0 ? null : (_leaders[team] != null && _leaders[team].IsAlive ? _leaders[team] : null),
                Seed = GameCompositionRoot.DeriveSeed(_config.RandomSeed, id.Value),
                Parent = _runtimeRoot
            };

            var bot = BootstrapUtility.Try(() => BotController.Create(args), "BotController.Create");
            if (bot == null)
                return null;

            var member = new Member { Bot = bot, Slot = slot, Name = name };
            _members[team].Add(member);
            EnsureRegistered(bot.Combatant, false);
            return member;
        }

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

        /// <summary>Üs çevresinde 5'li sıralar halinde doğuş noktası.</summary>
        private Vector3 SlotPosition(int team, int slot)
        {
            var yaw = _yaw[team] * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var right = new Vector3(forward.z, 0f, -forward.x);
            var offset = right * ((slot % 5) - 2) * 3f - forward * (slot / 5) * 3f;
            return SnapToNav(BootstrapUtility.GroundPoint(_world, _bases[team] + offset)) + Vector3.up * 0.05f;
        }

        private static Vector3 SnapToNav(Vector3 p)
        {
            return NavMesh.SamplePosition(p, out var hit, 12f, NavMesh.AllAreas) ? hit.position : p;
        }

        private void SetupPresentation(Camera[] sceneCameras)
        {
            _ui = GameplayUiController.Create(_runtimeRoot, _player, _settings, GameSession.ReturnToMainMenu);
            if (!ConvoyBootstrap.Active)
                BuildScoreBar();
            BootstrapUtility.InstallGrass(_world, _settings != null ? _settings.Current.QualityLevel : 2);

            var replaced = BootstrapUtility.DisableSceneCamerasIfReplaced(sceneCameras);
            if (!replaced && Camera.allCamerasCount == 0)
                BootstrapUtility.CreateObserverCamera(BootstrapUtility.GroundPoint(_world, _bases[0]));

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

            _onDied = OnPlayerDied;
            _eventBus.Subscribe(_onDied);
            _onEnded = _ => OnMatchEndedInternal();
            _eventBus.Subscribe(_onEnded);
        }

        // ------------------------------------------------------------------ Kurallar

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            if (_match == null || _flow != Flow.Running)
                return;

            var killerTeam = _match.GetTeam(e.KillerId);
            var victimTeam = _match.GetTeam(e.VictimId);
            _rules.RegisterKill(killerTeam, victimTeam);
        }

        /// <summary>Takviye dalgası: her timin eksik askerleri üssünde yeniden doğar; ölü oyuncu canlanır.</summary>
        private void SpawnWave()
        {
            var teamSize = Mathf.Max(1, _config.TeamSize);
            var spawnedLocal = 0;

            for (var team = 0; team < SkirmishRules.TeamCount; team++)
            {
                var list = _members[team];
                var used = new bool[teamSize];
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    var m = list[i];
                    if (m.Bot == null || m.Bot.Combatant == null || !m.Bot.Combatant.IsAlive)
                    {
                        list.RemoveAt(i);
                        continue;
                    }

                    if (m.Slot >= 0 && m.Slot < teamSize)
                        used[m.Slot] = true;
                }

                if (team == LocalTeam && _playerCombatant != null)
                {
                    used[0] = true;
                    if (!_playerCombatant.IsAlive)
                        RespawnPlayer();
                }

                for (var slot = 0; slot < teamSize; slot++)
                {
                    if (used[slot])
                        continue;

                    var id = new PlayerId(MatchBootstrap.LocalPlayerId.Value + RespawnIdOffset + _respawnCounter++);
                    var name = "Asker " + (_respawnCounter + 1);
                    var member = SpawnBot(team, slot, name, id);
                    if (member == null)
                        continue;

                    if (slot == 0)
                        _leaders[team] = member.Bot.Combatant;
                    if (team == LocalTeam)
                        spawnedLocal++;
                }
            }

            if (_ui != null && spawnedLocal > 0)
                _ui.ShowMessage("TAKVİYE ULAŞTI — " + spawnedLocal + " asker", 2f);
        }

        private void RespawnPlayer()
        {
            if (_player == null)
                return;

            var ok = BootstrapUtility.Try(() => _player.Respawn(SlotPosition(LocalTeam, 0), _yaw[LocalTeam]), "PlayerController.Respawn");
            if (!ok || !_playerCombatant.IsAlive)
                return;

            try
            {
                _match?.Revive(_playerCombatant.Id);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            if (_ui != null)
                _ui.SetHudVisible(true);
        }

        private void UpdateLocalDeathNotice()
        {
            if (_ui == null || _playerCombatant == null || !_playerCombatant.IsInitialized || _playerCombatant.IsAlive)
                return;

            if (Time.time - _lastDeadMessageAt < 1f)
                return;

            _lastDeadMessageAt = Time.time;
            _ui.ShowMessage("ŞEHİT DÜŞTÜN — yeniden doğuş " + Mathf.CeilToInt(_rules.SecondsToNextWave) + " sn", 1.2f);
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
            var won = _rules.WinnerTeam == LocalTeam;
            var teamName = SafeTeamName(LocalTeam);
            var total = SkirmishRules.TeamCount * Mathf.Max(1, _config.TeamSize);
            var elapsed = _rules.Elapsed;
            try
            {
                if (_stats != null)
                {
                    var b = _stats.BuildResult(MatchBootstrap.LocalPlayerId);
                    return new MatchResult(won, won ? 1 : 2, total, b.Kills, b.Headshots, b.DamageDealt, elapsed, b.Accuracy,
                        b.KillerName, won ? 1 : 2, SkirmishRules.TeamCount, teamName, _rules.GetKills(LocalTeam));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            return new MatchResult(won, won ? 1 : 2, total, 0, 0, 0f, elapsed, 0f, null, won ? 1 : 2,
                SkirmishRules.TeamCount, teamName, _rules.GetKills(LocalTeam));
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
            string message;
            if (_rules.WinnerTeam == LocalTeam)
                message = "ZAFER! " + SafeTeamName(LocalTeam).ToUpperInvariant() + " KAZANDI";
            else if (_rules.WinnerTeam >= 0)
                message = "ÇATIŞMA SONA ERDİ — " + SafeTeamName(_rules.WinnerTeam).ToUpperInvariant() + " KAZANDI";
            else
                message = "ÇATIŞMA BERABERE BİTTİ";

            if (_ui != null)
                _ui.ShowMessage(message, EndScreenDelaySeconds);
        }

        // ------------------------------------------------------------------ Skor çubuğu

        private void BuildScoreBar()
        {
            var canvas = UiFactory.CreateCanvas("SkirmishScoreCanvas", 60);
            canvas.transform.SetParent(_runtimeRoot, false);
            _hudCanvas = canvas;

            var panel = UiFactory.Panel(canvas.transform, UiTheme.PanelDark);
            UiFactory.Anchor(panel, UiAnchor.Top, new Vector2(0f, -14f), new Vector2(620f, 84f));

            _scoreLeft = UiFactory.Label(panel, "", UiTheme.FontMedium, TextAnchor.MiddleCenter, UiTheme.Khaki, FontStyle.Bold);
            UiFactory.SetRect(_scoreLeft, new Vector2(0f, 0.3f), new Vector2(0.38f, 1f), new Vector2(6f, 0f), new Vector2(-6f, -4f));
            _scoreRight = UiFactory.Label(panel, "", UiTheme.FontMedium, TextAnchor.MiddleCenter, UiTheme.Danger, FontStyle.Bold);
            UiFactory.SetRect(_scoreRight, new Vector2(0.62f, 0.3f), new Vector2(1f, 1f), new Vector2(6f, 0f), new Vector2(-6f, -4f));
            _clock = UiFactory.Label(panel, "", UiTheme.FontLarge, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(_clock, new Vector2(0.38f, 0.3f), new Vector2(0.62f, 1f), Vector2.zero, new Vector2(0f, -4f));
            _sub = UiFactory.Label(panel, "", UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.TextMuted);
            UiFactory.SetRect(_sub, new Vector2(0f, 0f), new Vector2(1f, 0.3f), new Vector2(6f, 2f), new Vector2(-6f, 0f));
        }

        private void RefreshHud()
        {
            if (_scoreLeft == null || _rules == null)
                return;

            var left = SafeTeamName(0).ToUpperInvariant() + "  " + _rules.GetKills(0);
            var right = _rules.GetKills(1) + "  " + SafeTeamName(1).ToUpperInvariant();
            var t = Mathf.CeilToInt(_rules.TimeRemaining);
            var clock = (t / 60).ToString("00") + ":" + (t % 60).ToString("00");
            var sub = "HEDEF " + _rules.KillTarget + " · TAKVİYE " + Mathf.CeilToInt(_rules.SecondsToNextWave) + " sn · " + _locationName;

            if (left != _lastLeft) { _scoreLeft.text = _lastLeft = left; }
            if (right != _lastRight) { _scoreRight.text = _lastRight = right; }
            if (clock != _lastClock) { _clock.text = _lastClock = clock; }
            if (sub != _lastSub) { _sub.text = _lastSub = sub; }
        }

        // ------------------------------------------------------------------ Kapanış

        private void Teardown()
        {
            if (_disposed)
                return;

            _disposed = true;
            Time.timeScale = 1f;

            if (_eventBus != null && _onDied != null)
                _eventBus.Unsubscribe(_onDied);
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
