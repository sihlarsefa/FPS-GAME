# HAREKÂT — Manuel Test Senaryoları

**Toplam:** 368 senaryo · **Kaynak:** `QA/cases.json` · **CSV:** [senaryolar.csv](senaryolar.csv)

| # | Modül | Senaryo | Dosya |
|---|-------|---------|-------|
| 1 | Hareket | 25 | [01_Hareket.md](01_Hareket.md) |
| 2 | Silahlar | 100 | [02_Silahlar.md](02_Silahlar.md) |
| 3 | Envanter | 14 | [03_Envanter.md](03_Envanter.md) |
| 4 | İyileşme | 22 | [04_Iyilesme.md](04_Iyilesme.md) |
| 5 | Zırh | 18 | [05_Zirh.md](05_Zirh.md) |
| 6 | İntikal | 13 | [06_Intikal.md](06_Intikal.md) |
| 7 | Komuta ve topçu | 15 | [07_Komuta_Topcu.md](07_Komuta_Topcu.md) |
| 8 | Bölge ve maç | 12 | [08_Bolge_Mac.md](08_Bolge_Mac.md) |
| 9 | Yapay zekâ | 12 | [09_Yapay_Zeka.md](09_Yapay_Zeka.md) |
| 10 | HUD ve harita | 20 | [10_HUD_Harita.md](10_HUD_Harita.md) |
| 11 | Menüler ayarlar kariyer | 15 | [11_Menuler_Ayarlar.md](11_Menuler_Ayarlar.md) |
| 12 | Atış Poligonu | 9 | [12_Atis_Poligonu.md](12_Atis_Poligonu.md) |
| 13 | Performans | 12 | [13_Performans.md](13_Performans.md) |
| 14 | Online ve dayanıklılık | 16 | [14_Online.md](14_Online.md) |
| 15 | Backend uçları | 37 | [15_Backend.md](15_Backend.md) |
| 16 | Erişilebilirlik | 8 | [16_Erisilebilirlik.md](16_Erisilebilirlik.md) |
| 17 | Windows matrisi | 10 | [17_Windows.md](17_Windows.md) |
| 18 | Denge | 10 | [18_Denge.md](18_Denge.md) |

## Öncelik özeti

- **P0:** 4
- **P1:** 364

## Kullanım

1. Excel/Google Sheets için `senaryolar.csv` dosyasını açın (UTF-8 BOM).
2. Modül dosyalarında tek tek çalıştırın; `status` alanını güncelleyin.
3. Kaynak yenileme: `python3 QA/scripts/seed_cases.py` sonra bu export'u yeniden üretin.
