# FAZ 2/3 — Cursor Durum

## FAZ 2 (önceki)
- F2-1/2/3 ✅ verify 0 + 399 test
- C2-1…8 ✅

## FAZ 3 — 2026-10-05 20:28 başlatıldı

| Görev | Klasör | Durum |
|-------|--------|-------|
| F3-1 Online istemci | `Scripts/Online/*` | ✅ 2026-10-05 21:49 |
| F3-2 Netcode | `Scripts/Online/Netcode/*` | ✅ 2026-10-05 20:31 |
| F3-3 Windows/MSSQL/IIS | `Backend/`, `Deploy/windows/` | ✅ 2026-10-05 21:49 |
| F3-4 ContentOverrides | `Infrastructure/Content/*`, Editor | ✅ 2026-10-05 20:35 |
| F3-5 Dev konsol | `Presentation/DevTools/*` | ✅ 2026-10-05 21:40 |
| F3-6 Diagnostics | `Infrastructure/Diagnostics/*` | ✅ 2026-10-05 21:40 |
| F3-7 Steam | `Scripts/Platform/*` | ✅ 2026-10-05 20:35 |
| F3-8 Installer/Launcher | `Tools/Installer/*`, `Tools/Launcher/*` | ✅ 2026-10-05 21:40 |
| F3-9 PlayMode tests | `Tests/PlayMode/*` | ✅ 2026-10-05 20:30 |
| C3-1…7 (Codex) | `Design/*`, `Web/` | ✅ 2026-10-05 21:40 |
| C3-8 | QA/Wiki | ⏳ Claude entegrasyon sonrası |

Kancalar: `Docs/FAZ3_KANCALAR.md`

### FAZ 3 — F3-2 Netcode (2026-10-05 20:31)
- `Assets/_Project/Scripts/Online/Netcode/` — `Project.Online.Netcode` asmdef (`defineConstraints: HAREKAT_NETCODE`, versionDefines → `com.unity.netcode.gameobjects` ≥2.0).
- `NetcodeNetworkSession : INetworkSession` — Host / Client / DedicatedServer; `IPlayerCommandSink`; fabrika kaydı.
- `NetworkPlayer` — ServerRpc komut/atış, NetworkVariable can/zırh/mermi, NetworkTransform, lag compensation (1 sn), prediction/reconciliation.
- `InterestManager` — 60 oyuncu ilgi alanı; `ServerBotGate` — botlar sunucu-only.
- `ServerBootstrap` — `-server -port -region -backend -serverKey -maxPlayers`; heartbeat; maç sonucu; 30 Hz; grafik/ses kapalı.
- Paketler manifest’e **eklenmedi** (F2-4). Doğrulama: Host+1 Client (F2-4).

### F3-4 özet (2026-10-05 20:35)
- `ContentOverrides` SO + `TryGetWeapon/Sound/Material/Soldier/Vehicle/Building`
- Editör: **HAREKÂT/İçerik/Varlık Eşleyici**, Mixamo Humanoid + Animator helper
- `Assets/ThirdParty/README.md`, `Design/Assets/*.csv` kolon sözleşmesi (C3-1 dolduracak)
- Claude dosyalarına dokunulmadı; kancalar `FAZ3_KANCALAR.md`

### F3-5 — Dev konsol (2026-10-05 21:40)
- `Presentation/DevTools/DevConsole` + `DevConsoleCommands` + `DevConsoleGate` + `PerfOverlay`
- Komutlar: give/ammo/heal/god/noclip, spawn_bots, kill_team, ai, zone, timescale, tp, artillery, rank/xp, fps/netstats/screenshot/loadscene
- Release kapalı; `-dev` / Development Build ile açık. Kanca: `FAZ3_KANCALAR.md` → MatchBootstrap `DevConsole.Ensure`

