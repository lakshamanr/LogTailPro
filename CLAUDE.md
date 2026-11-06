# LogTailPro — Claude Code Agent Instructions

## What You Are Building
A full-featured real-time log file viewer for Windows, built in **C# + WPF + .NET 10**.
The end product must be a functional clone of **BareTailPro** — and then extend beyond it.
This is a commercial-quality desktop application. Do not cut corners.

---

## Golden Rules (Never Break These)
1. **Read CHANGELOG.md first, every session.** It tells you what changed in the plan.
2. **Read SKILL.md before writing any code.** It is at `./SKILL/technical skill/SKILL.md`.
3. **Read the current phase file before starting each phase.** They are in `./prompts/`.
4. **Always review the plan before implementing.** Output a summary of what you will do, wait, then build.
5. **Never skip a phase.** Each phase builds on the last.
6. **After each phase: build, fix all errors, then git commit.**
7. **Write clean, commented, production-quality C# code.**
8. **Use MVVM architecture strictly.** No business logic in code-behind.

---

## Special Commands

| You say | Claude does |
|---|---|
| `start` | Read CHANGELOG + SKILL + PHASE_1, build all phases sequentially (Phases 1–7) |
| `add process explorer` | Read CHANGELOG + prompts/PHASE_7_PROCESS_EXPLORER.md, implement Phase 7 only |
| `fix the UI` | Read UI_AGENT.md + prompts/UI_FIX.md, overhaul all XAML files |
| `continue` | Resume from last completed phase (check git log to find it) |
| `status` | Report which phases are complete based on git log |
| `the plan has changed` | Read CHANGELOG.md, re-read affected phase files, continue |

---

## How Plan Changes Work

Before every session, read CHANGELOG.md.
If the version is newer than what you last built, re-read the affected phase files listed in that changelog entry.
You do not need to redo completed phases unless the changelog explicitly says so.

---

## Git Workflow (After Every Phase)

Initialise git once at the very start:
```bash
git init
echo "bin/" > .gitignore
echo "obj/" >> .gitignore
echo "publish/" >> .gitignore
echo "*.user" >> .gitignore
echo ".vs/" >> .gitignore
git add -A
git commit -m "Initial scaffold: solution, project structure"
```

After each phase passes its build and checklist:
```bash
git add -A
git commit -m "Phase N complete: <one line description>"
```

Commit messages to use:
- Phase 1: `Phase 1 complete: core log viewer with real-time tailing and follow-tail`
- Phase 2: `Phase 2 complete: regex search, filter mode, results table, saved patterns`
- Phase 3: `Phase 3 complete: large file support, chunked tail load, Go To Line`
- Phase 4: `Phase 4 complete: multi-tab, drag reorder, tab positioning, CLI args`
- Phase 5: `Phase 5 complete: highlight rules, TAB expansion, settings dialog`
- Phase 6: `Phase 6 complete: notifications, configurable export, stats panel, EXE publish`
- Phase 7: `Phase 7 complete: Process Explorer — dotPeek-style panel, OutputDebugString capture, Launch & Attach, Event Log monitor`
- UI fix: `UI overhaul: full dark theme, design system, professional developer tool aesthetic`

Never commit broken code. Never commit bin/, obj/, or publish/.

---

## Tech Stack
- **Language:** C# 12
- **Framework:** .NET 10 (Windows)
- **UI:** WPF (Windows Presentation Foundation)
- **Architecture:** MVVM (Model-View-ViewModel)
- **Settings storage:** JSON (Newtonsoft.Json)
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
        │   ├── SavedSearchPattern.cs
        │   ├── SearchResultRow.cs
        │   ├── AppSettings.cs
        │   ├── ProcessInfo.cs              ← Phase 7
        │   └── ProcessAttachmentType.cs   ← Phase 7
        ├── ViewModels/
        │   ├── MainViewModel.cs
        │   ├── LogTabViewModel.cs
        │   ├── SearchViewModel.cs
        │   ├── SavedPatternsViewModel.cs
        │   ├── HighlightRulesViewModel.cs
        │   ├── SettingsViewModel.cs
        │   ├── ProcessItemViewModel.cs    ← Phase 7
        │   └── ProcessExplorerViewModel.cs ← Phase 7
        ├── Views/
        │   ├── LogTabView.xaml
        │   ├── LogTabView.xaml.cs
        │   ├── SearchPanel.xaml
        │   ├── SearchPanel.xaml.cs
        │   ├── HighlightRulesDialog.xaml
        │   ├── HighlightRulesDialog.xaml.cs
        │   ├── SettingsDialog.xaml
        │   ├── SettingsDialog.xaml.cs
        │   ├── ProcessExplorerPanel.xaml   ← Phase 7
        │   ├── ProcessExplorerPanel.xaml.cs ← Phase 7
        │   ├── LaunchProcessDialog.xaml    ← Phase 7
        │   └── LaunchProcessDialog.xaml.cs ← Phase 7
        ├── Services/
        │   ├── FileWatcherService.cs
        │   ├── LogParserService.cs
        │   ├── SearchService.cs
        │   ├── HighlightService.cs
        │   ├── FilterService.cs
        │   ├── SettingsService.cs
        │   ├── ProcessSnapshotService.cs   ← Phase 7
        │   ├── ProcessOutputCaptureService.cs ← Phase 7
        │   └── DebugOutputMonitorService.cs   ← Phase 7
        ├── Converters/
        │   ├── BoolToVisibilityConverter.cs
        │   ├── BoolToWrapConverter.cs
        │   ├── FileStatusColorConverter.cs
        │   ├── RuleToBackgroundConverter.cs
        │   ├── RuleToForegroundConverter.cs
        │   ├── NotEmptyConverter.cs
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
  Data Binding only
