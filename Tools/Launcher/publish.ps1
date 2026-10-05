#Requires -Version 5.1
param(
    [string]$OutputDir = "",
    [string]$Configuration = "Release",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
if (-not $OutputDir) { $OutputDir = Join-Path $root "Builds\Windows\Launcher" }

$proj = Join-Path $PSScriptRoot "src\Harekat.Launcher\Harekat.Launcher.csproj"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$args = @(
    "publish", $proj,
    "-c", $Configuration,
    "-r", "win-x64",
    "-o", $OutputDir,
    "--nologo"
)
if ($SelfContained) {
    $args += @("--self-contained", "true")
} else {
    $args += @("--self-contained", "false")
}

Write-Host "dotnet $($args -join ' ')"
& dotnet @args
if ($LASTEXITCODE -ne 0) { throw "publish başarısız" }
Write-Host "OK: $OutputDir"
