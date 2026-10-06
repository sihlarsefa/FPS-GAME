# HAREKÂT — Geliştirici Rehberi

Sıfırdan açma, katmanlar, doğrulama ve ajan kuralları.

## Gereksinimler

- Unity **6000.6.4f1** (Mac + isteğe bağlı **Windows Build Support** + **Dedicated Server**)
- .NET 8 SDK (Backend)
- Node 20+ (Web / Wiki / LocTool)
- Git; büyük varlıklar için Git LFS önerilir

## Projeyi açma

1. `FPS-GAME` klasörünü Unity Hub ile 6000.6.4f1’de aç.
2. İlk import uzun sürer. Menü: **HAREKÂT → Kurulum → Her Şeyi Kur** (veya batch: `Project.EditorTools.BatchEntry.SetupAll`).
3. Sahneler: `Assets/_Project/Scenes/` — MainMenu, KuzgunVadisi, AyazGecidi, MaviLiman, TrainingRange.
4. Play: MainMenu sahnesi.

## Katmanlar (`Assets/_Project/Scripts`)

| Katman | Ne var | Bağımlılık |
|--------|--------|------------|
| Core | Domain, Events, Interfaces | yok |
| Application | Servisler, kataloglar (saf C#) | Core |
| Infrastructure | Unity sarmalayıcılar (AI, Combat, World…) | Core, Application |
| Presentation | UI, Bootstrap, Player | hepsi |
| Editor | Setup, Build, içerik araçları | Editor only |

**Tuzağı:** `Project.*` ad alanı içinde `Application.` yazma → `UnityEngine.Application.` kullan.

## Doğrulama

```bash
# Ucuz derleme + EditMode (Unity açmadan)
zsh Tools/UnityVerify/verify.sh Tools/UnityVerify/out_dev --player --tests

# Gerçek Unity EditMode
# (Unity -runTests -testPlatform EditMode …)

# PlayMode duman (Soak hariç)
zsh Tools/UnityVerify/run_playmode.sh

# Uzun dayanıklılık
zsh Tools/UnityVerify/run_soak.sh
```

## Yeni içerik ekleme (kısa)

- **Silah:** `WeaponCatalog` / `WeaponIds` + loot + (görsel) ContentOverrides. Claude silah/balistik alanına dikkat.
- **Harita:** `MapCatalog` + `MapLayout*` + `SceneBuilder.Ensure*` + build settings.
- **Mod:** `GameMode` + Bootstrap + `GameSession.Start*`.
- **Başarım:** `Resources/Progression/achievements.json` + `AchievementService` metrikleri.

## Ajan / sahiplik

- Anlık sahiplik: `Docs/DURUM.md`
- Cursor görevleri: `Docs/CURSOR_FAZ*.md`
- Claude dalgası listesindeki dosyalara **dokunma**; hata görürsen DURUM’a not.
- Git: yalnızca kendi dosyaların; `checkout/reset/clean/stash` yasak (eşzamanlı ajanlar).

## Backend / Deploy

- Backend: MSSQL, IIS — `Backend/`, `Deploy/windows/`
- Staging kontrol listesi: `Deploy/windows/STAGING_PROVA.md`
- Canlı siteler: harekat-web:80, harekat-api:3208, harekat-wiki:3210 (APP2025; diğer IIS uygulamalarına dokunma)

## Daha fazla

- Mimari kararlar: `Docs/ADR/`
- Katkı: `CONTRIBUTING.md`
- Oynanış QA: `Docs/OYUN_TESTI.md`
