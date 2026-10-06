export const meta = {
  name: 'harekat-realism-g3',
  description: 'HAREKÂT gerçekçilik dalgası G3: GERCEKCILIK_PLANI.md §4.2 C1–C15 (15 Sonnet ajanı, C7 arayüzü önce)',
  phases: [{ title: 'G3 bağımsız', detail: 'C1 C2 C3 C8 C11 C13 C15' }, { title: 'G3 C7', detail: 'ContentOverrides v2 arayüzü' }, { title: 'G3 bağımlı', detail: 'C4 C5 C6 C9 C10 C12 C14' }],
}
const ROOT = '/Users/f2gomac/Desktop/FPS-GAME'
const V = (n) => `zsh ${ROOT}/Tools/UnityVerify/verify.sh ${ROOT}/Tools/UnityVerify/out_g3_${n} --player --tests`
const RULES = (id) => `Project HAREKÂT (Turkish-military FPP squad BR), Unity 6000.6 URP 17.6 C# at ${ROOT}. Editor can't run here; verify with ${V(id)} — keep 0 compile errors in your files, tests passing.
FIRST read ONLY your row "${id}" in ${ROOT}/Docs/GERCEKCILIK_PLANI.md §4.2 (grep -n "${id} " then sed that line; §1.3 perf targets and §3.1 slots if relevant). That row is your spec: files, work, acceptance criteria. Implement it fully.
TOKEN-EFFICIENT: open only needed files (grep first). Stay strictly in your row's files; tiny null-safe hook edits elsewhere only if unavoidable. 14 other agents edit the other rows' files in parallel.
Golden rule: if no override/asset is present, the current procedural fallback must keep working unchanged. Things not available in this URP version → use reflection + warning log, never crash (stubs in Tools/UnityVerify may need small additions for new URP API you reference — prefer reflection instead).
No deletes, no .meta edits, no file-changing git. Write UnityEngine.Application inside Project.* namespaces. Turkish UI strings. Add EditMode tests for pure logic (tests needing Infrastructure/Presentation wrapped in #if UNITY_EDITOR; NUnit shim: no Assert.That/CollectionAssert).
Mark anything needing real Unity as "Cursor doğrulaması" in your concerns. Append one dated Turkish line to ${ROOT}/Docs/DURUM.md "Günlük" when done.`
const SCHEMA = { type: 'object', properties: { summary: { type: 'string' }, files: { type: 'array', items: { type: 'string' } }, concerns: { type: 'array', items: { type: 'string' } } }, required: ['summary', 'files', 'concerns'] }
const run = (id, extra, phaseName) => agent(`${RULES(id)}\nTASK ${id}. ${extra}`, { label: id, phase: phaseName, schema: SCHEMA, model: 'sonnet', effort: 'medium' })

const INDEP = [
  ['C1', 'Pipeline: Forward+, GPU Resident Drawer, GPU occlusion, STP + render scale tiers, APV, shadows, SRP batcher — single tier table in code.'],
  ['C2', 'Post 2.0 grading presets with 0.5 s blend, SSAO per tier, DoF curves; motion blur default off. SettingsPanel toggles only if missing (small edit).'],
  ['C3', 'Atmosphere: height fog + aerial perspective, Mie sun halo, HDRI SkyOverride hook (declare a local lookup via ContentOverrides only if the slot already exists; otherwise read through a small static hook that C7 will fill — e.g. Atmosphere.SkyOverrideProvider Func).'],
  ['C8', 'Viewmodel feel: spring-damper sway/bob/ADS, separate camera vs model recoil springs, viewmodel FOV, wall-proximity lowering, Grip_L IK hook. Frame-rate independent; put the math in a pure testable class.'],
  ['C11', 'Audio layers: append SoundIds at END (order preserved), distance crossfade, raycast occlusion (budgeted), indoor/outdoor tail + reverb, voice cap 48 with priority.'],
  ['C13', 'Measurement: F3 perf HUD (frame/CPU/GPU ms, draw calls if available) + -benchmark CLI mode flying a fixed camera path over Kuzgun Köyü, CSV (avg, 1% low, worst) to persistentDataPath/Benchmarks.'],
  ['C15', 'Name profile: DisplayName → localization key; Gercek/Kurgusal profiles (single setting) for weapons + vehicles; fictional names original. Keep network indices/ids unchanged.'],
]
const C7 = 'ContentOverrides v2: add the 8 new slots of §3.1 (TerrainLayerOverride, VegetationOverride, RockOverride, PropOverride/BuildingOverride, Arms, WeaponAnimationOverride, SkyOverride, DecalSetOverride) as serializable entries + lookup API; name-convention auto-fill (HK_W_*, HK_V_*); Editor validator menu (sockets, scale, pivot, LODGroup, tri budget, URP/Lit shader, license in Assets/ThirdParty/README.md) with OK/UYARI/HATA report. Backward-compatible serialization. Do this FAST: commit the public types/lookup API first (other agents wait on it), then the validator. If Atmosphere has a SkyOverrideProvider hook (C3), wire it.'
const DEP = [
  ['C4', 'Material system: detail normal/albedo, triplanar flag, macro variation, texel density in MaterialSpec; MaterialOverride priority; ORM packing consistency. Use C7 types (ContentOverrides) already present.'],
  ['C5', 'Terrain: TerrainLayerOverride (6–8 layers via C7 API), slope/height/road-mask painting, macro variation, basemap distance + detail density/distance per tier.'],
  ['C6', 'Vegetation & rocks: VegetationOverride/RockOverride per-species prefab selection (C7 API), auto LOD warning, billboard distance per tier, wind params, GRD-friendly placement. Note: VegetationTextures/TreeMeshes/LODGroup procedural trees were just added — build on them.'],
  ['C9', 'Soldier: humanoid override path LOD rules, Animator culling + distance-based update rate, camo MaterialId swap, role accessory sockets, ragdoll compatibility.'],
  ['C10', 'VFX: muzzle smoke, per-SurfaceKind impact sets, decal FIFO limits per tier, DecalSetOverride (C7 API), rotor dust only <15 m above ground (GameVfx.RotorWash exists).'],
  ['C12', 'Vehicle sockets: override prefab contract Rotor_Main/Rotor_Tail/Wheel_*/Door_*/Seat_*/Light_*; rotor blur disc swap; clean/dirty/burnt material states. Do not change colliders/seats of procedural models; do not edit FlyableHelicopter physics.'],
  ['C14', 'Credits: license record schema, Resources/Credits.json, CC-BY auto credits lines, pre-ship audit (catch CC-BY-NC / Editorial), credits screen from main menu (MainMenuController.ExtraButtons). Hook the C7 validator check if its API exists.'],
]

const out = await parallel([
  ...INDEP.map(([id, t]) => () => run(id, t, 'G3 bağımsız')),
  () => run('C7', C7, 'G3 C7').then(r => parallel(DEP.map(([id, t]) => () => run(id, t, 'G3 bağımlı'))).then(d => [r, ...d])),
])
return out.flat().filter(Boolean)
