# Unity Localization Paketine Geçiş Önerisi — HAREKÂT

Bu belge, `Assets/` altındaki C# koduna gömülü Türkçe UI metinlerinin
[Unity Localization](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/index.html)
paketine taşınması için pratik bir yol haritasıdır. Kaynak envanter: `Localization/strings.csv`.

## Mevcut durum

- UI metinleri çoğunlukla string literal olarak `Presentation/UI`, `Presentation/Player`,
  `Presentation/Bootstrap` ve `Application/Catalogs` içinde.
- Biçimlendirme yardımcıları `MenuText` (Türkçe büyük harf, süre, yüzde) kültüre bağlı değil;
  yerelleştirmede `Smart String` / `Plural` ile değiştirilmeli.
- Ana menü (`MainMenuController`) henüz iskelet; yeni ekranlar doğrudan Localization API ile açılmalı.

## Hedef mimari

```
Localization/
  strings.csv          ← kaynak doğruluk (bu klasör; CI doğrulama)
Assets/_Project/Localization/
  HAREKAT Shared Data.asset
  Tables/
    UI Shared Data.asset
    UI_tr.asset / UI_en.asset / UI_de.asset / UI_az.asset / UI_ar.asset
    Catalog Shared Data.asset   (rütbe, silah, eşya, lokasyon)
```

Önerilen tablo ayrımı:

| Tablo | İçerik | Örnek anahtar öneki |
|-------|--------|---------------------|
| `UI` | Menü, HUD, bildirim, ipucu | `pause.*`, `settings.*`, `notify.*` |
| `Catalog` | Rütbe, rol, eşya, silah kategorisi, harita | `rank.*`, `item.*`, `location.*` |
| `Narrative` (ileride) | Yama notu, eğitim diyaloğu | `story.*` |

## Adım adım geçiş

### 1. Paket ve yerel ayarlar

1. Package Manager → **Localization** (`com.unity.localization`).
2. Project Settings → Localization → Active Locales: `tr`, `en`, `de`, `az`, `ar`.
3. Varsayılan locale: `tr` (ürün dili Türkçe).
4. Fallback: `tr` → `en` (eksik çeviride İngilizce).

### 2. CSV içe aktarma

1. `Localization/strings.csv` dosyasını Unity’ye kopyalayın veya Addressable olmayan bir import yolu kullanın.
2. Window → Asset Management → Localization Tables → **Import**.
3. Sütun eşlemesi:

| CSV | Unity |
|-----|-------|
| `key` | Key |
| `tr` | Turkish (tr) |
| `en` | English (en) |
| `de` | German (de) |
| `az` | Azerbaijani (az) |
| `ar` | Arabic (ar) |

`source_file` sütunu Unity tablosuna aktarılmaz; denetim için CSV’de kalır.

### 3. Kod API’si

Yeni sarmalayıcı (yalnızca örnek — `Assets/` yazımı bu görev kapsamı dışı):

```csharp
// Pseudo: Project.Presentation.UI.Loc
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public static class Loc
{
    public static string T(string key) =>
        LocalizationSettings.StringDatabase.GetLocalizedString("UI", key);

    public static string T(string key, params object[] args) =>
        LocalizationSettings.StringDatabase.GetLocalizedString("UI", key, args);
}
```

Değişim örnekleri:

| Önce | Sonra |
|------|-------|
| `"DEVAM ET"` | `Loc.T("pause.btn.resume")` |
| `"BÖLGEYE " + dist` | `Loc.T("hud.minimap.to_zone", dist)` |
| `teamCount + " Tim = " + n + " Asker"` | `Loc.T("menu.team_summary", teamCount, n)` |
| `RankCatalog.GetName(rank)` | tablo anahtarı `rank.{id}` veya katalogda locale map |

### 4. Placeholder ve çoğul

CSV’de `{0}`, `{1}` Smart Format yer tutucularıdır.

- Sayısal çoğul (ileride): `{0:plural:Tim\|Tim\|Tim}` veya ayrı anahtarlar
  `menu.team_summary.one` / `.other` (Türkçe çoğul eki UI’da nadiren değişir;
  Azerbaycan ve Arapça için ayrı kural gerekir).
- Arapça: RTL — `LocalizeStringEvent` + TextMeshPro `isRightToLeftText`.
- Doğrulama: `cd Tools/LocTool && npm run validate` (eski: `scripts/validate_placeholders.py`).

### 5. Kataloglar

`RankCatalog`, `ItemCatalog`, `WeaponCatalog`, `LoadoutCatalog` DisplayName alanları
şu an TR sabit. İki seçenek:

1. **A)** DisplayName = localization key (`item.bandage`); UI çözer.
2. **B)** Katalog `GetDisplayName(locale)` ile String Table’dan okur.

Öneri: **A** — ağ senkronunda kimlik (`Id`) taşınır, görünen ad istemicide çözülür.

### 6. MenuText kültürü

- `ToUpperTr` yalnızca `tr`/`az` için; diğer dillerde `CultureInfo.TextInfo.ToUpper`.
- `FormatPercent` / `FormatThousands`: locale `NumberFormatInfo` kullanılsın
  (TR: `%42,5` / `12.500`; EN: `42.5%` / `12,500`; DE: `42,5 %` / `12.500`).

### 7. Sahne bileşenleri

- Statik UI: `LocalizeStringEvent` + `Text` / TMP.
- Dinamik HUD: koddan `Loc.T` (her kare string üretmeyin; değişince güncelleyin).

### 8. CI / kalite kapısı

```bash
cd Tools/LocTool && npm run validate && npm run export
# isteğe bağlı: python3 Localization/scripts/validate_placeholders.py
```

Kurallar: tüm dillerde aynı placeholder kümesi; boş hücre yok; DE düğme metni ≤ 18 karakter uyarısı
(`reports/v2/validation.json`, `reports/METIN_UZUNLUGU_RISK_RAPORU.md`).
Dosya:satır eşlemesi: `UNITY_STRING_MAP_V2.md` (LocTool `extract`).

## Öncelik sırası (uygulama)

1. `pause.*`, `settings.*`, `menu.dialog.*` — oyuncu görünür menü
2. `notify.*`, `match.msg.*`, `loading.*` — HUD / akış
3. `role.*`, `rank.*`, `item.*`, `weapon.category.*` — katalog
4. `location.*`, `team.*` — harita / kill feed
5. Ana menü iskeleti tamamlanınca doğrudan Localization

## Riskler

| Risk | Azaltma |
|------|---------|
| String birleştirme unutulması | CI’da `Assets/**/*.cs` içinde Türkçe karakterli literal taraması |
| Arapça taşma | Geniş düğme + wrap; risk raporundaki `ar` satırları |
| Silah marka adları | Çevrilmez (SAR 9, MPT-76…) — tüm dillerde aynı |
| TSK rütbe karşılıkları | Sözlükteki EN yaklaşık rütbeler; resmi çeviri değildir |

## İlgili dosyalar

- `Localization/strings.csv`
- `Localization/KEY_NAMING.md`
- `Localization/ASKERI_TERIMLER_SOZLUGU.md`
- `Localization/UNITY_STRING_MAP_V2.md`
- `Tools/LocTool/README.md`
- `Localization/scripts/validate_placeholders.py`
- `Localization/reports/METIN_UZUNLUGU_RISK_RAPORU.md`
