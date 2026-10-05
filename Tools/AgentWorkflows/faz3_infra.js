export const meta = {
  name: 'harekat-faz3-infra',
  description: 'HAREKÂT Faz 3: Cursor görevleri F3-1 online istemci, F3-3 Windows Server+MSSQL+backend uçları, F3-7 Steam, F3-8 kurulum/launcher — Claude devraldı',
  phases: [{ title: 'Cursor Faz 3', detail: '4 paralel ajan' }],
}
const ROOT = '/Users/f2gomac/Desktop/FPS-GAME'
const COMMON = `You are taking over a Cursor task for HAREKÂT (Turkish-military FPP squad battle royale; Unity 6000.6 client; ASP.NET Core backend) at ${ROOT}.
Cursor stopped on a usage limit mid-task: files may already exist — read them and CONTINUE/complete, don't restart. Read ${ROOT}/Docs/CURSOR_FAZ3.md
(section 0 KURALLAR + your task section), ${ROOT}/Docs/CURSOR_FAZ2.md (referenced parts), ${ROOT}/Docs/DURUM.md and ${ROOT}/Docs/FAZ2_DURUM.md.
Hosting: Windows Server ONLY (no Linux), MSSQL (SQL Server), IIS, plain HTML/CSS/JS web. C# pitfall: inside Project.* namespaces always write
UnityEngine.Application (never bare Application.). Never run file-changing git commands. Never touch Assets/_Project files outside your folder.
Append a dated line for your task to the "FAZ 3" table/log in ${ROOT}/Docs/FAZ2_DURUM.md when done (append only).
`
const SCHEMA = { type: 'object', properties: { summary: { type: 'string' }, files: { type: 'array', items: { type: 'string' } }, verification: { type: 'array', items: { type: 'string' } }, hooksRequested: { type: 'array', items: { type: 'string' } }, remaining: { type: 'array', items: { type: 'string' } } }, required: ['summary', 'files', 'verification', 'hooksRequested', 'remaining'] }
const TASKS = [
  { id: 'F3-1', p: `TASK F3-1 Online client layer. Write ONLY in Assets/_Project/Scripts/Online/** EXCEPT Online/Netcode/** (already done by Cursor — read it, don't edit).
BackendClient (UnityWebRequest), token store (UnityEngine.Application paths), OnlineProfileService, UI panels (OnlineLoginPanel, SquadPanel, MatchmakingPanel,
LeaderboardPanel) using Presentation.UI UiFactory/UiTheme. DTOs must match Backend/ClientSdk exactly. The Online assembly is NOT compiled by
Tools/UnityVerify/verify.sh, so add a compile check: write Tools/UnityVerify/verify_online.sh (new file, yours) that compiles Scripts/Online
(excluding Netcode subfolder) against the verify outputs (run verify.sh first to produce out dirs) and reports errors; make it 0 errors.
Hooks into Claude files (GameSession.NetworkSessionFactory, ONLİNE button in main menu) go into Docs/FAZ3_KANCALAR.md (append).` },
  { id: 'F3-3', p: `TASK F3-3 Windows Server infrastructure + backend endpoints. Write ONLY in Backend/** and Deploy/windows/** (+ Deploy/README.md note).
Do F3-3 fully (MSSQL default provider with SQL Server migrations, indexes, backup jobs; IIS scripts; Backend/Harekat.ServerManager Windows
service — continue the existing project; firewall; capacity table; monitoring; Deploy/windows/README.md). THEN add the backend endpoints other
tasks need: /telemetry/client-errors (F3-6), /auth/steam (F3-7, ticket validation behind an interface, configurable WebAPI key), /client/version
and /news (F3-8, admin-editable, stored in MSSQL), and the F2-8 Web↔Backend contract test (read Web/js/api/* read-only). Keep 'dotnet build'
and 'dotnet test' green (run them in Backend/). Document every new endpoint in Backend/README.md and OpenAPI.` },
  { id: 'F3-7', p: `TASK F3-7 Steam preparation. Write ONLY in Assets/_Project/Scripts/Platform/** (asmdef with defineConstraints STEAMWORKS_NET so it compiles
only when Steamworks.NET is installed). SteamPlatformService (init, user, achievements mapped to backend achievement ids from
Design/Progression if present else a placeholder map, rich presence, auth session ticket → backend /auth/steam). Scripts/Platform/README.md
(AppID, Windows depots, SteamPipe upload, store approval). Hook requests → Docs/FAZ3_KANCALAR.md (append).` },
  { id: 'F3-8', p: `TASK F3-8 Windows installer + launcher. Write ONLY in Tools/Installer/** and Tools/Launcher/** (do NOT edit Backend; use the endpoint contract
/client/version and /news exactly as documented in CURSOR_FAZ3 F3-8 — write the expected JSON shapes in Tools/Launcher/README.md so the Backend
agent's implementation can be checked). Inno Setup script (Builds/Windows → setup exe, shortcuts, uninstaller, VC++ runtime check, TR/EN),
code-signing notes. Launcher: .NET 10 WPF or WinForms (Windows-only target; on this Mac verify with 'dotnet build' using EnableWindowsTargeting=true),
news feed, version check, patch zip download with SHA-256 verification, install, PLAY button passing token args. Unit tests for the
updater logic (separate test project) must pass with 'dotnet test'.` },
]
phase('Cursor Faz 3')
const out = await parallel(TASKS.map(t => () => agent(COMMON + t.p, { label: t.id, phase: 'Cursor Faz 3', schema: SCHEMA })))
return out.filter(Boolean)
