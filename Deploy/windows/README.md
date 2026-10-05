# Windows Server dağıtım (IIS + SQL Server)

Hedef platform: **Windows Server + IIS + Microsoft SQL Server (+ Redis)**.  
Linux sunucu yok; canlı hedef: `134.149.201.54` (APP2025).

| Servis | Port | Not |
|--------|------|-----|
| Web portal (`harekat-web`) | 80 / 443 | IIS → `Web/dist` |
| Backend API (`harekat-api`) | 3208 | IIS + ASP.NET Core Module V2 |
| Oyun sunucuları | UDP/TCP **7777–7900** | `Harekat.ServerManager` spawn |
| ServerManager metrics | 9183 | `/metrics` |
| SQL Server | 1433 | Varsayılan provider: **SqlServer** |

---

## Sıfırdan canlıya (özet)

```powershell
# 1) Önkoşullar
#    Windows Server 2019+, IIS, Hosting Bundle, .NET 10 SDK, Node 18+, Git, SQL erişimi

# 2) Ortam
cd C:\harekat\repo\Deploy\windows
copy .env.ps1.example .env.ps1
notepad .env.ps1          # SqlConnectionString, JwtSecret, PublicHost

# 3) Veritabanı (SQL makinede)
sqlcmd -S SQL_HOST -i .\create-db.sql
sqlcmd -S SQL_HOST -d Kuzgun -i .\sql\indexes.sql
# Yedek job: sql\backup-jobs.sql → SQL Agent veya Task Scheduler

# 4) IIS siteleri
powershell -ExecutionPolicy Bypass -File .\iis-setup.ps1
# Opsiyonel /api reverse proxy:
# powershell -ExecutionPolicy Bypass -File .\iis-setup.ps1 -EnableApiReverseProxy

# 5) Firewall
powershell -ExecutionPolicy Bypass -File .\firewall.ps1
# Metrics scrape için: .\firewall.ps1 -OpenMetrics

# 6) Deploy (web build + API publish + IIS güncelle)
powershell -ExecutionPolicy Bypass -File .\deploy.ps1

# 7) Redis (opsiyonel)
powershell -ExecutionPolicy Bypass -File .\install-docker-redis.ps1

# 8) ServerManager (oyun hostu)
dotnet publish ..\..\Backend\Harekat.ServerManager\Harekat.ServerManager.csproj -c Release -o C:\harekat\servermanager
# appsettings: BackendBaseUrl, PublicHost, ServerKey, GameServerExe
sc.exe create Harekat.ServerManager binPath= "C:\harekat\servermanager\Harekat.ServerManager.exe" start= auto
sc.exe start Harekat.ServerManager
```

Kontrol:

- http://134.149.201.54/
- http://134.149.201.54:3208/health
- http://134.149.201.54:3208/swagger
- http://127.0.0.1:9183/metrics

Kapasite: [CAPACITY.md](CAPACITY.md) · İzleme: [monitoring.md](monitoring.md)

---

## Önkoşullar (APP sunucusu)

- Windows Server 2019+
- IIS + **ASP.NET Core Hosting Bundle** (ANCM V2)
- .NET 10 SDK (`deploy.ps1` yoksa kurar)
- Node.js 18+
- Git
- SQL Server ağ erişimi (`sqlcmd` / TCP 1433)

## SQL Server (varsayılan provider)

API: `Storage:Provider=SqlServer` (Npgsql kaldırıldı; Memory/Sqlite yalnızca test).

1. Veritabanı:

```sql
-- create-db.sql → Kuzgun DB
-- veya:
CREATE DATABASE Kuzgun;
```

2. `.env.ps1` connection string (örnek `.env.ps1.example`).

3. Deploy sonrası EF migration / EnsureCreated tabloları oluşturur.

4. İndeksler: `sql/indexes.sql` (EF ile örtüşür, idempotent).

5. Yedek: `sql/backup-jobs.sql` (günlük FULL, 14 gün saklama).

## IIS (`iis-setup.ps1`)

- App pool + site: `harekat-web` (:80), `harekat-api` (:3208)
- `managedRuntimeVersion = ''` (no managed CLR — Core)
- WebSocket açık (SignalR)
- Opsiyonel: `-EnableApiReverseProxy` → `/api` → `127.0.0.1:3208` (URL Rewrite + ARR)
- HTTPS: win-acme (Let's Encrypt) — script sonunda not

## Firewall (`firewall.ps1`)

Açar: TCP 80, 443, 3208; TCP/UDP **7777–7900**; isteğe bağlı TCP 9183.

```powershell
.\firewall.ps1
.\firewall.ps1 -OpenMetrics
.\firewall.ps1 -Remove
```

## ServerManager

Proje: `Backend/Harekat.ServerManager` (Windows Service).

- Port havuzu 7777–7900
- `GET /matches/pending-allocation` → `POST /matches/{id}/claim` → `HAREKAT_Server.exe -batchmode -nographics ...`
- Heartbeat, crash restart (saatlik limit), log rotation, CPU/RAM limiti
- Metrics: `:9183/metrics`

## İlk / güncel deploy

```powershell
cd C:\harekat\repo\Deploy\windows
copy .env.ps1.example .env.ps1
notepad .env.ps1
.\deploy.ps1
```

## GitHub Actions

Workflow: `.github/workflows/windows-deploy.yml`  
Environment: `windows` (veya `vps`)

| Secret | Değer |
|--------|--------|
| `WIN_HOST` | `134.149.201.54` |
| `WIN_USER` | `sefasihlar` |
| `WIN_SSH_PRIVATE_KEY` veya password | SSH |
| `HAREKAT_SQL_CONNECTION` | SQL Server connection string |
| `HAREKAT_JWT_SECRET` | ≥32 karakter |

## Docker + Redis (aynı Windows Server)

```powershell
powershell -ExecutionPolicy Bypass -File .\install-docker-redis.ps1
```

- Redis servisi: `HarekatRedis` @ `127.0.0.1:6379`
