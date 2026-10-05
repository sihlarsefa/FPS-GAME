# LocTool — HAREKÂT yerelleştirme hattı v2

Node ≥ 20. Bağımlılık yok. `Assets/` salt okunur taranır; yazma yalnızca `Localization/`.

## Komutlar

| Komut | Ne yapar |
|-------|----------|
| `npm run extract` | C# string literal adaylarını tarar → `Localization/reports/v2/extracted.json` + `UNITY_STRING_MAP_V2.md` |
| `npm run diff` | `extract` ile aynı; ek olarak `reports/v2/diff.json` (yeni / silinen / eşleşmeyen) |
| `npm run validate` | Placeholder, eksik çeviri, anahtar biçimi, uzunluk riski → `reports/v2/validation.json` |
| `npm run validate -- --strict` | Uyarıları da çıkış kodu 1 yapar |
| `npm run export` | `tables/<lang>.json` + `unity/strings_<lang>.json` + `unity/manifest.json` |
| `npm run font` | Karakter envanteri + LegacyRuntime sınıfı boşluk tahmini → `reports/v2/font-coverage.json` |
| `npm run font -- --font /path/to.ttf` | Exact SFNT cmap doğrulaması (TTF/OTF) |
| `npm run build` | export + extract + font |
| `npm test` / `npm run lint` | Birim testleri / sözdizimi |

## Kaynak doğruluk

- Ana tablo: `Localization/strings.csv` (`key,tr,en,de,az,ar,source_file`)
- Diller: **TR, EN, DE, AZ, AR**
- Anahtar standardı: `alan.alt.anahtar` (ör. `hud.ammo.reload`) — küçük harf, `_` ve `.` izinli

## Akış

```
Assets/**/*.cs (RO) ──extract──► candidates + UNITY_STRING_MAP_V2.md
strings.csv ──validate──► reports/v2/validation.json
strings.csv ──export──► tables/ + unity/strings_*.json
strings.csv ──font──► reports/v2/font-coverage.json
```

Çeviriler otomatik üretilmez / silinmez. `diff` yalnızca inceleme listesi verir.

## Örnek

```bash
cd Tools/LocTool
npm test
npm run build
npm run font -- --font "/System/Library/Fonts/Supplemental/Arial.ttf"
```
