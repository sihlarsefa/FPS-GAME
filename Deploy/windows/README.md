# Windows Server dağıtım (IIS + SQL Server)

Hedef platform: **Windows Server + IIS + Microsoft SQL Server**.  
Linux / Docker / Postgres bu yolda kullanılmaz.

Canlı hedef: `134.149.201.54` (APP2025)

| Servis | Port | IIS site |
|--------|------|----------|
| Web portal | 80 | `harekat-web` |
| Backend API | 3208 | `harekat-api` |
| Veritabanı | 1433 (SQL Server) | ayrı SQL VM veya local instance |

## Önkoşullar (APP sunucusu)

- Windows Server 2019+
- IIS + ASP.NET Core Module V2
- .NET 10 SDK (yoksa `deploy.ps1` kurar)
- Node.js 18+
- Git
- SQL Server’a ağ erişimi (`sqlcmd` veya TCP 1433)

## SQL Server

1. SQL makinede veritabanı oluştur:

```sql
CREATE DATABASE Harekat;
CREATE LOGIN harekat WITH PASSWORD = 'GucluParola!';
USE Harekat;
CREATE USER harekat FOR LOGIN harekat;
ALTER ROLE db_owner ADD MEMBER harekat;
```

2. `Deploy/windows/.env.ps1.example` → `.env.ps1` kopyala, connection string’i doldur.

Örnek:

```powershell
$SqlConnectionString = 'Server=SQL;Database=Harekat;User Id=harekat;Password=***;TrustServerCertificate=True;MultipleActiveResultSets=true'
```

API açılışında EF Core `EnsureCreated` ile tabloları oluşturur (`Storage:Provider=SqlServer`).

## İlk / güncel deploy (sunucuda)

```powershell
cd C:\harekat\repo\Deploy\windows   # veya repo yolu
copy .env.ps1.example .env.ps1
notepad .env.ps1
.\deploy.ps1
```

Kontrol:

- http://134.149.201.54/
- http://134.149.201.54:3208/health
- http://134.149.201.54:3208/swagger

## GitHub Actions

Workflow: `.github/workflows/windows-deploy.yml`

Environment: `windows` (veya `vps`)

| Secret | Değer |
|--------|--------|
| `WIN_HOST` | `134.149.201.54` |
| `WIN_USER` | `sefasihlar` |
| `WIN_SSH_PRIVATE_KEY` veya password secret | SSH kimliği |
| `HAREKAT_SQL_CONNECTION` | SQL Server connection string |
| `HAREKAT_JWT_SECRET` | ≥32 karakter |

`main` push veya `workflow_dispatch` → sunucuda `Deploy/windows/deploy.ps1` çalışır.
