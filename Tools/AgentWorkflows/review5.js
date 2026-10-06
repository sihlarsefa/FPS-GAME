export const meta = {
  name: 'harekat-review-5',
  description: 'HAREKÂT dalga 5: özellikler arası çakışma/hata avı (4 Sonnet ajanı)',
  phases: [{ title: 'Dalga 5', detail: '4 paralel inceleme ajanı' }],
}
const ROOT = '/Users/f2gomac/Desktop/FPS-GAME'
const V = (n) => `zsh ${ROOT}/Tools/UnityVerify/verify.sh ${ROOT}/Tools/UnityVerify/out_${n} --player --tests`
const RULES = `Project HAREKÂT (Turkish-military FPP squad BR), Unity 6000.6 C# at ${ROOT}. Editor can't run here; verify with ${V('<name>')} — keep 0 errors, all tests passing (471).
Read ${ROOT}/ARCHITECTURE.md sections you need. TOKEN-EFFICIENT: open only needed files (grep). Prefer NEW files in your scope; small null-safe edits elsewhere.
No deletes/.meta/file-changing git. UnityEngine.Application inside Project.* namespaces. Turkish UI. Procedural content. Add EditMode tests for pure logic
(tests that need Infrastructure/Presentation must be wrapped in #if UNITY_EDITOR). Cursor is concurrently fixing real-Unity errors anywhere; 7 other Claude agents run in parallel —
stay strictly in your scope.  Append a dated line to ${ROOT}/Docs/DURUM.md "Günlük" when done.`
const SCHEMA = { type: 'object', properties: { summary: { type: 'string' }, files: { type: 'array', items: { type: 'string' } }, concerns: { type: 'array', items: { type: 'string' } } }, required: ['summary', 'files', 'concerns'] }
const F = [
  ['tuslar-ui', `Cross-feature UI & input audit. Collect every key/mouse binding used across Infrastructure/Input/*, Presentation/Player/*, Presentation/UI/* (CommandWheel, Ping, Scoreboard, KeyBindingsPanel), Infrastructure/Vehicles/*, Infrastructure/Drone/*, Presentation/DevTools/*, Presentation/Tutorial/*, Presentation/Spectator/*.
Fix conflicts (e.g. F interact vs F-hold revive vs vehicle enter/exit; 1/2 seat switch vs weapon slots; U drone; Y/middle-mouse wheel/ping; CapsLock scoreboard; backquote console; N/F9 tutorial; Q/E lean vs spectator target switch) so each context has one clear owner; route new hardcoded keys through InputBindings where reasonable; make sure overlays (scoreboard, command wheel, map, inventory, pause, settings, console, cosmetics, tutorial summary) handle cursor lock/Time.timeScale/Esc consistently and never stack wrongly. Write Docs/KONTROLLER.md (final controls table, Turkish) and update README controls section.`],
  ['savas-durum', `Combat/state interactions audit: downed/revive (ReviveService, Combatant, BotReviveBehaviour, PlayerController downed path) × vehicles (downed while driving/passenger/turret → eject safely), × drone (cannot launch while downed), × item use/healing (blocked while downed), × zone damage and artillery (finishes downed), × spectator/killcam (only on real death, not on downed), × MatchService elimination + MatchSummary/achievements (no double counting), × tutorial/training (single-team training: downed must not soft-lock). Fix real bugs with minimal edits; add EditMode tests for pure logic fixes.`],
  ['araclar-ai', `Vehicles/AI audit: BotVehicleBoarding × BotController (boarded bots must not run NavMesh/perception or shoot through the hull; must re-enable cleanly on disembark incl. NavMesh warp), turret gunner (player and bot?) firing origin/collisions with own vehicle collider, DrivableVehicle × CombatService damage (bullets/explosions → vehicle health, occupants protected/ejected on destroy), BotLod × boarded/downed/recon-marked bots, recon drone markers × AI awareness, skirmish respawn × BotController re-creation and registries. Fix real bugs, minimal edits.`],
  ['akis-modlar', `Flow & modes audit: MainMenu setup (map Kuzgun/Ayaz/Mavi Liman, time of day/weather, insertion, difficulty, team count) → GameSession → correct scene (SceneNames.OperationSceneFor) for BR; Skirmish mode bootstrap path; Training free/tutorial; Editor/SceneBuilder builds ALL scenes (MainMenu, KuzgunVadisi, AyazGecidi, MaviLiman, TrainingRange) and EditorBuildSettings lists them all; EndScreen restart returns to the same mode/map; Loc keys used by new panels exist in Resources/Localization/*.json with Turkish fallback; Atmosphere applies per map (snow on Ayaz, sea fog on Mavi Liman). Fix real bugs, minimal edits.`],
]
phase('Dalga 5')
const out = await parallel(F.map(([k, task]) => () => agent(`${RULES}\nTASK ${k}: ${task}`, { label: k, phase: 'Dalga 5', schema: SCHEMA, model: 'sonnet', effort: 'high' })))
return out.filter(Boolean)
