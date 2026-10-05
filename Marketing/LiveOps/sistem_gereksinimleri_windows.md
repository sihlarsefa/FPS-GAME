# HAREKÂT — Windows Sistem Gereksinimleri

**Platform:** Windows istemci (Steam / resmi launcher hedefi)  
**Not:** Değerler taslak; build ölçümleriyle güncellenir. Sunucu tarafı Windows Server’dır (oyuncu tablosu değil).

Kaynak uyumu: `Marketing/steam/store_page_tr.md` / `_en.md`.

---

## Minimum

| Bileşen | Gereksinim |
|---------|------------|
| İşletim sistemi | Windows 10 64-bit (21H2 veya üzeri) |
| İşlemci | 4 çekirdek — örn. Intel Core i5-2500 / AMD FX-6300 sınıfı veya daha yenisi |
| Bellek | 8 GB RAM |
| Ekran kartı | DirectX 11 — NVIDIA GTX 1050 / AMD RX 560 sınıfı (3 GB+ VRAM) |
| DirectX | Sürüm 11 |
| Depolama | 8 GB boş alan (HDD kabul; SSD önerilir) |
| Ağ | Geniş bant (online için) |
| Ek | 64-bit OS zorunlu; SSE2 |

**Hedef deneyim (min):** 1080p, düşük ayar, ~30 FPS; bot / düşük oyuncu yoğunluğu senaryolarında oynanabilir.

## Önerilen

| Bileşen | Gereksinim |
|---------|------------|
| İşletim sistemi | Windows 10 veya 11 64-bit (güncel) |
| İşlemci | 6+ çekirdek — örn. Intel Core i5-10400 / AMD Ryzen 5 3600 veya daha iyisi |
| Bellek | 16 GB RAM |
| Ekran kartı | NVIDIA RTX 2060 / AMD RX 6600 sınıfı (6 GB+ VRAM) |
| DirectX | 12 uyumlu donanım (DX11 yolu da desteklenir) |
| Depolama | 12 GB SSD |
| Ağ | Sabit geniş bant; kablolu tercih |
| Ses | Windows ses cihazı / kulaklık (tim koordinasyonu) |

**Hedef deneyim (önerilen):** 1080p, orta–yüksek, ~60 FPS; 10’luk tim online maç.

## Üst düzey / yayın (opsiyonel)

| Bileşen | Öneri |
|---------|--------|
| İşlemci | 8 çekirdek (yayın + oyun) |
| Bellek | 32 GB (OBS + tarayıcı) |
| Ekran kartı | RTX 3070 / RX 6700 XT veya üzeri |
| Depolama | NVMe SSD |
| Ağ | Upload ≥ 10 Mbps (1080p60 yayın) |

## Çözünürlük ve ayar matrisi (rehber)

| Hedef | Çözünürlük | Preset | Donanım bandı |
|-------|------------|--------|---------------|
| Oynanabilir | 1920×1080 | Düşük | Minimum |
| Rekabet | 1920×1080 | Orta | Önerilen |
| Görsel | 2560×1440 | Yüksek | Üst düzey |
| 4K | 3840×2160 | Orta/Yüksek | Üst düzey+ |

## Bilinen kısıtlar

- macOS / Linux istemci: desteklenmiyor (geliştirici macOS yalnızca editör).  
- Entegre GPU (çok düşük UHD): resmi olarak desteklenmez; garanti yok.  
- Çoklu monitör + yüksek DPI: ölçekleme %100 veya %125 ile test edin.  
- Antivirus gerçek zamanlı tarama ilk açılışta hitch yapabilir.

## Destek metni (kısa)

**TR:** En az Windows 10 64-bit, 8 GB RAM, GTX 1050 sınıfı. Rahat oyun için 16 GB RAM, RTX 2060 / RX 6600 ve SSD.  
**EN:** Minimum: Windows 10 64-bit, 8 GB RAM, GTX 1050-class GPU. Recommended: 16 GB RAM, RTX 2060 / RX 6600-class, SSD.
