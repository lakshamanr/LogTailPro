# LogTailPro

A full-featured real-time log file viewer and diagnostic workbench for Windows — built in C# 12, WPF, and .NET 9.
Combines ultra-fast multi-file tailing with an IDE docking layout, structured JSON log parsing, cross-log correlation, multiline exception grouping, interactive error timeline analytics, and live process output capture.

---

## Key Features

### 1. Real-Time File Tailing & Search
- **Instant Live Tail**: Watches log files in real time with auto-scroll (`Follow Tail`), pause, and resume.
- **Large File Support**: Handles multi-gigabyte log files efficiently with bounded memory consumption and fast seeking.
- **Powerful Search & Filtering**: Plain text and regex search with match counters, case sensitivity, invert matching, and include/exclude filter modes.
- **Highlight Rules**: Configurable regex-based text and row highlighting with custom foreground and background colors.

### 2. IDE Docking Architecture (AvalonDock)
- Fully customizable, dockable, floatable, and pinnable panels:
  - **Error Timeline Chart**: Interactive chronological severity visualizer.
  - **Stats Panel**: Live stream analytics dashboard, KPI metrics, and mini sparkline.
  - **Line Details / Property Inspector**: Structured JSON tree, metadata inspection, and formatted exception viewer.
  - **Bookmarks Panel**: Manage, label, and jump between bookmarked log rows (`Ctrl+B`, `F2` / `Shift+F2`).
  - **Search & Search Results**: Paged search results list with timestamp and source headers.
  - **Process Explorer**: Monitor active system processes and attach live console/debug output streams.
  - **Help Guide**: Built-in searchable documentation panel (`F1`).
- **Save & Restore Layout**: Preserves panel positions and layout between sessions with one-click **Reset Layout**.

### 3. Interactive Error Timeline Chart (`Ctrl+Shift+T`)
- **Adaptive Time Bucketing**: Automatically aggregates logs into clean human-friendly time intervals (1s to 7d, bounded to 120 buckets).
- **Stacked Severity Bars**: Visualizes Error (red), Warning (amber), Info (blue), and Other (slate) event distributions.
- **Y-Axis Numeric Scale Labels**: Calibrated left-gutter numeric tick labels (0, 25%, 50%, 75%, and peak max count with `k` notation).
- **Severity Layer Toggles**: Click the header severity pills (Errors, Warnings, Info, Other) to toggle individual layers on/off and isolate rare error spikes.
- **Click & Drag Time-Range Filtering**: Click a single bucket or click-and-drag a marquee range to filter visible log events (AND-combined with search).
- **Right-Click Context Menu**:
  - Filter to bucket
  - Drill down to first event
  - Copy formatted time range
  - Clear time filter
  - Toggle severity layers
- **Rich Styled Tooltips**: Theme-aware tooltips with timestamp headers, colored severity indicator dots, aligned counts, and navigation hints.
- **Undated Records Notice**: Explicitly tracks records without dates rather than plotting fabricated timestamps.

### 4. Revamped Live Stream Stats Dashboard (`Ctrl+Shift+S`)
- **KPI Metrics Cards**: Dedicated 2x2 grid for **Errors**, **Warnings**, **Info**, and **Lines / Sec**. Click the **Errors** or **Warnings** card to instantly filter the log view.
- **Severity Ratio Bar**: Proportional multi-segment horizontal bar displaying exact event counts and percentages.
- **Embedded Mini Timeline Sparkline**: Visualizes chronological event distribution directly inside the Stats panel without needing to open the bottom dock pane. Includes a one-click **View Full Chart** button.
- **Stream Metrics**: Displays total lines, file size, active bookmarks, and undated events.
- **Quick Action Pills**: One-click shortcuts to **Filter Errors**, **Filter Warnings**, and **Clear Filter**.
- **Empty State**: Friendly placeholder when no log tab is open.

### 5. Multi-Source Combined Timeline
- Open two or more log files and choose **Edit ▸ Combined Timeline** (`Ctrl+Shift+M`).
- Merges loaded source lines chronologically in UTC with deterministic tie-breaking.
- Dedicated **Source** and **UTC Time** columns; right-click any line and choose **Open original line** to jump directly to the source file and line.
- Live updates follow source file changes asynchronously without freezing the UI.

### 6. Structured JSON Logs & Property Inspector
- Detects line-delimited JSON (JSONL) and Serilog CLEF records (`@t`, `@l`, `@m`, `@mt`, `@x`).
- Dedicated **Level** and **Timestamp** columns with color badges.
- **Property Inspector**: Hierarchical tree viewer for nested properties, exception stack traces, and pretty-printed raw JSON.
- **Typed Field Filters**: Direct syntax expressions (e.g. `StatusCode >= 400`, `exists(UserId)`, `Level == Error`).

### 7. Cross-Log Request & Trace ID Correlation
- Right-click any parsed or highlighted token (or select from Property Inspector) and choose **Correlate Request / Trace ID…**.
- Performs exact-token matching (supporting W3C trace IDs, GUIDs, `RequestId`, `@tr`, etc.) across selected open logs.
- Spawns a dedicated chronological correlation tab.