### F3-6 — Diagnostics (2026-10-05 21:40)
- `Infrastructure/Diagnostics/`: ClientLogCollector (500 satır halka), ClientErrorReporter → Telemetry `/client-errors`, PerfSampler/PerfRunController, DiagnosticsCommandLine
- `UnityEngine.Application` tam ad (Project.Application çakışması yok). verify: 0 hata, 399/399

### F3-7 — Steam (2026-10-05 20:35)
- `Scripts/Platform/`: SteamPlatformService, SteamAchievementMap, SteamRichPresenceFormat, SteamPlatformSettings
- asmdef `Project.Platform`; STEAMWORKS_NET define. Kancalar FAZ3_KANCALAR’da

### F3-8 — Installer / Launcher (2026-10-05 21:40)
- `Tools/Installer/Harekat.iss` + `build.ps1` + CodeSigning.md
- `Tools/Launcher/`: .NET 10 WinForms; sürüm kontrolü + indirme; `dotnet build` Release 0 hata

### C3-1…7 — Codex (2026-10-05 21:40)
- C3-1 `Design/Assets/*.csv` (models/sounds/materials/animations) kapsama OK
- C3-2 Art briefs, C3-3 Audio/telsiz, C3-4 Teams emblems/ranks, C3-5 Tutorial, C3-6 Progression, C3-7 Web download/sysreq/patches

### F3-3 — Windows Server + MSSQL + IIS + ServerManager (2026-10-05 21:49)
- `Backend/Harekat.ServerManager/Services/`: PortPool, MatchProcessRegistry, BackendApiClient, ProcessResourceMonitor, LogRotator; workers MatchSpawn/Heartbeat/MetricsHttp/LogRotation; stub `Worker.cs` silindi
- Claim yanıtı: `ClaimMatchResponse` (`Match` + `ServerId` + `Endpoint`)
- MSSQL varsayılan (`Storage:Provider=SqlServer`); Npgsql yok
- `Deploy/windows/`: `iis-setup.ps1`, `firewall.ps1`, `sql/backup-jobs.sql`, `sql/indexes.sql`, `CAPACITY.md`, `monitoring.md`; README sıfırdan-canlıya
- Build: `dotnet build Backend/Harekat.ServerManager` Release — 0 hata

### F3-1 — Online menü panelleri + testler (2026-10-05 21:49)
- `Scripts/Online/UI/`: `OnlineLoginPanel`, `OnlineHubPanel`, `OnlineSquadPanel`, `OnlineMatchmakingPanel`, `OnlineLeaderboardPanel` (+ mevcut `OnlineUi`)
- Türkçe etiketler; `UiFactory` / `UiTheme`; `OnlineServices.Client` API
- `Scripts/Online/Tests/`: `BackendJsonTests`, `TokenStoreTests` (jeton yenileme), `OfflineMatchQueueTests` (durum makinesi)
- Ana menü "ONLİNE" kancası: `Docs/FAZ3_KANCALAR.md` (Claude)

### F3-9 — PlayMode test altyapısı (2026-10-05 20:30)
- `Assets/_Project/Tests/PlayMode/Project.Tests.PlayMode.asmdef` — `UNITY_INCLUDE_TESTS`; ref: Core, Application, Infrastructure, Presentation, TestRunner, nunit
- Testler:
  - `MainMenuLoadTests` — MainMenu yükleme, Canvas, exception yok, `Logs/screens/mainmenu.png`
  - `KuzgunVadisiSmokeTests` — oyuncu + ≥30 bot, Insertion, hızlandırılmış InMatch
  - `FireHitPlayModeTests` — TrainingRange ateş: mermi azalır, manken hasar alır
  - `SquadPlayModeTests` — F1 Follow yaklaşma; komutan ölümü → `CommandTransferredEvent`
  - `TrainingRangeLoadTests` — poligon yükleme
- Batch: `Tools/UnityVerify/run_playmode.sh` (UnityVerify'da yalnızca bu yeni dosya)
- Çalıştırma: F2-4 penceresinde `SetupAll` sonrası `zsh Tools/UnityVerify/run_playmode.sh`
