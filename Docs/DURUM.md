# HAREKÂT — Anlık Durum ve Dosya Sahipliği

> Bu dosya Claude, Cursor ve Codex arasındaki koordinasyon kaydıdır.
> **Bir dosyayı düzenlemeden önce sahibine bak.** Görev başında ve sonunda kendi satırını güncelle.

**Altyapı kararları:**
- Sunucu: **Windows Server** (Linux yok).
- Veritabanı: **MSSQL**.
- Web: **düz HTML/CSS/JS**.
- Oyun sunucusu: **Windows Dedicated Server** build'i.

## Unity modülleri (Assets/_Project)
| Modül | Sahip | Durum |
|-------|-------|-------|
| app-weapons, app-items, app-match, app-sim | Claude | ✅ TAMAMLANDI (399/399 test) |
| infra-rendering, infra-vfx, infra-combat, pres-ui-kit | Claude | ✅ TAMAMLANDI |
| infra-player, infra-audio, infra-weapon-visuals, infra-loot, pres-map-inventory, pres-menus, infra-characters | Claude (Hat C) | ✅ TAMAMLANDI |
| infra-world-terrain, infra-world-buildings, infra-ai, infra-transport, pres-player, pres-bootstrap, pres-hud | Claude (Hat D) | ✅ TAMAMLANDI |
| editor-setup (`Scripts/Editor/*`) | **Cursor** (Faz 2 F2-1) | ✅ KOD HAZIR (verify 0) |
| infra-vehicle-drive (`Infrastructure/Vehicles/*`) | **Cursor** (F2-2) | ✅ KOD HAZIR (verify 0) |
| infra-world-locations (`PropFactory.cs`, `LocationBuilder.cs`, `TrainingRangeBuilder.cs`) | **Cursor** (F2-3) | ✅ KOD HAZIR (verify 0) |
| Faz 3: `Scripts/Online/*`, `Scripts/Platform/*`, `Infrastructure/Content/*`, `Infrastructure/Diagnostics/*`, `Presentation/DevTools/*`, `Tests/PlayMode/*` | **Cursor** ([CURSOR_FAZ3.md](CURSOR_FAZ3.md)) | 🔄 F3-1…9 paralel |
| Entegrasyon + 6 alanlı inceleme (`Tools/AgentWorkflows/integration_review.js`) | Claude | 🔄 ÇALIŞIYOR (başladı 20:58) |
| Gerçek Unity doğrulaması (F2-4) | Cursor — **Claude entegrasyon/inceleme 'TAMAMLANDI' yazdıktan sonra** | ⏳ BEKLİYOR |

## Unity dışı klasörler
| Klasör | Sahip | Durum |
|--------|-------|-------|
| `Backend/` (MSSQL, IIS) | Cursor | ✅ ilk sürüm (build 0 hata, 29/29 test) → F2-7 ile MSSQL varsayılan |
| `Deploy/` (Windows Server) | Cursor | ✅ ilk sürüm → F2-7 |
| `Tools/LoadTest`, `Tools/DiscordBot`, `.github/` | Cursor | ✅ |
| `Web/` (HTML/CSS/JS) | **Cursor** (Codex C2-1) | ✅ |
| `Design/Assets/`, `Design/Art/`, `Design/Audio/`, `Design/Teams/`, `Design/Tutorial/`, `Design/Progression/` | **Cursor** (Codex C3) | 🔄 C3-1…7 paralel |
| `Tools/UnityVerify/` (derleme doğrulayıcı) | Claude | ✅ |
| `Wiki/` | **Cursor** (C2-2) | ✅ |
| `QA/` | **Cursor** (C2-3) | ✅ (368 senaryo) |
| `Tools/SqlReports/` | **Cursor** (C2-4) | ✅ |
| `Localization/` + `Tools/LocTool/` | **Cursor** (C2-5) | ✅ |
| `Design/Maps/v2/` | **Cursor** (C2-6) | ✅ |
| `Design/UI/` | **Cursor** (C2-7) | ✅ |
| `Marketing/LiveOps/` | **Cursor** (C2-8) | ✅ |

## Ortak kurallar
- Claude ajanları çalışırken Assets'te dosya değiştiren git komutları (checkout/reset/clean/stash) **yasak**.
- Commit atarken yalnızca kendi dosyalarını ekle.
- Hızlı doğrulama: `zsh Tools/UnityVerify/verify.sh Tools/UnityVerify/out_<isim> --player --tests`
- Ayrıntılı kayıtlar: Claude → bu dosya; Cursor → `Docs/FAZ2_DURUM.md`.

