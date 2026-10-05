# HAREKÂT — MSSQL Analitik ve Raporlama (`Tools/SqlReports`)

Salt okunur T-SQL raporlar, view/procedure/index **önerileri**, Node ile tek dosyalık HTML KPI ve Windows Görev Zamanlayıcısı betikleri.

## Kaynak şema

EF: `Backend/Harekat.Infrastructure/Persistence/HarekatDbContext.cs`  
Ayrıntı: [`schema/ef-mapping.md`](schema/ef-mapping.md)

Ana tablolar: `Players`, `Squads`, `MatchTickets`, `Matches`, `GameServers`, `Reports`, …  
Silah / hile metrikleri için önerilen: `Telemetry.MatchEvents`, `Telemetry.PlayerSuspicionReports` (şu an bellek içi; Cursor kalıcılaştırır).

## Klasörler

| Yol | İçerik |
|-----|--------|
| `scripts/` | Günlük rapor SELECT betikleri (01–09) |
| `views/` | Önerilen view DDL |
| `procedures/` | Önerilen saklı yordam DDL |
| `indexes/` | Önerilen indeks DDL |
| `extensions/` | UZATMA: denge karşılaştırması, kohort, z-skoru |
| `scheduler/` | PowerShell haftalık zamanlayıcı |
| `lib/` | Node HTML/SVG/örnek veri |
| `out/` | Üretilen HTML |

## Rapor betikleri

1. `01_active_players.sql` — DAU / WAU / MAU  
2. `02_retention.sql` — D1 / D7 / D30  
3. `03_match_duration.sql` — maç süresi dağılımı  
4. `04_weapon_usage.sql` — silah öldürme / kullanım  
5. `05_squad_placement.sql` — tim yerleşimi / galibiyet  
6. `06_rank_distribution.sql` — rütbe dağılımı  
7. `07_matchmaking_wait.sql` — eşleştirme bekleme  
8. `08_server_occupancy.sql` — sunucu doluluğu  
9. `09_cheat_suspicion.sql` — şüphe / rapor trendi  

`views/`, `procedures/`, `indexes/` **yalnızca öneridir**; üretimde Cursor / DBA uygular.

## Node KPI aracı

```bash
cd Tools/SqlReports
npm install
npm run report
# veya
node report.mjs --out out/kpi-report.html
```

**Bağlantı dizesi** (öncelik sırası):

1. `HAREKAT_SQL_CONNECTION`  
2. `MSSQL_CONNECTION`  
3. `SQL_CONNECTION_STRING`  

Örnek:

```text
Server=sql01;Database=Harekat;User Id=report_ro;Password=***;Encrypt=true;TrustServerCertificate=false
```

Bağlantı yoksa veya sorgu hata verirse **örnek veriyle** aynı HTML şablonu üretilir (`mode: sample`).

Grafikler bağımlılıksız **inline SVG** (`lib/svg.mjs`).

### Komutlar

| Script | Açıklama |
|--------|----------|
| `npm run report` | HTML KPI |
| `npm run lint` | `node --check` |
| `npm test` | Birim testleri |

## Windows Görev Zamanlayıcısı

Yönetici veya oturum açmış kullanıcı PowerShell:

```powershell
cd Tools\SqlReports\scheduler
.\Register-WeeklyReport.ps1
# isteğe bağlı:
.\Register-WeeklyReport.ps1 -Time '08:30' -DayOfWeek Monday
```

Elle çalıştırma:

```powershell
.\Run-WeeklyReport.ps1
```

Çıktı: `out/kpi-weekly-YYYYMMDD-HHmm.html` ve `out/kpi-report.html`.

Kullanıcı ortamına `HAREKAT_SQL_CONNECTION` ekleyin; görev bu kullanıcı altında çalışır.

Kaldırma:

```powershell
Unregister-ScheduledTask -TaskName 'Harekat-SqlReports-Weekly' -Confirm:$false
```

## UZATMA

- `extensions/01_weapon_balance_vs_calc.sql` — BalanceCalc TTK vs telemetri  
- `extensions/02_cohort_analysis.sql` — haftalık kohort  
- `extensions/03_anomaly_zscore.sql` — DAU / KD z-skoru  

## Güvenlik notu

Rapor kullanıcısı **salt okunur** olmalı (`db_datareader` + gerekirse view yetkisi). Öneri DDL dosyalarını production’da incelemeden çalıştırmayın.
