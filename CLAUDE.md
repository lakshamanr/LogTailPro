# LogTailPro — Claude Code Agent Instructions

## What You Are Building
A full-featured real-time log file viewer for Windows, built in **C# + WPF + .NET 10**.
The end product must be a functional clone of **BareTailPro** — and then extend beyond it.
This is a commercial-quality desktop application. Do not cut corners.

## Golden Rules (Never Break These)
1. **Read the SKILL.md before writing any code.** It is at `./SKILL.md`.
2. **Read the current phase file before starting each phase.** They are in `./prompts/`.
3. **Always review the plan before implementing.** Output a summary of what you will do, wait, then build.
4. **Never skip a phase.** Each phase builds on the last.
5. **After each phase, run the build and fix all errors before moving on.**
6. **Write clean, commented, production-quality C# code.**
7. **Use MVVM architecture strictly.** No business logic in code-behind.

---

## Tech Stack
- **Language:** C# 12
- **Framework:** .NET 10 (Windows)
- **UI:** WPF (Windows Presentation Foundation)
- **Architecture:** MVVM (Model-View-ViewModel)
- **Settings storage:** JSON (System.Text.Json)
- **Packaging:** Single-file self-contained EXE via `dotnet publish`
- **NuGet packages allowed:**
  - `CommunityToolkit.Mvvm` — MVVM helpers
  - `Microsoft.Xaml.Behaviors.Wpf` — XAML behaviors
  - `Newtonsoft.Json` — settings serialization

---

## Project Structure (Create Exactly This)
```
LogTailPro/
├── LogTailPro.sln
└── src/
    └── LogTailPro/
        ├── LogTailPro.csproj
        ├── App.xaml
        ├── App.xaml.cs
        ├── MainWindow.xaml
        ├── MainWindow.xaml.cs
        ├── Models/
        │   ├── LogFile.cs
        │   ├── LogLine.cs
        │   ├── HighlightRule.cs
        │   ├── FilterProfile.cs
        │   └── AppSettings.cs
        ├── ViewModels/
        │   ├── MainViewModel.cs
        │   ├── LogTabViewModel.cs
        │   ├── SearchViewModel.cs
        │   └── SettingsViewModel.cs
        ├── Views/
        │   ├── LogTabView.xaml
        │   ├── LogTabView.xaml.cs
        │   ├── SearchPanel.xaml
        │   ├── SearchPanel.xaml.cs
        │   ├── HighlightRulesDialog.xaml
        │   ├── HighlightRulesDialog.xaml.cs
        │   ├── SettingsDialog.xaml
        │   └── SettingsDialog.xaml.cs
        ├── Services/
        │   ├── FileWatcherService.cs
        │   ├── LogParserService.cs
        │   ├── SearchService.cs
        │   ├── HighlightService.cs
        │   ├── FilterService.cs
        │   └── SettingsService.cs
        ├── Converters/
        │   ├── BoolToVisibilityConverter.cs
        │   ├── LineNumberConverter.cs
        │   └── HighlightColorConverter.cs
        ├── Controls/
        │   └── VirtualLogViewer.cs
        ├── Themes/
        │   ├── Dark.xaml
        │   └── Light.xaml
        └── Assets/
            └── app.ico
```

---

## Architecture: MVVM Strictly

```
VIEW (XAML)
  ↕ Data Binding only
VIEWMODEL (CommunityToolkit.Mvvm)
  ↕ Interface calls only
SERVICES (business logic, file I/O)
  ↕
MODELS (plain data classes)
```

- Views: XAML only, zero business logic in .xaml.cs (only wiring)
- ViewModels: commands, properties, state — no direct file I/O
- Services: all heavy lifting (file watching, search, filter)
- Models: pure data, no behavior

---

## Build & Run Commands

```bash
# Restore packages
dotnet restore src/LogTailPro/LogTailPro.csproj

# Build debug
dotnet build src/LogTailPro/LogTailPro.csproj

# Run
dotnet run --project src/LogTailPro/LogTailPro.csproj

# Publish single EXE
dotnet publish src/LogTailPro/LogTailPro.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish
```

---

## Start Command — Read This First

When the user says **"start"** or **"build"**, do this in order:

1. Read `./SKILL.md` (full technical reference)
2. Read `./prompts/PHASE_1.md`
3. Scaffold the solution and project file
4. Implement Phase 1 fully
5. Build — fix all errors
6. Confirm Phase 1 complete, then read `./prompts/PHASE_2.md`
7. Continue through all phases sequentially

**Never jump phases. Never skip the build step.**

---

## Quality Standards
- All public classes and methods must have XML doc comments
- No `var` for non-obvious types
- Async/await for all file I/O — never block the UI thread
- Dispose all FileStream, StreamReader, FileSystemWatcher properly
- Handle file-locked, file-deleted, file-truncated scenarios gracefully
- Target: zero compiler warnings

---

## Definition of Done
The app is complete when:
- [ ] User can open any log file (drag-drop or File menu)
- [ ] Log lines stream in real-time as file grows
- [ ] Follow-tail toggle works (auto-scroll to bottom)
- [ ] Regex and plain-text search works with live highlight
- [ ] Filter mode (show only / exclude lines) works
- [ ] Color highlight rules are configurable and persist
- [ ] Multiple files open in tabs simultaneously
- [ ] Files > 2GB open without crash or freeze
- [ ] App launches in < 2 seconds
- [ ] Single EXE ships with no installer required
