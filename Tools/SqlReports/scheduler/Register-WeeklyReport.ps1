#Requires -Version 5.1
<#
.SYNOPSIS
  Windows Görev Zamanlayıcısı'na haftalık KPI raporu kaydeder.

.DESCRIPTION
  Varsayılan: her Pazartesi 07:00 (yerel saat).
  Yönetici PowerShell gerekebilir.

.EXAMPLE
  .\Register-WeeklyReport.ps1
  .\Register-WeeklyReport.ps1 -Time '08:30' -DayOfWeek Monday
#>
[CmdletBinding()]
param(
  [string]$TaskName = 'Harekat-SqlReports-Weekly',
  [string]$DayOfWeek = 'Monday',
  [string]$Time = '07:00',
  [string]$ReportRoot = ''
)

$ErrorActionPreference = 'Stop'

if (-not $ReportRoot) {
  $ReportRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}

$runner = Join-Path $PSScriptRoot 'Run-WeeklyReport.ps1'
if (-not (Test-Path $runner)) { throw "Bulunamadı: $runner" }

$arg = "-NoProfile -ExecutionPolicy Bypass -File `"$runner`" -ReportRoot `"$ReportRoot`""
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arg
$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek $DayOfWeek -At $Time
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -StartWhenAvailable -DontStopIfGoingOnBatteries
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null

Write-Host "Görev kaydedildi: $TaskName"
Write-Host "Zaman: her $DayOfWeek $Time"
Write-Host "Betik: $runner"
Write-Host "İpucu: bağlantı dizesini kullanıcı ortamına HAREKAT_SQL_CONNECTION olarak ekleyin."
Write-Host "Kaldırmak için: Unregister-ScheduledTask -TaskName '$TaskName' -Confirm:`$false"
