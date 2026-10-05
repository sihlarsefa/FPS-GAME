export const meta = {
  name: 'harekat-faz3-content',
  description: 'HAREKÂT Faz 3: Codex görevleri C3-1…C3-7 (tasarım, varlık listesi, ses, amblemler, tutorial, ilerleme, web) — Claude devraldı',
  phases: [{ title: 'Codex Faz 3', detail: '7 paralel ajan' }],
}
const ROOT = '/Users/f2gomac/Desktop/FPS-GAME'
const TASKS = ['C3-1', 'C3-2', 'C3-3', 'C3-4', 'C3-5', 'C3-6', 'C3-7']
const SCHEMA = { type: 'object', properties: { summary: { type: 'string' }, files: { type: 'array', items: { type: 'string' } }, checks: { type: 'array', items: { type: 'string' } }, remaining: { type: 'array', items: { type: 'string' } } }, required: ['summary', 'files', 'checks', 'remaining'] }
phase('Codex Faz 3')
const out = await parallel(TASKS.map(t => () => agent(
`You are taking over Codex task ${t} for the game HAREKÂT (Turkish-military FPP squad battle royale, Unity) at ${ROOT}.
The original Codex/Cursor runs stopped on usage limits; some files for this task may already exist — read them and CONTINUE, don't restart.
Read ${ROOT}/Docs/CODEX_FAZ3.md: the KURALLAR section and the section "GÖREV ${t}" — execute it completely, INCLUDING its UZATMA items.
Also read ${ROOT}/Docs/DURUM.md. Hosting decisions: Windows Server, MSSQL, plain HTML/CSS/JS web (no frameworks), no Linux.
STRICT: write ONLY in the folders that task names (and Design/CODEX_DURUM.md for a dated "FAZ 3 — ${t}" log line). Never touch Assets/,
Packages/, ProjectSettings/, Backend/, Deploy/, .github/, Docs/, Tools/UnityVerify/, Tools/AgentWorkflows/. Never run file-changing git commands.
Read-only sources: Assets/_Project/Scripts/** (catalogs, enums, WorldTypes), Backend/** (models, endpoints), Design/GDD/*.
Turkish primary language. Original stylized designs only (no official TSK insignia copies, no real-world organizations).
If Web/ is touched: keep its lint/test/build scripts passing (run them). If you produce CSV/JSON consumed by tools, validate them with a small script.
Return what you produced and how you verified it.`, { label: t, phase: 'Codex Faz 3', schema: SCHEMA })))
return out.filter(Boolean)
