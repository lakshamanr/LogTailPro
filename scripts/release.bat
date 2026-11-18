@echo off
REM release.bat -- Publish LogTailPro as a self-contained single EXE (win-x64 Release).
REM Output: .\publish\LogTailPro.exe
REM
REM Usage:
REM   release.bat          -- publish to .\publish\
REM   release.bat clean    -- wipe .\publish\ first, then publish

setlocal EnableDelayedExpansion

set ROOT=%~dp0..
set PROJECT=%ROOT%\src\LogTailPro\LogTailPro.csproj
set OUTDIR=%ROOT%\publish
set RID=win-x64
set CONFIG=Release

echo.
echo ==================================================
echo   LogTailPro -- Release Publish
echo ==================================================
echo.

REM --- .NET SDK check (supports system dotnet or bundled portable SDK) ---
set DOTNET_CMD=dotnet
if exist "%ROOT%\installer\out\dotnet\dotnet.exe" (
    for /f "tokens=1 delims=." %%a in ('dotnet --version 2^>nul') do (
        if %%a lss 9 set DOTNET_CMD="%ROOT%\installer\out\dotnet\dotnet.exe"
    )
)

%DOTNET_CMD% --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [FAIL] dotnet.exe not found. Install .NET 9 SDK or higher.
    exit /b 1
)
for /f "tokens=*" %%v in ('%DOTNET_CMD% --version') do set DOTNET_VER=%%v
echo [ OK ] .NET SDK %DOTNET_VER%

REM --- Project file check ---
if not exist "%PROJECT%" (
    echo [FAIL] Project not found: %PROJECT%
    exit /b 1
)
echo [ OK ] Project: %PROJECT%

REM --- Stop any running instance so the EXE is not locked ---
taskkill /f /im LogTailPro.exe >nul 2>&1

REM --- Optional clean ---
if /i "%1"=="clean" (
    echo.
    echo [....] Cleaning: %OUTDIR%\
    if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"
    echo [ OK ] Cleaned.
)

REM --- Restore ---
echo.
echo [....] Restoring NuGet packages...
%DOTNET_CMD% restore "%PROJECT%" --nologo -v q
if %errorlevel% neq 0 (
    echo [FAIL] Restore failed.
    exit /b 1
)
echo [ OK ] Restore complete.

REM --- Publish ---
echo.
echo [....] Publishing [%CONFIG% / %RID%] ...
echo.

%DOTNET_CMD% publish "%PROJECT%" -c %CONFIG% -r %RID% --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishReadyToRun=true --nologo -o "%OUTDIR%"

if %errorlevel% neq 0 (
    echo.
    echo [FAIL] Publish failed -- see errors above.
    exit /b 1
)

REM --- Result ---
echo.
echo ==================================================
echo   Publish complete!
echo ==================================================
echo.

set EXEPATH=%OUTDIR%\LogTailPro.exe
if exist "%EXEPATH%" (
    for %%f in ("%EXEPATH%") do set FILESIZE=%%~zf
    set /a SIZE_MB=!FILESIZE! / 1048576
    echo   EXE  : %EXEPATH%
    echo   Size : !SIZE_MB! MB
) else (
    echo   Output folder: %OUTDIR%\
)

echo.
echo   To run: %EXEPATH%
echo.
endlocal
