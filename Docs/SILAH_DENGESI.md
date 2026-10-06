# Silah Dengesi (P2) — nihai tablo

Kaynak: `WeaponCatalog.cs`, `PenetrationRules.cs` (zırh sınıfı, balistik, elleme), `DamageCalculator.cs`, `RecoilPattern.cs`, `WeaponRuntimeService.cs`, `AttachmentCatalog.cs`.
Sağlık 100. Silah kimlikleri ve katalog sırası (ağ indeksi) değişmedi. Tüm sayılar `SilahDengeTests` ile kilitli.

## 1. Silah tablosu

Atış/dk = RPM. Atış-öldür (AÖ) = gövdeye zırhsız isabetli atış sayısı (10 m); AÖ Sv.3 = Sv.3 yelek (indirim 0.55) ile; Kafa = kafa AÖ (10 m). SÖ = ilk mermiden ölüme saniye, (AÖ-1) × aralık.

| Silah | Kalibre | Hasar | RPM | Şarjör | Namlu hızı m/s | Düşüş başlar→biter (m) / min | Kafa x | AÖ | SÖ s | AÖ Sv.3 | SÖ Sv.3 s | Kafa AÖ | ADS s | Ağırlık kg | Koşu→ateş s |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| SAR 9 | 9x19 | 28 | 375 | 15 | 360 | 20→70 / .55 | 2.0 | 4 | 0.48 | 8 | 1.12 | 2 | 0.15 | 1.0 | 0.15 |
| Canik TP9 | 9x19 | 26 | 400 | 18 | 365 | 20→70 / .55 | 2.0 | 4 | 0.45 | 9 | 1.20 | 2 | 0.15 | 0.9 | 0.15 |
| Canik METE SFT | 9x19 | 27 | 429 | 17 | 370 | 22→72 / .55 | 2.0 | 4 | 0.42 | 9 | 1.12 | 2 | 0.15 | 0.95 | 0.15 |
| SAR 109T | 9x19 | 22 | 800 | 30 | 400 | 25→100 / .50 | 1.8 | 5 | 0.30 | 10 | 0.67 | 3 | 0.22 | 3.0 | 0.21 |
| MPT-55 | 5.56x45 | 26 | 750 | 30 | 880 | 80→300 / .65 | 2.0 | 4 | 0.24 | 7 | 0.48 | 2 | 0.23 | 3.4 | 0.22 |
| SAR 223 | 5.56x45 | 24 | 850 | 30 | 930 | 70→290 / .65 | 2.0 | 5 | 0.28 | 8 | 0.49 | 3 | 0.22 | 3.3 | 0.22 |
| MPT-76 | 7.62x51 | 36 | 600 | 20 | 820 | 100→400 / .70 | 2.0 | **3** | 0.20 | 5 | 0.40 | 2 | 0.25 | 4.1 | 0.24 |
| MPT-76K | 7.62x51 | 34 | 700 | 20 | 740 | 60→260 / .60 | 2.0 | 3 | 0.17 | 5 | 0.34 | 2 | 0.23 | 3.5 | 0.22 |
| G3A7 | 7.62x51 | 38 | 550 | 20 | 800 | 110→400 / .70 | 2.0 | 3 | 0.22 | 5 | 0.44 | 2 | 0.26 | 4.4 | 0.25 |
| KNT-76 (DMR 3x) | 7.62x51 | 52 | 176 | 10 | 850 | 160→600 / .75 | 2.2 | 2 | 0.34 | 4 | 1.02 | 1 | 0.32 | 5.2 | 0.28 |
| SAR 762 MT (DMR 2.5x) | 7.62x51 | 46 | 273 | 15 | 840 | 140→560 / .75 | 2.2 | 3 | 0.44 | 4 | 0.66 | 1 | 0.32 | 5.0 | 0.27 |
| JNG-90 (6x, sürgülü) | .338 sınıfı* | 90 | 43 | 5 | 915 | 250→800 / .80 | 2.5 | 2 | 1.40 | 2 | 1.40 | 1 | 0.37 | 6.5 | 0.32 |
| PMT-76 (LMG) | 7.62x51 | 34 | 650 | 100 | 830 | 80→400 / .70 | 1.8 | 3 | 0.18 | 5 | 0.37 | 2 | 0.47 | 11.0 | 0.45 |
| MG3 (LMG) | 7.62x51 | 21 | 1100 | 120 | 820 | 80→360 / .65 | 1.7 | 5 | 0.22 | 8 | 0.38 | 3 | 0.49 | 11.5 | 0.46 |
| Escort (pompalı) | 12 ga | 20 x9 pelet | 71 | 7 | 400 | 6→30 / .12 | 1.5 | 1 (tüm pelet) | — | 2 | 0.85 | 1 | 0.23 | 3.6 | 0.23 |
| Escort Magnum | 12 ga | 11 x8 pelet | 133 | 6 | 400 | 6→28 / .12 | 1.5 | 2 | 0.45 | 4 | 1.35 | 1 | 0.24 | 3.9 | 0.24 |

\* JNG-90: oyunda `AmmoType.Mm762` (enum'a .338 eklenmedi); hız/hasar .338 Lapua sınıfı (915 m/s). Ağ/envanter uyumu için kalibre enum'u aynı kaldı.

Hedef bantları (testte): tabanca AÖ 4–5, SMG/piyade/LMG 3–5, DMR 2–3, keskin nişancı 1–2, pompalı 1–2. **MPT-76: <100 m'de gövdeye 3 isabet** (400 m'de 4).

