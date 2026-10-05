#Requires -Version 5.1
param(
    [string]$SourceDir = "",
    [string]$OutputDir = "",
    [string]$Version = "0.1.0",
    [string]$IsccPath = "",
    [switch]$Sign,
    [string]$PfxPath = "",
    [string]$PfxPassword = ""
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
if (-not $SourceDir) { $SourceDir = Join-Path $root "Builds\Windows" }
if (-not $OutputDir) { $OutputDir = Join-Path $root "Builds\Installer" }

if (-not (Test-Path $SourceDir)) {
    throw "Kaynak klasör yok: $SourceDir — önce Unity Windows build alın."
}

if (-not $IsccPath) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    $IsccPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $IsccPath -or -not (Test-Path $IsccPath)) {
    throw "ISCC.exe bulunamadı. Inno Setup 6 kurun veya -IsccPath verin."
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$iss = Join-Path $PSScriptRoot "Harekat.iss"

Write-Host "Inno Setup: $IsccPath"
Write-Host "Source: $SourceDir"
Write-Host "Output: $OutputDir"
Write-Host "Version: $Version"

& $IsccPath $iss `
    "/DMyAppVersion=$Version" `
    "/DSourceDir=$SourceDir" `
    "/DOutputDir=$OutputDir"

if ($LASTEXITCODE -ne 0) { throw "ISCC başarısız: $LASTEXITCODE" }

$setup = Join-Path $OutputDir "HarekatSetup-$Version.exe"
if (-not (Test-Path $setup)) {
    $setup = Get-ChildItem $OutputDir -Filter "HarekatSetup-*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}

if ($Sign) {
    if (-not $PfxPath) { $PfxPath = $env:HAREKAT_CODESIGN_PFX }
    if (-not $PfxPassword) { $PfxPassword = $env:HAREKAT_CODESIGN_PASSWORD }
    if (-not $PfxPath -or -not (Test-Path $PfxPath)) {
        throw "İmzalama için -PfxPath veya HAREKAT_CODESIGN_PFX gerekli."
    }
    $signtool = Get-Command signtool -ErrorAction SilentlyContinue
    if (-not $signtool) {
        throw "signtool.exe PATH'te yok (Windows SDK)."
    }
    & signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f $PfxPath /p $PfxPassword $setup
    & signtool verify /pa $setup
}

Write-Host "OK: $setup"
