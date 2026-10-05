# 5. Silahlar

Kaynak: `WeaponCatalog`, `WeaponIds`. RPM = `60 / FireIntervalSeconds`.

## 5.1 Özet tablo

| Silah | ID | Kategori | Hasar | Şarjör | RPM / aralık | Doldurma | Menzil | HS× | Mermi |
|-------|-----|----------|-------|--------|--------------|----------|-------|-----|-------|
| SAR 9 | pistol_sar9 | Tabanca | 28 | 15 | ~375 (0.16s) | 1.5s | 120 | 2.0 | 9 mm |
| Canik TP9 | pistol_tp9 | Tabanca | 26 | 18 | ~400 (0.15s) | 1.6s | 120 | 2.0 | 9 mm |
| SAR 109T | smg_sar109t | Hafif mak. | 22 | 30 | 800 | 2.0s | 180 | 1.8 | 9 mm |
| MPT-55 | ar_mpt55 | Piyade | 26 | 30 | 750 | 2.3s | 450 | 2.0 | 5.56 |
| MPT-76 | ar_mpt76 | Piyade | 36 | 20 | 600 | 2.5s | 600 | 2.0 | 7.62 |
| G3A7 | ar_g3a7 | Piyade | 38 | 20 | 550 | 2.6s | 600 | 2.0 | 7.62 |
| KNT-76 | dmr_knt76 | Nişancı (DMR) | 52 | 10 | ~214 (0.28s) | 2.8s | 800 | 2.2 | 7.62 |
| JNG-90 | sr_jng90 | Keskin nişancı | 90 | 5 | ~43 (1.4s) | 3.4s | 1000 | 2.5 | 7.62 |
| PMT-76 | lmg_pmt76 | Makineli | 34 | 100 | 650 | 6.0s | 600 | 1.8 | 7.62 |
| Escort | sg_escort | Pompalı | 20×9 saçma | 7 | ~70 (0.85s) | 4.2s | 70 | 1.5 | 12 ga |

## 5.2 Ek ballistik / kullanım

| Silah | Namlu hızı | Düşüş (başla→bit) | Min hasar faktörü | Ağırlık | ADS zoom | Not |
|-------|------------|-------------------|-------------------|---------|----------|-----|
| SAR 9 | 360 | 20–70 | 0.55 | 1.0 | 1.15 | Düşük geri tepme |
| TP9 | 365 | 20–70 | 0.55 | 0.9 | 1.15 | Daha büyük şarjör |
| SAR 109T | 400 | 25–100 | 0.50 | 3.0 | 1.2 | Single/Burst/Auto, burst 3 |
| MPT-55 | 880 | 70–300 | 0.65 | 3.4 | 1.35 | Esnek orta mesafe |
| MPT-76 | 820 | 90–400 | 0.70 | 4.1 | 1.35 | Milli ana tüfek |
| G3A7 | 800 | 90–400 | 0.70 | 4.4 | 1.35 | Daha yavaş, daha sert |
| KNT-76 | 840 | 150–600 | 0.75 | 5.2 | **3×** | Scope |
| JNG-90 | 850 | 200–800 | 0.80 | 6.5 | **6×** | Bolt-action |
| PMT-76 | 830 | 90–400 | 0.70 | **11** | 1.3 | Sustained fire |
| Escort | 400 | 8–40 | 0.20 | 3.6 | 1.15 | PelletSpread 4.5° |

Uzuv çarpanı çoğu silahta 0.8 (JNG 0.75, SMG/Escort 0.85).

## 5.3 Rol, artı / eksi

### SAR 9
- **Rol:** Standart yan silah.  
- **+** Hızlı donanma, güvenilir HS.  
- **−** Menzil düşüşü sert; şarjör 15.

### Canik TP9
- **Rol:** Alternatif yan silah (Telsizci varsayılan).  
- **+** 18 mermi, biraz daha yumuşak tepme.  
- **−** Biraz daha düşük hasar.

### SAR 109T
- **Rol:** CQB / KN ikincil.  
- **+** Yüksek RPM, düşük tepme, çok mod.  
- **−** 9 mm menzil zayıflığı.

### MPT-55
- **Rol:** Genel amaçlı 5.56; sıhhiye/telsiz/piyade.  
- **+** 30 mermi, kontrollü otomatik.  
- **−** 7.62’ye göre düşük tek vuruş.

### MPT-76
- **Rol:** Komutan / piyade ana silahı.  
- **+** Güçlü hasar, iyi menzil.  
- **−** 20 mermi, daha ağır tepme.

### G3A7
- **Rol:** Bombacı; sert orta-uzun.  
- **+** En yüksek AR hasarı (38).  
- **−** En düşük AR RPM; ağır.

### KNT-76
- **Rol:** Yarı otomatik nişancı; yağmada güçlü.  
- **+** 3×, yüksek hasar, hızlı takip atışı.  
- **−** Kalça kötü; bloom yüksek.

### JNG-90
- **Rol:** Keskin nişancı birincil.  
- **+** 90 hasar, 2.5× kafa, 6×.  
- **−** Bolt, 5 mermi, yavaş doldurma.

### PMT-76
- **Rol:** Baskı / bastırma.  
- **+** 100 mermi, sürekli ateş.  
- **−** 6 sn doldurma, ağırlık 11, yavaş donanma.

### Escort
- **Rol:** Bina / köşe.  
- **+** Yakın mesafede yüksek burst (9 pellet).  
- **−** 40 m sonrası etkisiz; yavaş ateş.

## 5.4 Bot tercih kademesi (`ItemCatalog` Level)

1: SAR 9, TP9 → 2: SAR 109T, Escort → 3: MPT-55, G3A7 → 4: MPT-76, KNT-76, JNG-90, PMT-76.

Ayrıntılı denge: [16-silah-denge-gerekceleri.md](16-silah-denge-gerekceleri.md).
