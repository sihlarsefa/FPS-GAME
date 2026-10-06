export const meta = {
  name: 'harekat-ai-online',
  description: 'HAREKÂT: yapay zekâ runtime incelemesi + Online/Steam modüllerinin yeni olaylara bağlanması (2 Sonnet ajanı)',
  phases: [{ title: 'AI ve Online', detail: '2 paralel ajan' }],
}
const ROOT = '/Users/f2gomac/Desktop/FPS-GAME'
const V = (n) => `zsh ${ROOT}/Tools/UnityVerify/verify.sh ${ROOT}/Tools/UnityVerify/out_${n} --player --tests`
const RULES = `Project HAREKÂT, Unity 6000.6 C# at ${ROOT} (editor can't run; verify with ${V('<name>')} — keep 0 errors, tests passing).
TOKEN-EFFICIENT: open only files in scope (grep to locate). Minimal targeted fixes, no rewrites, no deletes, no .meta, no file-changing git.
Inside Project.* namespaces write UnityEngine.Application. Concurrent agents edit Infrastructure/Combat, Infrastructure/Zone, Presentation/UI, Infrastructure/World, Editor — do not edit those.`
const SCHEMA = { type: 'object', properties: { summary: { type: 'string' }, fixes: { type: 'array', items: { type: 'string' } }, concerns: { type: 'array', items: { type: 'string' } } }, required: ['summary', 'fixes', 'concerns'] }
phase('AI ve Online')
const out = await parallel([
  () => agent(`${RULES}
AREA ai. Scope: Infrastructure/AI/* (BotController*.cs, perception, tactics, director), Application/Services/BotDecisionService.cs, ChainOfCommandService.cs, SquadOrderService.cs.
Trace runtime: seat → disembark → NavMesh.SamplePosition+Warp → agent enabled; perception never targets allies (team check) and smoke blocks LOS;
wedge formation; squad orders F1–F4 from the player's team; following the NEW commander after CommandTransferredEvent; engage/reload/heal/zone/loot;
bots stop cleanly on death; 60-bot performance (no per-frame allocations, staggered perception, throttled SetDestination/raycasts). Fix real bugs.`, { label: 'inceleme:ai', phase: 'AI ve Online', schema: SCHEMA, model: 'sonnet', effort: 'medium' }),
  () => agent(`${RULES}
AREA online-wiring. Scope: Assets/_Project/Scripts/Online/** and Assets/_Project/Scripts/Platform/** ONLY (separate assemblies Project.Online, Project.Online.Netcode, Project.Platform;
not compiled by verify.sh). New game-side hooks exist (read Docs/FAZ3_KANCALAR.md bottom section): GameSession.MatchStarting/MatchFinished events,
MainMenuController.ExtraButtons list, BotRuntimeGate.ShouldRunBots, GameCompositionRoot.NetworkSessionFactory.
Wire them: (1) Online registers an "ONLİNE" entry into MainMenuController.ExtraButtons at [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] opening its login/hub panel;
(2) ServerBootstrap subscribes MatchStarting → NotifyMatchStarted and MatchFinished → SubmitMatchResultAndIdleAsync; sets BotRuntimeGate.ShouldRunBots = () => ServerBotGate.ShouldRunBots when netcode session active;
(3) SteamPlatformService subscribes MatchStarting/MatchFinished for rich presence and achievement unlocks (keep STEAMWORKS_NET constraint).
Remove any reflection-based hook lookups that are now unnecessary. Then write ${ROOT}/Tools/UnityVerify/verify_online.sh that compiles Scripts/Online (excluding Netcode and Tests subfolders)
against the outputs of verify.sh (run verify.sh first into Tools/UnityVerify/out_online) and make it 0 errors. Update Docs/FAZ3_KANCALAR.md and append a dated line to Docs/FAZ2_DURUM.md.`, { label: 'online-bağlama', phase: 'AI ve Online', schema: SCHEMA, model: 'sonnet', effort: 'medium' }),
])
return out.filter(Boolean)
