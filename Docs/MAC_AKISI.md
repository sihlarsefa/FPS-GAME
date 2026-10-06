# HAREKÂT — Maç Akışı (Battle Royale tempo planı)

Hedef: 1 km x 1 km "Kuzgun Vadisi", 6 tim x 10 kişi (60 savaşan). Her saniye bir şey olsun, ama kimse anında "üçüncü taraf" olmasın.
Tüm değerler `MatchConfig` varsayılanlarıdır; sunucu otoritelidir (PlayerCommand/ShotRequest yolu değişmedi).

## Zaman çizelgesi (InMatch başlangıcından itibaren)

| Süre (sn) | Olay | Not |
|---|---|---|
| -T | Intikal (T-70 / Kirpi) | Her timin inişi yakınında garantili başlangıç kiti (`MatchFlowLoot.SpawnTeamStartKits`) |
| 0 | Maç başlar, bölge saati işler | İlk 90 sn: yağma + ilk temas penceresi |
| 90 | **İkmal #1 duyurusu** | T-70 sandık bırakır; harita işareti + telsiz; ölü zaman kırıcı |
| 120 | Faz 1 daralma başlar (740 → 400 m, 1 hp/sn) | İlk bekleme 120 sn |
| 130 | İkmal #1 yere iner | Duman; sandık kilitli |
| 138 | **İkmal #1 açılır** (yüksek kademe yağma) | 8 sn kilit: anında üçüncü taraf yok |
| 190 | Faz 1 biter; faz 2 bekleme (75 sn) | |
| 200 | İkmal #2 duyurusu | Sonraki güvenli çemberin içinde |
| 240 / 248 | İkmal #2 iner / açılır | |
| 265 | Faz 2 daralma (→ 270 m, 2 hp/sn, 55 sn) | |
| 310 / 350 / 358 | İkmal #3 duyuru / iner / açılır | |
| 320 | Faz 3 bekleme (60 sn) | |
| 380 | Faz 3 daralma (→ 170 m, 3.5 hp/sn, 45 sn) | |
| 420 / 460 / 468 | İkmal #4 duyuru / iner / açılır | Son sandık |
| 425 | Faz 4 bekleme (50 sn) | |
| 475 | Faz 4 daralma (→ 100 m, 5 hp/sn, 40 sn) | Arazi çıpaları devrede (hedef ≤ 280 m) |
| 515 | Faz 5 bekleme (40 sn) | |
| 555 | Faz 5 daralma (→ 55 m, 8 hp/sn, 35 sn) | |
| 590 | Faz 6 bekleme (30 sn) | Sonraki çember < 60 m: ikmal yok |
| 620 | Faz 6 daralma (→ 22 m, 12 hp/sn, 30 sn) | |
| 650 | Faz 7 bekleme (20 sn) | |
| 670 | Son daralma (→ 0 m, 18 hp/sn, 30 sn) | Bitiş zorlaması |
| 700 | Bölge tamam (Finished) | Toplam ~11,7 dk |

Topçu (telsizle, tim başına): bekleme 150 → **180 sn** (6 timde baraj spamını önler); 6 sn gecikme, 8 mermi, r=18 m.

## Ayarlar (MatchConfig)

| Alan | Varsayılan | Etki |
|---|---|---|
| `AirdropFirstSeconds` | 90 | İlk ikmal duyurusu |
| `AirdropIntervalSeconds` | 110 | İkmaller arası |
| `AirdropCount` | 4 | 0 = kapalı |
| `AirdropDescentSeconds` | 40 | Duyuru → iniş (yetişme süresi) |
| `AirdropOpenDelaySeconds` | 8 | İniş → açılış (anında üçüncü taraf engeli) |
| `AirdropMinZoneRadius` | 60 | Sonraki çember bundan küçükse ikmal iptal |
| `ArtilleryCooldownSeconds` | 180 | Tim başına topçu bekleme |

Bölge planı `MatchConfig.DefaultZonePhases()`: kenar hızı hiçbir fazda 5,5 m/sn'yi geçmez (koşan oyuncu yetişir), hasar fazla artar, bekleme kısalır (testle korunur).

## Yağma dağılımı (konum kademesi)

| Kademe | Konum | Dolu olma | Karakter |
|---|---|---|---|
| Low | tarla / yol | %38 | tabanca, mermi, sargı; seyrek |
| Medium | köy | %58 | SMG/5.56, 2. seviye zırh, aksesuar |
| High | kasaba / sanayi | %74 | 7.62, 3. seviye parçalar nadir |
| Military | askeri üs | %90 | 7.62, KNT-76/JNG-90/PMT-76, 3. seviye zırh, NVG |

- **Başlangıç kiti** (`LootSpawnService.RollStartKit`): üye başına yan silah + 2 mermi yığını + 2 sargı; her 3. üyeye ek ana silah (SAR109T / MPT-55 / SAR223 / Escort) + 2 mermi yığını. Takım inişinin 3–7 m çevresine bırakılır.
- **İkmal sandığı** (`RollSupplyCrate`): 1 yüksek kademe silah (MPT-76 / KNT-76 / SAR762MT; nadir PMT-76, JNG-90, MG3) + 2 mermi yığını, 3. seviye zırh + kask (garanti), MedKit, İlk Yardım, el bombası, 1 aksesuar, %50 3. seviye çanta.

## Karşılaşma temposu

- Ölü zaman < 90 sn: ilk olay (ikmal duyurusu) 90. sn; ardından zaman çizelgesinde hiçbir 60 sn aralık olaysız değil (daralma / ikmal / topçu).
- Anında üçüncü taraf yok: ikmal 40 sn paraşütle iner + 8 sn kilitli açılır; sandık sonraki çemberin **içinde**, merkezden yarıçapın %25–65'i uzakta (merkeze yürüyen timleri çatıştırır, final çemberini tek noktaya kilitlemez); iki sandık ≥ 120 m ayrı.
- Final çemberi arazi lehine: `ZoneService.SetAnchors(...)` ile tepe/kale/köy/geçit çıpaları verilir; hedef yarıçapı ≤ 280 m olan fazlar (faz 3+) aday merkezlerden çıpaya en iyi oturanı seçer. "Mevcut çemberin tamamen içinde" ve harita sınırı kuralları korunur. Çıpa yoksa eski rastgele seçim.

## Entegrasyon durumu

Yazıldı ve testli: `AirdropService` (Application), `AirdropEvent`, `ZoneAnchor`, `LootSpawnService.RollStartKit/RollSupplyCrate`, `MatchFlowLoot` (Infrastructure/Loot).
Bağlanması gereken (kapsam dışı): `GameCompositionRoot` (AirdropService oluştur + `GameTickCoordinator`'a ekle), harita yerleşimlerinden `ZoneAnchor` listesi, iniş sonrası `SpawnTeamStartKits`, `MatchFlowLoot.BindAirdropOpen`, sandık/paraşüt/duman görseli + HUD işareti + T-70 geçiş sesi, botların ikmale yönelmesi (BotDirector).