### 8. Multiline Exception Grouping
- Automatically groups .NET stack frames, inner exceptions (`--->`), Java stack traces (`Caused by:`), and Python tracebacks under their root error line.
- Interactive expand/collapse buttons (`▶` / `▼`) and continuation frame count badges (`+N frames`).
- Global toggles for **Group Exceptions** vs **Plain Line View**, plus **Expand All** and **Collapse All**.
- Searches and filters seamlessly match against hidden stack trace lines.

### 9. Saved Workspaces (`.ltpw`)
- Save and restore entire multi-file investigation sessions (`File ▸ Save Workspace…`).
- Restores all file sources, combined timelines, correlation tabs, search queries, filter modes, bookmarks, scroll positions, and custom docking layout XML.
- Atomic, versioned saves with graceful recovery for missing files.

### 10. Process Explorer & Live Capture
- Inspect running Windows processes with search, PID, memory, and path details.
- Attach to live console output (`stdout`/`stderr`), capture `OutputDebugString` debug output, or stream Windows Event Logs directly into tailing tabs.

---

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+O` | Open log file(s) |
| `Ctrl+F` | Open Search panel |
| `F3` / `Shift+F3` | Next / Previous search match |
| `Ctrl+B` | Toggle bookmark on selected line |
| `F2` / `Shift+F2` | Next / Previous bookmark |
| `Ctrl+Shift+T` | Toggle Error Timeline Chart panel |
| `Ctrl+Shift+S` | Toggle Stats panel |
| `Ctrl+Shift+M` | Open Combined Timeline dialog |
| `Ctrl+Shift+C` | Correlate Request / Trace ID |
| `Ctrl+Shift+P` | Toggle Process Explorer panel |
| `Ctrl+P` | Pretty-print selected JSON line |
| `Ctrl+G` | Go to line number |
| `Ctrl+W` | Close active tab |
| `F1` | Open dockable Help Guide |

---

## Repository Structure

```text
LogTailPro/
├── docs/                     # Documentation & guides
│   ├── CHANGELOG.md          # Release notes and version history
│   ├── HELP_GUIDE.md         # In-depth application user guide
│   ├── GEMINI_HANDOFF.md     # Architecture and handoff notes
│   └── ROADMAP.md            # Feature roadmap & milestones
├── installer/                # WiX Toolset v5 MSI installer package
├── scripts/                  # Automation & build scripts
│   ├── build-run.ps1         # Debug build and launch script
│   ├── release.bat           # Self-contained single EXE publisher
│   ├── start.ps1             # Developer quickstart script
│   ├── zip-source.bat        # Clean source archive creator (CMD)
│   └── zip-source.ps1        # Clean source archive creator (PowerShell)
├── src/LogTailPro/           # Main WPF application
│   ├── Converters/           # Value converters
│   ├── Models/               # Data structures and domain models
│   ├── Parsing/              # Log parsers (JSON, CLEF, Regex, Exception grouping)
│   ├── Services/             # File watching, process streaming, timeline services
│   ├── ViewModels/           # MVVM view models (CommunityToolkit.Mvvm)
│   └── Views/                # WPF UI components
│       ├── Controls/         # Vector canvases, selectors, and helpers
│       ├── Dialogs/          # Modal windows and configuration dialogs
│       └── Panels/           # AvalonDock dockable panels and tab views
└── tools/                    # Test suites & CLI tools
    ├── Timeline.Tests/       # Unit tests (ErrorTimelineChart & Stats)
    └── Timeline.IntegrationTests/ # Integration tests (Real-file streaming)
```

---

## Building from Source

### Prerequisites
- Windows 10/11
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (or later)

### Quick Build & Run

```powershell
# Restore dependencies and build
dotnet build src/LogTailPro/LogTailPro.csproj -c Release

# Run the application
dotnet run --project src/LogTailPro/LogTailPro.csproj -c Release
```

### Self-Contained Single EXE Publish

```powershell
# Publish as a standalone single-file EXE (win-x64) to .\publish\
dotnet publish src/LogTailPro/LogTailPro.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

Or run the included batch script:
```cmd
release.bat
```

### Building the MSI Installer

To build the Windows MSI installer locally, install WiX Toolset 5:

```powershell
dotnet tool install -g wix --version 5.0.2
wix extension add -g WixToolset.UI.wixext/5.0.2
wix extension add -g WixToolset.Util.wixext/5.0.2
.\installer\build-msi.ps1
```

The installer will be generated in `installer/out/LogTailPro-<version>-x64.msi`.

---

## Automated CI/CD & MSI Releases

Pre-built MSI installers are available under GitHub [Releases](https://github.com/lakshamanr/LogTailPro/releases).
Every commit pushed to `master` runs unit and integration test suites, builds a self-contained MSI installer, and publishes an automated release.

---

## Tech Stack
- **Language & Runtime**: C# 12 / .NET 9 (`net9.0-windows7.0`) / WPF
- **MVVM Framework**: [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (8.3.2)
- **Docking Manager**: [Dirkster.AvalonDock](https://github.com/Dirkster99/AvalonDock) (4.72.1) + VS2013 Light Theme
- **Serialization**: Newtonsoft.Json (13.0.3)
- **Installer**: WiX Toolset v5

---

## License

This project is licensed under the MIT License — see the repository files for details.
