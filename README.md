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

## MSI installer downloads

For published versions, download the MSI directly from [Releases](https://github.com/lakshamanr/LogTailPro/releases).
Pushing a tag matching the project version (for example `v1.3.5`) builds and publishes
that version with its MSI attached. Normal branch pushes continue to provide build artifacts.

Every push to any branch runs the [Build MSI workflow](https://github.com/lakshamanr/LogTailPro/actions/workflows/build-msi.yml).
Open the successful run for your commit and download `LogTailPro-win-x64-<commit SHA>`
from **Artifacts** (or use the download link in the run summary). Extract the ZIP to
get the Windows x64 MSI. Sign in to GitHub to download artifacts; builds are kept for 30 days.
You can also start a build with **Run workflow** on the workflow page.

The MSI version comes from `src/LogTailPro/LogTailPro.csproj`; the artifact name
identifies the exact commit. Increase the project version when shipping an upgrade
over an installed version.

To build locally, install the .NET 9 SDK and WiX 5.0.2:

```powershell
dotnet tool install -g wix --version 5.0.2
wix extension add -g WixToolset.UI.wixext/5.0.2
wix extension add -g WixToolset.Util.wixext/5.0.2
.\installer\build-msi.ps1
```

Output: `installer/out/LogTailPro-<version>-x64.msi`.

## Project Structure

See `CLAUDE.md` for full project structure and architecture decisions.

## Tech Stack
- C# 12 / .NET 10 / WPF
- MVVM via CommunityToolkit.Mvvm
- Settings via Newtonsoft.Json
- No external UI libraries