VIEWMODEL (CommunityToolkit.Mvvm)
  Interface calls only
SERVICES (business logic, file I/O)

MODELS (plain data classes)
```

- Views: XAML only, zero business logic in .xaml.cs (only UI wiring)
- ViewModels: commands, properties, state — no direct file I/O
- Services: all heavy lifting (file watching, search, filter, settings)
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

## Start Command Sequence

When the user says **"start"** or **"build"**, do this in order:

1. Read `./CHANGELOG.md` (check for plan changes)
2. Read `./SKILL/technical skill/SKILL.md` (full technical reference)
3. Read `./prompts/PHASE_1.md`
4. Scaffold the solution and project file
5. Implement Phase 1 fully
6. Build — fix all errors
7. Git commit Phase 1
8. Read `./prompts/PHASE_2.md` — implement, build, commit
9. Continue through all 7 phases sequentially
10. Phase 7 is `./prompts/PHASE_7_PROCESS_EXPLORER.md` — Process Explorer feature

**Never jump phases. Never skip the build step. Never skip the git commit.**

---

## Add Process Explorer Command Sequence

When the user says **"add process explorer"**, do this in order:

1. Read `./CHANGELOG.md` (confirm v2.0 is the current version)
2. Read `./prompts/PHASE_7_PROCESS_EXPLORER.md` completely
3. Output a one-paragraph summary of what you will implement, then wait for the user to confirm
4. Implement in this exact order:
   a. Models: `ProcessInfo.cs`, `ProcessAttachmentType.cs`
   b. Services: `ProcessSnapshotService.cs`, `ProcessOutputCaptureService.cs`, `DebugOutputMonitorService.cs`
   c. ViewModels: `ProcessItemViewModel.cs`, `ProcessExplorerViewModel.cs`
   d. Modify `LogTabViewModel.cs` — add process-capture constructor and fields
   e. Modify `MainViewModel.cs` — wire ProcessExplorer VM and new commands
   f. Views: `ProcessExplorerPanel.xaml + .xaml.cs`, `LaunchProcessDialog.xaml + .xaml.cs`
   g. Modify `MainWindow.xaml` — GridSplitter layout, toolbar button, View menu, Ctrl+Shift+P
   h. Modify `Models/AppSettings.cs` — add 5 new settings
5. Build: `dotnet build src/LogTailPro/LogTailPro.csproj` — fix ALL errors and warnings
6. Run through the **Manual Test Checklist** at the bottom of `PHASE_7_PROCESS_EXPLORER.md`
7. Git commit: `Phase 7 complete: Process Explorer — dotPeek-style panel, OutputDebugString capture, Launch & Attach, Event Log monitor`

---

## UI Fix Command Sequence

When the user says **"fix the UI"**, do this in order:

1. Read `./UI_AGENT.md` completely (design system authority)
2. Read `./prompts/UI_FIX.md` (execution steps)
3. Follow the fix order: Themes -> App.xaml -> MainWindow -> LogTabView -> Dialogs
4. Build — fix all XAML errors
5. Run through every item in the Visual QA Checklist (bottom of UI_AGENT.md)
6. Git commit with message: `UI overhaul: full dark theme, design system, professional developer tool aesthetic`

---

## Quality Standards
- All public classes and methods must have XML doc comments
- No `var` for non-obvious types
- Async/await for all file I/O — never block the UI thread
- Dispose all FileStream, StreamReader, FileSystemWatcher properly
- Handle file-locked, file-deleted, file-truncated scenarios gracefully
- Never hardcode colour values in XAML — always use named theme resources
- Target: zero compiler warnings

---

## Definition of Done
The app is complete when:
- [ ] User can open any log file (drag-drop or File menu)
- [ ] Log lines stream in real-time as file grows
- [ ] Follow-tail toggle works (auto-scroll to bottom)
- [ ] Regex and plain-text search works with live highlight
- [ ] Search results shown in sortable table (Timestamp | Line# | Text | Groups)
- [ ] Saved/named search patterns work
- [ ] Filter mode (show only / exclude lines) works
- [ ] Color highlight rules are configurable and persist
- [ ] Multiple files open in tabs simultaneously
- [ ] Files > 2GB open without crash or freeze
- [ ] TAB expansion works (configurable width)
- [ ] IIS log format supported (null-terminated lines)
- [ ] Network / UNC path files supported
- [ ] Command line arguments work (-wp, -ws, -tc, -ti)
- [ ] App launches in < 2 seconds
- [ ] Single EXE ships with no installer required
- [ ] UI matches the design system in UI_AGENT.md
- [ ] Process Explorer panel opens/closes with Ctrl+Shift+P (Phase 7)
- [ ] All running processes shown in a tree view with CPU% and memory (Phase 7)
- [ ] OutputDebugString capture streams into a log tab (Phase 7)
- [ ] Launch & Attach dialog starts any EXE and streams stdout/stderr (Phase 7)
- [ ] Windows Event Log entries route to a log tab (Phase 7)
- [ ] Panel width and visibility persist across restarts (Phase 7)
