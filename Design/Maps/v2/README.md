# HAREKÂT — Harita Tasarım Verisi v2

Yalnızca `Design/Maps/v2/`. Oyun koduna yazılmaz. Kaynak şema: `WorldTypes.cs` / `MapLayout`.

| Harita | Klasör | Tema | HalfSize | MaxHeight | WaterLevel |
|--------|--------|------|----------|-----------|------------|
| **Ayaz Geçidi** | [AyazGecidi/](AyazGecidi/) | Karlı yüksek dağ, geçit, kayak evi, radar | 500 | 220 | 12 |
| **Mavi Liman** | [MaviLiman/](MaviLiman/) | Kıyı kasabası, liman, fener, zeytinlik | 500 | 120 | 18 |
| Kuzgun Vadisi (gece) | [KuzgunVadisiGece/](KuzgunVadisiGece/) | Mevcut haritanın gece harekâtı konsepti | 512* | 160* | 18* |

\*Kuzgun yerleşimi Unity `CreateKuzgunVadisi` kaynağındadır; gece klasörü yalnızca varyant konseptidir.

## Dosya düzeni (her harita)

| Dosya | İçerik |
|-------|--------|
| `layout.json` | `MapLayout` alanları: Locations, Roads, Lakes, Rivers, Bridges, HalfSize, MaxHeight, WaterLevel, Seed, Name |
| `design.json` | Grid, intikal sektörleri, yağma sayıları, yüzey notu (oyun runtime’ına gitmez) |
| `pafta.svg` | 100 m grid, A–J / 1–10, lokasyon + sektör lejantı |
| `TaktikNotlar.md` | Lokasyon başına taktik, intikal, yağma, ses |
| `SesOrtami.md` | Ses ortamı listesi (uzatma) |

## Kuzgun Vadisi ile karşılaştırma

| Boyut | Kuzgun Vadisi | Ayaz Geçidi | Mavi Liman |
|-------|---------------|-------------|------------|
| Ölçek | 1024 m (HalfSize 512) | 1000 m (HalfSize 500, temiz 100 m grid) | aynı |
| Rölyef | Orta dağ, dere omurgası | Dik geçit, yüksek sırt, düşük su | Alçak kıyı, sınırda deniz diskleri |
| Görüş | Vadi + sırt dengesi | Açık kar / uzun radar hattı | Sokak yakın + sahil uzun |
| Örtü | Orman / kaya | Seyrek ağaç, kaya, yapı | Zeytinlik + taş teras + sokak |
| Askerî yağma | İleri Üs (tek) | Dağ İkmal Üssü (tek) | Liman Depoları (tek) |
| İntikal | 6 sektör (S1–S6) | 4 sektör (2 Kirpi + 2 T-70) | 4 sektör (2 Kirpi + 2 T-70) |
| Su riski | Dere + baraj göleti | Küçük buz göleti | Geniş deniz kesişimi + dere |

## Oynanış hedefleri

### Ayaz Geçidi
- Erken oyun: geçit boğazı trafiği + kayak evi ikmali.
- Orta: radar / karakol hakimiyeti; açık kar yüzeyi KN için değerli, savunmasız.
- Geç: tek Military çekirdek (Dağ İkmal) üç yaklaşımlı risk.
- Hareket: dik yamaç yavaşlatır; ana asfalt omurga kritik.

### Mavi Liman
- Erken: han → pazar / kasaba kısa rotasyon.
- Orta: liman depo baskını vs fener / karakol flanşı.
- Geç: zeytinlik ve teras üzerinden kıyıya sıkıştırma.
- Hareket: sokak yakın menzil; sahil yolu uzun görüş koridoru.

## Performans riskleri

| Risk | Ayaz | Mavi | Not |
|------|------|------|-----|
| Kar / beyaz yüzey | Yüksek | — | Albedo + silüet; işaret kontrastı, kar ayak izi şeması |
| Su yansıması / yüzey | Düşük (küçük göl) | Yüksek (deniz diskleri) | Mevcut göl primitive’leri; liman için ayrı üretici gerekir |
| Sis / görüş mesafesi | Orta–yüksek | Düşük–orta (deniz sisi) | LOD ve düşman silüeti testi |
| Yol eğimi | Dik sırt servis yolları | Alçak | `validate.mjs` IDS tahmini; Unity navmesh onayı şart |
| Ses / ayak izi | Kar yüzey örnekleri | Dalga + liman | Atmosfer kanalı ayrı ayar |

## Koordinat ve şema

- `Center.x` = dünya X (doğu), `Center.y` = dünya Z (kuzey) — Unity `Vector2` / `LocationSpec.CenterWorld`.
- Grid: sütun A–J batı→doğu, satır 1–10 kuzey→güney, hücre 100 m.
- `Kind` / `Tier` sayısal enum (`LocationKind`, `LootTier`).
- Yol/dere `Points` ≈ 8 m densify (`build.mjs`).

## Komutlar

```bash
cd Design/Maps/v2
npm run build      # layout.json, design.json, pafta.svg, TaktikNotlar.md
npm run validate  # çakışma, sınır, yol eğimi tahmini
```

## Gece harekâtı

Bkz. [KuzgunVadisiGece/README.md](KuzgunVadisiGece/README.md) — aydınlatma, gece görüş, ses konsepti.
