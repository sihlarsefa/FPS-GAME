# HAREKÂT Silah Denge Raporu

Kaynak: `WeaponCatalog.cs` + `ItemCatalog.cs` + `DamageCalculator.cs` formülleri.

## Varsayımlar

- Maksimum can: **100**
- Mesafe düşüşü: `FalloffStart`→`FalloffEnd` doğrusal, min = `MinDamageFactor`
- Zırh: `hasar × (1 − DamageReduction)`; emilen kadar dayanıklılık düşer
- Gövde → yelek; kafa → kask
- Pompalı: tüm saçmalar isabet (deterministik TTK); Monte Carlo'da sapma var
- TTK = `(vuruş − 1) × fireInterval` (ilk atış t=0)

## Silah özeti

| Silah | Kategori | Hasar | RPM | Şarjör | Falloff | HS× | Saçma |
|---|---|---:|---:|---:|---|---:|---:|
| SAR 9 | Tabanca | 28 | 375 | 15 | 20–70 m (×0.55) | 2 | 1 |
| Canik TP9 | Tabanca | 26 | 400 | 18 | 20–70 m (×0.55) | 2 | 1 |
| SAR 109T | Hafif Makineli | 22 | 800 | 30 | 25–100 m (×0.5) | 1.8 | 1 |
| MPT-55 | Piyade Tüfeği | 26 | 750 | 30 | 70–300 m (×0.65) | 2 | 1 |
| MPT-76 | Piyade Tüfeği | 36 | 600 | 20 | 90–400 m (×0.7) | 2 | 1 |
| G3A7 | Piyade Tüfeği | 38 | 550 | 20 | 90–400 m (×0.7) | 2 | 1 |
| KNT-76 | Nişancı Tüfeği | 52 | 214 | 10 | 150–600 m (×0.75) | 2.2 | 1 |
| JNG-90 | Keskin Nişancı | 90 | 43 | 5 | 200–800 m (×0.8) | 2.5 | 1 |
| PMT-76 | Makineli Tüfek | 34 | 650 | 100 | 90–400 m (×0.7) | 1.8 | 1 |
| Escort | Pompalı | 20 | 71 | 7 | 8–40 m (×0.2) | 1.5 | 9 |

## Özet TTK — 50 m gövde, Zırh Sv.2 / Kask Sv.2

| Silah | Vuruş | TTK (s) | İlk hasar | DPS |
|---|---:|---:|---:|---:|
| PMT-76 | 5 | 0.369 | 20.4 | 270.8 |
| MPT-76 | 5 | 0.400 | 21.6 | 250.0 |
| G3A7 | 5 | 0.436 | 22.8 | 229.2 |
| MPT-55 | 7 | 0.480 | 15.6 | 208.3 |
| SAR 109T | 10 | 0.675 | 11.0 | 148.1 |
| KNT-76 | 4 | 0.840 | 31.2 | 119.0 |
| Canik TP9 | 9 | 1.200 | 11.4 | 83.3 |
| SAR 9 | 9 | 1.280 | 12.3 | 78.1 |
| JNG-90 | 2 | 1.400 | 54.0 | 71.4 |
| Escort | 5 | 3.400 | 21.6 | 29.4 |

## Mesafe karşılaştırması — gövde, zırh yok

| Silah | 10 m | 50 m | 100 m | 200 m | 300 m |
|---|---:|---:|---:|---:|---:|
| SAR 9 | 0.48s/4v | 0.64s/5v | 0.96s/7v | 0.96s/7v | 0.96s/7v |
| Canik TP9 | 0.45s/4v | 0.75s/6v | 0.90s/7v | 0.90s/7v | 0.90s/7v |
| SAR 109T | 0.30s/5v | 0.38s/6v | 0.68s/10v | 0.68s/10v | 0.68s/10v |
| MPT-55 | 0.24s/4v | 0.24s/4v | 0.32s/5v | 0.32s/5v | 0.40s/6v |
| MPT-76 | 0.20s/3v | 0.20s/3v | 0.20s/3v | 0.30s/4v | 0.30s/4v |
| G3A7 | 0.22s/3v | 0.22s/3v | 0.22s/3v | 0.22s/3v | 0.33s/4v |
| KNT-76 | 0.28s/2v | 0.28s/2v | 0.28s/2v | 0.28s/2v | 0.56s/3v |
| JNG-90 | 1.40s/2v | 1.40s/2v | 1.40s/2v | 1.40s/2v | 1.40s/2v |
| PMT-76 | 0.18s/3v | 0.18s/3v | 0.18s/3v | 0.28s/4v | 0.28s/4v |
| Escort | 0.00s/1v | 1.70s/3v | 1.70s/3v | 1.70s/3v | 1.70s/3v |

