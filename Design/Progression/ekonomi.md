# UZATMA — Ekonomi Dengesi (Sezon 1)

Kaynak formül: `RankCatalog.CalculateMatchXp` / GDD `12-ilerleme.md`  
`XP = kills×100 + headshots×25 + (teamCount−placement)×150 + (win ? 1000 : 0)`

Aynı XP hem kariyer `Experience` hem `SeasonXp`'e eklenir (`MatchResultService`). Para birimi yok; ekonomi = zaman ↔ kozmetik açılım.

## 1. Tipik maç XP

Varsayım: 8 tim, ortalama 25 dk maç.

| Senaryo | Kill | HS | Placement | Win | XP |
|---------|------|----|-----------|-----|----|
| Zayıf | 1 | 0 | 6 | hayır | 100 + 300 = **400** |
| Ortalama | 4 | 1 | 4 | hayır | 400+25+600 = **1025** |
| İyi | 7 | 2 | 2 | hayır | 700+50+900 = **1650** |
| Zafer | 6 | 2 | 1 | evet | 600+50+1050+1000 = **2700** |

**Sezon tasarım ortalaması:** ~**1200 XP / maç** (ortalama–iyi karışımı).

## 2. Sezon merdiveni vs süre

| Hafta | Gerekli SeasonXp (kümülatif) | ~Maç (1200 XP) | ~Saat (25 dk) | Ödül |
|------:|-----------------------------:|---------------:|--------------:|------|
| 1 | 500 | 1 | 0,4 | `armband_season1` |
| 2 | 1 500 | 2 | 0,8 | `camo_coast` |
| 3 | 3 000 | 3 | 1,3 | `skin_mpt55_coast` |
| 4 | 5 000 | 5 | 2,1 | `beret_steel` |
| 5 | 7 500 | 7 | 2,9 | `frame_season1` |
| 6 | 10 500 | 9 | 3,8 | `skin_jng90_snow` (erken) |
| 7 | 14 000 | 12 | 5,0 | `pose_binoculars` (erken) |
| 8 | 18 000 | 15 | 6,3 | `beret_frost` |
| 9 | 23 000 | 20 | 8,3 | `camo_mountain` (erken) |
| 10 | 30 000 | 25 | 10,4 | `pose_flag_plant` |

Bonus eşikler: 1 000 / 20 000 / 28 000 SeasonXp → ek kozmetik.

**Tam merdiven:** ~25 maç ≈ **10–11 saat** / 10 hafta → haftada ~1 saat (casual dostu).  
**Hardcore (zafer ağırlıklı ~2000 XP/maç):** ~15 maç ≈ **6–7 saat**.

## 3. Kariyer kozmetik (UnlockXp)

| Bant | UnlockXp | Örnek | ~Maç (1200) |
|------|----------|-------|-------------|
| Ücretsiz | 0 | `camo_standard`, `beret_green`, `pose_salute` | 0 |
| Erken | 1 500–5 000 | kol bantları, çöl/orman | 2–5 |
| Orta | 6 000–15 000 | silah kaplamaları, lacivert bere | 5–13 |
| Geç | 20 000–35 000 | kış/gece kamuflaj, altın çerçeve | 17–30 |
| Prestij | 50 000 | `beret_gold` | ~42 |

Kariyer XP sezonlar arasında birikir; sezon ödülleri erken/özel yol.

## 4. Beklenen sezon kazanımı (oyuncu segmenti)

| Segment | Maç/hafta | XP/hafta | 10 hf SeasonXp | Merdiven |
|---------|-----------|----------|----------------|----------|
| Casual | 2 | ~2 400 | ~24 000 | Hafta 9 civarı |
| Regular | 4 | ~4 800 | ~48 000 | Hafta 10 + bonus |
| Hardcore | 8 | ~12 000* | ~80 000+ | Tam + arşiv şansı |

\*Zafer oranı yüksek varsayımı.

## 5. Denge notları

- Ücretsiz hat anlamlı: her hafta en az bir kozmetik; ilk 2 hafta düşük eşik.
- Sezon erken açılımları (`skin_jng90_snow` vb.) kariyer UnlockXp’ten önce gelebilir; çift yol kasıtlı.
- Arşiv rozetleri (`frame_archive_s1_top10/100`) rekabetçi; güç vermez.
- Başarım `xpReward` alanları öneri; backend şu an yalnızca unlock yazar — Cursor entegrasyonunda opsiyonel bonus.

## 6. Yasak listesi (ekonomi)

Satılamaz / verilemez: hasar, şarjör, tepme, zırh, topçu cooldown, XP çarpanı, bot zorluğu düşürme (GDD `15-monetizasyon.md`).
