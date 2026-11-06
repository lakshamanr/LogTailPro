@echo off
rem Creates ..\LogTailPro-source.zip without bin, obj, .git, .vs, publish, installer\out
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0zip-source.ps1"
pause