Pompalı notu: AÖ tüm peletlerin isabet ettiği varsayımıyla; pelet hasarı 6 m'den sonra düşer, 30 m'de %12. Gerçekte 4.5° yayılımla ~8 m'de 7/9 pelet tutar → öldürme mesafesi ~8–9 m.

## 2. Vücut bölgesi çarpanları

Gövde x1.0 · Kafa: silaha özgü (1.5 pompalı … 2.5 keskin nişancı) · Kol/Bacak: silaha özgü (0.75–0.85). Kafa vuruşu: DMR ve JNG-90 zırhsız tek atış; JNG-90 Sv.3 kaskla da tek atış.

## 3. Hasar düşüşü

Doğrusal: `FalloffStart`'a dek 1.0, `FalloffEnd` ve ötesinde `MinDamageFactor` (tablodaki sütun). Tabancalar/SMG kısa menzilli, DMR/JNG uzun menzilde neredeyse tam hasar.

## 4. Zırh sınıfı vs kalibre

Zırhın `DamageReduction` değeri kalibreye göre ölçeklenir: `etkinlik = clamp(1.2 − 0.6 × KalibreGücü / SeviyeDirenci, 0.4, 1)`.
Kalibre gücü: 7.62 = 1.0 · 5.56 = 0.7 · 9 mm = 0.4 · 12 ga = 0.18. Seviye direnci: Sv.1 = 0.6, Sv.2 = 0.85, Sv.3 = 1.1. Seviye bilinmiyorsa (≤0) etkinlik 1.

| Kalibre | Sv.1 | Sv.2 | Sv.3 |
|---|---|---|---|
| 7.62x51 | 0.40 | 0.49 | 0.65 |
| 5.56x45 | 0.50 | 0.71 | 0.82 |
| 9x19 | 0.80 | 0.92 | 0.98 |
| 12 ga | 1.00 | 1.00 | 1.00 |

Sonuç: Sv.3 yelek 9 mm'yi neredeyse tamamen durdurur (SAR 109T 10 atış ister), 7.62'ye karşı sadece 2 atış ekler (MPT-76: 3→5). Yelek/kask indirimleri (`ItemCatalog`: 0.30/0.40/0.55) değişmedi.

## 5. Balistik (kalibre başına)

Üstel sürükleme `v(x) = v0·e^(−k x)`, düşüş `½ g t²` (sıfırlamasız).

| Kalibre | k (1/m) | 100 m uçuş (ms) | 300 m uçuş (ms) | 300 m düşüş (m) |
|---|---|---|---|---|
| 7.62x51 @820 | 0.00071 | ~125 | ~408 | ~0.82 |
| 5.56x45 @880 | 0.00085 | ~114 | ~388 | ~0.74 |
| 9x19 @360 | 0.0022 | ~300 | — | 100 m'de ~0.8 m |
| 12 ga @400 | 0.0040 | ~270 | — | — |

## 6. Elleme süreleri

`ADS = 0.12 + 0.032 × kg (+0.04 dürbünlü)`; `koşu→ateş = 0.12 + 0.03 × kg`. Katalog ADS değerleri formüle ±0.03 içinde. Eklenti ağırlığı iki süreye de eklenir.

## 7. Sekme

Her silahın `RecoilPattern` tohumundan **deterministik** deseni vardır (20 atış; ilk 10 öğrenilebilir): ilk atış güçlü yukarı (x1.25), 2–4 x1.1, sonra yatışır; yatayda silaha özgü sağa/sola eğilim + sinüs salınımı. Üstüne küçük rastgele bileşen: dikey ±%6, yatay ±%18. Desen dışı atışlar eskisi gibi rastgele. Toparlanma: `RecoilPattern.RecoveryDegPerSecond(RecoilRecovery, birikmişPitch) = RecoilRecovery × (3 + 0.6 × birikmiş)` derece/sn.

## 8. Eklenti ödünleşimleri

| Eklenti | Artı | Eksi |
|---|---|---|
| Kırmızı Nokta | 1.5x, ADS yayılım x0.9 | +0.1 kg |
| 2x Dürbün | ADS yayılım x0.85 | +0.3 kg, ADS süresi x1.08 |
| 4x Dürbün | ADS yayılım x0.80 | +0.5 kg, ADS süresi x1.18, kalça yayılımı x1.1 |
| Susturucu | Sekme x0.9, sessiz, iz yok | +0.4 kg, namlu hızı x0.94 (daha çok düşüş) |
| Uzatılmış Şarjör | Şarjör x1.4 | +0.3 kg, şarjör değiştirme x1.12 |
| Dikey Tutamak | Sekme x0.85, kalça yayılımı x0.9 | +0.25 kg, ADS x1.05 |
| Nişancı Dipçiği | Sekme x0.88, ADS yayılım x0.9 | +0.45 kg |

## 9. Entegrasyon bekleyenler

Bkz. görev raporu: `PlayerWeaponHandler` ADS'te `WeaponRuntimeService.AdsTime`, koşu bırakılınca `BeginSprintRecovery()`, kamera toparlanmasında `RecoilPattern.RecoveryDegPerSecond`; `BallisticsSystem` `MuzzleVelocity` (eklentili) ve `BallisticsMath.DragPerMeter` kullanımı.
