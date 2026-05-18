@echo off
REM release.bat — Publish LogTailPro as a self-contained single EXE for Windows x64.
REM Output: .\publish\LogTailPro.exe
REM
REM Usage:
REM   release.bat            — publish to .\publish\
REM   release.bat clean      — wipe .\publish\ first, then publish

setlocal EnableDelayedExpansion

set PROJECT=src\LogTailPro\LogTailPro.csproj
set OUTDIR=publish
set RID=win-x64
set CONFIG=Release

echo.
echo ==================================================
echo   LogTailPro -- Release Publish
echo ==================================================
echo.

REM ── .NET SDK check ──────────────────────────────────────────────────────────
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [FAIL] dotnet.exe not found. Install .NET 10 SDK from https://dotnet.microsoft.com/download
    exit /b 1
)
for /f %%v in ('dotnet --version') do set DOTNET_VER=%%v
echo [ OK ] .NET SDK %DOTNET_VER%

REM ── Project file check ──────────────────────────────────────────────────────
if not exist "%PROJECT%" (
    echo [FAIL] Project not found: %PROJECT%
    exit /b 1
)
echo [ OK ] Project: %PROJECT%

REM ── Optional clean ──────────────────────────────────────────────────────────
if /i "%1"=="clean" (
    echo.
    echo ^>^> Cleaning publish directory: %OUTDIR%\
    if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"
    echo [ OK ] Cleaned.
)

REM ── Restore ─────────────────────────────────────────────────────────────────
echo.
echo ^>^> Restoring NuGet packages...
dotnet restore "%PROJECT%" --nologo -v q
if errorlevel 1 (
    echo [FAIL] Restore failed.
    exit /b 1
)
echo [ OK ] Restore complete.

REM ── Publish ─────────────────────────────────────────────────────────────────
echo.
echo ^>^> Publishing single-file EXE [%CONFIG% / %RID%]...
echo.

dotnet publish "%PROJECT%" ^
    -c %CONFIG% ^
    -r %RID% ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:PublishReadyToRun=true ^
    --nologo ^
    -o "%OUTDIR%"

if errorlevel 1 (
    echo.
    echo [FAIL] Publish failed -- check errors above.
    exit /b 1
)

REM ── Result ──────────────────────────────────────────────────────────────────
echo.
echo ==================================================
echo   Publish complete!
echo ==================================================
echo.

if exist "%OUTDIR%\LogTailPro.exe" (
    for %%f in ("%OUTDIR%\LogTailPro.exe") do (
        set /a SIZE_MB=%%~zf / 1048576
        echo   EXE : %OUTDIR%\LogTailPro.exe
        echo   Size: !SIZE_MB! MB
    )
) else (
    echo   Output : %OUTDIR%\
)

echo.
echo   To run:  %OUTDIR%\LogTailPro.exe
echo.
endlocal
