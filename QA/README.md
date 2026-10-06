# QA — HAREKÂT (C2-3)

Manuel test planı, 300+ senaryo, smoke, playtest ve hata şablonu.

| Dosya / klasör | İçerik |
|----------------|--------|
| [TestPlan.md](TestPlan.md) | Kapsam, ortamlar, giriş/çıkış, riskler |
| [Senaryolar/](Senaryolar/) | **398** senaryo (MD + CSV) |
| [Smoke.md](Smoke.md) | ≤15 dk duman listesi |
| [Playtest/](Playtest/) | Protokol, gözlem, anket HTML+JSON şema, metrikler |
| [RegresyonListesi.md](RegresyonListesi.md) | Her sürümde 60 kritik senaryo |
| [dashboard.html](dashboard.html) | Sonuç takip panosu (CSV okur) |
| [scripts/add_faz3_cases.py](scripts/add_faz3_cases.py) | FAZ3 senaryoları (Kirpi, konsol, paneller, sunucu, override) + regresyon |
| [Hata_Sablonu.md](Hata_Sablonu.md) | Önem×öncelik + issue önerisi |
| [cases.json](cases.json) | Makine okunur senaryo kaynağı |
| [scripts/seed_cases.py](scripts/seed_cases.py) | Katalogdan cases üretimi |
| [scripts/export_scenarios.py](scripts/export_scenarios.py) | MD+CSV export |

## Hızlı komutlar

```bash
python3 QA/scripts/seed_cases.py      # BalanceCalc/katalog varsa yeniler
python3 QA/scripts/add_faz3_cases.py  # FAZ3 ekler + RegresyonListesi
python3 QA/scripts/export_scenarios.py  # Senaryolar/ yeniler
```

## UZATMA (dahil)

`cases.json` içinde zaten: **Denge (BAL)**, **Online dayanıklılık (NET)**, **Erişilebilirlik (ACC)**, **Windows matrisi (WIN)**.
