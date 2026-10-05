#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
  HAREKÂT — Windows güvenlik duvarı kuralları
.DESCRIPTION
  Açılan portlar:
    TCP 80, 443     — Web (IIS)
    TCP 3208        — Backend API
    TCP 3210        — Wiki (oyuncu kılavuzu)
    TCP/UDP 7777-7900 — Oyun sunucuları (ServerManager port havuzu)
    TCP 9183        — ServerManager /metrics (opsiyonel, yerel scrape)
.EXAMPLE
  .\firewall.ps1
  .\firewall.ps1 -Remove
#>
param(
    [switch]$Remove,
    [switch]$OpenMetrics
)

$ErrorActionPreference = 'Stop'
$prefix = 'Harekat'

$rules = @(
    @{ Name = "$prefix-Web-80";     Proto = 'TCP'; Port = '80' },
    @{ Name = "$prefix-Web-443";    Proto = 'TCP'; Port = '443' },
    @{ Name = "$prefix-API-3208";   Proto = 'TCP'; Port = '3208' },
    @{ Name = "$prefix-Wiki-3210";  Proto = 'TCP'; Port = '3210' },
    @{ Name = "$prefix-Game-UDP";   Proto = 'UDP'; Port = '7777-7900' },
    @{ Name = "$prefix-Game-TCP";   Proto = 'TCP'; Port = '7777-7900' }
)

if ($OpenMetrics) {
    $rules += @{ Name = "$prefix-Metrics-9183"; Proto = 'TCP'; Port = '9183' }
}

Write-Host '==> HAREKÂT firewall' -ForegroundColor Cyan

foreach ($r in $rules) {
    $existing = Get-NetFirewallRule -DisplayName $r.Name -ErrorAction SilentlyContinue
    if ($Remove) {
        if ($existing) {
            Remove-NetFirewallRule -DisplayName $r.Name
            Write-Host "Silindi: $($r.Name)"
        }
        continue
    }

    if ($existing) {
        Write-Host "Zaten var: $($r.Name)"
        continue
    }

    New-NetFirewallRule `
        -DisplayName $r.Name `
        -Direction Inbound `
        -Action Allow `
        -Protocol $r.Proto `
        -LocalPort $r.Port `
        -Profile Any `
        -ErrorAction Stop | Out-Null
    Write-Host "Eklendi: $($r.Name) $($r.Proto)/$($r.Port)" -ForegroundColor Green
}

if ($Remove) {
    Write-Host 'Firewall kuralları kaldırıldı.' -ForegroundColor Yellow
} else {
    Write-Host 'Firewall kuralları hazır.' -ForegroundColor Green
}
