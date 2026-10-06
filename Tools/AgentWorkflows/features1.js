export const meta = {
  name: 'harekat-features-1',
  description: 'HAREKÂT özellik dalgası 1: eğitim, başarımlar, yerelleştirme, izleyici modu, Ayaz Geçidi haritası, tuş atama (6 Sonnet ajanı)',
  phases: [{ title: 'Özellikler', detail: '6 paralel ajan' }],
}
const ROOT = '/Users/f2gomac/Desktop/FPS-GAME'
const V = (n) => `zsh ${ROOT}/Tools/UnityVerify/verify.sh ${ROOT}/Tools/UnityVerify/out_${n} --player --tests`
const RULES = `Project HAREKÂT (Turkish-military FPP squad BR), Unity 6000.6 C# at ${ROOT}. Editor can't run here; verify with ${V('<name>')} — keep 0 errors, all tests passing.
Architecture: read ${ROOT}/ARCHITECTURE.md and the parts of ${ROOT}/Docs/CONTRACTS.md you need. Layers: Core/Application pure C# (no UnityEngine), Infrastructure (Unity), Presentation (UI/flow), Editor.
TOKEN-EFFICIENT: open only files you need (grep). Prefer NEW files in your own folder; when you must touch an existing file keep the change small and null-safe.
No deletes, no .meta edits, no file-changing git. Write UnityEngine.Application inside Project.* namespaces. Turkish UI strings. Procedural content only.
NEVER edit: Infrastructure/AI/**, Scripts/Online/**, Scripts/Platform/** (other agents). Other features run in parallel — stay in your scope listed below.
Add EditMode tests (Assets/_Project/Tests/EditMode) for any pure logic you add.`
const SCHEMA = { type: 'object', properties: { summary: { type: 'string' }, files: { type: 'array', items: { type: 'string' } }, hooks: { type: 'array', items: { type: 'string' } }, concerns: { type: 'array', items: { type: 'string' } } }, required: ['summary', 'files', 'hooks', 'concerns'] }
const F = [
  ['egitim', `Tutorial in the Training Range. Input: Design/Tutorial/tutorial_steps.json + poligon_adimlar.md. Copy the JSON to Assets/_Project/Resources/Tutorial/tutorial_steps.json.
Pure step state machine in Application/Services/TutorialService.cs (+tests) driven by game events (IEventBus) and simple conditions; Presentation/Tutorial/TutorialPresenter.cs shows step text/hints
(UiFactory) with progress, skip key, completion message. Hook: TrainingBootstrap starts it when GameSession has a "tutorial" flag (add GameSession.StartTutorial bool) and main menu "ATIŞ POLİGONU"
offers "Eğitimli / Serbest". Scope: those new files + small edits in TrainingBootstrap.cs, GameSession.cs, MainMenuController.cs.`],
  ['basarim', `Achievements client side. Input: Design/Progression/achievements.json (copy to Resources/Progression/achievements.json). Application/Services/AchievementService.cs (pure; evaluates conditions from
events/match results; persists unlocked ids + progress via ISettingsStore; raises an event AchievementUnlocked) + tests; Presentation/UI/AchievementToastView.cs toast popup; CareerPanel shows list
(edit Presentation/UI/CareerPanel.cs minimally). Register service in GameCompositionRoot (small edit) and raise unlocks also through GameSession (static event AchievementUnlocked) so Steam/backend can sync.`],
  ['dil', `Localization runtime. Input: Localization/unity/strings_<lang>.json + manifest.json + Localization/KEY_NAMING.md. Copy JSONs to Resources/Localization/. Infrastructure/Localization/Loc.cs (static Get(key, fallback),
language switch event, lazy load, missing-key logging once) + GameSettings language field (Core model + SettingsService key) + SettingsPanel language selector (TR/EN/DE/AZ/AR; RTL just left-aligned for now).
Convert the HIGHEST-visibility strings only: main menu buttons, pause menu, end screen titles, HUD top labels — using Loc.Get("key", "Türkçe fallback"). Scope: new Localization folder, Core GameSettings, SettingsService, SettingsPanel, MainMenuController, PauseMenu, EndScreen, HudController (labels only).`],
  ['izleyici', `Spectator / killcam after the local player dies (before the end screen): Presentation/Spectator/SpectatorController.cs — follows alive teammates (then any combatant) in 3rd person orbit camera, Q/E or mouse to switch target,
shows "İZLENİYOR: <rütbeli ad>" banner and "Ana menüye dön / Maç sonu" buttons; brief 3-second killcam that frames the killer (LastAttackerId) first. Disable HUD parts properly. Hook into MatchBootstrap death flow with a small edit
(currently shows death then end screen after 4 s — replace with: killcam → spectator; end screen when match ends or player chooses). Scope: new Spectator folder + small edits in MatchBootstrap.cs.`],
  ['harita2', `Second map "Ayaz Geçidi" (snowy mountain pass). Input: Design/Maps/v2/AyazGecidi/layout.json + README. Implement MapLayout.CreateAyazGecidi(int seed) in a NEW file Infrastructure/World/MapLayoutAyazGecidi.cs (partial or static helper — read WorldTypes.cs/MapLayout first),
add a map id to WorldGenerationOptions (e.g. string MapId = "kuzgun"), make WorldGenerator choose the layout and snow-heavy painting (TerrainPainter: lower snow line for this map), add SceneNames.AyazGecidi + Editor/SceneBuilder EnsureAyazGecidi
(+ build settings), MatchConfig.MapName selection in GameSession, and a map selector in the main menu setup panel ("Kuzgun Vadisi" / "Ayaz Geçidi"). Keep Kuzgun Vadisi behaviour identical.`],
  ['tuslar', `Key rebinding + gamepad. Infrastructure/Input/UnityInputReader.cs currently hardcodes Keyboard keys. Add Infrastructure/Input/InputBindings.cs (action → Key list, defaults = current keys, persisted via PlayerPrefs JSON)
and make UnityInputReader read from it; add gamepad mappings (move/look sticks, triggers fire/aim, buttons jump/crouch/reload/interact, d-pad weapon select) with look sensitivity for sticks.
Presentation/UI/KeyBindingsPanel.cs (list actions, click → "tuşa basın" capture, reset defaults) opened from SettingsPanel ("TUŞ ATAMALARI" button). Scope: Input folder, new panel, small SettingsPanel edit.`],
]
phase('Özellikler')
const out = await parallel(F.map(([k, task]) => () => agent(`${RULES}\nFEATURE ${k}: ${task}\nAt the end append a dated line to ${ROOT}/Docs/DURUM.md "Günlük" describing the feature (append only).`,
  { label: k, phase: 'Özellikler', schema: SCHEMA, model: 'sonnet', effort: 'medium' })))
return out.filter(Boolean)
