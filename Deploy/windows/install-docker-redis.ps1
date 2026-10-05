#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Windows Server 2019+ üzerinde Docker CE (Windows containers) + Redis kurulumu.
.NOTES
  - Linux alpine Redis imajı Server 2019'da güvenilir değil; Redis Windows portu servis olarak kurulur.
  - Docker, ileride Windows container işleri / CI için hazır tutulur.
#>
param(
    [int]$RedisPort = 6379,
    [string]$InstallRoot = 'C:\harekat\runtime'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

Write-Host '==> Docker + Redis (Windows Server)' -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $InstallRoot, (Join-Path $InstallRoot 'redis') | Out-Null

# --- Features ---
$containers = Get-WindowsFeature Containers
if ($containers.InstallState -ne 'Installed') {
    Write-Host 'Containers özelliği kuruluyor (reboot gerekebilir)...' -ForegroundColor Yellow
    Install-WindowsFeature -Name Containers | Out-Null
}

# Hyper-V: opsiyonel; reboot ister. Küçük VM için faydalı.
$hv = Get-WindowsFeature Hyper-V
if ($hv.InstallState -ne 'Installed') {
    Write-Host 'Hyper-V özelliği kuruluyor (reboot gerekebilir)...' -ForegroundColor Yellow
    try {
        Install-WindowsFeature -Name Hyper-V -IncludeManagementTools | Out-Null
    } catch {
        Write-Host ('Hyper-V atlandı: ' + $_.Exception.Message) -ForegroundColor Yellow
    }
}

$rebootNeeded = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -Name PendingFileRenameOperations -ErrorAction SilentlyContinue) -ne $null
if (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending') { $rebootNeeded = $true }

# --- Docker CE (Microsoft Windows Containers script) ---
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host 'Docker CE indiriliyor...' -ForegroundColor Yellow
    $dockerScript = Join-Path $env:TEMP 'install-docker-ce.ps1'
    Invoke-WebRequest -UseBasicParsing `
        -Uri 'https://raw.githubusercontent.com/microsoft/Windows-Containers/Main/helpful_tools/Install-DockerCE/install-docker-ce.ps1' `
        -OutFile $dockerScript
    # No reboot inside script if possible; -NoRestart where supported
    & powershell -NoProfile -ExecutionPolicy Bypass -File $dockerScript
    $env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
}

if (Get-Command docker -ErrorAction SilentlyContinue) {
    Write-Host ('Docker: ' + (docker --version))
    try { Start-Service docker -ErrorAction SilentlyContinue } catch {}
    try { docker info 2>&1 | Select-Object -First 15 } catch { Write-Host $_.Exception.Message }
} else {
    Write-Host 'Docker henüz PATH/serviste değil — reboot sonrası kontrol edin.' -ForegroundColor Yellow
}

# --- Redis for Windows (tporadowski) ---
$redisDir = Join-Path $InstallRoot 'redis'
$redisExe = Join-Path $redisDir 'redis-server.exe'
if (-not (Test-Path $redisExe)) {
    Write-Host 'Redis for Windows indiriliyor...' -ForegroundColor Yellow
    $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/tporadowski/redis/releases/latest'
    $asset = $rel.assets | Where-Object { $_.name -match 'Redis-x64-.*\.zip$' } | Select-Object -First 1
    if (-not $asset) { throw 'Redis Windows zip bulunamadı' }
    $zip = Join-Path $env:TEMP $asset.name
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip
    Expand-Archive -Path $zip -DestinationPath $redisDir -Force
}

$conf = Join-Path $redisDir 'harekat-redis.conf'
@"
bind 127.0.0.1
port $RedisPort
protected-mode yes
save 900 1
save 300 10
dir $(($redisDir -replace '\\','/'))
logfile "$((Join-Path $redisDir 'redis.log') -replace '\\','/')"
"@ | Set-Content $conf -Encoding ASCII

# Redis Windows native service install
$existing = Get-Service -Name 'HarekatRedis','harekatredis' -ErrorAction SilentlyContinue
if ($existing) {
    try { & $redisExe --service-stop --service-name HarekatRedis } catch {}
    try { & $redisExe --service-uninstall --service-name HarekatRedis } catch {}
    Start-Sleep -Seconds 1
}
Write-Host 'HarekatRedis servisi (redis-server --service-install)...' -ForegroundColor Yellow
& $redisExe --service-install $conf --service-name HarekatRedis
& $redisExe --service-start --service-name HarekatRedis
Start-Sleep -Seconds 2
$svc = Get-Service -Name 'HarekatRedis','harekatredis' -ErrorAction SilentlyContinue | Select-Object -First 1
Write-Host ("Redis service: " + $(if ($svc) { $svc.Status } else { 'MISSING' }))

$redisCli = Join-Path $redisDir 'redis-cli.exe'
if (Test-Path $redisCli) {
    $pong = & $redisCli -h 127.0.0.1 -p $RedisPort ping 2>&1
    Write-Host ("redis-cli ping: " + $pong)
}

# Connection info for API
$redisInfo = @{
    Host = '127.0.0.1'
    Port = $RedisPort
    ConnectionString = "127.0.0.1:$RedisPort"
} | ConvertTo-Json
Set-Content (Join-Path $InstallRoot 'redis-connection.json') $redisInfo -Encoding UTF8

Write-Host 'DONE' -ForegroundColor Green
Write-Host "Redis: 127.0.0.1:$RedisPort (HarekatRedis service)"
Write-Host 'Docker: docker version / docker info'
if ($rebootNeeded) {
    Write-Host 'UYARI: Pending reboot var. Gerekirse: Restart-Computer -Force' -ForegroundColor Yellow
}
