# Yerelleştirme hattı v2 (GÖREV C2-5)

HAREKÂT UI metin envanteri ve Unity geçiş listesi. **Yalnızca `Localization/` + `Tools/LocTool/` yazılır**; `Assets/` salt okunur taranır.

## Diller

| Kod | Dil | Not |
|-----|-----|-----|
| `tr` | Türkçe | Kaynak / ürün dili |
| `en` | English | İkinci dil |
| `de` | Deutsch | UI uzunluk riski yüksek |
| `az` | Azərbaycan | Latin; Ə/ə font gerektirir |
| `ar` | العربية | RTL — bkz. `reports/RTL_ARAYUZ_RISKLERI.md` |

## Dosyalar

| Dosya | Açıklama |
|-------|----------|
| `strings.csv` | Kaynak doğruluk: `key,tr,en,de,az,ar,source_file` (295 anahtar) |
| `KEY_NAMING.md` | Anahtar standardı (`hud.ammo.reload`) |
| `ASKERI_TERIMLER_SOZLUGU.md` | Askeri terim sözlüğü v2 (TR/EN/DE/AZ/AR) |
| `UNITY_LOCALIZATION_GECIS.md` | Unity Localization paket geçiş planı |
| `UNITY_STRING_MAP_V2.md` | Dosya:satır → anahtar aday listesi (LocTool üretir) |
| `tables/<lang>.json` | Dil tabloları |
| `unity/strings_<lang>.json` | Unity içe aktarma (anahtar → metin) |
| `unity/manifest.json` | Şema, dil listesi, RTL bayrağı |
| `reports/v2/*` | extract / diff / validate / font JSON |
| `reports/RTL_ARAYUZ_RISKLERI.md` | Arapça RTL riskleri (UZATMA) |
| `reports/FONT_KAPSAM_RAPORU.md` | LegacyRuntime / font kapsamı (UZATMA) |

## LocTool

```bash
cd Tools/LocTool
npm test
npm run build          # export + extract + font
npm run validate
npm run extract        # UNITY_STRING_MAP_V2.md yenile
npm run font -- --font /path/to.ttf   # exact cmap
```

Ayrıntı: `Tools/LocTool/README.md`.

## Eski doğrulayıcı

`scripts/validate_placeholders.py` hâlâ çalışır; CI tercihi LocTool `validate` olmalıdır.

## Tarama kapsamı (salt okunur)

`Assets/**/*.cs` — yorumlar, `Debug.Log*`, `[Tooltip]` / `[Header]`, `NameRoster` hariç.
