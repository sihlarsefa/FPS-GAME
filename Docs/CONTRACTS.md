# HAREKÂT — Module Contracts (agents MUST follow)

Game: **HAREKÂT** — Turkish-military themed, FPP **squad battle royale**. Teams ("tim") of **10** soldiers.
Each team inserts into the operation area by **T-70 helicopter** or **Kirpi armored vehicle**, the operation
boundary (blue zone) shrinks, last team standing wins. Offline today: player = team leader (Tim Komutanı) + 9 AI
teammates vs N AI teams. Architecture is server-authoritative-ready for online (thousands of players across many
match instances). Fiction: inter-team "harekât tatbikatı" (Mavi/Kırmızı kuvvetler) — never name real-world groups.
UI language: **Turkish**. Map: **"Kuzgun Vadisi"** — 1024 m × 1024 m (x,z ∈ [-512, 512]) rugged mountain terrain.

Unity **6000.6.0f1**, URP 17.6, Input System 1.20 (new input only — never use `UnityEngine.Input`), C# 9.

## 0. Ground rules
- Layers (assemblies): `Project.Core` (pure C#, no UnityEngine) → `Project.Application` (pure C#) →
  `Project.Infrastructure` (Unity) → `Project.Presentation` (Unity UI/flow) → `Project.Editor` (editor only).
  Never reference upward. Core/Application must stay engine-free.
- Namespaces = folder: `Project.Infrastructure.Combat`, `Project.Presentation.UI`, ...
- Style: 4 spaces, Allman braces, `sealed` classes, `_camelCase` private fields, `var`. Turkish XML summaries welcome.
- **Ownership:** only edit files you own (see AJAN_PLANI.md). You MAY add new files inside your own folder.
  You MUST NOT change public signatures listed here; you MAY add members. Never delete files (if a legacy file is
  obsolete, keep it compiling, e.g. reduce it to a small `[System.Obsolete]` class). Don't touch `.meta` files.
- Skeleton files contain `throw new NotImplementedException()` — the owner replaces all bodies.
- Verify: `zsh /private/tmp/claude-501/-Users-f2gomac-Desktop-FPS-GAME/68731bbb-b09c-4658-bc93-146394d39180/scratchpad/verify.sh <PRIVATE_OUTDIR> [--tests] [--player]`
  (use `.../scratchpad/out_<your-agent-name>`). Fix every error in YOUR files. Errors in other owners' files are
  theirs — ignore. `--player` checks runtime code compiles without UnityEditor (wrap editor-only code in `#if UNITY_EDITOR`).
  `--tests` compiles+runs `Assets/_Project/Tests/EditMode/*.cs` (NUnit subset: Assert.AreEqual/IsTrue/IsFalse/
  IsNull/IsNotNull/Greater/Less/GreaterOrEqual/LessOrEqual/Throws/DoesNotThrow/Contains/IsEmpty, [Test], [TestCase], [SetUp]).
- Unity 6.6 API notes: `Rigidbody.linearVelocity` (not velocity), avoid `FindObjectsOfType`/`FindObjectOfType`
  (use registries or `Object.FindAnyObjectByType`), avoid `GetInstanceID`, never use `UnityEngine.Input`.
  Package sources for reference (read-only): `/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/Resources/PackageManager/BuiltInPackages/`
  (URP shaders: `com.unity.render-pipelines.universal/Shaders/Lit.shader` etc.).
- No external assets exist: everything (meshes, textures, sounds) is procedural (primitives, generated meshes,
  `Texture2D`, `AudioClip.Create`). Low-poly military look.
- Performance: up to 60 combatants. No per-frame allocations in hot paths, pool objects, stagger AI.
- Authority rule: gameplay mutation (damage, inventory, AI) runs only where `GameContext.Network.HasAuthority`
  (always true offline). Presentation reacts to events.

## 1. Core & Application (already defined — read the source)
`Assets/_Project/Scripts/Core/**` and `Assets/_Project/Scripts/Application/**` — public APIs are in the skeleton
files (doc comments describe behaviour). Key types: `PlayerId`, `Float3`, `WeaponDefinitionData`, `ItemDefinition`,
`LootItemData`, `ArmorPiece`, `MatchConfig` (TeamCount×TeamSize), `PlayerCommand`, `BotSenses`, `TeamRole`,
`InsertionMethod`, `SquadOrder`, events (`PlayerDamagedEvent`, `PlayerDiedEvent`, `HitConfirmedEvent`,
`WeaponFiredEvent`, `WeaponReloadStartedEvent`, `WeaponReloadedEvent`, `LootPickedUpEvent`, `ItemUsedEvent`,
`ExplosionEvent`, `ZoneStageChangedEvent`, `MatchPhaseChangedEvent`, `MatchEndedEvent`, `SquadOrderIssuedEvent`,
`ArtilleryStrikeEvent`). Services: `WeaponRuntimeService`, `CombatService`, `HealthService`, `InventoryService`,
`ItemUseService`, `BoostService`, `LootSpawnService`, `MatchService` (also `ICombatantDirectory`, `ITeamRelations`),
`ZoneService`, `MatchStatsService`, `KillFeedService`, `SquadOrderService`, `ArtilleryService`, `InsertionPlanner`,
`PlaneRouteService`, `SkydiveSimulator`, `BotDecisionService`, `BotDifficultyProfile`, `SettingsService`,
`CareerStatsService`, `SeededRandom`, `SimulationClock`, `GameTickCoordinator`, `DamageableRegistry`.
Catalogs: `WeaponIds`/`WeaponCatalog`, `ItemIds`/`ItemCatalog`, `LoadoutCatalog`, `NameRoster`, `DamageSourceIds`.

Turkish weapons (WeaponIds): SAR 9, Canik TP9 (9mm pistols) · SAR 109T (9mm SMG) · MPT-55 (5.56) · MPT-76 (7.62) ·
G3A7 (7.62) · KNT-76 (7.62 DMR, 3x) · JNG-90 Bora-12 (7.62 bolt sniper, 6x, HasScope) · PMT-76 (7.62 MG, 100 rnd) ·
Escort (12ga pump). Team = 10: slot0 Leader, 1 Marksman, 2 MachineGunner, 3 Medic, 4 Radioman, 5 Grenadier, 6-9 Rifleman.

**Turkish chain of command (TSK):** `MilitaryRank` enum (Er … Onbaşı, Çavuş, Sözleşmeli Er, Uzman Onbaşı, Uzman Çavuş,
Astsubay Çavuş … Astsubay Kıdemli Başçavuş, Asteğmen, Teğmen, Üsteğmen, Yüzbaşı, Binbaşı …). `RankCatalog` gives
names/abbreviations ("Yzb.", "Astsb.Kd.Çvş.", "Uzm.Çvş."), XP thresholds and per-slot ranks (leader Yüzbaşı/Üsteğmen,
deputy an Astsubay, others Uzman Çavuş/Sözleşmeli Er/Er). `ChainOfCommandService` keeps each team ordered by rank;
when the commander dies command transfers to the next most senior (`CommandTransferredEvent`) — AI squads then follow
the new commander; if the player (commander) dies, the AI chain continues the fight. Display names everywhere use
`RankCatalog.FormatName(rank, name)`. Player career rank grows with XP (`CareerStats.Experience`).
`ChainOfCommandService` must also be registered in the container.

## 2. Service access (`Project.Infrastructure.GameContext`, written)
Static, scene-scoped. Composition root (pres-bootstrap) MUST register in the `ServiceContainer`:
`IEventBus`, `IRandom`, `SimulationClock` (+`ISimulationClock`), `GameTickCoordinator` (+`IGameTickService`),
`MatchService` (+`IMatchService`, `ICombatantDirectory`, `ITeamRelations`), `ZoneService` (+`IZoneService`),
`CombatService`, `DamageableRegistry` (+`IDamageableRegistry`), `LootSpawnService` (+`ILootSpawnService`),
`MatchStatsService`, `KillFeedService`, `SquadOrderService`, `ArtilleryService`, `INetworkSession`,
`SettingsService`, `CareerStatsService`, `MatchConfig`.
Use `GameContext.Get<T>()` / `GameContext.TryGet<T>(out T)`; `GameContext.Network` (INetworkSession).
`GameLayers` (written): Default 0, Viewmodel 8, Player 9, Bot 10, Hitbox 11, Loot 12, Projectile 13, Vehicle 14.
Masks: `BulletMask` (Default|Hitbox|Vehicle), `LineOfSightMask` (Default|Vehicle), `GroundMask` (Default|Vehicle),
`InteractMask` (Loot|Vehicle). `GameLayers.ConfigureCollisionMatrix()` is called by bootstraps.

## 3. Infrastructure contracts

### 3.1 Rendering — `Infrastructure/Rendering` (owner infra-rendering)
```csharp
public enum MaterialId { /* see MaterialId.cs — fixed list, do not reorder */ }
public static class MaterialLibrary {
  Material Get(MaterialId id);
  Material Lit(Color color, float smoothness = 0.2f, float metallic = 0f);   // cached per color
  Material Unlit(Color color);
  Material Transparent(Color color, bool unlit);
  Material ParticleAdditive { get; }  Material ParticleAlpha { get; }
  MaterialSpec GetSpec(MaterialId id);  Material CreateFromSpec(MaterialSpec spec);
  void Use(GameArtLibrary library);     // editor-generated assets (Resources/GameArtLibrary)
}
public sealed class GameArtLibrary : ScriptableObject { const string ResourcePath = "GameArtLibrary"; Material[] materials; Material terrainMaterial; Material skybox; Texture2D softParticle; static GameArtLibrary Load(); }
public static class ProceduralTextures { Texture2D SoftCircle; Circle; Ring; Triangle; WhitePixel; TurkishFlag; DigitalCamo(Color a,Color b,Color c,Color d,int seed); Noise(int size,int seed); Sprite ToSprite(Texture2D t); }
public sealed class CameraRig : MonoBehaviour { Camera WorldCamera; Camera ViewmodelCamera; AudioListener Listener;
  static CameraRig Create(Transform parent, float fieldOfView, bool withViewmodel = true);
  void SetFieldOfView(float fov); float FieldOfView; void SetViewmodelVisible(bool visible); }
public static class PostProcessing { enum Look { Gameplay, Menu } ; GameObject EnsureGlobalVolume(Look look); void ApplyQuality(int level /*0..3*/); }
```
Runtime fallback must work without the editor-generated library (Shader.Find). URP shader names:
"Universal Render Pipeline/Lit", ".../Unlit", ".../Particles/Unlit", ".../Terrain/Lit", "Skybox/Procedural".

### 3.2 Audio — `Infrastructure/Audio` (owner infra-audio)
```csharp
public enum SoundId { /* SoundId.cs — fixed */ }
public static class GameAudio {
  bool IsInitialized; float MasterVolume {get;set;} float AmbientVolume {get;set;}
  void Initialize();  // idempotent, DontDestroyOnLoad pool, synthesizes all clips once
  AudioClip GetClip(SoundId id);
  void Play(SoundId id, Vector3 position, float volume = 1f, float pitch = 1f, float maxDistance = 80f);
  void Play2D(SoundId id, float volume = 1f, float pitch = 1f);
  void PlayGunshot(WeaponDefinitionData weapon, Vector3 position, bool isLocalShooter);
  AudioSource StartLoop(SoundId id, Transform follow, float volume = 1f, bool spatial = true, float maxDistance = 200f);
  void StopLoop(AudioSource source);
  void SetAmbience(SoundId loop, float volume);   // SoundId.None stops
}
```

### 3.3 VFX — `Infrastructure/Vfx` (owner infra-vfx)
```csharp
public enum SurfaceKind { Default, Dirt, Concrete, Metal, Wood, Flesh, Water, Foliage }
public static class GameVfx {
  void Initialize();
  void MuzzleFlash(Vector3 position, Vector3 direction, float scale = 1f);
  void Tracer(Vector3 from, Vector3 to, float duration = 0.05f, float width = 0.025f);
  void Impact(Vector3 point, Vector3 normal, SurfaceKind surface);   // includes bullet hole decal
  void Blood(Vector3 point, Vector3 normal);
  void Explosion(Vector3 position, float radius);
  void SmokeCloud(Vector3 position, float radius, float duration);
  void Dust(Vector3 position, float scale);                           // landing, rotor wash
  SurfaceKind Classify(Collider collider, Vector3 point);
}
```

### 3.4 Combat — `Infrastructure/Combat` + `Infrastructure/Zone` (owner infra-combat)
```csharp
public sealed class Combatant : MonoBehaviour, IDamageable, IHealable, IHealthReadModel, IArmored {
  PlayerId Id; PlayerId OwnerId; string DisplayName; bool IsLocalPlayer; bool IsBot; int Team; TeamRole Role;
  HealthService Health; InventoryService Inventory; BoostService Boost; ItemUseService ItemUse;
  HealthState State; bool IsAlive; IArmorProvider Armor;
  Transform EyePoint {get;set;} Transform AimPoint {get;set;} Vector3 Velocity {get;set;}
  bool IsTargetable {get;set;} DropState DropState {get;set;} Stance Stance {get;set;} bool DropLootOnDeath {get;set;}
  float LastDamageTime; Vector3 LastDamageSource; PlayerId LastAttackerId;
  event Action<Combatant, DamageInfo> Damaged; event Action<Combatant, DamageInfo> Died;
  void Initialize(PlayerId id, string displayName, bool isLocalPlayer, bool isBot, int team, TeamRole role,
                  IEventBus eventBus, IDamageableRegistry registry, float maxHealth = 100f);
  void ApplyDamage(DamageInfo damage);  void Heal(float amount);
  Vector3 GetAimPosition(BodyPart part);
  bool IsAllyOf(Combatant other);
}
public static class CombatantRegistry { IReadOnlyList<Combatant> All; Combatant LocalPlayer; void Register(Combatant); void Unregister(Combatant);
  bool TryGet(PlayerId id, out Combatant c); int AliveCount; void Clear(); void GetTeam(int team, List<Combatant> output); }
public sealed class Hitbox : MonoBehaviour { Combatant Owner; BodyPart Part; void Bind(Combatant owner, BodyPart part);
  static Hitbox CreateBox(Transform parent, Combatant owner, BodyPart part, Vector3 localCenter, Vector3 size);
  static Hitbox CreateSphere(Transform parent, Combatant owner, BodyPart part, Vector3 localCenter, float radius);
  static Hitbox CreateCapsule(Transform parent, Combatant owner, BodyPart part, Vector3 localCenter, float radius, float height, int direction); }
public sealed class BallisticsSystem : MonoBehaviour { static BallisticsSystem Instance; static BallisticsSystem Create(CombatService combat, IEventBus eventBus);
  int ActiveProjectiles;
  void FireWeapon(Combatant shooter, IWeaponRuntime weapon, Vector3 origin, Vector3 aimDirection, float spreadDegrees, Vector3 visualMuzzle);
  void SpawnProjectile(PlayerId shooterId, WeaponDefinitionData weapon, Vector3 origin, Vector3 direction, Vector3 visualMuzzle, bool tracer);
  static Vector3 ApplySpread(Vector3 direction, float spreadDegrees); }
public static class ExplosionSystem { void Explode(Vector3 position, float radius, float maxDamage, PlayerId attackerId, string sourceId); }
public enum ThrowableKind { Frag, Smoke }
public sealed class ThrowableProjectile : MonoBehaviour { static ThrowableProjectile Throw(ThrowableKind kind, Vector3 position, Vector3 velocity, PlayerId thrower); }
public static class SmokeVolume { void Spawn(Vector3 center, float radius, float duration); bool BlocksLineOfSight(Vector3 from, Vector3 to); void Clear(); }
public static class MeleeAttack { bool TryPunch(Combatant attacker, Vector3 origin, Vector3 direction, float range = 2.2f); }
public sealed class ArtilleryExecutor : MonoBehaviour { static ArtilleryExecutor Create(ArtilleryService artillery); } // polls DueImpacts → incoming whistle + ExplosionSystem.Explode
// Infrastructure/Zone/ZoneDamageController.cs (namespace Project.Infrastructure.Zone):
public sealed class ZoneDamageController : MonoBehaviour { static ZoneDamageController Create(IZoneService zone, CombatService combat, IMatchService match); }
```
Bullets: hits on `Hitbox` → `CombatService.ApplyBulletHit`; world → `GameVfx.Impact`; skip shooter's own hitboxes
and (unless friendly fire) allies' hitboxes. `FireWeapon` publishes `WeaponFiredEvent` (with origin), plays
`GameAudio.PlayGunshot`, world `GameVfx.MuzzleFlash` for non-local shooters, tracers. Combatant death: unregister,
`Died` event, `Inventory.DropAll` → `LootSpawner.DropAround` when `DropLootOnDeath`.

### 3.5 Player motor & camera — `Infrastructure/Player`, `Infrastructure/Config` (owner infra-player)
```csharp
public sealed class CharacterControllerMotor : MonoBehaviour, IPlayerMotor {
  void Configure(PlayerMovementConfig config); PlayerMovementConfig Config; CharacterController Controller;
  /* IPlayerMotor */ ; float CurrentHeight; float EyeHeight; Vector3 Velocity; bool ControlEnabled {get;set;}
  event Action<float> Landed;   // impact speed m/s
  void Teleport(Vector3 position, float yawDegrees); void MoveRaw(Vector3 displacement); }
public sealed class FirstPersonCameraController : MonoBehaviour, IFirstPersonCamera {
  void Configure(Transform pitchPivot, PlayerMovementConfig config, CameraRig rig);
  float BaseFieldOfView {get;set;} bool InvertY {get;set;} Camera Camera;
  void SetZoom(float zoomFactor); void SetLean(float lean); void SetEyeHeight(float height); void SetBob(float speed01, bool grounded);
  Vector3 AimOrigin; Vector3 AimForward; }
```

### 3.6 Weapon visuals — `Infrastructure/Weapons` (owner infra-weapon-visuals)
```csharp
public static class WeaponModelFactory { GameObject Build(WeaponDefinitionData weapon, Transform parent, int layer, bool forViewmodel, out Transform muzzle); }
public sealed class WeaponViewModel : MonoBehaviour { static WeaponViewModel Create(Transform cameraTransform, int layer);
  WeaponDefinitionData Current; bool IsEquipping; float AimBlend; Transform Muzzle; Vector3 MuzzleWorldPosition;
  void Equip(WeaponDefinitionData weapon /*null = fists*/); void SetAim(bool aiming); void OnFire();
  void PlayReload(float durationSeconds); void StopReload(); void PlayMelee(); void PlayThrow();
  void PlayUse(float durationSeconds); void StopUse(); void SetMotion(float speed01, bool sprinting, bool grounded, float lookYawDelta, float lookPitchDelta);
  void SetHidden(bool hidden); }
```

### 3.7 Characters — `Infrastructure/Characters` (+ `Player/DamageableTarget.cs`) (owner infra-characters)
```csharp
public sealed class SoldierLook { Color CamoA, CamoB, CamoC, CamoD; Color Skin; Color Gear; Color Armband; bool Beret;
  static SoldierLook ForTeam(int team, System.Random rng); static SoldierLook Default; }
public sealed class SoldierModel : MonoBehaviour { static SoldierModel Build(Transform parent, SoldierLook look, Combatant owner, bool createHitboxes, int visualLayer);
  Transform Head, Chest, WeaponSocket; void SetLocomotion(Vector3 worldVelocity, Stance stance, bool grounded);
  void SetAimPitch(float pitchDegrees); void HoldWeapon(WeaponDefinitionData weapon); void SetEquipment(int helmetLevel, int vestLevel, int backpackLevel);
  void SetSeated(bool seated); void PlayFire(); void PlayDeath(Vector3 hitDirection); void SetVisible(bool visible); }
public sealed class DamageableTarget : MonoBehaviour { static DamageableTarget CreateDummy(Vector3 position, IEventBus eventBus, IDamageableRegistry registry, int id, float health = 100f);
  static DamageableTarget CreateMoving(Vector3 a, Vector3 b, float speed, IEventBus eventBus, IDamageableRegistry registry, int id);
  Combatant Combatant; float RespawnSeconds; }
```

### 3.8 AI — `Infrastructure/AI` (owner infra-ai)
```csharp
public sealed class BotSpawnArgs { PlayerId Id; string Name; int Team; TeamRole Role; BotDifficulty Difficulty; TransportVehicle Transport; int Seat;
  bool SpawnOnGround; Vector3 GroundPosition; Combatant SquadLeader; int Seed; }
public sealed class BotController : MonoBehaviour { static BotController Create(BotSpawnArgs args); Combatant Combatant; BotState State; bool HasLanded;
  void SetSquadLeader(Combatant leader); }
```
Uses NavMeshAgent, `BotDecisionService`, `BotDifficultyProfile`, `SquadOrderService`, `ITeamRelations`, `SoldierModel`,
`BallisticsSystem`, `LootRegistry`. Squad: followers keep a wedge formation around the leader, obey orders,
engage enemies only (never allies), team leaders of AI teams pick objectives and may call artillery.

### 3.9 Transport — `Infrastructure/Transport` (owner infra-transport)
```csharp
public abstract class TransportVehicle : MonoBehaviour { InsertionMethod Method; int Team; int SeatCount;
  bool HasArrived; bool IsUnloading; bool IsDeparting; Vector3 LandingZone;
  Transform GetSeat(int index); Transform PassengerViewPoint(int index);
  Vector3 GetDisembarkPoint(int index); event Action Arrived; event Action Departed;
  void Begin();          // starts travelling
  void ReleasePassengers(); }
public sealed class Helicopter : TransportVehicle { static Helicopter Create(TeamInsertion plan, float altitude); }      // T-70
public sealed class ArmoredCarrier : TransportVehicle { static ArmoredCarrier Create(TeamInsertion plan); }            // Kirpi
public static class TransportFactory { TransportVehicle Create(TeamInsertion plan); }
```

### 3.10 Drivable vehicle — `Infrastructure/Vehicles` (owner infra-vehicle-drive)
```csharp
public sealed class DrivableVehicle : MonoBehaviour { static DrivableVehicle Spawn(Vector3 position, float yaw);   // Kirpi
  bool HasDriver; Combatant Driver; bool TryEnter(Combatant c); void Exit(); void SetInput(float throttle, float steer, bool brake);
  Transform DriverViewPoint; float SpeedKmh; float Health; }
public static class VehicleRegistry { IReadOnlyList<DrivableVehicle> All; DrivableVehicle FindNearest(Vector3 pos, float radius); }
```

### 3.11 World — `Infrastructure/World` (owners infra-world-terrain / infra-world-structures)
```csharp
// terrain owner
public sealed class MapLayout { float HalfSize; float MaxHeight; float WaterLevel; List<LocationSpec> Locations; List<RoadSpec> Roads;
  List<LakeSpec> Lakes; List<RiverSpec> Rivers; static MapLayout CreateKuzgunVadisi(int seed); }   // types in MapLayout.cs
public sealed class WorldMetadata : MonoBehaviour { static WorldMetadata Instance; float MapHalfSize; float WaterLevel; Terrain Terrain;
  Texture2D MinimapTexture; List<LootSpawnPointData> LootPoints; List<NamedLocation> Locations; List<Vector3> GroundSpawnPoints;
  List<VehicleSpawnData> VehicleSpawns; NavMeshData NavMesh;
  float SampleGroundHeight(Vector3 position); bool TryGetGroundPoint(Vector3 position, out Vector3 point);
  Vector2 WorldToMapUV(Vector3 world); string GetLocationName(Vector3 position); }
public sealed class WorldGenerationOptions { int Seed; TerrainData TerrainData; Transform Parent; bool BakeNavMesh; bool GenerateMinimap; int MinimapSize; }
public static class WorldGenerator { WorldMetadata Generate(WorldGenerationOptions options); }
public static class TerrainGenerator { Terrain Create(MapLayout layout, TerrainData data, Transform parent, int seed); }
public static class MinimapTextureGenerator { Texture2D Generate(Terrain terrain, MapLayout layout, IReadOnlyList<Bounds> structures, int size); }
public static class NavMeshBaker { NavMeshData Bake(Bounds bounds, Terrain terrain, int layerMask); void EnsureLoaded(NavMeshData data); }
public static class MeshFactory { Mesh Cone(...); Mesh Cylinder(...); Mesh Hemisphere(...); Mesh ZoneWall(int segments); Mesh Wedge(...); Mesh PineTree(...); Mesh OakTree(...); Mesh Rock(int seed); }
// structures owner
public enum BuildingStyle { VillageHouse, TwoStoryHouse, Mosque, Shop, Barracks, Karakol, WatchTower, Hangar, Warehouse, FactoryHall, Barn, Bunker, RadarStation, Shed, DamControl, ShepherdHut }
public sealed class BuildingSpec { string Name; BuildingStyle Style; Vector3 Position; float Yaw; float Width; float Depth; int Floors; float FloorHeight; bool Ruined; LootTier Tier; int Seed; }
public sealed class BuildingResult { GameObject Root; Bounds Bounds; List<Vector3> LootPoints; }
public static class BuildingGenerator { BuildingResult Build(BuildingSpec spec, Transform parent); }
public static class PropFactory { /* Sandbags, Hesco, Container, AmmoCrate, Barrel, Wreck, Tent, Fence, HayBale, Rock, Hedgehog, FlagPole(Turkish flag), Helipad, Antenna, CamoNet ... (Transform parent, Vector3 pos, float yaw, System.Random rng) => GameObject */ }
public static class LocationBuilder { void BuildAll(MapLayout layout, Terrain terrain, Transform parent, int seed, List<LootSpawnPointData> lootOut, List<Bounds> structuresOut, List<VehicleSpawnData> vehiclesOut); }
public static class TrainingRangeBuilder { WorldMetadata Build(Transform parent); }
```
All world geometry on layer Default, marked static by the editor, colliders on everything solid, doorways ≥ 1.3 m wide,
stairs as ramps ≤ 35°. Buildings provide loot points per floor.

### 3.12 Loot — `Infrastructure/Loot` (owner infra-loot)
```csharp
public sealed class LootPickupComponent : MonoBehaviour, ILootPickup { static LootPickupComponent Spawn(LootItemData item, Vector3 position, float yaw = 0f);
  LootItemData Item; bool IsAvailable; string PromptText; PickupResult PickupBy(Combatant combatant); /* + ILootPickup */ }
public static class LootRegistry { IReadOnlyList<LootPickupComponent> All; void Register(..); void Unregister(..);
  LootPickupComponent FindNearest(Vector3 position, float radius, Func<LootPickupComponent, bool> filter = null);
  LootPickupComponent FindLookTarget(Vector3 origin, Vector3 direction, float maxDistance); void Clear(); }
public static class LootSpawner { int SpawnWorldLoot(IReadOnlyList<LootSpawnPointData> points, ILootSpawnService service, IRandom random);
  void DropAround(Vector3 center, IReadOnlyList<LootItemData> items); LootPickupComponent SpawnDropped(LootItemData item, Vector3 near); }
```

## 4. Presentation contracts
```csharp
// Bootstrap (owner pres-bootstrap)
public static class SceneNames { const string MainMenu = "MainMenu", Operation = "KuzgunVadisi", Training = "TrainingRange"; string PathOf(string scene); }
public static class GameSession { GameMode Mode; MatchConfig Config; MatchResult? LastResult; SettingsService Settings; CareerStatsService Career;
  void EnsureInitialized(); MatchConfig CreateMatchConfig(); void LoadScene(string sceneName); }
public static class GameCompositionRoot { ServiceContainer Build(MatchConfig config, SettingsService settings); }   // Infrastructure/DI (owned by pres-bootstrap)
public sealed class MatchBootstrap : MonoBehaviour {}      // in KuzgunVadisi scene
public sealed class TrainingBootstrap : MonoBehaviour {}   // in TrainingRange scene
public sealed class MainMenuBootstrap : MonoBehaviour {}   // in MainMenu scene
// Player (owner pres-player)
public interface IPlayerHudSource { Combatant Combatant; InventoryService Inventory; WeaponRuntimeService ActiveWeapon; bool IsAiming; bool IsScoped;
  float ScopeZoom; float SpreadAngle; string InteractionPrompt; DropState DropState; ItemUseService ItemUse; float Yaw; Vector3 Position;
  bool IsDead; FirstPersonCameraController CameraController; SquadOrder CurrentOrder; float ArtilleryCooldown; bool IsInVehicle; }
public sealed class PlayerSpawnArgs { PlayerId Id; string Name; int Team; TeamRole Role; TransportVehicle Transport; int Seat; Vector3 GroundPosition; float GroundYaw; GameSettings Settings; }
public sealed class PlayerController : MonoBehaviour, IPlayerHudSource { static PlayerController Create(PlayerSpawnArgs args);
  bool InputEnabled {get;set;} void ApplySettings(GameSettings settings); event Action Died; UnityInputReader Input; }
// UI kit (owner pres-hud): UiTheme, UiSprites, UiFactory (CreateCanvas, Panel, Label, Button, Slider, Toggle, Image, RawImage, Stretch/Anchor helpers, EnsureEventSystem)
public sealed class HudController : MonoBehaviour { static HudController Create(IPlayerHudSource player); void SetVisible(bool visible); void ShowCenterMessage(string text, float seconds); RectTransform Root; }
// Map & inventory (owner pres-map-inventory)
public sealed class MinimapView : MonoBehaviour { static MinimapView Create(RectTransform hudRoot, IPlayerHudSource player); }
public sealed class FullMapView : MonoBehaviour { static FullMapView Create(Transform canvasRoot, IPlayerHudSource player); bool IsOpen; void Toggle(); event Action<Vector3> PointMarked; }
public sealed class InventoryView : MonoBehaviour { static InventoryView Create(Transform canvasRoot, IPlayerHudSource player); bool IsOpen; void Toggle(); }
// Menus (owner pres-menus)
public sealed class MainMenuController : MonoBehaviour {}
public sealed class SettingsPanel : MonoBehaviour { static SettingsPanel Create(Transform parent, SettingsService settings, Action onClose); }
public sealed class PauseMenu : MonoBehaviour { static PauseMenu Create(Action onResume, Action onMainMenu, SettingsService settings); bool IsOpen; void Open(); void Close(); }
public sealed class EndScreen : MonoBehaviour { static EndScreen Show(MatchResult result, Action onRestart, Action onMainMenu); }
public static class LoadingScreen { void Show(string message); void Hide(); }
```
Controls (UnityInputReader): WASD, mouse, Space, Shift, Ctrl/C crouch, Z prone, Q/E lean, LMB fire, RMB aim, R reload,
F interact/enter vehicle/disembark, 1-4 weapons, wheel, B fire mode, H heal, J boost, G frag, T smoke, X holster,
Tab inventory, M map, Esc pause. Squad orders (pres-player reads keyboard directly via Input System):
F1 Follow, F2 Hold, F3 Attack (aim point), F4 Regroup; V = artillery (Radioman/Leader) at aim point or map marker.
