export const meta = {
  name: 'harekat-features-2',
  description: 'HAREKÂT dalga 2: başarım bağlantıları, cila (Esc/harita kaydı/dil yenileme), eğitim v2, performans (4 Sonnet ajanı)',
  phases: [{ title: 'Dalga 2', detail: '4 paralel ajan' }],
}
const ROOT = '/Users/f2gomac/Desktop/FPS-GAME'
const V = (n) => `zsh ${ROOT}/Tools/UnityVerify/verify.sh ${ROOT}/Tools/UnityVerify/out_${n} --player --tests`
const RULES = `Project HAREKÂT, Unity 6000.6 C# at ${ROOT}. Editor can't run; verify with ${V('<name>')} — keep 0 errors, all tests passing (currently 426).
Read ${ROOT}/ARCHITECTURE.md only if needed. TOKEN-EFFICIENT: open only needed files (grep). Prefer new files; small null-safe edits elsewhere.
No deletes/.meta/file-changing git. UnityEngine.Application inside Project.* namespaces. Turkish UI. Add EditMode tests for pure logic.
Stay strictly inside your scope; other agents run in parallel. Append a dated line to ${ROOT}/Docs/DURUM.md "Günlük" when done.`
const SCHEMA = { type: 'object', properties: { summary: { type: 'string' }, files: { type: 'array', items: { type: 'string' } }, concerns: { type: 'array', items: { type: 'string' } } }, required: ['summary', 'files', 'concerns'] }
const F = [
  ['basarim-baglanti', `Wire the remaining achievement metric keys. Read Assets/_Project/Resources/Progression/achievements.json (metric keys) and Application/Services/AchievementService.cs (AddProgress/SetMax).
Create Presentation/Bootstrap/AchievementTracker.cs (or Infrastructure if it needs only events) that subscribes to IEventBus events (SquadOrderIssuedEvent, ItemUsedEvent, ArtilleryStrikeEvent, PlayerDiedEvent with weapon/headshot/distance,
HitConfirmedEvent, CommandTransferredEvent, LootPickedUpEvent, ExplosionEvent, MatchEndedEvent...) for the LOCAL player and feeds metrics; add new tiny events only if a metric is impossible otherwise.
Instantiate it from MatchBootstrap/TrainingBootstrap (one-line edits). Scope: new file(s) + those one-liners + AchievementService if a helper is missing.`],
  ['cila', `Polish: (1) Esc during KeyBindingsPanel capture must not open/close pause (use KeyBindingsPanel.IsCapturing in GameplayUiController.HandleEscape / PauseMenu).
(2) Persist selected map in GameSettings (Core model + SettingsService key), default "kuzgun"; GameSession.SelectedMap reads it; main menu map selector writes it.
(3) Loc.LanguageChanged → MainMenuController, PauseMenu, EndScreen, HudStatusView refresh their texts (re-apply labels; rebuild if simplest).
(4) Online/ServerBootstrap scene choice: expose a static GameSession.ResolveSceneForMap(string mapId) and use it in Scripts/Online/Netcode/ServerBootstrap.cs (accept -map arg) and wherever SceneNames.Operation is hard-coded for match start.
Scope: GameplayUiController, PauseMenu, KeyBindingsPanel (read-only), GameSettings, SettingsService, GameSession, MainMenuController, EndScreen, HudStatusView, Loc, ServerBootstrap.`],
  ['egitim-v2', `Tutorial v2: Presentation/Tutorial/*, Application/Services/TutorialService.cs, TrainingBootstrap.cs, Infrastructure/World/TrainingRangeBuilder.cs (waypoint props only).
(1) Add visible waypoint markers (glowing pole + ground ring) in the range for PlayerReachedWaypoint steps and complete when the player enters radius.
(2) Read real player state via PlayerController/IPlayerHudSource (stance, lean, IsAiming/IsScoped, fire mode, inventory open, map open, in vehicle) instead of guessing from keys; signal scope/smoke/map-marker/Kirpi steps properly.
(3) Award XP on completion through CareerStatsService (add a method AddExperience(int) if missing) and show a completion screen with totals; implement JSON filters ignored so far (IsKill, MinRadius, IsImpact, MinSeconds) using a richer DTO.`],
  ['performans', `Performance pass, scope: Infrastructure/Rendering/*, Editor/SceneBuilder.cs + Editor/AssetGeneration.cs, Infrastructure/World/TreeScatter.cs, RockScatter.cs, BuildingGenerator.cs (only LOD/static/collider tweaks), Infrastructure/Vfx/*.
Targets for 60 FPS on mid Windows PCs with 40–60 combatants: camera layerCullDistances (small props/loot/bots far cull), terrain treeDistance/detail settings, LODGroups (or culling) for buildings/props/rocks,
static batching flags set in SceneBuilder (StaticEditorFlags incl. BatchingStatic, OccluderStatic/OccludeeStatic for big structures) and an optional Occlusion bake step, shadow cascades/distances per quality tier,
SRP Batcher-friendly shared materials (no per-object material instances in hot paths — check MaterialLibrary.Lit(color) usage), VFX pool caps. Also add a "Performans" quality table to Docs/PERFORMANS.md (new).
Do NOT touch Infrastructure/AI/** (bot update LOD is a separate task — list your recommendations in concerns).`],
]
phase('Dalga 2')
const out = await parallel(F.map(([k, task]) => () => agent(`${RULES}\nTASK ${k}: ${task}`, { label: k, phase: 'Dalga 2', schema: SCHEMA, model: 'sonnet', effort: 'medium' })))
return out.filter(Boolean)
