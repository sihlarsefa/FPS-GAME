export const meta = {
  name: 'harekat-hooks-flow',
  description: 'HAREKÂT: kancaları bağla + oyun akışı runtime incelemesi (2 Sonnet ajanı, tutumlu)',
  phases: [{ title: 'Kanca ve Akış', detail: '2 paralel ajan' }],
}
const ROOT = '/Users/f2gomac/Desktop/FPS-GAME'
const V = (n) => `zsh ${ROOT}/Tools/UnityVerify/verify.sh ${ROOT}/Tools/UnityVerify/out_${n} --player --tests`
const RULES = `Project: HAREKÂT, Unity 6000.6 C# at ${ROOT} (Unity editor can't run here; verify with ${V('<name>')} — must stay 0 errors, 399+ tests passing).
Be TOKEN-EFFICIENT: do not read the whole project; open only the files named below (use grep to locate symbols). Never delete files,
never touch .meta, never run file-changing git commands. Never edit Assets/_Project/Scripts/Online/** or Scripts/Platform/** (separate assemblies,
not referenced by game code — use dependency inversion: static events/registries in game code that those assemblies subscribe to).
Inside Project.* namespaces always write UnityEngine.Application (never bare Application.). Turkish UI strings.`
const SCHEMA = { type: 'object', properties: { summary: { type: 'string' }, changes: { type: 'array', items: { type: 'string' } }, concerns: { type: 'array', items: { type: 'string' } } }, required: ['summary', 'changes', 'concerns'] }
phase('Kanca ve Akış')
const out = await parallel([
  () => agent(`${RULES}
TASK: apply the hooks listed in ${ROOT}/Docs/FAZ3_KANCALAR.md that can be done WITHOUT new packages (skip Netcode manifest/prefab/NetworkObject items):
1. ContentOverrides lookups (Infrastructure/Content/ContentOverrides.cs API: TryGetWeapon/TryGetSound/TryGetMaterial/TryGetSoldier/TryGetVehicle/TryGetKirpi/TryGetHelicopter)
   at the start of: Infrastructure/Weapons/WeaponModelFactory.Build (instantiate override prefab, find child "Muzzle", set layer recursively, strip colliders),
   Infrastructure/Audio/GameAudio clip lookup, Infrastructure/Rendering/MaterialLibrary.Get, Infrastructure/Characters/SoldierModel.Build (only if simple: otherwise leave a TODO note in concerns),
   Infrastructure/Transport Helicopter/ArmoredCarrier visual builders. Fully null-safe; procedural path stays the fallback.
2. Dev console: call Presentation/DevTools DevConsole.Ensure() from Presentation/Bootstrap/MatchBootstrap and TrainingBootstrap (respect its gate).
3. Inversion hooks for Online/Platform: add to Presentation/Bootstrap/GameSession a static event surface they can subscribe to, e.g.
   public static event Action<MatchConfig> MatchStarting; public static event Action<MatchResult> MatchFinished; (raise them from MatchBootstrap),
   and in Infrastructure a static BotRuntimeGate { public static Func<bool> ShouldRunBots; } checked in BotController update (default run).
   Also add a main-menu extension point in Presentation/UI/MainMenuController: public static readonly List<(string label, Action<Transform> open)> ExtraButtons
   rendered as extra buttons. Document each new hook in FAZ3_KANCALAR.md (mark applied, tell Cursor which static to subscribe to).
Verify after each step.`, { label: 'kancalar', phase: 'Kanca ve Akış', schema: SCHEMA, model: 'sonnet', effort: 'medium' }),
  () => agent(`${RULES}
TASK: runtime-bug review of the core match flow by tracing code (no rewrites; minimal targeted fixes). Trace in this order, opening only these files
plus the ones they directly call: Presentation/Bootstrap/MainMenuBootstrap.cs, GameSession.cs, MatchBootstrap.cs, Infrastructure/DI/GameCompositionRoot.cs,
Infrastructure/Transport/*.cs (seat/arrive/release), Infrastructure/AI/BotController.cs (Create + disembark/NavMesh warp only),
Presentation/Player/PlayerController.cs (Create + transport/disembark only), Presentation/UI/EndScreen.cs, TrainingBootstrap.cs.
Look for: NullReference on first Play, Awake/Start order bugs (AddComponent runs Awake immediately), services used before GameContext.Set,
missing service registrations (CONTRACTS §2 in Docs/CONTRACTS.md), events never unsubscribed in OnDestroy, Time.timeScale/cursor not restored,
static registries not cleared on scene unload, match never reaching InMatch or Ending. Fix real bugs directly, verify, report.`, { label: 'akış-inceleme', phase: 'Kanca ve Akış', schema: SCHEMA, model: 'sonnet', effort: 'medium' }),
])
return out.filter(Boolean)
