export const meta = {
  name: 'harekat-docs-qa',
  description: 'HAREKÂT: C3-8 QA+Wiki güncellemesi ve README/ARCHITECTURE/oynanış rehberi (2 Sonnet ajanı)',
  phases: [{ title: 'Doküman', detail: '2 paralel ajan' }],
}
const ROOT = '/Users/f2gomac/Desktop/FPS-GAME'
const SCHEMA = { type: 'object', properties: { summary: { type: 'string' }, files: { type: 'array', items: { type: 'string' } } }, required: ['summary', 'files'] }
const COMMON = `Project HAREKÂT (Turkish-military FPP squad battle royale; Unity 6000.6; Windows Server + MSSQL + plain HTML/CSS/JS web; no Linux) at ${ROOT}.
TOKEN-EFFICIENT: read only what you need (grep, catalogs, Docs/*.md). NEVER edit anything under Assets/, Packages/, ProjectSettings/, Backend/, Deploy/.
No file-changing git commands. Turkish language.`
phase('Doküman')
const out = await parallel([
  () => agent(`${COMMON}
TASK: Docs/CODEX_FAZ3.md "GÖREV C3-8" (QA + Wiki update) — write only in QA/ and Wiki/. Re-generate Wiki from catalogs (keep its build passing: run its build/test),
add QA scenarios for new features (drivable Kirpi, dev console, online panels, Windows dedicated server, content overrides), write QA/RegresyonListesi.md (60 critical cases).
Update Design/CODEX_DURUM.md C3-8 row to ✅ with a dated line.`, { label: 'C3-8', phase: 'Doküman', schema: SCHEMA, model: 'sonnet', effort: 'medium' }),
  () => agent(`${COMMON}
TASK: rewrite ${ROOT}/README.md and ${ROOT}/ARCHITECTURE.md (Turkish) to reflect the CURRENT project (they still describe the old Cursor prototype):
- README: what HAREKÂT is (10-person teams, TSK ranks/chain of command, T-70/Kirpi insertion, Turkish weapons, Kuzgun Vadisi, artillery), repo map
  (Assets layers, Backend, Deploy/windows, Web, Wiki, Tools, Design, QA, Docs), how to open in Unity 6.6 (Hub, modules Windows Build Support +
  Windows Dedicated Server, Projects→Add, "HAREKÂT → Kurulum → Her Şeyi Kur", Play from MainMenu), controls table (from Infrastructure/Input/UnityInputReader.cs
  and Docs/CONTRACTS.md incl. F1–F4, V, dev console backquote), builds (Windows client, Windows Dedicated Server, macOS), Tools/UnityVerify usage, status/known limits.
- ARCHITECTURE: layers & asmdefs (Core, Application, Infrastructure, Presentation, Editor, Online, Online.Netcode, Platform, Tests), server-authoritative design
  (PlayerCommand, SimulationClock 30Hz, INetworkSession, ShotRequest), event bus list, composition root/GameContext, world generation pipeline, content override
  pipeline (procedural → ready-made assets → realistic), backend/Windows deployment overview, mermaid diagrams.
Read Docs/CONTRACTS.md, Docs/DURUM.md, AJAN_PLANI.md and grep the asmdef files for accuracy. Also write Docs/OYNANIS_REHBERI.md (player guide: modes, ranks, roles, orders, tips).`, { label: 'dokümanlar', phase: 'Doküman', schema: SCHEMA, model: 'sonnet', effort: 'medium' }),
])
return out.filter(Boolean)