## Denge önerileri

### 🟡 Gövde TTK’da DMR’dan yavaş — JNG-90

**Kategori:** Keskin Nişancı · **Seviye:** uyarı

**Gerekçe:** JNG-90 gövde TTK 1.400s vs KNT-76 0.840s (bolt-action aralığı 1.4s).

**Öneri:** Kasıtlıysa sorun yok (kafa odaklı). Gövde rekabeti isteniyorsa hasarı 95–100’e çıkarıp 2-shot’ı Sv.2’de koruyun.

### 🔵 Sv.3 kask vuruş ekliyor — Canik TP9

**Kategori:** Tabanca · **Seviye:** bilgi

**Gerekçe:** Zırhsız kafa 3 vuruş → Sv.3 kask 6 vuruş (TTK 0.30s → 0.75s, hasar 38 → 17).

**Öneri:** Kask ilerleme ödülü çalışıyor; Sv.3’ü end-game hedefi olarak tutun.

### 🔵 50 m’de pompalı yavaş (beklenen) — Escort

**Kategori:** Pompalı · **Seviye:** bilgi

**Gerekçe:** Escort TTK=3.400s — falloff 8–40 m tasarımı nedeniyle 50 m’de zayıf.

**Öneri:** Yakın dövüş rolü korunuyor; CQB mesafesinde (≤15 m) kontrol edin.

### 🔵 Pompalı mesafe düşüşü sağlıklı — Escort

**Kategori:** Pompalı · **Seviye:** bilgi

**Gerekçe:** 10 m: 1 vuruş / 0.00s → 300 m: 3 vuruş / 1.70s (ilk hasar 171 → 36).

**Öneri:** Yakın muharebe rolü korunuyor; değişiklik gerekmez.

### 🔵 Sv.3 kask vuruş ekliyor — Escort

**Kategori:** Pompalı · **Seviye:** bilgi

**Gerekçe:** Zırhsız kafa 2 vuruş → Sv.3 kask 5 vuruş (TTK 0.85s → 3.40s, hasar 54 → 24).

**Öneri:** Kask ilerleme ödülü çalışıyor; Sv.3’ü end-game hedefi olarak tutun.

### 🔵 Menzil düşüşü makul — G3A7

**Kategori:** Piyade Tüfeği · **Seviye:** bilgi

**Gerekçe:** 10 m 3v/0.22s → 300 m 4v/0.33s (hasar 38.0 → 30.3).

**Öneri:** Mevcut falloff eğrisi orta menzil rolüyle uyumlu.

### 🔵 Sv.3 kask vuruş ekliyor — G3A7

**Kategori:** Piyade Tüfeği · **Seviye:** bilgi

**Gerekçe:** Zırhsız kafa 2 vuruş → Sv.3 kask 3 vuruş (TTK 0.11s → 0.22s, hasar 76 → 34).

**Öneri:** Kask ilerleme ödülü çalışıyor; Sv.3’ü end-game hedefi olarak tutun.

### 🔵 Tek kafa — keskin nişancı rolü — JNG-90

**Kategori:** Keskin Nişancı · **Seviye:** bilgi

**Gerekçe:** Zırhsız kafa tek vuruş (225 hasar). Gövde 2 vuruş / 1.40s.

**Öneri:** Bolt-action temposu (1.4 s) ile dengelenmiş; Sv.3 kaskta 2. vuruşa çıkıyor mu kontrol edin.

### 🔵 Menzil düşüşü makul — KNT-76

**Kategori:** Nişancı Tüfeği · **Seviye:** bilgi

**Gerekçe:** 10 m 2v/0.28s → 300 m 3v/0.56s (hasar 52.0 → 47.7).

**Öneri:** Mevcut falloff eğrisi orta menzil rolüyle uyumlu.

### 🔵 Sv.3 kask vuruş ekliyor — KNT-76

**Kategori:** Nişancı Tüfeği · **Seviye:** bilgi

**Gerekçe:** Zırhsız kafa 1 vuruş → Sv.3 kask 2 vuruş (TTK 0.00s → 0.28s, hasar 114 → 51).

