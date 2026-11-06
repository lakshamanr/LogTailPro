# Plan Changelog

Claude Code must read this file BEFORE starting any phase.
If the version here is newer than what you last built, re-read the affected phase files.

---

## v2.4 - 2026-11-06 (CURRENT) - ROADMAP PHASE 3: REQUEST/TRACE ID CORRELATION

- Implemented roadmap Phase 3: Request/Trace ID correlation across open logs.
- Added CorrelationService with exact-value matching for common trace/request fields (TraceId, @tr, RequestId, CorrelationId, SpanId, TransactionId), custom nested JSON properties, and plain-text tokens with strict boundaries.
- Excluded partial substring matches (e.g. req-123 never matches req-12345 or pre-req-123; abc never matches abcdef).
- Added suggested correlation ID extraction recognizing structured keys, key-value patterns, prefixed tokens (req-*, trace-*, corr-*), GUIDs, and W3C trace IDs.
- Added CorrelateIdDialog allowing users to inspect and select open file sources, with Select/Deselect All buttons and search scope explanation.
- Added live correlation tabs with dedicated source and UTC timestamp columns, sorting chronologically with stable tie-breaking and bounded rendering (with truncation notices).
- Integrated correlation actions in LogTabView context menu, DetailsPanel header button, and DetailsPanel property tree context menu ('Correlate by Value').
- Preserved navigation back to original lines via OpenOriginalLineCommand.
- Added unit checks in Timeline.Tests and real-file live tailing integration checks in Timeline.IntegrationTests.

---

## v2.3 - 2026-11-06 - ROADMAP PHASE 2: STRUCTURED JSON LOG VIEWER

- Implemented roadmap Phase 2: structured JSON log viewer for line-delimited JSON and Serilog CLEF format.
- Added StructuredLogEvent preserving raw text, exact timestamps with offsets, normalized levels, rendered message vs template distinction, exceptions, and typed properties (primitives, nested objects, and arrays).
- Added JsonLogParser with safety limits (max length 2MB, max depth 32) and graceful per-line malformed JSON handling.
- Added TypedFieldFilter with support for numeric comparisons (>, <, >=, <=), exact string equality (==, !=), existence (exists, !exists), dot-notation for nested fields, and defined type-mismatch/missing semantics.
- Integrated structured timestamps into TimelineService for accurate UTC ordering across mixed JSON and plain-text files.
- Upgraded DetailsPanel into a hierarchical Property Inspector with summary card, property tree (LogPropertyNode), exception viewer with copy button, and raw/pretty-printed text tab.
- Added structured Level badge and Timestamp columns to LogTabView with a toggle for Structured View vs Raw Text View in Toolbar, View menu, and context menu.
- Added unit checks to Timeline.Tests and real-file integration checks to Timeline.IntegrationTests.

---

## v2.2 - 2026-11-06 - LOG INVESTIGATION ROADMAP

- Added prompts/ROADMAP.md with six prioritized phases and acceptance criteria.
- Roadmap Phase 1 is the combined live timeline; existing Phases 1-7 retain their numbering.
- Implemented roadmap Phase 1 in app 1.4.0: selectable live combined timeline, UTC ordering, source columns, original navigation, source lifecycle and independent filters. Added service and real-file integration checks to CI.
- Phases 2-6 remain planned. Application/MSI version is independent of this plan version.

---

## v2.1 - 2026-11-06 (previous plan) — PROCESS EXPLORER: LOG DISCOVERY + PATTERN SEARCH

