# 2. Tim Yapısı ve Roller

Kaynak: `TeamRole`, `LoadoutCatalog` (SquadSize = 10).

## 2.1 Slot dağılımı

| Slot | Rol | Kısa kod | Görevin özü |
|------|-----|----------|-------------|
| 0 | Tim Komutanı | KMT | Emir, yön, MPT-76 |
| 1 | Keskin Nişancı | KN | Uzun mesafe, JNG-90 |
| 2 | Makineli Tüfekçi | MAK | Baskı ateşi, PMT-76 |
| 3 | Sıhhiyeci | SHH | Tıbbi stok, MPT-55 |
| 4 | Telsizci | TEL | Topçu çağrısı, MPT-55 |
| 5 | Bombacı | BMB | El bombası, G3A7 |
| 6–9 | Piyade | PYD | Ana muharebe hattı |

## 2.2 Rol kartları

### Tim Komutanı
- **Silah:** MPT-76 + SAR 9  
- **Zırh:** Yelek 2, Kask 2, Çanta 2  
- **Stok:** 150×7.62, 30×9mm, 3 sargı, 1 ilk yardım, 1 el bombası, 2 sis  
- **Oynanış:** Emir verir (`Follow`, `HoldPosition`, `Attack`, `Regroup`); harita okur; risk alır ama hayatta kalır.

### Keskin Nişancı
- **Silah:** JNG-90 + SAR 109T  
- **Zırh:** Yelek 1, Kask 2, Çanta 1  
- **Stok:** 120×7.62, 120×9mm, 3 sargı, 1 el bombası, 1 sis  
- **Oynanış:** Yüksek zemin, köprü ve FOB yaklaşımlarında erken uyarı; yakın mesafede SMG’ye geçer.

### Makineli Tüfekçi
- **Silah:** PMT-76 + SAR 9  
- **Zırh:** Yelek 2, Kask 1, Çanta 2  
- **Stok:** 300×7.62, 30×9mm, 3 sargı, 1 el bombası, 1 sis  
- **Oynanış:** Koridor ve açık alan baskısı; yavaş hareket / uzun doldurma maliyeti.

### Sıhhiyeci
- **Silah:** MPT-55 + SAR 9  
- **Zırh:** Yelek 1, Kask 1, Çanta 2  
- **Stok:** 150×5.56, 30×9mm, 6 sargı, 4 ilk yardım, 2 sıhhiye çantası, 1 ağrı kesici, 1 el bombası, 2 sis  
- **Oynanış:** Timin arkasında kalır; sisle can kurtarma alanı açar.

### Telsizci
- **Silah:** MPT-55 + Canik TP9  
- **Zırh:** Yelek 1, Kask 1, Çanta 2  
- **Stok:** 150×5.56, 30×9mm, 3 sargı, 1 enerji içeceği, 1 el bombası, 2 sis  
- **Oynanış:** Topçu hedefi işaretler; hayatta kalması stratejik önceliktir.

### Bombacı
- **Silah:** G3A7 + SAR 9  
- **Zırh:** Yelek 2, Kask 1, Çanta 2  
- **Stok:** 150×7.62, 30×9mm, 3 sargı, **4 el bombası**, 2 sis  
- **Oynanış:** Bina temizleme, köprü başı, mevzi kırıcı.

### Piyade
- **Silah:** Slot çift → MPT-76, tek → MPT-55 + SAR 9  
- **Zırh:** Yelek 1, Kask 1, Çanta 1  
- **Stok:** 150 ana mermi, 30×9mm, 3 sargı, 1 el bombası, 1 sis  
- **Oynanış:** Esnek hat; yağma ve flank.

## 2.3 Emirler (`SquadOrder`)

| Emir | Davranış (bot) |
|------|----------------|
| Beni takip et | Lidere yaklaş (başla >8 m, dur <4 m) |
| Mevzi al | Hold; dolaşmaz |
| Taarruz | İşaretli noktaya Assault |
| Toplan | Regroup → lidere Follow kuralları |

Öncelik: görünen düşman > bölge dışı > emir (bkz. [10-bot-ai.md](10-bot-ai.md)).