**Öneri:** Kask ilerleme ödülü çalışıyor; Sv.3’ü end-game hedefi olarak tutun.

### 🔵 AR ailesi TTK ayrışması — MPT-55

**Kategori:** Piyade Tüfeği · **Seviye:** bilgi

**Gerekçe:** En hızlı AR MPT-76 (0.400s) vs MPT-55 (0.480s). 7.62’ler daha az vuruş, 5.56 daha yüksek RPM ile telafi ediyor.

**Öneri:** MPT-55’in vuruş sayısını 1 düşürmek için hasarı ~18–20 bandına yaklaştırmayın; mevcut ayrım rol farkını koruyor. İsterseniz MPT-55 MinDamageFactor’ü 0.70 yaparak uzak mesafeyi güçlendirin.

### 🔵 Menzil düşüşü makul — MPT-55

**Kategori:** Piyade Tüfeği · **Seviye:** bilgi

**Gerekçe:** 10 m 4v/0.24s → 300 m 6v/0.40s (hasar 26.0 → 16.9).

**Öneri:** Mevcut falloff eğrisi orta menzil rolüyle uyumlu.

### 🔵 Sv.3 kask vuruş ekliyor — MPT-55

**Kategori:** Piyade Tüfeği · **Seviye:** bilgi

**Gerekçe:** Zırhsız kafa 2 vuruş → Sv.3 kask 5 vuruş (TTK 0.08s → 0.32s, hasar 52 → 23).

**Öneri:** Kask ilerleme ödülü çalışıyor; Sv.3’ü end-game hedefi olarak tutun.

### 🔵 Menzil düşüşü makul — MPT-76

**Kategori:** Piyade Tüfeği · **Seviye:** bilgi

**Gerekçe:** 10 m 3v/0.20s → 300 m 4v/0.30s (hasar 36.0 → 28.7).

**Öneri:** Mevcut falloff eğrisi orta menzil rolüyle uyumlu.

### 🔵 Sv.3 kask vuruş ekliyor — MPT-76

**Kategori:** Piyade Tüfeği · **Seviye:** bilgi

**Gerekçe:** Zırhsız kafa 2 vuruş → Sv.3 kask 4 vuruş (TTK 0.10s → 0.30s, hasar 72 → 32).

**Öneri:** Kask ilerleme ödülü çalışıyor; Sv.3’ü end-game hedefi olarak tutun.

### 🔵 Menzil düşüşü makul — PMT-76

**Kategori:** Makineli Tüfek · **Seviye:** bilgi

**Gerekçe:** 10 m 3v/0.18s → 300 m 4v/0.28s (hasar 34.0 → 27.1).

**Öneri:** Mevcut falloff eğrisi orta menzil rolüyle uyumlu.

### 🔵 Sv.3 kask vuruş ekliyor — PMT-76

**Kategori:** Makineli Tüfek · **Seviye:** bilgi

**Gerekçe:** Zırhsız kafa 2 vuruş → Sv.3 kask 4 vuruş (TTK 0.09s → 0.28s, hasar 61 → 28).

**Öneri:** Kask ilerleme ödülü çalışıyor; Sv.3’ü end-game hedefi olarak tutun.

### 🔵 SMG tabancadan hızlı (beklenen) — SAR 109T

**Kategori:** Hafif Makineli · **Seviye:** bilgi

**Gerekçe:** SAR 109T TTK 0.675s < Canik TP9 1.200s (50 m Sv.2).

**Öneri:** Tabancayı yan silah olarak tutun; SMG CQB birincil kalmalı.

### 🔵 Sv.3 kask vuruş ekliyor — SAR 109T

**Kategori:** Hafif Makineli · **Seviye:** bilgi

**Gerekçe:** Zırhsız kafa 4 vuruş → Sv.3 kask 7 vuruş (TTK 0.23s → 0.45s, hasar 33 → 15).

**Öneri:** Kask ilerleme ödülü çalışıyor; Sv.3’ü end-game hedefi olarak tutun.

### 🔵 Sv.3 kask vuruş ekliyor — SAR 9

**Kategori:** Tabanca · **Seviye:** bilgi

**Gerekçe:** Zırhsız kafa 3 vuruş → Sv.3 kask 6 vuruş (TTK 0.32s → 0.80s, hasar 41 → 18).

**Öneri:** Kask ilerleme ödülü çalışıyor; Sv.3’ü end-game hedefi olarak tutun.

