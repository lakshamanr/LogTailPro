# LogTailPro

A full-featured real-time log file viewer for Windows — built in C# + WPF + .NET 10.
Designed as a BareTailPro clone that goes beyond it.

## Quick Start

```powershell
.\start.ps1        # checks environment, shows instructions
claude             # open Claude Code in this directory
# then type: start
```

## Features (Target)

| Feature | Status |
|---|---|
| Real-time file tailing | Phase 1 |
| Follow-tail mode | Phase 1 |
| Files > 2GB | Phase 3 |
| Regex + plain text search | Phase 2 |
| Filter mode (include/exclude) | Phase 2 |
| Color highlight rules | Phase 5 |
| Multiple file tabs | Phase 4 |
| Recent files | Phase 4 |
| Desktop notifications | Phase 6 |
| Export filtered lines | Phase 6 |
| Log stats panel | Phase 6 |
| Single EXE, no installer | Phase 6 |

## Build Manually

```bash
dotnet restore src/LogTailPro/LogTailPro.csproj
dotnet build src/LogTailPro/LogTailPro.csproj
dotnet run --project src/LogTailPro/LogTailPro.csproj

# Release EXE
dotnet publish src/LogTailPro/LogTailPro.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

## Project Structure

See `CLAUDE.md` for full project structure and architecture decisions.

## Tech Stack
- C# 12 / .NET 10 / WPF
- MVVM via CommunityToolkit.Mvvm
- Settings via Newtonsoft.Json
- No external UI libraries