## Günlük
- 2026-10-05 20:28 — Cursor: **FAZ 3 başladı** — F3-1…9 + C3-1…7 paralel ajanlar. C3-8 Claude entegrasyon bitince. Kancalar: `Docs/FAZ3_KANCALAR.md`.
- 2026-10-05 20:25 — Cursor: Codex C2-1…C2-8 **tamamlandı ve doğrulandı** (Web lint/test, Wiki 83 sayfa, QA 368 senaryo, LocTool 8/8, haritalar, UI, LiveOps). Backend CI + PR Checks yeşil (önceki CI fix push).
- 2026-10-05 20:15 — Cursor: F2-1/2/3 kod tamam + verify **0 hata** (Editor/Infra/Presentation/player). Codex C2-1…C2-8 ajanları yeniden başlatıldı (önceki tur limit yüzünden ölmüştü); aktif yazıyorlar.
- 2026-10-05 20:09 — Cursor: Codex FAZ2 (C2-1…C2-8) işleri Cursor ajanlarına devredildi; paralel başlatıldı. Codex klasör sahipliği Cursor'a geçti.
- 2026-10-05 19:55 — Claude: 8/25 modül tamam. Kalan 14 modül iki hatta yeniden başlatıldı (14 paralel ajan). 3 modül, Unity doğrulaması, Windows Server ve MSSQL işleri Cursor Faz 2'ye verildi.
- 2026-10-05 20:22 — Claude: 11/14 modül bitti (kalan: pres-menus, infra-world-terrain, infra-world-buildings). **Tüm Unity assembly'leri + player config 0 hata, 399/399 test.** Cursor F2-1/2/3 kodu hazır. Sıradaki: Claude entegrasyon+inceleme → sonra Cursor F2-4.
- 2026-10-05 20:40 — Claude Hat C 7/7 TAMAMLANDI. Hat D'de kalan: infra-world-terrain, infra-world-buildings. Cursor/Codex Faz 3 görevleri tanımlandı. Claude'un mevcut dosyalarına kanca isteği → `Docs/FAZ3_KANCALAR.md`.
- 2026-10-05 20:32 — ⚠️ **Cursor'a not:** `Infrastructure/Diagnostics/*` (F3-6) derlenmiyor ve tüm Infrastructure'ı kırıyor. Sebep: `Project.*` ad alanı içinde `Application.version` gibi çağrılar `Project.Application` ad alanına çözümleniyor. Çözüm: her yerde **`UnityEngine.Application.`** yaz (ör. `UnityEngine.Application.version`, `.persistentDataPath`, `.logMessageReceivedThreaded`, `.Quit()`). Bu kural tüm `Project.*` dosyaları için geçerli.
- 2026-10-05 20:38 — Cursor limit nedeniyle durdu (son değişiklik 20:32). Derlemeyi kıran F3-6 dosyalarında Claude mekanik düzeltme yaptı: `Infrastructure/Diagnostics/*` ve `Presentation/DevTools/*` içinde `Application.` → `UnityEngine.Application.`; `PerfRunController.cs` içinde bozuk `UnityEditor.EditorUnityEngine.Application` → `UnityEditor.EditorApplication`. Mantık değiştirilmedi. **Cursor geri dönünce F3-5/F3-6'yı buradan sürdürsün.**
- 2026-10-05 20:45 — **Cursor ve Codex limitte → Claude devraldı (12 paralel ajan):**
  - `Tools/AgentWorkflows/faz3_content.js`: C3-1…C3-7 (Design/*, Web/)
  - `Tools/AgentWorkflows/faz3_infra.js`: F3-1 Online istemci (`Scripts/Online/*` hariç Netcode), F3-3 Windows Server + MSSQL + backend uçları (`Backend/`, `Deploy/windows/`), F3-7 Steam (`Scripts/Platform/*`), F3-8 Installer + Launcher (`Tools/Installer`, `Tools/Launcher`)
  - Hat D: infra-world-buildings
  - **Cursor/Codex geri dönerse:** bu görevlere DOKUNMA; Claude bitirince `Docs/FAZ2_DURUM.md` güncellenecek. Boşta kalan: F3-5 DevConsole ve F3-6 Diagnostics (Claude entegrasyonundan sonra), C3-8 (entegrasyondan sonra), F2-4 gerçek Unity doğrulaması (entegrasyondan sonra).
- 2026-10-05 20:58 — **25/25 Unity modülü TAMAMLANDI.** Entegrasyon + 6 inceleme ajanı başladı. Unity Hub ve 6000.6.0f1 editörü bozuk çıktı (Hub imzası, editörde Info.plist yok) — kullanıcı Unity 6.6 (6000.6.4f1) + Windows Build Support + Windows Dedicated Server modülleriyle yeniden kuruyor.
