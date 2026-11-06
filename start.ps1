#!/usr/bin/env pwsh
# LogTailPro - Start Script
# Run this first. It checks your environment and tells Claude Code exactly what to do.

Write-Host ""
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "         LogTailPro - Build System               " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""

# Check .NET 8 or higher
$dotnetVersion = dotnet --version 2>$null
$major = [int]($dotnetVersion -split '\.')[0]
if ($major -ge 8) {
    Write-Host "[OK] .NET SDK: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "[FAIL] .NET 8 or higher required. Found: $dotnetVersion" -ForegroundColor Red
    Write-Host "       Download from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Check Windows
if ($IsWindows -or $env:OS -eq "Windows_NT") {
    Write-Host "[OK] Platform: Windows (WPF supported)" -ForegroundColor Green
} else {
    Write-Host "[FAIL] WPF requires Windows. Current platform is not supported." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "-------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""
Write-Host "PROJECT FILES READY:" -ForegroundColor White
Write-Host "  CLAUDE.md                 - Agent master instructions" -ForegroundColor Gray
Write-Host "  SKILL/technical skill/SKILL.md               - Technical reference (WPF/C# patterns)" -ForegroundColor Gray
Write-Host "  prompts/PHASE_1.md        - Core viewer" -ForegroundColor Gray
Write-Host "  prompts/PHASE_2.md        - Search and filter" -ForegroundColor Gray
Write-Host "  prompts/PHASE_3.md        - Large file support" -ForegroundColor Gray
Write-Host "  prompts/PHASE_4.md        - Multi-tab and file management" -ForegroundColor Gray
Write-Host "  prompts/PHASE_5.md        - Highlight rules and settings" -ForegroundColor Gray
Write-Host "  prompts/PHASE_6.md        - Extensions and publish" -ForegroundColor Gray
Write-Host "  references/WPF_QUICK_REF.md - WPF cheat sheet" -ForegroundColor Gray
Write-Host ""
Write-Host "-------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""
Write-Host "TO START BUILDING:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  1. Open Claude Code in this directory:" -ForegroundColor White
Write-Host "       cd $PSScriptRoot" -ForegroundColor Cyan
Write-Host "       claude" -ForegroundColor Cyan
Write-Host ""
Write-Host "  2. Then type exactly this one word:" -ForegroundColor White
Write-Host "       start" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Claude Code will then:" -ForegroundColor White
Write-Host "     - Read CLAUDE.md and SKILL.md" -ForegroundColor Gray
Write-Host "     - Read PHASE_1.md and scaffold the solution" -ForegroundColor Gray
Write-Host "     - Build each phase sequentially" -ForegroundColor Gray
Write-Host "     - Run dotnet build after each phase" -ForegroundColor Gray
Write-Host "     - Deliver LogTailPro.exe in ./publish/" -ForegroundColor Gray
Write-Host ""
Write-Host "-------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""
Write-Host "ESTIMATED BUILD TIME: 2-4 hours (Claude Code running unattended)" -ForegroundColor Yellow
Write-Host ""