## Monte Carlo (Sv.2 zırh, ADS)

| Silah | Mesafe | Bölge | İsabet | E[TTK] | P50 | P90 |
|---|---:|---|---:|---:|---:|---:|
| SAR 9 | 50 | gövde | 10% | 21.282 | 20.940 | 27.620 |
| SAR 9 | 100 | gövde | 2% | inf | inf | inf |
| SAR 9 | 200 | gövde | 1% | inf | inf | inf |
| Canik TP9 | 50 | gövde | 12% | 18.176 | 17.650 | 25.800 |
| Canik TP9 | 100 | gövde | 3% | inf | inf | inf |
| Canik TP9 | 200 | gövde | 1% | inf | inf | inf |
| SAR 109T | 50 | gövde | 12% | 10.736 | 10.150 | 13.650 |
| SAR 109T | 100 | gövde | 2% | inf | inf | inf |
| SAR 109T | 200 | gövde | 1% | inf | inf | inf |
| MPT-55 | 50 | gövde | 26% | 4.254 | 4.780 | 5.340 |
| MPT-55 | 100 | gövde | 5% | 13.228 | 14.340 | 15.700 |
| MPT-55 | 200 | gövde | 1% | inf | inf | inf |
| MPT-76 | 50 | gövde | 42% | 2.568 | 1.600 | 4.600 |
| MPT-76 | 100 | gövde | 8% | 14.555 | 13.900 | 22.500 |
| MPT-76 | 200 | gövde | 1% | 24.000 | 24.000 | 24.000 |
| G3A7 | 50 | gövde | 35% | 3.621 | 4.782 | 5.327 |
| G3A7 | 100 | gövde | 7% | 16.640 | 15.000 | 24.236 |
| G3A7 | 200 | gövde | 1% | 21.954 | 19.236 | 24.673 |
| KNT-76 | 50 | gövde | 100% | 0.840 | 0.840 | 0.840 |
| KNT-76 | 100 | gövde | 91% | 1.048 | 0.840 | 1.400 |
| KNT-76 | 200 | gövde | 24% | 10.377 | 11.200 | 17.640 |
| JNG-90 | 50 | gövde | 100% | 1.400 | 1.400 | 1.400 |
| JNG-90 | 100 | gövde | 100% | 1.400 | 1.400 | 1.400 |
| JNG-90 | 200 | gövde | 100% | 1.400 | 1.400 | 1.400 |
| PMT-76 | 50 | gövde | 7% | 11.281 | 15.231 | 16.246 |
| PMT-76 | 100 | gövde | 1% | 16.000 | 15.969 | 16.338 |
| PMT-76 | 200 | gövde | 0% | inf | inf | inf |
| Escort | 50 | gövde | 12% | inf | inf | inf |
| Escort | 100 | gövde | 3% | inf | inf | inf |
| Escort | 200 | gövde | 1% | inf | inf | inf |

## 1v1 düello örnekleri (Sv.2 gövde)

| A | B | Mesafe | A kazanma |
|---|---|---:|---:|
| SAR 9 | Canik TP9 | 100 | 0% |
| SAR 9 | Canik TP9 | 200 | 0% |
| SAR 9 | SAR 109T | 10 | 0% |
| SAR 9 | SAR 109T | 100 | 0% |
| SAR 9 | SAR 109T | 200 | 0% |
| SAR 9 | MPT-55 | 10 | 0% |
| SAR 9 | MPT-55 | 100 | 0% |
| SAR 9 | MPT-55 | 200 | 0% |
| SAR 9 | MPT-76 | 10 | 0% |
| SAR 9 | MPT-76 | 50 | 0% |
| SAR 9 | MPT-76 | 100 | 0% |
| SAR 9 | MPT-76 | 200 | 0% |
| SAR 9 | G3A7 | 10 | 0% |
| SAR 9 | G3A7 | 50 | 0% |
| SAR 9 | G3A7 | 100 | 0% |

## Ne olur? örnekleri

- **MPT-55** `{'damage': 24.0}` → TTK 0.480s → 0.480s (Δ +0.000s, vuruş +0)
- **G3A7** `{'fire_interval_seconds': 0.12}` → TTK 0.436s → 0.480s (Δ +0.044s, vuruş +0)
- **Escort** `{'min_damage_factor': 0.35}` → TTK 3.400s → 1.700s (Δ -1.700s, vuruş -2)

---
*Tools/BalanceCalc ile üretildi.*