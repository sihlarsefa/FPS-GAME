export const meta = {
  name: 'harekat-reviews-lean',
  description: 'HAREKÂT: çatışma, arayüz ve dünya için runtime incelemesi (3 Sonnet ajanı)',
  phases: [{ title: 'İnceleme', detail: '3 paralel ajan' }],
}
const ROOT = '/Users/f2gomac/Desktop/FPS-GAME'
const V = (n) => `zsh ${ROOT}/Tools/UnityVerify/verify.sh ${ROOT}/Tools/UnityVerify/out_${n} --player --tests`
const RULES = `Project: HAREKÂT, Unity 6000.6 C# at ${ROOT} (editor can't run here; verify with ${V('<name>')} — keep 0 errors and tests passing).
TOKEN-EFFICIENT: open only the files in your scope (grep to locate). Minimal targeted fixes of REAL runtime bugs (null refs, lifecycle order —
AddComponent runs Awake immediately, missing init, unsubscribed events, wrong math, per-frame GC in hot paths). No rewrites, no deletes, no .meta,
no file-changing git. Inside Project.* namespaces write UnityEngine.Application. Other agents are concurrently editing: MatchBootstrap.cs,
TrainingBootstrap.cs, GameSession.cs, MainMenuController.cs, EndScreen.cs, PlayerController.cs, BotController*.cs, Transport/*, WeaponModelFactory.cs,
GameAudio.cs, MaterialLibrary.cs, SoldierModel.cs, GameCompositionRoot.cs — DO NOT edit those; report issues there in concerns as "file:line — problem — fix".`
const SCHEMA = { type: 'object', properties: { summary: { type: 'string' }, fixes: { type: 'array', items: { type: 'string' } }, concerns: { type: 'array', items: { type: 'string' } } }, required: ['summary', 'fixes', 'concerns'] }
const AREAS = [
  ['combat', `Scope: Infrastructure/Combat/*, Infrastructure/Zone/*, Presentation/Player/PlayerWeaponHandler.cs, PlayerInteraction.cs, Application/Services/CombatService.cs, WeaponRuntimeService.cs.
Trace: fire → TryTrigger → BallisticsSystem.FireWeapon (spread, pellets, own/ally hitbox skip) → Hitbox → CombatService → HealthService → events (hitmarker, killfeed, stats);
grenades/smoke, ArtilleryExecutor, ZoneDamageController, death loot drop, recoil/ADS/scope.`],
  ['ui', `Scope: Presentation/UI/* EXCEPT MainMenuController.cs and EndScreen.cs. Check HudController & views, MinimapView/FullMapView, InventoryView, PauseMenu, SettingsPanel,
GameplayUiController: single EventSystem (InputSystemUIInputModule), null-safety before GameContext ready, subscribe/unsubscribe pairs, cursor lock transitions (map/inventory/pause),
Time.timeScale restore, per-frame string allocations (cache/throttle), Turkish text.`],
  ['world', `Scope: Infrastructure/World/* (BuildingGenerator, StructureKit, LocationBuilder, PropFactory, TrainingRangeBuilder, TerrainGenerator, WorldGenerator, NavMeshBaker, WorldMetadata),
Editor/SceneBuilder.cs, Editor/AssetGeneration.cs. Check walkable doors/ramps, colliders, WorldMetadata fields serializable for saved scenes, procedural assets persisted by the editor
before scene save (meshes/textures/materials/TerrainLayers/NavMeshData/minimap), NavMeshBuilder API usage, runtime fallback, object counts.`],
]
phase('İnceleme')
const out = await parallel(AREAS.map(([k, scope]) => () => agent(`${RULES}\nAREA: ${k}. ${scope}`, { label: `inceleme:${k}`, phase: 'İnceleme', schema: SCHEMA, model: 'sonnet', effort: 'medium' })))
return out.filter(Boolean)
