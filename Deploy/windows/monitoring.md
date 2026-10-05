# HAREKÂT — İzleme (Windows Server)

Hedef: oyun süreçleri, API, SQL ve kuyruk derinliği için uyarı üretmek.

## Seçenek A — windows_exporter + Prometheus + Grafana (önerilen filo)

1. **windows_exporter** (her Windows host):
   - https://github.com/prometheus-community/windows_exporter/releases
   - Collectors: `cpu`, `memory`, `logical_disk`, `net`, `process`, `service`
   - Servis: `windows_exporter` port **9182**

2. **ServerManager metrics** (oyun hostu):
   - `http://HOST:9183/metrics` — `harekat_sm_*` gauge’ları
   - Firewall: `.\firewall.ps1 -OpenMetrics` (veya yalnızca Prometheus IP’sine açın)

3. **Backend API metrics**:
   - `http://HOST:3208/metrics` (OpenTelemetry Prometheus scrape)

4. **Grafana panoları** (örnek paneller):
   - CPU / RAM (host + `HAREKAT_Server` süreçleri)
   - `harekat_sm_active_matches`, `harekat_sm_ports_available`
   - `harekat_sm_queue_depth` (kuyruk birikmesi)
   - API `/health` up, HTTP 5xx oranı

### Örnek Prometheus scrape

```yaml
scrape_configs:
  - job_name: windows
    static_configs:
      - targets: ['134.149.201.54:9182']
  - job_name: harekat-sm
    static_configs:
      - targets: ['134.149.201.54:9183']
  - job_name: harekat-api
    metrics_path: /metrics
    static_configs:
      - targets: ['134.149.201.54:3208']
```

## Seçenek B — Basit (exporter yok)

- Backend `/metrics` + `/health`
- ServerManager Event Log kaynağı: `Harekat.ServerManager` (Warning+)
- Windows Event Viewer / scheduled task ile log tarama
- IIS Failed Request Tracing (isteğe bağlı)

## Uyarı kuralları

| Uyarı | Koşul | Süre | Aksiyon |
|-------|--------|------|---------|
| Oyun süreci çöktü | ServerManager log / Event Log `çöktü` | anında | Restart (otomatik); 6/saat aşımı → sayfa |
| Heartbeat timeout | `GameServers.LastHeartbeatAt` > 45s | 1 dk | Offline işaretle; maç tahsisini durdur |
| Kuyruk birikti | `harekat_sm_queue_depth` > 50 (veya `Alerts:QueueDepthWarning`) | 2 dk | Filo ekle / WarmReadySlots |
| CPU yüksek | host CPU > %90 veya `harekat_sm_cpu_percent` > MaxCpuPercent | 3 dk | Yeni spawn durur (SM); scale-out |
| RAM yüksek | `harekat_sm_ram_mb` > MaxRamMb | 3 dk | Aynı |
| DB yavaş | API yavaş sorgu / `Alerts:DbSlowQueryMs` (500ms) | 5 dk | SQL indeks / plan kontrol |
| API down | `/health` fail | 1 dk | IIS app pool recycle; sayfa |

## Event Log

ServerManager Windows Service olarak Event Log’a Warning+ yazar (`appsettings.json` → `Logging:EventLog:SourceName`).

```powershell
Get-WinEvent -FilterHashtable @{ LogName = 'Application'; ProviderName = 'Harekat.ServerManager' } -MaxEvents 50
```

## Hızlı sağlık kontrolü

```powershell
Invoke-WebRequest http://127.0.0.1:3208/health -UseBasicParsing
Invoke-WebRequest http://127.0.0.1:9183/metrics -UseBasicParsing
Get-Service Harekat.ServerManager
Get-Service W3SVC
```
