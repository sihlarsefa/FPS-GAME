export const meta = {
  name: 'harekat-gaps',
  description: 'HAREKÂT: inceleme boşlukları — UI kalanı, binalar+sahne varlık kaydı, PlayerController notları (3 Sonnet ajanı)',
  phases: [{ title: 'Boşluklar', detail: '3 paralel ajan' }],
}
const ROOT = '/Users/f2gomac/Desktop/FPS-GAME'
const V = (n) => `zsh ${ROOT}/Tools/UnityVerify/verify.sh ${ROOT}/Tools/UnityVerify/out_${n} --player --tests`
const RULES = `Project HAREKÂT, Unity 6000.6 C# at ${ROOT} (editor can't run; verify with ${V('<name>')} — keep 0 errors, tests passing).
TOKEN-EFFICIENT: open only files in scope. Minimal targeted fixes of real runtime bugs; no rewrites/deletes/.meta/file-changing git. Write UnityEngine.Application inside Project.* namespaces.
Concurrent agents edit Infrastructure/AI/*, Scripts/Online/**, Scripts/Platform/** — never edit those.`
const SCHEMA = { type: 'object', properties: { summary: { type: 'string' }, fixes: { type: 'array', items: { type: 'string' } }, concerns: { type: 'array', items: { type: 'string' } } }, required: ['summary', 'fixes', 'concerns'] }
phase('Boşluklar')
const out = await parallel([
  () => agent(`${RULES}
SCOPE ui-rest: Presentation/UI/SettingsPanel.cs, MinimapView.cs, FullMapView.cs (Update paths), GameplayUiController.cs, KillFeedView.cs, CompassView.cs, SquadPanelView.cs, AllyMarkersView.cs,
CrosshairView.cs, NotificationView.cs, DamageIndicatorView.cs. Find per-frame string allocations (string.Format/interpolation/ToString in Update/LateUpdate) and replace with cached/throttled
updates (UiWidgets.Number/Clock helpers exist), null-safety when GameContext/player missing, Escape key handling with nested panels (settings inside pause), settings applying immediately.`,
    { label: 'ui-kalan', phase: 'Boşluklar', schema: SCHEMA, model: 'sonnet', effort: 'medium' }),
  () => agent(`${RULES}
SCOPE world-structures + scene persistence: Infrastructure/World/BuildingGenerator.cs, StructureKit.cs, PropFactory.cs, LocationBuilder.cs, Editor/SceneBuilder.cs.
(1) Audit doors (≥1.3 m wide, ≥2.2 m high, no collider blocking), stairs/ramps (≤35°, continuous collider, reach upper floors), floors present under loot points, no huge collider counts.
(2) In SceneBuilder.EnsureKuzgunVadisi and EnsureTrainingRange: route WorldGenerator.CollectGeneratedObjects (and equivalent for the training range) to save every runtime Mesh/Material/Texture/TerrainLayer
as assets under Assets/_Project/Generated/<Scene>/ before EditorSceneManager.SaveScene, so the saved scene references assets instead of embedding/losing them; dedupe shared meshes/materials by instance.
Keep it idempotent (overwrite on rebuild).`, { label: 'dünya-kayıt', phase: 'Boşluklar', schema: SCHEMA, model: 'sonnet', effort: 'medium' }),
  () => agent(`${RULES}
SCOPE player: Presentation/Player/PlayerController.cs (and PlayerWeaponHandler.cs only if needed). Reviewer notes to resolve:
(a) PlayerWeaponHandler subscribes to ItemUseService events (ctor + HookItemUse) — ensure PlayerController calls its Dispose() on destroy/death/respawn;
(b) on camera rebuild call PlayerWeaponHandler.ResetState so zoom resets; (c) verify OnDestroy unsubscribes everything and PlayerController.Local is cleared.
Also sanity-check fall damage uses DamageCalculator.ComputeFallDamage (use it if a custom formula exists).`, { label: 'oyuncu-notları', phase: 'Boşluklar', schema: SCHEMA, model: 'sonnet', effort: 'medium' }),
])
return out.filter(Boolean)
