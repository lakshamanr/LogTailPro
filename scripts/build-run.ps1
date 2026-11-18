#!/usr/bin/env pwsh
# build-run.ps1 -- Build LogTailPro (Debug) and launch it.
# Usage:  .\build-run.ps1           # build + run
#         .\build-run.ps1 -BuildOnly # build only, no launch
#         .\build-run.ps1 -Clean     # clean build artifacts first

param(
    [switch]$BuildOnly,
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path "$PSScriptRoot\..").Path
$ProjectFile = Join-Path $ProjectRoot "src\LogTailPro\LogTailPro.csproj"
$BinDir      = Join-Path $ProjectRoot "src\LogTailPro\bin\Debug\net9.0-windows7.0"
if (-not (Test-Path $BinDir)) {
    $alt = Join-Path $ProjectRoot "src\LogTailPro\bin\Debug\net9.0-windows"
    if (Test-Path $alt) { $BinDir = $alt }
}
$ExeName     = "LogTailPro.exe"
$Sep         = "=" * 52

function Write-Step([string]$text) { Write-Host ""; Write-Host ">> $text" -ForegroundColor Yellow }
function Write-OK([string]$text)   { Write-Host "[OK]   $text" -ForegroundColor Green }
function Write-Fail([string]$text) { Write-Host "[FAIL] $text" -ForegroundColor Red }

# ----- Pre-flight checks ------------------------------------------------------

Write-Host ""
Write-Host $Sep -ForegroundColor Cyan
Write-Host "  LogTailPro -- Debug Build and Run" -ForegroundColor Cyan
Write-Host $Sep -ForegroundColor Cyan

# .NET SDK version check (supports system dotnet or bundled portable SDK)
$dotnetCmd = "dotnet"
$portableDotnet = Join-Path $ProjectRoot "installer\out\dotnet\dotnet.exe"
$sdkVer = & $dotnetCmd --version 2>$null
$major = 0
if ($sdkVer) { $major = [int]($sdkVer -split '\.')[0] }
if ($major -lt 9 -and (Test-Path $portableDotnet)) {
    $dotnetCmd = $portableDotnet
    $sdkVer = & $dotnetCmd --version 2>$null
    if ($sdkVer) { $major = [int]($sdkVer -split '\.')[0] }
}
if ($major -lt 9) {
    Write-Fail ".NET 9 or higher required -- found $sdkVer."
    exit 1
}
Write-OK ".NET SDK $sdkVer"

# Project file check
if (-not (Test-Path $ProjectFile)) {
    Write-Fail "Project not found: $ProjectFile"
    exit 1
}
Write-OK "Project: $ProjectFile"

# ----- Optional clean ---------------------------------------------------------

if ($Clean) {
    Write-Step "Cleaning build artifacts..."
    & $dotnetCmd clean $ProjectFile -c Debug --nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Fail "Clean failed."; exit $LASTEXITCODE }
    Write-OK "Clean complete."
}

# ----- Restore ----------------------------------------------------------------

Write-Step "Restoring NuGet packages..."
& $dotnetCmd restore $ProjectFile --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Fail "Restore failed."; exit $LASTEXITCODE }
Write-OK "Restore complete."

# ----- Build ------------------------------------------------------------------

Write-Step "Building (Debug)..."
$buildStart = Get-Date
& $dotnetCmd build $ProjectFile -c Debug --no-restore --nologo
if ($LASTEXITCODE -ne 0) { Write-Fail "Build failed -- check errors above."; exit $LASTEXITCODE }
$buildSecs = [math]::Round(((Get-Date) - $buildStart).TotalSeconds, 1)
Write-OK "Build succeeded in ${buildSecs}s."

# ----- Run --------------------------------------------------------------------

if ($BuildOnly) {
    Write-Host ""
    Write-Host "Build-only mode -- skipping launch." -ForegroundColor Gray
    exit 0
}

$exe = Join-Path $BinDir $ExeName
if (-not (Test-Path $exe)) {
    Write-Fail "Executable not found at: $exe"
    exit 1
}

Write-Step "Launching LogTailPro..."
Write-Host "  $exe" -ForegroundColor Gray
Write-Host ""
Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe)
Write-OK "Launched. This window can be closed."
Write-Host ""
