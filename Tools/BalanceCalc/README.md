# HAREKÂT Denge Hesaplayıcısı

`WeaponCatalog.cs` ve `ItemCatalog.cs` dosyalarını ayrıştırarak silah öldürme süresi (TTK), vuruş sayısı, Monte Carlo isabet modeli, 1v1 düello ısı haritası ve otomatik denge önerileri üretir.

Yalnızca `Tools/BalanceCalc/` altına yazar. Unity `Assets/` değiştirilmez.

## Gereksinimler

- Python 3.10+ (yalnızca standart kütüphane)
- Katalog yolları (salt okunur):
  - `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
  - `Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs`
- Formül kaynağı: `DamageCalculator.cs`

## Çalıştırma

```bash
cd Tools/BalanceCalc
python3 generate.py
```

Seçenekler:

```bash
python3 generate.py --mc-trials 400 --duel-trials 150
python3 generate.py --skip-duel --skip-mc   # yalnızca deterministik TTK
```

## Çıktılar (`out/`)

| Dosya | Açıklama |
|---|---|
| `ttk.csv` | Tüm silah × mesafe × bölge × zırh/kask matrisi |
| `monte_carlo.csv` | Sekme/sapma dahil beklenen TTK |
| `duel_heatmap.csv` | 1v1 kazanma oranları |
| `denge_raporu.md` | Türkçe Markdown rapor + öneriler |
| `denge_raporu.html` | Chart.js grafikli statik rapor |
| `balance_ui.html` | Etkileşimli tek dosya arayüz (kopya) |
| `catalog.json` | Ayrıştırılmış katalog |
| `oneriler.json` | Otomatik öneri listesi |

Etkileşimli arayüz ayrıca: `ui/balance.html` (tek HTML+CSS+JS, CDN yok).

Tarayıcıda açın:

```bash
open ui/balance.html
# veya
open out/denge_raporu.html
```

## Hesap modeli

- **Can:** 100
- **Mesafe düşüşü:** `FalloffStart`’a kadar ×1; `FalloffEnd` ve ötesinde `MinDamageFactor`; arada doğrusal
- **Bölge:** gövde ×1, kafa × `HeadshotMultiplier`
- **Zırh:** `hasar × (1 − DamageReduction)`; emilen miktar dayanıklılıktan düşer (kırılınca etkisiz)
- **Gövde → yelek, kafa → kask** (Sv.0–3)
- **Mesafeler:** 10 / 50 / 100 / 200 / 300 m
- **TTK:** `(vuruş − 1) × fireInterval` (ilk atış t = 0)
- **Pompalı:** deterministik TTK’da tüm saçmalar isabet; Monte Carlo’da sapma var

Zırh değerleri (`ItemCatalog`):

| Seviye | Yelek dayanıklılık / azaltma | Kask dayanıklılık / azaltma |
|---|---|---|
| 1 | 200 / 30% | 80 / 30% |
| 2 | 220 / 40% | 150 / 40% |
| 3 | 250 / 55% | 230 / 55% |

## Etkileşimli arayüz sekmeleri

1. **Anlık TTK** — kaydırıcılarla mesafe / zırh / kask; anlık vuruş ve TTK
2. **Karşılaştır** — tüm silahlar aynı koşullarda
3. **Monte Carlo** — bloom + sapma ile beklenen TTK
4. **1v1 Düello** — seçilen silaha karşı ısı haritası
5. **Ne Olur?** — hasar/RPM/falloff değiştirip etkiyi görme
6. **Öneriler** — kategori medyanına göre otomatik denge notları

## Klasör yapısı

```
Tools/BalanceCalc/
  generate.py              # CLI giriş
  README.md
  balance_calc/
    parse_catalogs.py      # C# ayrıştırıcı
    damage.py              # TTK motoru
    monte_carlo.py
    duel.py
    recommendations.py
    report.py
  ui/
    balance.template.html  # şablon
    balance.html           # gömülü verili UI (üretilir)
  out/                     # raporlar
```

## Test

```bash
python3 -m unittest tests.test_smoke -v
```

## Notlar

- Katalog değişince `python3 generate.py` yeniden çalıştırın; UI içindeki gömülü JSON güncellenir.
- İnteraktif UI çevrimdışı çalışır (harici grafik CDN’si yok). Statik HTML rapor Chart.js CDN kullanır.
