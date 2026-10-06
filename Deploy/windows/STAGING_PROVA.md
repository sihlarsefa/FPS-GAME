# HAREKÂT — Windows Staging Provası (kontrol listesi)

Hedef: `134.149.201.54` (APP2025) üzerinde **yalnızca** `C:\harekat` + `harekat-*` IIS siteleri.
Diğer sitelere (Mobilite, whatsapp, akaryakitnet-*) **dokunma**.

## Önkoşul

- [ ] GitHub `sihlarsefa/FPS-GAME` `main` güncel
- [ ] `Deploy/windows/.env.ps1` var (SQL + JWT; git’e ekleme)
- [ ] Sunucuda .NET 10 SDK, Node 18+, IIS + Hosting Bundle
- [ ] `HarekatRedis` Windows servisi Running (eşleştirme için)

## 1) Veritabanı (MSSQL)

```powershell
sqlcmd -S SQL_HOST -i Deploy\windows\create-db.sql
sqlcmd -S SQL_HOST -d Kuzgun -i Deploy\windows\sql\indexes.sql
# Yedek: sql\backup-jobs.sql → SQL Agent
```

- [ ] DB bağlantısı API `appsettings.Production.json` ile aynı
- [ ] API açılışında şema ayakta (EnsureCreated / Migrate — boş history’de EnsureCreated)

## 2) IIS

```powershell
cd C:\harekat\repo\Deploy\windows
.\deploy.ps1   # Web + Wiki + Api (+ ServerManager publish)
# veya ilk kurulum: .\iis-setup.ps1 ; .\firewall.ps1
```

| Site | Port | Kontrol |
|------|------|---------|
| harekat-web | 80 | http://HOST/ → 200 |
| harekat-api | 3208 | http://HOST:3208/health → ok |
| harekat-wiki | 3210 | http://HOST:3210/ → 200 |

- [ ] Wiki `web.config` sade (500.19 yok)
- [ ] HTTPS (win-acme) — opsiyonel staging

## 3) ServerManager + Dedicated Server

```powershell
dotnet publish ..\..\Backend\Harekat.ServerManager\Harekat.ServerManager.csproj -c Release -o C:\harekat\servermanager
# HAREKAT_Server.exe Unity F4-1 Windows Dedicated Server build’den kopyala → C:\harekat\game\
sc.exe create Harekat.ServerManager binPath= "C:\harekat\servermanager\Harekat.ServerManager.exe" start= auto
# appsettings: BackendBaseUrl, PublicHost, ServerKey, GameServerExe, PortMin/Max
sc.exe start Harekat.ServerManager
```

- [ ] `pending-allocation` → claim → process spawn
- [ ] Maç sonu → `/matches/{id}/result`
- [ ] metrics :9183

## 4) Yük testi (opsiyonel)

```powershell
cd Tools\LoadTest
# README’deki senaryo: eşleştirme + sonuç; staging API URL
```

- [ ] 100 / 1000 sanal oyuncu notları `Deploy/windows/CAPACITY.md` ile karşılaştır

## Dry-run (CI / yerel, sunucusuz)

```powershell
# Betikler -WhatIf desteklemiyorsa: sadece dosya varlığı
Test-Path Deploy\windows\deploy.ps1, iis-setup.ps1, firewall.ps1, create-db.sql, sql\indexes.sql
```

## Sonuç

Tarih: ____  
API: ____ · Web: ____ · Wiki: ____ · ServerManager: ____  
Notlar: ____
