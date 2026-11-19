# LogTailPro — Comprehensive In-Depth Help Guide & Feature Manual

Welcome to the definitive feature-by-feature manual for **LogTailPro**. This guide documents every feature, menu item, toolbar button, dockable panel, and keyboard shortcut in LogTailPro.

Each entry includes:
1. **Option / Feature Overview & UI Location**
2. **Demo & Visual Walkthrough Scenario**
3. **Actual How It Works (Step-by-Step Instructions & Under-the-Hood Technical Mechanics)**

---

## Table of Contents
1. [Core File Ingestion & Real-Time Monitoring](#1-core-file-ingestion--real-time-monitoring)
   - 1.1 Open File (`Ctrl+O`, Drag-and-Drop)
   - 1.2 Follow Tail Mode (`F` / Toolbar)
   - 1.3 Pause / Resume Stream (`Space` / Search Bar)
   - 1.4 Large File Engine (>2GB & Partial Chunk Loading)
   - 1.5 Network Share Polling & Disconnect Recovery
   - 1.6 File Rotation & Truncation Detection (Warning Banner)
   - 1.7 Watch Directory (`File > Watch Directory…`)
   - 1.8 Open from Clipboard (`Ctrl+Shift+V`)
   - 1.9 Recent Files List
2. [Search, Filtering & Saved Patterns](#2-search-filtering--saved-patterns)
   - 2.1 Find Bar & Query Execution (`Ctrl+F`, `F3`, `Shift+F3`)
   - 2.2 Plain Text vs Regular Expression Modes
   - 2.3 Case Sensitivity & Invert Matching
   - 2.4 Filter Tail Mode (Real-Time Dynamic Grep)
   - 2.5 Unified Search & Results Panel
   - 2.6 Saved Search Patterns & Presets
   - 2.7 Export Filtered Lines
3. [Structured JSON Logs & Property Inspector](#3-structured-json-logs--property-inspector)
   - 3.1 Line-Delimited JSON & Serilog CLEF Auto-Detection
   - 3.2 Structured Event View vs Raw Text View (`Toolbar: ⛯ Structured`)
   - 3.3 Structured Severity Badges & Level Normalization
   - 3.4 Line Details / Property Inspector Panel
   - 3.5 Hierarchical Typed Property Tree & "Correlate by Value"
   - 3.6 JSON / XML Pretty-Printing (`Ctrl+P`)
4. [Multiline Exception Grouping (Stack Trace Folding)](#4-multiline-exception-grouping-stack-trace-folding)
   - 4.1 Group Multiline Exceptions Toggle
   - 4.2 Multi-Language Trace Detection (.NET, Java, Python)
   - 4.3 Interactive Expand/Collapse Glyphs & Frame Badges
   - 4.4 Search & Filter Transparency Across Hidden Frames
   - 4.5 Full Trace Clipboard Copying
   - 4.6 Mid-Stream & Truncated Trace Detection (`[Incomplete trace]`)
5. [Cross-Log Request & Trace ID Correlation](#5-cross-log-request--trace-id-correlation)
   - 5.1 Correlation Workflow (`Edit > Correlate Request / Trace ID…`)
   - 5.2 Auto-Suggested Correlation IDs (W3C, GUIDs, Custom Tokens)
   - 5.3 Multi-Source Log Scope Selection
   - 5.4 Live Correlated Tab & Source Columns
   - 5.5 Jump to Original Line Navigation
6. [Combined Multi-File Live Timeline](#6-combined-multi-file-live-timeline)
   - 6.1 Creating a Combined Timeline (`Edit > Combined Timeline…`)
   - 6.2 UTC Chronological Normalization & Timezone Handling
   - 6.3 Source Identification & File Path Tooltips
   - 6.4 Independent Filtering & Freezing
7. [Interactive Error Timeline Chart](#7-interactive-error-timeline-chart)
   - 7.1 Error Timeline Chart Panel (`Ctrl+Shift+T` / `Toolbar: 📊 Chart`)
   - 7.2 Adaptive Time Bucketing & Severity Distribution
   - 7.3 Click-and-Drag Time Range Marquee Filtering
   - 7.4 Click-to-Drill-Down Navigation
   - 7.5 Undated Records Isolation
8. [Process Explorer & Live Output Capture](#8-process-explorer--live-output-capture)
   - 8.1 Process Explorer Panel (`Ctrl+Shift+P` / `Toolbar: ⚙ Processes`)
   - 8.2 Process Tree View (.NET Badges, CPU%, Memory, PID)
   - 8.3 Windows Services Mode (Service Status, Control, Hosting PID)
   - 8.4 Automated Log Discovery (Open Handles, Config Files, Recent Logs)
   - 8.5 Batch Process Log Discovery (`Logs (N)`)
   - 8.6 Win32 `OutputDebugString` Live Capture
   - 8.7 Windows Event Log Live Monitoring
   - 8.8 Launch & Attach stdout/stderr (`LaunchProcessDialog`)
9. [Navigation, Bookmarking & Live Analytics](#9-navigation-bookmarking--live-analytics)
   - 9.1 Bookmarking System (`Ctrl+B`, `F2`, `Shift+F2`, Bookmarks Panel)
   - 9.2 Go To Line (`Ctrl+G`)
   - 9.3 Line Wrapping Toggle
   - 9.4 Elapsed Time Delta Column (`Ctrl+Shift+E`)
   - 9.5 Live Stats Dashboard Panel (`Ctrl+Shift+S`)
10. [Custom Highlighting & Log Parsing Engine](#10-custom-highlighting--log-parsing-engine)
    - 10.1 Highlight Rules Manager (`Tools > Highlight Rules…`)
    - 10.2 Custom Foreground & Background Color Swatches
    - 10.3 Log Parser Regex Configuration (`Tools > Configure Log Parser…`)
11. [Docking, Workspaces & Command Line Interface](#11-docking-workspaces--command-line-interface)
    - 11.1 AvalonDock Docking Layout & Tab Reordering
    - 11.2 Split View Manager (`Toolbar: ◫ Split View…`)
    - 11.3 Reset Layout (`Toolbar: ⟲ Reset Layout`)
    - 11.4 Save & Load Workspace (`.ltpw` v2)
    - 11.5 Application Settings & Persistence
    - 11.6 Command Line Arguments & Multi-Window Tiling (`-wp`, `-ws`, `-tc`, `-ti`)

---

# 1. Core File Ingestion & Real-Time Monitoring

## 1.1 Open File
- **UI Location**: `File > Open…`, Toolbar button `📂 Open`, Shortcut: `Ctrl+O`, Drag-and-Drop anywhere onto the window.
- **Purpose**: Opens one or multiple local or network log files into individual document tabs.

### Demo / Scenario
You are investigating a production web server error. You drag `api-service.log` from Windows Explorer and drop it onto LogTailPro. The file immediately loads into a new tab titled `api-service.log` with file size and status displayed in the top-right toolbar. The welcome card vanishes and the latest log lines render instantly.

### Actual How It Works
1. **User Action**: Click **📂 Open** or press `Ctrl+O`. Pick a file. Alternatively, drag a file from Windows Explorer over LogTailPro and release.
2. **Under the Hood**:
   - `FileWatcherService` attaches to the file using non-locking shared read access (`FileShare.ReadWrite | FileShare.Delete`).
   - If the file is smaller than the configured `MaxLoadMegabytes` threshold (default 0 = whole file), an asynchronous file stream reads the lines using a background thread and batches them into `ObservableCollection<LogLine>`.
   - WPF virtualization (`VirtualizingStackPanel` with `Recycling` mode) ensures that even with 500,000 lines, memory and UI render times remain instantaneous because only the lines visible on screen are instantiated as WPF visual elements.
   - The file is added to `Settings.RecentFiles` and persisted to `%APPDATA%\LogTailPro\settings.json`.

---

## 1.2 Follow Tail Mode
- **UI Location**: Toolbar toggle button `⏬ Follow Tail`, Menu: `View > Toggle Follow Tail`, Shortcut: `F`.
- **Purpose**: Automatically scrolls the viewer to the bottom as new lines are appended in real time.

### Demo / Scenario
A worker process writes 20 lines every second. You turn on **Follow Tail**. The scrollbar stays pinned at the bottom. As each batch arrives, the view smoothly follows the new events. The bottom status bar displays a green **"Following"** indicator. If you manually scroll up with the mouse wheel to read an earlier error, Follow Tail turns off automatically so your reading position is not interrupted. Pressing `F` re-engages Follow Tail and snaps back to the live tail.

### Actual How It Works
1. **User Action**: Click `⏬ Follow Tail` or press `F`.
2. **Under the Hood**:
   - `LogTabViewModel.IsFollowTail` controls auto-scrolling.
   - When new lines are detected by `FileWatcherService`, lines are appended to `AllLines`. If `IsFollowTail == true` and no filters hide the new lines, `ScrollToLineRequested` fires and executes `ScrollIntoView` on the last item.
   - If user interaction triggers a mouse scroll up or manual scrollbar drag away from the bottom, `LogListView_ScrollChanged` detects `VerticalOffset < ScrollableHeight` and clears `IsFollowTail = false`.
   - The status bar reflects `"Following"` in bold green (`StatusGreen`).

---

## 1.3 Pause / Resume Live Stream
- **UI Location**: Search panel **Stop** / **Pause** button, Shortcut: `Space` (when log list is focused).
- **Purpose**: Temporarily freezes the log view to inspect incoming logs without dropping incoming disk events.

### Demo / Scenario
Your service is flooding logs at 5,000 lines per second. You spot an interesting error. You press `Space` or click `Stop`. The view freezes. The service continues writing to disk in the background. Once you finish reviewing the error, you resume, and LogTailPro catches up instantly without dropping lines.

### Actual How It Works
1. **User Action**: Click `Stop` in the Search bar or toggle pause.
2. **Under the Hood**:
   - `FileWatcherService.Pause()` suspends reading file stream offsets without closing the OS handle.
   - For combined timelines, `IsWatchPaused = true` freezes the incoming merge queue.
   - When resumed (`_watcher.Resume()`), the service determines the current file length, reads from the last unread byte offset up to the new end-of-file, parses lines, and appends them in batch.

---

## 1.4 Large File Engine (>2GB & Partial Chunk Loading)
- **UI Location**: `Tools > Settings… > Max load size (MB)`.
- **Purpose**: Enables opening multi-gigabyte log files (e.g. 10GB–50GB) without running out of RAM or locking the UI.

### Demo / Scenario
You need to open a 14 GB IIS dump log. Normally, text editors crash. In LogTailPro with `Max load size` set to `100 MB`, LogTailPro opens the file in under 1 second. It reads backwards from the end of the file by 100 MB, finds the nearest newline boundary, and displays the most recent 600,000 lines while watching for new appends in real time.

### Actual How It Works
1. **Configuration**: Open `Tools > Settings…`, set `Max load size (MB)` (e.g. `100`).
2. **Under the Hood**:
   - `FileWatcherService` checks `FileInfo.Length`. If `Length > MaxLoadMegabytes * 1024 * 1024`, it skips the start of the file and seeks directly to `Length - MaxBytes`.
   - It scans forward for the first `\n` to prevent displaying a cut-off partial line.
   - Lines are indexed from this boundary onward.
   - A warning status indicates that only the tail portion of the file is loaded.
   - Memory retention limits (`Max lines in memory`) can also be configured to discard oldest loaded lines in RAM via circular buffer mechanics as new lines stream in.

---

## 1.5 Network Share Polling & Disconnect Recovery
- **UI Location**: `Tools > Settings… > Network poll (ms)`.
- **Purpose**: Monitors log files hosted on UNC network shares (`\\server\logs\app.log`) where OS file change notifications (`ReadDirectoryChangesW`) are unsupported or unreliable.

### Demo / Scenario
You tail `\\192.168.1.50\logs\orders.log`. SMB file system change events are often dropped across routers. LogTailPro activates periodic polling every 1,000 ms. If the VPN disconnects, LogTailPro shows a yellow warning banner and safely retries without crashing until the network reconnects.

### Actual How It Works
1. **Configuration**: Set `Network poll (ms)` in `Tools > Settings…` (500–10,000 ms).
2. **Under the Hood**:
   - `FileWatcherService` checks whether the path is a UNC path (`\\...`) or mapped network drive.
   - If network-based, it instantiates a background polling timer in addition to `FileSystemWatcher`.
   - On each tick, it queries file metadata (`GetFileInformationByHandleEx` / length check). If file size has increased, it streams the new delta.
   - On `IOException` / network timeout, it transitions status to `FileStatus.Error`, displays a warning banner, and enters an exponential backoff reconnect loop.

---

## 1.6 File Rotation & Truncation Detection (Warning Banner)
- **UI Location**: Floating warning banner at the top of the log tab (`BgWarningBanner`).
- **Purpose**: Detects when a log file is cleared, rolled over by logrotate/Serilog, or replaced, alerting the user and auto-reloading seamlessly.

### Demo / Scenario
An application rolls `app.log` at midnight. The file size suddenly shrinks from 500 MB to 0 bytes and restarts. In legacy viewers, the viewer hangs or displays corrupted characters. In LogTailPro, an amber banner appears: `"File was truncated or rotated — reloaded from beginning"`, and the viewer automatically resets its stream offset and tails the new content.

### Actual How It Works
1. **Under the Hood**:
   - On every append notification, `FileWatcherService` checks if `currentLength < lastReadPosition`.
   - If true, a truncation or file rotation has occurred.
   - The watcher resets stream position to 0, signals `FileTruncated`, re-reads from the beginning, and updates `LogTabViewModel.WarningMessage`.
   - When the user acknowledges or new lines arrive, the banner updates state smoothly.

---

## 1.7 Watch Directory
- **UI Location**: `File > Watch Directory…`.
- **Purpose**: Monitors an entire folder for newly created `*.log` files and automatically opens each new log file in a new tab.

### Demo / Scenario
You run an automated test suite that generates a new log file per test case into `C:\Build\Logs\`. You select **Watch Directory**. As tests run, new log tabs automatically pop up in LogTailPro and begin tailing immediately.

### Actual How It Works
1. **User Action**: Click `File > Watch Directory…`. Select a directory.
2. **Under the Hood**:
   - `DirectoryMonitorService` initializes a `FileSystemWatcher` on the folder targeting `*.log`.
   - On `Created` event, it dispatches to `MainViewModel.OpenFileTabAsync(e.FullPath)` on the UI thread.
   - Status bar reports `"Watching directory: <path>"`.

---

## 1.8 Open from Clipboard
- **UI Location**: `File > Open from Clipboard`, Shortcut: `Ctrl+Shift+V`.
- **Purpose**: Creates an instant temporary log tab containing text or stack traces copied from Slack, emails, or issue trackers.

### Demo / Scenario
A developer posts a 200-line JSON exception dump in Teams. You copy it, switch to LogTailPro, and press `Ctrl+Shift+V`. A tab titled **Clipboard** opens immediately with full syntax highlighting, structured JSON parsing, and search functionality.

### Actual How It Works
1. **User Action**: Press `Ctrl+Shift+V`.
2. **Under the Hood**:
   - `MainViewModel.OpenFromClipboardAsync()` reads `Clipboard.GetText()`.
   - Writes it to a unique temporary file `%TEMP%\LogTailPro_clipboard_<timestamp>.log`.
   - Opens the tab in memory with `DisplayName = "Clipboard"` and sets `TempFilePath` for automatic cleanup on tab close.

---

## 1.9 Recent Files List
- **UI Location**: `File > Recent Files` submenu, and welcome card buttons when no files are open.
- **Purpose**: Quick access to the last 15 opened files.

### Demo / Scenario
You launch LogTailPro in the morning. The central welcome card displays your frequently opened logs. Clicking any file re-opens it instantly.

### Actual How It Works
1. File paths are persisted to `Settings.RecentFiles`.
2. If an entry no longer exists on disk, clicking it prompts an alert and removes the missing path from the recent list automatically.

---

# 2. Search, Filtering & Saved Patterns

## 2.1 Find Bar & Query Execution
- **UI Location**: `Search > Find…`, Toolbar button `🔍 Search`, Shortcut: `Ctrl+F`, Close: `Esc`.
- **Purpose**: Searches loaded lines in memory with highlighting and jump navigation.

### Demo / Scenario
You want to find every database query containing `"TIMEOUT"`. Press `Ctrl+F`. The search panel docks open at the bottom. Type `TIMEOUT` and press `Enter`. Matching rows are instantly highlighted in bright amber (`SearchMatchBg`). Pressing `F3` jumps to the next match; `Shift+F3` jumps backwards.

### Actual How It Works
1. **User Action**: Press `Ctrl+F`, type query into `QueryBox`, press `Enter`.
2. **Navigation**:
   - `F3` runs `NextMatchCommand` to scan down to the next matching line.
   - `Shift+F3` runs `PreviousMatchCommand` to scan upwards.
   - `Esc` closes the search panel and clears match highlights.
3. **Under the Hood**:
   - `SearchService` executes asynchronously on a background thread using a cancellation token (`_searchCts`).
   - If the user types while a large search is running, the prior search is cancelled immediately.

---

## 2.2 Plain Text vs Regular Expression Modes
- **UI Location**: Search panel dropdown (`Text` vs `Regex`), or `Regex syntax` checkbox.
- **Purpose**: Toggle between high-speed literal string searching and regex matching.

### Demo / Scenario
To match all HTTP 5xx errors: switch mode to `Regex` and search `HTTP/\d\.\d" 5\d\d`. All matching status lines highlight instantly. If you enter an invalid pattern such as `[a-z(`, a clear red inline error appears: `"parsing '[a-z(' - Unterminated [] set."` without crashing.

### Actual How It Works
1. In `Text` mode, `string.IndexOf(..., StringComparison.OrdinalIgnoreCase)` provides maximum throughput.
2. In `Regex` mode, `Regex(pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(1))` is compiled with a 1-second timeout guard to prevent catastrophic regex backtracking.

---

## 2.3 Case Sensitivity & Invert Matching
- **UI Location**: Checkboxes: `Ignore case`, `Invert match`.
- **Purpose**:
  - `Ignore case`: Matches letters regardless of uppercase or lowercase.
  - `Invert match`: Inverts the query logic to highlight/display only lines that **DO NOT** contain the query.

### Demo / Scenario
Your log is cluttered with repetitive health check pings (`GET /healthcheck 200 OK`). You type `/healthcheck`, check **Invert match**, and enable **Filter tail**. All health checks disappear from the screen; only meaningful application traffic remains.

### Actual How It Works
- The filtering predicate evaluates `isMatch ^ IsInvertMatch`. If a line matched the pattern but `IsInvertMatch == true`, it evaluates to `false` and is omitted from the visible view.

---

## 2.4 Filter Tail Mode (Real-Time Dynamic Grep)
- **UI Location**: Search panel checkbox `Filter tail`.
- **Purpose**: Collapses the view so that ONLY matching lines (or non-matching lines if inverted) are shown. As new log lines append, they pass through the filter in real time.

### Demo / Scenario
You are tailing a live log generating 1,000 lines/sec. You only care about errors. You search `ERROR`, check `Filter tail`. The 100,000 existing lines collapse to only the 14 error lines. As the backend runs, when a new error occurs 10 minutes later, it appears at the bottom immediately.

### Actual How It Works
1. `LogTabViewModel.VisibleLines` is bound to the WPF `ListView`.
2. When `Filter tail` is active, `ApplySearchAsync` populates `VisibleLines` with only items passing `LinePassesFilter(line)`.
3. Incoming appends from `FileWatcherService` are checked against `LinePassesFilter`:
   - If passing, the line is added to `VisibleLines`.
   - If not passing, it is added to `AllLines` for memory integrity but omitted from `VisibleLines`.

---

## 2.5 Unified Search & Results Panel
- **UI Location**: `Ctrl+F` | `View > Panels > Search & Results`, docked at the bottom of the workspace.
- **Purpose**: Integrated search controls and tabular grid of all search hits across the entire file with line numbers, timestamps, and extracted regex capture groups.

### Demo / Scenario
Searching for regex `order (?<ID>\d+) failed` produces 45 matches across a 2,000,000 line file. The **Search & Results** panel displays the query options and lists all 45 occurrences in a clean grid showing Line Number, Extracted ID, and the text snippet. Double-clicking row #12 jumps the main log viewer directly to Line 841,203.

### Actual How It Works
1. Rows are stored in `ObservableCollection<SearchResultRow>`.
2. Columns are sortable by line number or timestamp.
3. Double-clicking or selecting a row triggers `SelectedTab.ScrollToLineRequested` to instantly position the log view.

---

## 2.6 Saved Search Patterns & Presets
- **UI Location**: Button `＋ Save pattern` in Search Panel.
- **Purpose**: Save complex regex or text filters with custom names to reuse across sessions.

### Demo / Scenario
You often use regex `(FATAL|CRITICAL|NullReferenceException)`. Click **＋ Save pattern**, name it `"Fatal Errors"`. It is saved permanently. Next time, choose `"Fatal Errors"` from the saved patterns list to re-apply the filter in one click.

### Actual How It Works
- Stored in `AppSettings.SavedPatterns` as `List<SavedSearchPattern>` and serialized to JSON.
- Remembers pattern string, regex mode flag, case sensitivity flag, and filter mode.

---

## 2.7 Export Filtered Lines
- **UI Location**: `File > Export Filtered Lines…`.
- **Purpose**: Exports currently visible (filtered) lines into a new clean text or log file on disk.

### Demo / Scenario
After filtering a 5 GB file down to 142 error lines, you need to email those errors to an external vendor. Click **File > Export Filtered Lines…**, choose `errors_20261106.log`. Only the 142 lines are saved.

### Actual How It Works
- `LogTabViewModel.ExportFilteredLinesAsync()` iterates `VisibleLines.Select(l => l.Text)` and writes them using asynchronous streaming `File.WriteAllLinesAsync`.

---

# 3. Structured JSON Logs & Property Inspector

## 3.1 Line-Delimited JSON & Serilog CLEF Auto-Detection
- **UI Location**: Automatic on file load.
- **Purpose**: Detects structured logs formatted as newline-delimited JSON (NDJSON) or Serilog Compact Log Event Format (CLEF - `@t`, `@l`, `@m`, `@mt`, `@x`).

### Demo / Scenario
Load a file containing:
```json
{"@t":"2026-11-06T14:02:11.123Z","@l":"Error","@mt":"Payment {PaymentId} failed for user {UserId}","PaymentId":"pay-9812","UserId":48102,"ElapsedMs":412.5,"Exception":"System.TimeoutException: Gateway timeout..."}
```
LogTailPro automatically flags `HasStructuredLogs = true` and enables the **⛯ Structured** toggle.

### Actual How It Works
- `JsonLogParser` samples lines during ingestion.
- Safely parses JSON with limits (max 2MB per line, max depth 32) without throwing unhandled exceptions on malformed lines.
- Extracts standard keys: `@t`/`timestamp`, `@l`/`level`, `@mt`/`template`, `@m`/`message`, and `@x`/`exception`.

---

## 3.2 Structured Event View vs Raw Text View
- **UI Location**: Toolbar toggle `⛯ Structured`, View Menu: `View > Structured View`.
- **Purpose**: Toggles between human-readable rendered event summaries and the raw raw JSON line.

### Demo / Scenario
- **Raw View**:
  `{"@t":"2026-11-06T14:02:11.123Z","@l":"Error","@mt":"Payment {PaymentId} failed","PaymentId":"pay-9812"}`
- **Structured View**:
  `[14:02:11] [ERROR] Payment pay-9812 failed`
The clutter of JSON syntax is hidden, allowing clean scanning.

### Actual How It Works
- The WPF ListView row switches bindings: when structured view is active, it renders `StructuredMessage`, formatted `StructuredTimeDisplay`, and colored `StructuredLevel` pill.

---

## 3.3 Structured Severity Badges & Level Normalization
- **UI Location**: Left-hand badge column in Structured View.
- **Purpose**: Normalizes severity across different logging frameworks into unified color badges:
  - **Fatal**: Dark Red
  - **Error**: Red (`BadgeErrorBg`)
  - **Warning**: Amber / Yellow (`BadgeWarnBg`)
  - **Information / Info**: Blue (`BadgeInfoBg`)
  - **Debug / Verbose / Trace**: Purple / Muted (`BadgeDebugBg`)

---

## 3.4 Line Details / Property Inspector Panel
- **UI Location**: Dockable panel on the right side: `View > Panels > Line Details`.
- **Purpose**: When any log line is clicked, this inspector provides an in-depth breakdown of all structured properties, metadata, and exceptions.

### Demo / Scenario
Click any log line. The **Line Details** panel immediately shows:
1. **Header Card**: Severity badge, full ISO timestamp, rendered message, and message template.
2. **Properties Tab**: An interactive tree view of every key-value pair extracted from the JSON.
3. **Raw / Formatted Text Tab**: Shows the raw line with buttons for **Pretty JSON** indentation and **Copy**.

---

## 3.5 Hierarchical Typed Property Tree & "Correlate by Value"
- **UI Location**: Property tree in **Line Details** panel.
- **Purpose**: Inspect nested objects, arrays, and primitive values, and correlate values across logs with one click.

### Demo / Scenario
The tree displays:
```
▼ RequestInfo : Object
   Method : String = "POST"
   Path : String = "/api/v2/checkout"
   ▼ Headers : Object
      X-Correlation-Id : String = "c89b-4412"
```
Right-click `X-Correlation-Id` and click **Correlate by Value**. LogTailPro opens a multi-log correlation tab tracking that exact token across all open microservice logs.

---

## 3.6 JSON / XML Pretty-Printing
- **UI Location**: Shortcut: `Ctrl+P`, Menu: `Tools > Pretty-Print Selected Line`.
- **Purpose**: Formats unformatted, single-line JSON or XML payloads embedded inside log lines into clean, indented code blocks.

---

# 4. Multiline Exception Grouping (Stack Trace Folding)

## 4.1 Group Multiline Exceptions Toggle
- **UI Location**: Toolbar toggle `▶ Group Exceptions`, Menu: `View > Group Multiline Exceptions`.
- **Purpose**: Collapses massive 50-line stack traces into single expandable rows, restoring clean 1-line-per-event readability.

### Demo / Scenario
Without grouping, an unhandled exception floods your screen with 80 lines of `at System.Data.SqlClient...`. With **Group Exceptions** turned on, those 80 lines collapse into a single row:
`▶ [+79 frames] 14:02:11 [ERROR] SqlException: Connection timeout expired`
Clicking `▶` expands the full trace; clicking `▼` collapses it back.

---

## 4.2 Multi-Language Trace Detection (.NET, Java, Python)
- **Engine Mechanics**:
  - **.NET**: Identifies exception headers, `at Namespace.Class.Method(Args) in File.cs:line N`, inner exceptions (`---> System.Exception`), and boundary markers (`--- End of stack trace from previous location ---`).
  - **Java**: Identifies `Caused by:`, `at com.package.Class.method(Class.java:123)`, and `... 42 common frames omitted`.
  - **Python**: Identifies `Traceback (most recent call last):`, `File "...", line N, in func`, and chained exception messages (`During handling of the above exception, another exception occurred:`).

---

## 4.3 Interactive Expand/Collapse Glyphs & Frame Badges
- **Expander Glyph (`▶` / `▼`)**: Click to toggle the individual group.
- **Continuation Badge (`+N frames`)**: Displays exact number of hidden stack trace frames.
- **Global Actions**:
  - `View > Expand All Exceptions`: Expands all groups across the file.
  - `View > Collapse All Exceptions`: Collapses all groups back to single-line roots.

---

## 4.4 Search & Filter Transparency Across Hidden Frames
- **Critical Capability**: If an exception group is collapsed and you search for a term that only exists on line 35 inside the hidden stack trace (e.g. `UserRepository.cs`), LogTailPro's search engine searches inside the collapsed continuation frames. The root row highlights as a match so critical errors are never missed while collapsed.

---

## 4.5 Full Trace Clipboard Copying
- **Command**: `Ctrl+C` or `Ctrl+Shift+C`.
- **Behavior**: Copying a collapsed exception root automatically copies the entire full stack trace with all continuation lines to the Windows clipboard.

---

## 4.6 Mid-Stream & Truncated Trace Detection (`[Incomplete trace]`)
- If LogTailPro starts tailing or loads a chunk that starts midway through an already-occurring stack trace, it displays a distinct `[Incomplete trace]` badge so you know the root exception occurred earlier in the file.

---

# 5. Cross-Log Request & Trace ID Correlation

## 5.1 Correlation Workflow
- **UI Location**: Menu: `Edit > Correlate Request / Trace ID…`, or click **Correlate ID** in Line Details header.
- **Purpose**: Follows a distributed transaction across multiple open log files (e.g. Frontend Gateway, Auth Service, Database Service) in one synchronized view.

---

## 5.2 Auto-Suggested Correlation IDs
- **Engine**: When you select a line, LogTailPro's `CorrelationService` automatically inspects the line for:
  - Structured fields: `TraceId`, `@tr`, `RequestId`, `CorrelationId`, `SpanId`, `TransactionId`.
  - W3C traceparent headers (`00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01`).
  - Standard GUIDs (`8-4-4-4-12` hex).
  - Prefixed tokens (`req-12345`, `trace-abcde`, `corr-9876`).
- If detected, the **Correlate ID** button lights up with the extracted ID pre-filled.

---

## 5.3 Multi-Source Log Scope Selection
- Opening the correlation dialog presents a checklist of all currently open log tabs.
- Check or uncheck sources (with **Select All** / **Deselect All** helpers) to define the search scope.

---

## 5.4 Live Correlated Tab & Source Columns
- A new tab opens named `Correlation: <ID>`.
- Displays an integrated, chronological timeline of every matching event from all selected sources.
- Includes a dedicated **Source** column showing which log file generated each line, and a normalized **UTC Timestamp** column.
- Continues tailing live: if any service writes a new line containing that correlation ID, it appends in real time.

---

## 5.5 Jump to Original Line Navigation
- **UI Location**: Right-click any row in the correlated tab > **Open original line**.
- **Behavior**: Switches focus to the original log file tab and scrolls directly to that line.

---

# 6. Combined Multi-File Live Timeline

## 6.1 Creating a Combined Timeline
- **UI Location**: `Edit > Combined Timeline…`.
- **Purpose**: Merges 2 or more open log files into a single master chronological stream.

### Demo / Scenario
You open `web-server.log` and `database.log`. Click **Edit > Combined Timeline…**, select both files. A new timeline tab displays both streams interleaved in exact chronological order.

---

## 6.2 UTC Chronological Normalization & Timezone Handling
- Timestamps with timezone offsets (`+02:00`, `Z`, `-05:00`) are converted to UTC for exact sorting.
- Offset-free timestamps are treated using the local machine timezone.
- Undated lines (such as continuation frames) sort directly following their parent dated record.

---

## 6.3 Source Identification & File Path Tooltips
- Each line features a distinct `Source` column displaying the file name. Hovering the file name displays a tooltip with the complete absolute file path.

---

## 6.4 Independent Filtering & Freezing
- Searching and filtering in a combined timeline does not affect the filters of the underlying source files.
- Pausing the timeline freezes the merged view while the original source tabs keep streaming.

---

# 7. Interactive Error Timeline Chart

## 7.1 Error Timeline Chart Panel
- **UI Location**: Dockable bottom panel: `View > Panels > Error Timeline Chart`, Toolbar toggle `📊 Chart`, Shortcut: `Ctrl+Shift+T`.
- **Purpose**: A high-performance vector bar chart visualizing log event frequency and severity spikes over time.

---

## 7.2 Adaptive Time Bucketing & Severity Distribution
- Analyzes loaded log timestamps and groups them into up to 120 visual time buckets (from seconds to hours depending on total span).
- Each bucket renders as a stacked bar:
  - **Red**: Error count
  - **Yellow**: Warning count
  - **Blue**: Information count
  - **Gray**: Other events
- Hovering any bar displays a tooltip with exact start/end times and breakdown counts.

---

## 7.3 Click-and-Drag Time Range Marquee Filtering
- **Action**: Click and drag your mouse across any section of the chart.
- **Behavior**: An accent marquee highlights the selected time span. The log view immediately filters to display ONLY lines that occurred within that exact time range.
- A banner appears: `⏱ Filter: 14:10:00 - 14:15:30 [✕ Clear Filter]`. Clicking **Clear Filter** restores the full log view.

---

## 7.4 Click-to-Drill-Down Navigation
- Clicking a single bar in the chart immediately scrolls the log viewer to the first log event of that time bucket.

---

## 7.5 Undated Records Isolation
- If a log contains records without timestamps, LogTailPro does not invent fake dates. Instead, it reports `N undated events` in the chart metadata header so data accuracy is preserved.

---

# 8. Process Explorer & Live Output Capture

## 8.1 Process Explorer Panel
- **UI Location**: Dockable left panel: `View > Panels > Process Explorer`, Toolbar button `⚙ Processes`, Shortcut: `Ctrl+Shift+P`.
- **Purpose**: JetBrains dotPeek / Process Explorer style management tool to discover, inspect, and tail output from running processes and Windows services.

---

## 8.2 Process Tree View
- Displays running Windows processes in a parent-child process tree.
- **Metrics**: Real-time CPU percentage, private memory usage, process ID (PID), user account, and start time.
- **.NET Badge (`ᴺ`)**: Automatically detects and marks processes executing the .NET runtime.
- **Filters**:
  - Filter box: filter by name, PID, user, or path with wildcard (`w3wp*`) or regex support.
  - `.NET` checkbox: show only .NET processes.
  - `Sys` checkbox: toggle visibility of NT AUTHORITY system processes.
  - `Auto`: toggle automatic 2-second refresh.

---

## 8.3 Windows Services Mode
- Switch the radio button at the top of Process Explorer from **Processes** to **Services**.
- Displays all Windows services with status dots:
  - **Green**: Running
  - **Yellow**: Paused / Starting / Stopping
  - **Gray**: Stopped
- **Context Menu Actions**: **Start Service**, **Stop Service**, **Restart Service**, **Open Log Files…**, **Kill Hosting Process**.

---

## 8.4 Automated Log Discovery
- **Action**: Right-click any process or service > **Open Log Files…**, or press `Enter`.
- **Purpose**: Automatically discovers the log files written by that process without requiring manual path hunting.
- **Scoring Engine**:
  1. **Open File Handles**: Examines file handles currently held open by the process.
  2. **Configuration Scanning**: Reads configuration files next to the executable (`appsettings.json`, `log4net.config`, `NLog.config`, Serilog configs) and resolves dynamic targets like `${basedir}/logs/${shortdate}.log`.
  3. **Folder Scanning**: Scans common log locations (`logs\`, `%PROGRAMDATA%`, `%LOCALAPPDATA%`, `%TEMP%`) for recently modified `*.log` files.
- **Picker Dialog**: Displays discovered candidates with file sizes, last written timestamps, and marks the most probable main log with a gold star (`★`).

---

## 8.5 Batch Process Log Discovery (`Logs (N)`)
- Filter processes by query (e.g. `w3wp`).
- Click the toolbar button **Logs (N)**.
- Discovers and lists logs for all matching processes simultaneously in a single picker dialog.

---

## 8.6 Win32 `OutputDebugString` Live Capture
- **Action**: Right-click process > **Capture Debug Output (OutputDebugString)**.
- **Purpose**: Captures unmanaged Win32 and .NET debug prints (identical to Sysinternals DebugView) into a live streaming tab.
- **Engine**: Listens on Win32 shared memory buffer `DBWIN_BUFFER` and synchronization mutexes `DBWIN_DATA_READY` / `DBWIN_BUFFER_READY`.

---

## 8.7 Windows Event Log Live Monitoring
- **Action**: Right-click process > **Monitor Windows Event Log**.
- **Purpose**: Streams Windows Event Log entries (Application, System, Service errors) related to that process into a live tab.

---

## 8.8 Launch & Attach stdout/stderr
- **Action**: Right-click > **Launch & Attach stdout/stderr…**.
- **Dialog**: Configure executable path, command-line arguments, working directory, and select capture mode: `stdout`, `stderr`, or `Both`.
- **Behavior**: Launches the process as a child process, redirects its standard output streams, and streams console output directly into a LogTailPro tab in real time.

---

# 9. Navigation, Bookmarking & Live Analytics

## 9.1 Bookmarking System
- **Toggle Bookmark**: Select a row, press `Ctrl+B`. An accent circle appears in the left gutter.
- **Jump to Next / Previous Bookmark**: Press `F2` for next bookmark; `Shift+F2` for previous bookmark.
- **Bookmarks Dock Panel**: `View > Panels > Bookmarks` displays a list of all active bookmarks. Clicking any bookmark jumps directly to that line.
- **Persistence**: Bookmarks are automatically saved per file in `%APPDATA%\LogTailPro\settings.json` and restored when the file is re-opened.

---

## 9.2 Go To Line
- **UI Location**: Shortcut: `Ctrl+G`, Menu: `View > Go To Line…`.
- **Action**: Enter a line number and press Enter. The view scrolls immediately to that line and selects it.

---

## 9.3 Line Wrapping Toggle
- **UI Location**: Toolbar toggle button `↩ Wrap`, Menu: `View > Toggle Line Wrap`.
- **Behavior**: Toggles text wrapping for long lines. When wrapping is enabled, line numbers and gutter markers remain aligned with the top of the wrapped text.

---

## 9.4 Elapsed Time Delta Column
- **UI Location**: Shortcut: `Ctrl+Shift+E`.
- **Purpose**: Displays a `+hh:mm:ss.fff` delta column showing the exact elapsed time between consecutive log lines.
- **Use Case**: Instantly identifies latency bottlenecks and freezes in application execution.

---

## 9.5 Live Stats Dashboard Panel
- **UI Location**: Dockable panel: `View > Panels > Stats`, Shortcut: `Ctrl+Shift+S`.
- **Metrics**: Displays real-time counters for:
  - **ERRORS** (Red)
  - **WARNINGS** (Amber)
  - **INFO** (Blue)
  - **LINES / SEC** (Green live throughput counter)

---

# 10. Custom Highlighting & Log Parsing Engine

## 10.1 Highlight Rules Manager
- **UI Location**: Menu: `Tools > Highlight Rules…`, Toolbar toggle: `🎨 Highlight`.
- **Purpose**: Define custom text or regex rules that colorize entire lines or text backgrounds.

---

## 10.2 Custom Foreground & Background Color Swatches
- **Configuration**:
  - Click **Add Rule**.
  - Enter pattern (e.g. `EXCEPTION` or `(?i)order-\d+`).
  - Choose mode: `Text` or `Regex`.
  - Select Foreground color swatch and Background color swatch.
  - Test with live preview.
- **Evaluation Order**: Rules are evaluated top-down; the first matching rule applies its styling. Enable/disable rules with the checkbox.

---

## 10.3 Log Parser Regex Configuration
- **UI Location**: Menu: `Tools > Configure Log Parser…`.
- **Presets**: Apache Common Log, IIS W3C Log, Log4Net, Serilog, NLog, Custom.
- **Named Groups**: Parse custom log formats by defining named regex groups:
  - `(?<Timestamp>...)`
  - `(?<Level>...)`
  - `(?<Thread>...)`
  - `(?<Class>...)`
  - `(?<Message>...)`
- Includes interactive sample line tester with live preview of parsed groups.

---

# 11. Docking, Workspaces & Command Line Interface

## 11.1 AvalonDock Docking Layout & Tab Reordering
- LogTailPro features a dockable IDE layout engine.
- Every panel (**Process Explorer**, **Search & Results**, **Line Details**, **Stats**, **Bookmarks**, **Help Guide**, **Error Timeline Chart**) can be:
  - Dragged and docked to any window edge (Left, Right, Top, Bottom).
  - Pinned or auto-hidden.
  - Popped out into a floating window.

---

## 11.2 Split View Manager
- **UI Location**: Toolbar button `◫ Split View…`, Menu: `View > Split View…`.
- **Purpose**: View two or more logs side-by-side or stacked vertically in the same window.
- **Options**:
  - Choose tabs to show next to active log.
  - Choose layout: **Side by side (left | right)** or **Stacked (top / bottom)**.

---

## 11.3 Reset Layout
- **UI Location**: Toolbar button `⟲ Reset Layout`, Menu: `View > Reset Layout`.
- **Purpose**: Restores all panels, toolbars, and docking panes to their clean factory default layout if panels were accidentally hidden or resized.

---

## 11.4 Save & Load Workspace (`.ltpw` v2)
- **UI Location**: `File > Save Workspace…`, `File > Load Workspace…`.
- **Purpose**: Saves your entire investigation state to a file.
- **Persisted State**:
  - All open files, combined timelines, and correlation tabs.
  - Exact docking layout XML.
  - Active searches, regex flags, and filter states.
  - Scroll positions, bookmarks, and highlight rules.
- **Atomic Storage**: Uses temporary file replacement to prevent file corruption.

---

## 11.5 Application Settings & Persistence
- **UI Location**: `Tools > Settings…`.
- **Options**:
  - **Font Family & Size**: Consolas, Cascadia Mono, Courier New, Lucida Console, Fixedsys.
  - **Line Spacing & Char Offset**: Adjust vertical density.
  - **Max Lines in Memory**: Ring-buffer limit (0 = unlimited).
  - **Max Load Size (MB)**: Large file chunk threshold.
  - **Tab Width**: Spaces per tab character.
  - **Follow tail when opening a new file**: Auto-engage follow tail.
  - **Automatically open last file on startup**: Resume session.
  - **Preferences Storage**: Save to file (`settings.json`), save to Windows Registry, or don't save on exit.

---

## 11.6 Command Line Arguments & Multi-Window Tiling
LogTailPro supports full command-line automation for DevOps, scripts, and external tools:

```powershell
# Open one or multiple files
LogTailPro.exe C:\Logs\app.log C:\Logs\error.log

# Position window explicitly: -wp <left> <top> <width> <height>
LogTailPro.exe -wp 100 100 1200 800 C:\Logs\app.log

# Window state: -ws <0=Normal, 1=Minimized, 2=Maximized>
LogTailPro.exe -ws 2 C:\Logs\app.log

# Automatic multi-window monitor tiling: -tc <total_count> -ti <tile_index>
# Example: Tile 2 windows side-by-side across the primary screen:
Start-Process LogTailPro.exe -ArgumentList "-tc 2 -ti 0 C:\Logs\service1.log"
Start-Process LogTailPro.exe -ArgumentList "-tc 2 -ti 1 C:\Logs\service2.log"
```

---

## Quick Reference: Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+O` | Open Log File |
| `Ctrl+W` | Close Selected Tab |
| `Ctrl+Shift+V` | Open from Clipboard |
| `Ctrl+F` | Open Search Panel |
| `F3` / `Shift+F3` | Next / Previous Search Match |
| `Esc` | Close Search / Clear Highlights |
| `F` | Toggle Follow Tail Mode |
| `Ctrl+G` | Go To Line Number |
| `Ctrl+B` | Toggle Bookmark on Selected Line |
| `F2` / `Shift+F2` | Jump to Next / Previous Bookmark |
| `Ctrl+P` | Pretty-Print JSON / XML on Selected Line |
| `Ctrl+C` | Copy Selected Lines (Includes hidden exception traces) |
| `Ctrl+Shift+C` | Copy with Line Numbers |
| `Ctrl+A` | Select All Visible Lines |
| `Ctrl+Shift+P` | Toggle Process Explorer Panel |
| `Ctrl+Shift+T` | Toggle Error Timeline Chart Panel |
| `Ctrl+Shift+S` | Toggle Stats Panel |
| `Ctrl+Shift+E` | Toggle Elapsed Time Delta Column |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | Next / Previous Document Tab |
| `Ctrl+1` … `Ctrl+9` | Select Tab 1 through 9 |
| `Ctrl+Home` / `Ctrl+End` | Scroll to Top / Scroll to End |
