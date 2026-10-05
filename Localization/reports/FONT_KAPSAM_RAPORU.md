# Font kapsamı — LegacyRuntime (UZATMA C2-5)

Üretim: `Tools/LocTool` → `npm run font` → `reports/v2/font-coverage.json`.

## Durum

Unity 6 Hub kurulumunda `LegacyRuntime.ttf` ayrı dosya olarak **yok** (gömülü kaynak). LocTool bu yüzden iki mod sunar:

1. **Heuristic** (varsayılan): Latin/Türkçe güvenli; Arapça + AZ `Ə/ə` + em dash `—` (U+2014) eksik varsayılır.
2. **Exact** (`--font path.ttf`): SFNT cmap format 4/12 okuyup glif varlığını doğrular.

## Envanter (strings.csv, 5 dil)

| Metrik | Değer |
|--------|-------|
| Benzersiz karakter (boşluksuz) | 145 |
| Heuristic “likely missing” | 48 |
| Arapça blok | 45 |
| Azerbaycan Ə/ə | 2 |
| Diğer (em dash —) | 1 |
| Türkçe ek Latin (çğıöşü…) | 12 — LegacyRuntime’da genelde var |

## Proxy ölçüm (Inter-Regular.otf, Unity TMP örneği)

Latin odaklı yüz: **45 Arapça karakter eksik**; Türkçe + AZ schwa Inter’da mevcut. Bu, LegacyRuntime sınıfı Latin fontların Arapça HUD için yetmediğini doğrular.

```bash
npm run font -- --font "/path/to/Inter-Regular.otf"
# missing ≈ 45 (Arabic)
```

macOS `Arial.ttf` tüm 145 karakteri kapsar; yine de **RTL shaping** ve Unity Legacy Text sınırları ayrı konudur — Arial varlığı ship kararı değildir.

## LegacyRuntime’da yok / riskli

| Karakter / küme | Kod | Etki |
|-----------------|-----|------|
| Arapça harfler ve noktalama (`ء`…`ي`, `،؛؟`) | U+0600–U+06FF | `ar` locale okunaksız |
| `Ə` `ə` | U+018F / U+0259 | AZ metinlerde kutu / fallback |
| `—` (em dash) | U+2014 | Bazı bitmap atlaslarda yok; `-` ile değiştirilebilir |

## Öneri

| Locale | Font |
|--------|------|
| `tr`, `en`, `de` | TMP + Noto Sans / proje HUD fontu |
| `az` | Aynı + `Əə` içeren yüz (Noto Sans) |
| `ar` | Noto Naskh Arabic (veya eşdeğeri) + TMP fallback zinciri |

**Ship kuralı:** `ar` ve tercihen `az` için LegacyRuntime yasak. CI: `npm run font` heuristic missing > 0 iken release notuna yazılır; exact font yolu sağlandığında sıfır missing hedeflenir.