### Changes
- **Open Log Files:** right-click a process (or double-click / Enter) to locate its log files and open
  them in normal tabs. Discovery combines three sources and merges/scores the results:
  - **Open handles** — files the process currently has open (needs no config; best effort for other users' processes)
  - **Config files** next to the EXE — NLog, log4net, Serilog, appsettings*.json, *.config, ini/yaml/properties;
    expands `${basedir}`, `${specialfolder}`, `%ENV%`, `~/`, relative paths and turns dated/rolling names
    (`${shortdate}`, `%date{…}`, `{Date}`, `app-.txt`, `yyyyMMdd`) into wildcards resolved to real files
  - **Folder scan** — recent `*.log/*.txt/*.out/*.err` in the EXE folder, `logs\`, ProgramData, LocalAppData, Temp
- **Picker dialog** lists candidates (size, last written, how found), pre-ticks the best one (★) per process.
- **Pattern search** in the explorer: plain text, wildcards (`w3wp*, sqlservr*`), or regex (new "Regex" toggle);
  matches name, PID, path and user. Invalid regex shows the error and keeps the last good list.
- **"Logs (N)"** button opens the logs of every process matching the current search (max 25) in one picker.

### New files
`Models/LogCandidate.cs`, `Services/ProcessLogDiscoveryService.cs`, `Services/ProcessSearchMatcher.cs`,
`ViewModels/LogDiscoveryViewModel.cs`, `Views/LogDiscoveryDialog.xaml(.cs)`

### Modified files
`ProcessItemViewModel`, `ProcessExplorerViewModel`, `MainViewModel`, `ProcessExplorerPanel.xaml(.cs)`,
`AppSettings` (+`ProcessExplorerRegexSearch`)

### Not included (follow-ups)
"Has logs" filter (would run discovery on every refresh) and command-line matching (needs PEB reads).

---

## v2.0 - 2026-05-21 — PROCESS EXPLORER

### Changes
- **New feature:** Phase 7 — Process Explorer side panel (JetBrains dotPeek-style)
- **New file:** `prompts/PHASE_7_PROCESS_EXPLORER.md` — full implementation guide
- **Affects:** All existing phases remain complete. Phase 7 is purely additive.

### What Phase 7 adds
- Collapsible left panel listing all running Windows processes in a parent-child tree
- Search/filter by name or PID; .NET-only filter; system process filter; auto-refresh
- OutputDebugString capture via Win32 shared memory (DebugView technique — all processes)
- Windows Event Log monitoring per process
- Launch & Attach dialog: start any EXE and stream its stdout or stderr into a new tab
- Process details strip (PID, path, user, CPU%, memory, start time)
- Context menu: Attach, Open in Explorer, Copy PID/Path, Kill Process
- Panel width and visibility persist in `AppSettings`
- Ctrl+Shift+P keyboard shortcut to toggle the panel
- All captured output uses the existing search / highlight / filter / bookmark pipeline

### New files
| File | Purpose |
|---|---|
| `Models/ProcessInfo.cs` | Process snapshot model |
| `Models/ProcessAttachmentType.cs` | Capture mode enum |
| `Services/ProcessSnapshotService.cs` | Process enumeration + CPU/memory metrics |
| `Services/ProcessOutputCaptureService.cs` | stdout/stderr capture for launched processes |
| `Services/DebugOutputMonitorService.cs` | OutputDebugString via Win32 (DebugView technique) |
| `ViewModels/ProcessItemViewModel.cs` | Tree node ViewModel |
| `ViewModels/ProcessExplorerViewModel.cs` | Panel root ViewModel |
| `Views/ProcessExplorerPanel.xaml` | Side panel UI |
| `Views/LaunchProcessDialog.xaml` | Launch & Attach dialog |

### Modified files
| File | Change |
|---|---|
| `Models/AppSettings.cs` | 5 new Process Explorer settings |
| `ViewModels/LogTabViewModel.cs` | 3rd constructor for process-capture mode |
| `ViewModels/MainViewModel.cs` | ProcessExplorer VM, OpenProcessTabAsync, ToggleProcessExplorerCommand |
| `MainWindow.xaml` | GridSplitter layout, panel slot, toolbar button, View menu item, Ctrl+Shift+P |

### Build commands used
```
dotnet restore src/LogTailPro/LogTailPro.csproj
dotnet build   src/LogTailPro/LogTailPro.csproj -c Debug
dotnet build   src/LogTailPro/LogTailPro.csproj -c Release
```

---

## v1.5 - 2026-05-18 (PREVIOUS FINAL) — FINAL RELEASE

### Changes
- **Logo:** New SVG logo (`Assets/logo.svg`) — dark navy background, cyan log-lines, amber tail arrow
- **Icon:** Regenerated `Assets/app.ico` — 16/32/48/256 px, PNG-in-ICO, matches design system colors
- **UI fix (v1.4):** Menu text color bugs resolved — no hardcoded colors on `<Menu>`, both themes have full `ControlTemplate` with `TextElement.Foreground="{TemplateBinding Foreground}"`
- **Build:** 0 errors, 0 warnings (`dotnet build -c Release`)
- **Publish:** Single self-contained EXE → `publish/LogTailPro.exe` (67.6 MB, win-x64)

### Build commands used
```
dotnet build src/LogTailPro/LogTailPro.csproj -c Release
dotnet publish src/LogTailPro/LogTailPro.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

---

## v1.4 - 2026-05-18
**Bug Report: Menu text color wrong in both themes**
**Affects:** `Themes/Dark.xaml`, `Themes/Light.xaml`, `MainWindow.xaml`

### Bug Description
- **Dark theme:** menu text shows as black/near-black — should be `#D4D4D4` (light gray / white)
- **Light theme:** menu text shows as wrong color — should be `#1A1A1A` (near-black / dark)

### Root Causes (3 issues, all theme/UI code only)

**Cause 1 — `MainWindow.xaml` line 147–149:**
The `<Menu>` element has hardcoded local values:
```xml
Background="#252535"  Foreground="#D4D4D4"  BorderBrush="#444455"
```
Local values on an element always win over `Style` setters. This permanently locks
the menu bar to dark-theme colors regardless of which theme is active. Hardcoded
`Foreground="#D4D4D4"` flows into `MenuItem` children via WPF's inherited property system.

**Cause 2 — `Dark.xaml` `MenuItem` template (line ~97):**
The custom `ControlTemplate` uses `ContentPresenter ContentSource="Header"` without
an explicit `TextElement.Foreground="{TemplateBinding Foreground}"` binding. WPF's
`ContentPresenter` does not reliably inherit foreground from the template host in this
context; an explicit `TemplateBinding` is required to guarantee the text color.

**Cause 3 — `Light.xaml` has no `MenuItem` custom template:**
`Light.xaml` sets `MenuItem.Foreground` via a style setter but has no `ControlTemplate`.
WPF falls back to the system Aero2 template which internally uses `SystemColors.MenuTextBrush`
and ignores the `Foreground` style setter entirely. Menu text color is therefore
determined by Windows system settings, not by the theme.

### Fix Plan (theme/UI code only — 3 steps)

| Step | File | Change |
|---|---|---|
| 1 | `MainWindow.xaml` | Remove hardcoded `Background`, `Foreground`, `BorderBrush` from `<Menu>`; let the theme `Style TargetType="Menu"` control all three via named tokens |
| 2 | `Dark.xaml` | Add `TextElement.Foreground="{TemplateBinding Foreground}"` to the `ContentPresenter` inside the `MenuItem` `ControlTemplate` |
| 3 | `Light.xaml` | Add the same full `ControlTemplate` to the `MenuItem` style (mirrors Dark.xaml); `Foreground` is already set to `TextPrimary=#1A1A1A` — the template just needs to bind to it |

No ViewModels, Services, Models, or business logic are touched.

---

## v1.3 - 2025-05-18
**Affects:** UI_AGENT.md (new file), prompts/UI_FIX.md (new file), CLAUDE.md

New files added — copy them into your project folder:
- UI_AGENT.md: full WPF design system with colour tokens, component specs,
  anti-patterns list, and a 30-item visual QA checklist
- prompts/UI_FIX.md: step-by-step UI overhaul agent prompt

Changes to CLAUDE.md:
- Added Special Commands table (start / fix the UI / continue / status / the plan has changed)
- Added git workflow with exact commit messages per phase
- Updated Golden Rules: rule 6 now requires git commit after every phase

To fix UI: open Claude Code, type "fix the UI"
Claude will read UI_AGENT.md + prompts/UI_FIX.md and overhaul all XAML files.

---

## v1.2 - 2025-05-17
**Affects:** PHASE_2, PHASE_4, PHASE_5, PHASE_6, SKILL.md

Changes made after reviewing the official BareTailPro website:

- PHASE_2: Added search results TABLE (Timestamp | Line# | Text | Capturing Groups columns, sortable)
- PHASE_2: Added regex capturing groups as individual columns in results table
- PHASE_2: Added ArrivedAt timestamp on each log line (when it arrived from watcher)
- PHASE_2: Added saved/named search patterns (save, name, load, delete)
- PHASE_4: Added tab strip positioning (top/bottom/left/right)
- PHASE_4: Added full command line argument support (-wp, -ws, -tc, -ti, file args)
- PHASE_4: Added window tiling support (--tile-window-count / --tile-window-index)
- PHASE_5: Added TAB expansion (configurable tab width, default 8 spaces)
- PHASE_5: Added IIS log format support (null-terminated lines)
- PHASE_5: Added network file support (UNC paths) with disconnect/retry
- PHASE_5: Added font line spacing and character offset settings
- PHASE_5: Added preferences storage mode (file / registry / do not save)
- PHASE_5: Added load/save preferences on demand (share config with teammates)
- PHASE_6: Replaced simple export with full column-configurable export dialog
           (per-column: prefix, suffix, width, alignment, truncation, decimal align)

---

## v1.1 - 2025-05-17
**Affects:** start.ps1, CLAUDE.md, SKILL.md, PHASE_1.md

- Fixed .NET version: upgraded from net8.0-windows to net10.0-windows
- Fixed start.ps1: version check now accepts .NET 8 or higher

---

## v1.0 - 2025-05-17
Initial plan created.
