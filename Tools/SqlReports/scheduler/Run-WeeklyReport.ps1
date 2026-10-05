#Requires -Version 5.1
<#
.SYNOPSIS
  Haftalık HAREKÂT KPI raporunu üretir (Node report.mjs).

.DESCRIPTION
  Windows Görev Zamanlayıcısı veya elle çağrılır.
  Ortam değişkeni HAREKAT_SQL_CONNECTION yoksa örnek veriyle HTML üretir.

.PARAMETER ReportRoot
  Tools/SqlReports klasörü.

.PARAMETER OutDir
  HTML çıktı klasörü (varsayılan: ReportRoot\out).
#>
[CmdletBinding()]
param(
  [string]$ReportRoot = $PSScriptRoot + '\..',
  [string]$OutDir = ''
)

$ErrorActionPreference = 'Stop'
$ReportRoot = [System.IO.Path]::GetFullPath($ReportRoot)
if (-not $OutDir) { $OutDir = Join-Path $ReportRoot 'out' }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$outFile = Join-Path $OutDir "kpi-weekly-$stamp.html"

Push-Location $ReportRoot
try {
  if (-not (Test-Path (Join-Path $ReportRoot 'node_modules\mssql'))) {
    Write-Host 'npm install çalıştırılıyor...'
    npm install --omit=dev
  }

  $env:NODE_ENV = 'production'
  node report.mjs --out $outFile
  if ($LASTEXITCODE -ne 0) { throw "report.mjs exit $LASTEXITCODE" }

  $latest = Join-Path $OutDir 'kpi-report.html'
  Copy-Item -Force $outFile $latest
  Write-Host "Tamam: $outFile"
  Write-Host "Güncel kopya: $latest"
}
finally {
  Pop-Location
}
