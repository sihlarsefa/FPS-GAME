# HAREKÂT — Oyun Tasarım Dokümanı (GDD)

**HAREKÂT**, Türk askeri temalı, 10 kişilik timlerle oynanan FPP (birinci şahıs) tim battle royale’dir.  
Kurgu: *harekât tatbikatı* — **Mavi** ve **Kırmızı** kuvvetler. Gerçek örgüt veya grup adı kullanılmaz.  
Arayüz dili: **Türkçe**. Harita: **Kuzgun Vadisi** (1024 × 1024 m).

Bu klasör yalnızca tasarım belgelerini içerir. Değerler `WeaponCatalog`, `ItemCatalog`, `RankCatalog`, `WeaponIds`, `MilitaryRank`, `WorldTypes` / `MapLayoutKuzgunVadisi` ve `MatchConfig` kaynaklarından alınmıştır.

---

## İçindekiler

### Ana bölümler

| # | Belge | Konu |
|---|--------|------|
| 1 | [01-vizyon-oyun-dongusu.md](01-vizyon-oyun-dongusu.md) | Vizyon, oyuncu fantazisi, maç döngüsü |
| 2 | [02-tim-yapisi-roller.md](02-tim-yapisi-roller.md) | Tim rolleri ve başlangıç teçhizatı |
| 3 | [03-rutbeler-komuta.md](03-rutbeler-komuta.md) | TSK rütbeleri, XP, komuta devri |
| 4 | [04-intikal.md](04-intikal.md) | T-70 helikopter / Kirpi intikal |
| 5 | [05-silahlar.md](05-silahlar.md) | Silah katalog değerleri, rol, artı/eksi |
| 6 | [06-techizat-envanter.md](06-techizat-envanter.md) | Mermi, tıbbi, zırh, çanta, bomba |
| 7 | [07-zone-fazlari.md](07-zone-fazlari.md) | Harekât alanı (mavi bölge) fazları |
| 8 | [08-topcu-destegi.md](08-topcu-destegi.md) | Telsizci topçu çağrısı |
| 9 | [09-kuzgun-vadisi.md](09-kuzgun-vadisi.md) | Harita bölgeleri ve taktik önem |
| 10 | [10-bot-ai.md](10-bot-ai.md) | Bot karar öncelikleri ve tim AI |
| 11 | [11-ses-gorsel.md](11-ses-gorsel.md) | Ses/görsel yönelim (özet) |
| 12 | [12-ilerleme.md](12-ilerleme.md) | Kariyer XP ve istatistikler |
| 13 | [13-gelecek-modlar.md](13-gelecek-modlar.md) | Gece, rehine, konvoy (fikir) |
| 14 | [14-online-ozet.md](14-online-ozet.md) | Sunucu otoriteli online özeti |
| 15 | [15-monetizasyon.md](15-monetizasyon.md) | Yalnız kozmetik; pay-to-win yok |

### Uzatma hedefleri

| # | Belge | Konu |
|---|--------|------|
| U1 | [16-silah-denge-gerekceleri.md](16-silah-denge-gerekceleri.md) | Silah denge gerekçeleri |
| U2 | [17-tim-taktikleri.md](17-tim-taktikleri.md) | Tim taktikleri el kitabı |
| U3 | [18-rol-yetenekleri.md](18-rol-yetenekleri.md) | Rol yetenekleri maliyet/fayda |
| U4 | [19-ses-tasarim.md](19-ses-tasarim.md) | Ses olayları ve karışım öncelikleri |
| U5 | [20-ui-ux-akislari.md](20-ui-ux-akislari.md) | Mermaid UI/UX akışları |
| U6 | [21-ilk-10-dk-tutorial.md](21-ilk-10-dk-tutorial.md) | İlk 10 dk + eğitim / poligon |

---

## Hızlı referans

| Özellik | Değer |
|---------|--------|
| Tim boyutu | 10 |
| Varsayılan tim sayısı | 4 (40 savaşçı) |
| Maç süresi hedefi | ~25 dk (`MatchDurationSeconds` 1500) |
| Zone fazları | 7 faz (~13 dk daralma planı) |
| Topçu bekleme | 150 sn |
| Intikal | T-70 (helikopter) veya Kirpi (zırhlı) |
| Kazanma koşulu | Son ayakta kalan tim |

## Kaynak kod eşlemesi

- Silahlar → `Assets/.../Catalogs/WeaponCatalog.cs`, `WeaponIds.cs`
- Eşyalar → `ItemCatalog.cs`, `LoadoutCatalog.cs`
- Rütbeler → `RankCatalog.cs`, `MilitaryRank.cs`
- Harita → `WorldTypes.cs`, `MapLayoutKuzgunVadisi.cs`
- Zone / topçu → `MatchConfig.cs`, `ZoneService.cs`, `ArtilleryService`
