#Requires -Version 5.1
<#
.SYNOPSIS
  HAREKÂT — Windows Server (IIS) + SQL Server deploy
.DESCRIPTION
  Repo'yu çeker, Web'i build eder, API'yi publish eder, IIS sitelerini günceller.
  Veritabanı: Microsoft SQL Server (Storage__Provider=SqlServer).
.EXAMPLE
  .\deploy.ps1 -SqlConnectionString "Server=SQL;Database=Harekat;User Id=harekat;Password=***;TrustServerCertificate=True"
#>
param(
    [string]$RepoUrl = 'https://github.com/sihlarsefa/FPS-GAME.git',
    [string]$Root = 'C:\harekat',
    [string]$WebPort = '80',
    [string]$ApiPort = '3208',
    [string]$WikiPort = '3210',
    [string]$PublicHost = '134.149.201.54',
    [string]$SqlConnectionString = '',
    [string]$JwtSecret = ''
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$env:Path = 'C:\Program Files\Git\cmd;C:\Program Files\dotnet;C:\Program Files\nodejs;' +
    [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
    [Environment]::GetEnvironmentVariable('Path', 'User')

Write-Host '==> HAREKAT Windows + SQL Server deploy' -ForegroundColor Cyan

$Repo = Join-Path $Root 'repo'
$ApiOut = Join-Path $Root 'api'
$WebOut = Join-Path $Root 'web'
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
$WebStage = Join-Path $Root ('web_' + $stamp)
$WikiStage = Join-Path $Root ('wiki_' + $stamp)
New-Item -ItemType Directory -Force -Path $Root, $ApiOut | Out-Null

if (-not $SqlConnectionString) {
    $envFile = Join-Path $PSScriptRoot '.env.ps1'
    if (Test-Path $envFile) { . $envFile }
}
if (-not $SqlConnectionString) {
    $SqlConnectionString = $env:HAREKAT_SQL_CONNECTION
}
if (-not $SqlConnectionString) {
    throw 'SQL connection string gerekli. -SqlConnectionString veya Deploy/windows/.env.ps1 veya HAREKAT_SQL_CONNECTION kullanın.'
}
if (-not $JwtSecret) {
    $JwtSecret = $env:HAREKAT_JWT_SECRET
}
if (-not $JwtSecret -or $JwtSecret.Length -lt 32) {
    $JwtSecret = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 48 | ForEach-Object { [char]$_ })
    Write-Host 'JWT secret üretildi (Production appsettings içine yazıldı).' -ForegroundColor Yellow
}

# .NET 10 SDK
if (-not ((& dotnet --list-sdks 2>$null) -match '^10\.')) {
    Write-Host '.NET 10 SDK kuruluyor...' -ForegroundColor Yellow
    $install = Join-Path $env:TEMP 'dotnet-install.ps1'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $install
    & powershell -NoProfile -ExecutionPolicy Bypass -File $install -Channel 10.0 -InstallDir 'C:\Program Files\dotnet'
    $env:Path = 'C:\Program Files\dotnet;' + $env:Path
}

$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'never'
if (-not (Test-Path (Join-Path $Repo '.git'))) {
    git -c credential.helper= clone --depth 1 $RepoUrl $Repo
} else {
    git -C $Repo -c credential.helper= fetch --depth 1 origin main
    git -C $Repo -c credential.helper= reset --hard origin/main
}

# Web
Write-Host 'Web build...' -ForegroundColor Yellow
Push-Location (Join-Path $Repo 'Web')
if (Test-Path package-lock.json) { npm ci } else { npm install }
npm run build
if ($LASTEXITCODE -ne 0) { throw 'Web build failed' }
Pop-Location

$dist = Join-Path $Repo 'Web\dist'
$cfgPath = Join-Path $dist 'js\config.js'
$cfg = Get-Content $cfgPath -Raw
$cfg = [regex]::Replace($cfg, "return '';.*", "return 'http://${PublicHost}:${ApiPort}';")
Set-Content $cfgPath -Value $cfg -Encoding UTF8

@'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <defaultDocument>
      <files>
        <clear />
        <add value="index.html" />
      </files>
    </defaultDocument>
    <httpErrors existingResponse="PassThrough" />
    <staticContent>
      <remove fileExtension=".json" />
      <mimeMap fileExtension=".json" mimeType="application/json" />
      <remove fileExtension=".webmanifest" />
      <mimeMap fileExtension=".webmanifest" mimeType="application/manifest+json" />
      <remove fileExtension=".mjs" />
      <mimeMap fileExtension=".mjs" mimeType="text/javascript" />
    </staticContent>
  </system.webServer>
</configuration>
'@ | Set-Content (Join-Path $dist 'web.config') -Encoding UTF8

New-Item -ItemType Directory -Force -Path $WebStage | Out-Null
Copy-Item (Join-Path $dist '*') $WebStage -Recurse -Force

# Wiki (oyuncu saha kılavuzu)
Write-Host 'Wiki build...' -ForegroundColor Yellow
Push-Location (Join-Path $Repo 'Wiki')
if (Test-Path package-lock.json) { npm ci } else { npm install }
npm run build
if ($LASTEXITCODE -ne 0) { throw 'Wiki build failed' }
Pop-Location

$wikiDist = Join-Path $Repo 'Wiki\dist'
if (-not (Test-Path $wikiDist)) { throw "Wiki dist yok: $wikiDist" }
# IIS-friendly minimal web.config (CSP/HSTS'li config 500.19 verebiliyor)
@'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <defaultDocument>
      <files>
        <clear />
        <add value="index.html" />
      </files>
    </defaultDocument>
    <httpErrors existingResponse="PassThrough" />
    <staticContent>
      <remove fileExtension=".json" />
      <mimeMap fileExtension=".json" mimeType="application/json" />
      <remove fileExtension=".svg" />
      <mimeMap fileExtension=".svg" mimeType="image/svg+xml" />
      <remove fileExtension=".mjs" />
      <mimeMap fileExtension=".mjs" mimeType="text/javascript" />
    </staticContent>
  </system.webServer>
</configuration>
'@ | Set-Content (Join-Path $wikiDist 'web.config') -Encoding UTF8
New-Item -ItemType Directory -Force -Path $WikiStage | Out-Null
Copy-Item (Join-Path $wikiDist '*') $WikiStage -Recurse -Force

# API
Write-Host 'API publish...' -ForegroundColor Yellow
$apiProj = Join-Path $Repo 'Backend\Harekat.Api\Harekat.Api.csproj'
Import-Module WebAdministration -ErrorAction SilentlyContinue
$apiSite = 'harekat-api'
$webSite = 'harekat-web'
$wikiSite = 'harekat-wiki'
try { if (Get-Website -Name $apiSite -ErrorAction SilentlyContinue) { Stop-Website $apiSite } } catch {}
try { if (Test-Path "IIS:\AppPools\$apiSite") { Stop-WebAppPool $apiSite } } catch {}
Start-Sleep -Seconds 2

dotnet publish $apiProj -c Release -o $ApiOut --self-contained false
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# ServerManager (Windows Service) — publish only; service install is optional
$smProj = Join-Path $Repo 'Backend\Harekat.ServerManager\Harekat.ServerManager.csproj'
$smOut = Join-Path $Root 'servermanager'
if (Test-Path $smProj) {
    Write-Host 'ServerManager publish...' -ForegroundColor Yellow
    New-Item -ItemType Directory -Force -Path $smOut | Out-Null
    dotnet publish $smProj -c Release -o $smOut --self-contained false
    if ($LASTEXITCODE -ne 0) { Write-Host 'ServerManager publish FAILED (API devam)' -ForegroundColor Yellow }
    else { Write-Host ("ServerManager -> " + $smOut) -ForegroundColor Green }
}

$prod = @{
    Logging            = @{ LogLevel = @{ Default = 'Information'; 'Microsoft.AspNetCore' = 'Warning' } }
    AllowedHosts       = '*'
    Storage            = @{ Provider = 'SqlServer' }
    ConnectionStrings  = @{ SqlServer = $SqlConnectionString }
    Jwt                = @{
        Secret        = $JwtSecret
        Issuer        = 'harekat'
        Audience      = 'harekat-clients'
        AccessMinutes = '60'
    }
    Swagger            = @{ Enabled = $true }
} | ConvertTo-Json -Depth 6
Set-Content (Join-Path $ApiOut 'appsettings.Production.json') -Value $prod -Encoding UTF8

@'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" arguments=".\Harekat.Api.dll" stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
          <environmentVariable name="Storage__Provider" value="SqlServer" />
          <environmentVariable name="Swagger__Enabled" value="true" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
'@ | Set-Content (Join-Path $ApiOut 'web.config') -Encoding UTF8
New-Item -ItemType Directory -Force -Path (Join-Path $ApiOut 'logs') | Out-Null
icacls $Root /grant 'IIS_IUSRS:(OI)(CI)M' /T | Out-Null
icacls $ApiOut /grant 'IIS_IUSRS:(OI)(CI)M' /T | Out-Null
icacls $WebStage /grant 'IIS_IUSRS:(OI)(CI)RX' /T | Out-Null
icacls $WikiStage /grant 'IIS_IUSRS:(OI)(CI)RX' /T | Out-Null

# IIS — yalnızca harekat-* siteleri
foreach ($pool in @($apiSite, $webSite, $wikiSite)) {
    if (-not (Test-Path "IIS:\AppPools\$pool")) { New-WebAppPool -Name $pool | Out-Null }
    Set-ItemProperty "IIS:\AppPools\$pool" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$pool" -Name startMode -Value 'AlwaysRunning'
}

if (-not (Get-Website -Name $apiSite -ErrorAction SilentlyContinue)) {
    New-Website -Name $apiSite -PhysicalPath $ApiOut -ApplicationPool $apiSite -Port ([int]$ApiPort) -Force | Out-Null
} else {
    Set-ItemProperty "IIS:\Sites\$apiSite" -Name physicalPath -Value $ApiOut
}
if (-not (Get-Website -Name $webSite -ErrorAction SilentlyContinue)) {
    New-Website -Name $webSite -PhysicalPath $WebStage -ApplicationPool $webSite -Port ([int]$WebPort) -Force | Out-Null
} else {
    Set-ItemProperty "IIS:\Sites\$webSite" -Name physicalPath -Value $WebStage
}
if (-not (Get-Website -Name $wikiSite -ErrorAction SilentlyContinue)) {
    New-Website -Name $wikiSite -PhysicalPath $WikiStage -ApplicationPool $wikiSite -Port ([int]$WikiPort) -Force | Out-Null
} else {
    Set-ItemProperty "IIS:\Sites\$wikiSite" -Name physicalPath -Value $WikiStage
}

New-NetFirewallRule -DisplayName "Harekat Web $WebPort" -Direction Inbound -Protocol TCP -LocalPort $WebPort -Action Allow -ErrorAction SilentlyContinue | Out-Null
New-NetFirewallRule -DisplayName "Harekat API $ApiPort" -Direction Inbound -Protocol TCP -LocalPort $ApiPort -Action Allow -ErrorAction SilentlyContinue | Out-Null
New-NetFirewallRule -DisplayName "Harekat Wiki $WikiPort" -Direction Inbound -Protocol TCP -LocalPort $WikiPort -Action Allow -ErrorAction SilentlyContinue | Out-Null

Start-WebAppPool $apiSite
Start-WebAppPool $webSite
Start-WebAppPool $wikiSite
Start-Website $apiSite
Start-Website $webSite
Start-Website $wikiSite
Start-Sleep -Seconds 5

try {
    $h = Invoke-WebRequest "http://127.0.0.1:$ApiPort/health" -UseBasicParsing -TimeoutSec 30
    Write-Host ("API health: " + $h.StatusCode + ' ' + $h.Content) -ForegroundColor Green
} catch {
    Write-Host ("API health FAIL: " + $_.Exception.Message) -ForegroundColor Red
    throw
}
try {
    $w = Invoke-WebRequest "http://127.0.0.1:$WebPort/" -UseBasicParsing -TimeoutSec 15
    Write-Host ("Web: " + $w.StatusCode) -ForegroundColor Green
} catch {
    Write-Host ("Web FAIL: " + $_.Exception.Message) -ForegroundColor Yellow
}
try {
    $wk = Invoke-WebRequest "http://127.0.0.1:$WikiPort/" -UseBasicParsing -TimeoutSec 15
    Write-Host ("Wiki: " + $wk.StatusCode) -ForegroundColor Green
} catch {
    Write-Host ("Wiki FAIL: " + $_.Exception.Message) -ForegroundColor Yellow
}

Write-Host "Portal  http://${PublicHost}:${WebPort}/"
Write-Host "Wiki    http://${PublicHost}:${WikiPort}/"
Write-Host "API     http://${PublicHost}:${ApiPort}/health"
Write-Host "Swagger http://${PublicHost}:${ApiPort}/swagger"
Write-Host 'DONE' -ForegroundColor Green
