<#
.SYNOPSIS
  Publishes LogTailPro (self-contained, single file) and builds the MSI installer.
.DESCRIPTION
  Requires the WiX Toolset v5 dotnet tool:  dotnet tool install -g wix --version 5.0.2
  and its UI + Util extensions:             wix extension add -g WixToolset.UI.wixext/5.0.2
                                            wix extension add -g WixToolset.Util.wixext/5.0.2
  Output: installer\out\LogTailPro-<version>-x64.msi
#>
$ErrorActionPreference = 'Stop'
$root    = Split-Path -Parent $PSScriptRoot
$csproj  = Join-Path $root 'src\LogTailPro\LogTailPro.csproj'
$publish = Join-Path $root 'publish'
$outDir  = Join-Path $PSScriptRoot 'out'

# Version comes from the csproj so the MSI and the EXE always match.
$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'Could not read <Version> from the csproj.' }

Write-Host "Publishing LogTailPro $version ..."
dotnet publish $csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

New-Item -ItemType Directory -Force $outDir | Out-Null
$msi = Join-Path $outDir "LogTailPro-$version-x64.msi"

Write-Host "Building $msi ..."
wix build (Join-Path $PSScriptRoot 'LogTailPro.wxs') `
    -arch x64 `
    -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext `
    -d Version=$version -d PublishDir="$publish\" -d ProjectDir="$PSScriptRoot\" `
    -o $msi
if ($LASTEXITCODE -ne 0) { throw 'wix build failed.' }

Write-Host "Done: $msi"
