#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
  HAREKÂT — IIS site ve app pool kurulumu
.DESCRIPTION
  - harekat-web  : port 80, Web/dist (statik)
  - harekat-api  : port 3208, ASP.NET Core Module V2
  - WebSocket, opsiyonel /api reverse proxy (ARR + URL Rewrite)
.EXAMPLE
  .\iis-setup.ps1
  .\iis-setup.ps1 -EnableApiReverseProxy
#>
param(
    [string]$Root = 'C:\harekat',
    [string]$WebPort = '80',
    [string]$ApiPort = '3208',
    [switch]$EnableApiReverseProxy,
    [switch]$SkipHostingBundleCheck
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration -ErrorAction Stop

$ApiOut = Join-Path $Root 'api'
$WebOut = Join-Path $Root 'web'
$apiSite = 'harekat-api'
$webSite = 'harekat-web'

Write-Host '==> HAREKÂT IIS kurulumu' -ForegroundColor Cyan

# ASP.NET Core Hosting Bundle / ANCM
if (-not $SkipHostingBundleCheck) {
    $ancm = Get-WebGlobalModule -Name AspNetCoreModuleV2 -ErrorAction SilentlyContinue
    if (-not $ancm) {
        Write-Host 'AspNetCoreModuleV2 bulunamadı. Hosting Bundle kurun:' -ForegroundColor Yellow
        Write-Host '  https://dotnet.microsoft.com/permalink/dotnetcore-current-windows-runtime-bundle-installer'
        throw 'ASP.NET Core Hosting Bundle gerekli.'
    }
}

# Windows özellikleri: WebSocket
try {
    $ws = Get-WindowsOptionalFeature -Online -FeatureName IIS-WebSockets -ErrorAction SilentlyContinue
    if ($ws -and $ws.State -ne 'Enabled') {
        Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebSockets -All -NoRestart | Out-Null
        Write-Host 'IIS-WebSockets etkinleştirildi.' -ForegroundColor Green
    }
} catch {
    Write-Host "WebSocket özelliği atlandı: $($_.Exception.Message)" -ForegroundColor Yellow
}

New-Item -ItemType Directory -Force -Path $ApiOut, $WebOut | Out-Null

foreach ($pool in @($apiSite, $webSite)) {
    if (-not (Test-Path "IIS:\AppPools\$pool")) {
        New-WebAppPool -Name $pool | Out-Null
        Write-Host "App pool oluşturuldu: $pool"
    }
    Set-ItemProperty "IIS:\AppPools\$pool" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$pool" -Name startMode -Value 'AlwaysRunning'
    Set-ItemProperty "IIS:\AppPools\$pool" -Name processModel.idleTimeout -Value ([TimeSpan]::FromMinutes(0))
}

if (-not (Get-Website -Name $apiSite -ErrorAction SilentlyContinue)) {
    New-Website -Name $apiSite -PhysicalPath $ApiOut -ApplicationPool $apiSite -Port ([int]$ApiPort) -Force | Out-Null
    Write-Host "Site oluşturuldu: $apiSite :$ApiPort"
} else {
    Set-ItemProperty "IIS:\Sites\$apiSite" -Name physicalPath -Value $ApiOut
    Write-Host "Site güncellendi: $apiSite"
}

if (-not (Get-Website -Name $webSite -ErrorAction SilentlyContinue)) {
    New-Website -Name $webSite -PhysicalPath $WebOut -ApplicationPool $webSite -Port ([int]$WebPort) -Force | Out-Null
    Write-Host "Site oluşturuldu: $webSite :$WebPort"
} else {
    Set-ItemProperty "IIS:\Sites\$webSite" -Name physicalPath -Value $WebOut
    Write-Host "Site güncellendi: $webSite"
}

# WebSocket protokolü (SignalR)
foreach ($site in @($apiSite, $webSite)) {
    try {
        Set-WebConfigurationProperty -PSPath "IIS:\Sites\$site" -Filter 'system.webServer/webSocket' -Name 'enabled' -Value $true -ErrorAction SilentlyContinue
    } catch { }
}

# Opsiyonel: harekat-web /api → harekat-api reverse proxy
if ($EnableApiReverseProxy) {
    Write-Host 'ARR + URL Rewrite reverse proxy (/api) yapılandırılıyor...' -ForegroundColor Yellow
    $rewrite = Get-WebGlobalModule -Name RewriteModule -ErrorAction SilentlyContinue
    if (-not $rewrite) {
        Write-Host 'URL Rewrite modülü yok. https://www.iis.net/downloads/microsoft/url-rewrite' -ForegroundColor Yellow
    } else {
        $webConfig = Join-Path $WebOut 'web.config'
        $proxySnippet = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="HarekatApiProxy" stopProcessing="true">
          <match url="^api/(.*)" />
          <action type="Rewrite" url="http://127.0.0.1:$ApiPort/{R:1}" />
        </rule>
      </rules>
    </rewrite>
    <defaultDocument>
      <files>
        <clear />
        <add value="index.html" />
      </files>
    </defaultDocument>
  </system.webServer>
</configuration>
"@
        if (-not (Test-Path $webConfig)) {
            Set-Content -Path $webConfig -Value $proxySnippet -Encoding UTF8
            Write-Host "web.config yazıldı (proxy): $webConfig"
        } else {
            Write-Host "Mevcut web.config korundu — /api proxy için elle ekleyin." -ForegroundColor Yellow
        }
        try {
            Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/proxy' -Name 'enabled' -Value 'True' -ErrorAction SilentlyContinue
        } catch {
            Write-Host 'ARR proxy enable atlandı (ARR kurulu olmayabilir).' -ForegroundColor Yellow
        }
    }
}

# HTTPS notu (win-acme)
Write-Host ''
Write-Host 'HTTPS: win-acme ile Let''s Encrypt önerilir:' -ForegroundColor Cyan
Write-Host '  https://www.win-acme.com/  →  wacs.exe'
Write-Host "  Site: $webSite, binding :443"

Start-WebAppPool $apiSite
Start-WebAppPool $webSite
Start-Website $apiSite
Start-Website $webSite

Write-Host ''
Write-Host "Web  http://127.0.0.1:$WebPort/" -ForegroundColor Green
Write-Host "API  http://127.0.0.1:$ApiPort/health" -ForegroundColor Green
Write-Host 'DONE' -ForegroundColor Green
