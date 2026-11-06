using System.Diagnostics;
using System.IO;
using System.Windows;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>
/// Provides the comprehensive repository of all in-depth feature help topics and search capabilities.
/// </summary>
public sealed class HelpContentService
{
    private readonly List<HelpTopic> _topics;

    public HelpContentService()
    {
        _topics = InitializeTopics();
    }

    public IReadOnlyList<HelpTopic> GetAllTopics() => _topics;

    public IReadOnlyList<string> GetCategories() =>
        _topics.Select(t => t.Category).Distinct().ToList();

    public IEnumerable<HelpTopic> Search(string? query, string? category = null)
    {
        IEnumerable<HelpTopic> stream = _topics;

        if (!string.IsNullOrWhiteSpace(category) && category != "All Categories")
        {
            stream = stream.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return stream;
        }

        string q = query.Trim();
        return stream.Where(t =>
            t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            t.ShortTitle.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            t.Overview.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            t.DemoScenario.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            t.UiLocation.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            t.Shortcut.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    public static void OpenMarkdownGuide()
    {
        try
        {
            string? baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string guidePath = Path.Combine(baseDir, "HELP_GUIDE.md");

            if (!File.Exists(guidePath))
            {
                // Fallback to project root if running from bin folder
                string altPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "HELP_GUIDE.md"));
                if (File.Exists(altPath)) guidePath = altPath;
            }

            if (File.Exists(guidePath))
            {
                Process.Start(new ProcessStartInfo(guidePath) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show($"HELP_GUIDE.md not found at:\n{guidePath}", "LogTailPro Help",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open guide: {ex.Message}", "LogTailPro Help",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static List<HelpTopic> InitializeTopics()
    {
        return new List<HelpTopic>
        {
            // ── 1. File Ingestion & Real-Time Monitoring ─────────────────────────────
            new()
            {
                Id = "open-file",
                Category = "1. Core File Ingestion & Real-Time Monitoring",
                Title = "1.1 Open File",
                ShortTitle = "Open File",
                Shortcut = "Ctrl+O",
                UiLocation = "File > Open… | Toolbar: 📂 Open | Drag-and-drop",
                Overview = "Opens one or multiple local or network log files into individual document tabs with non-exclusive, shared read permissions.",
                DemoScenario = "You drag 'api-service.log' from Windows File Explorer onto LogTailPro.\n" +
                               "The central welcome card vanishes and a new tab 'api-service.log' appears.\n" +
                               "Top-right toolbar displays: 'api-service.log (48.2 MB)'.\n" +
                               "Status bar displays: '412,890 lines', and the latest lines render instantly.",
                StepByStep = "1. Click '📂 Open' on the toolbar or press Ctrl+O.\n" +
                             "2. Select one or more log files (*.log, *.txt, *.json, *.out).\n" +
                             "3. Alternatively, drag files directly into the LogTailPro window.",
                UnderTheHood = "• Opens via FileShare.ReadWrite | FileShare.Delete to prevent ERROR_SHARING_VIOLATION.\n" +
                               "• Asynchronously streams line boundaries (\\r\\n and \\n).\n" +
                               "• Virtualized via WPF VirtualizingStackPanel in Recycling mode, keeping memory and CPU flat even with 1,000,000+ lines.\n" +
                               "• Persists path to Settings.RecentFiles in %APPDATA%\\LogTailPro\\settings.json."
            },
            new()
            {
                Id = "follow-tail",
                Category = "1. Core File Ingestion & Real-Time Monitoring",
                Title = "1.2 Follow Tail Mode",
                ShortTitle = "Follow Tail Mode",
                Shortcut = "F",
                UiLocation = "Toolbar: ⏬ Follow Tail | View > Toggle Follow Tail",
                Overview = "Automatically pins the viewer to the bottom of the log file, scrolling in real time as new lines append to disk.",
                DemoScenario = "A live backend service generates 20 lines every second.\n" +
                               "With Follow Tail ON, the view scrolls smoothly with new records.\n" +
                               "The bottom status bar lights up a green 'Following' badge.\n" +
                               "Scrolling up with your mouse wheel automatically unchecks Follow Tail so you can read an earlier error without disruption.\n" +
                               "Pressing 'F' re-engages Follow Tail and snaps back to the live tail.",
                StepByStep = "1. Press 'F' or click '⏬ Follow Tail' to toggle auto-scrolling.\n" +
                             "2. Scroll up with the mouse wheel to pause follow-tail and inspect history.\n" +
                             "3. Press 'F' or Ctrl+End to snap back to the bottom and resume following.",
                UnderTheHood = "• When FileWatcherService appends lines, if IsFollowTail is true and lines pass filters, ScrollToLineRequested fires.\n" +
                               "• LogListView_ScrollChanged detects when the user scrolls away from the bottom and disables IsFollowTail to protect reading focus.\n" +
                               "• Status bar binds to IsFollowTail with StatusGreen indicator."
            },
            new()
            {
                Id = "pause-resume",
                Category = "1. Core File Ingestion & Real-Time Monitoring",
                Title = "1.3 Pause / Resume Stream",
                ShortTitle = "Pause / Resume Stream",
                Shortcut = "Space",
                UiLocation = "Search Panel: Stop / Pause button | Space (when list is focused)",
                Overview = "Freezes the live log view to inspect incoming logs without losing any disk events.",
                DemoScenario = "Your app is outputting 5,000 lines/sec. An error scrolls by.\n" +
                               "Press Space or click 'Stop'. The view freezes completely.\n" +
                               "The service continues writing to disk in the background.\n" +
                               "Click 'Resume' (or press Space) to catch up instantly without missing records.",
                StepByStep = "1. Click 'Stop' in the Search bar or hit Space.\n" +
                             "2. Status bar reports 'Paused'.\n" +
                             "3. Click 'Resume' or hit Space to stream new appends.",
                UnderTheHood = "• FileWatcherService.Pause() suspends stream reads without closing the OS file handle.\n" +
                               "• On Resume(), it determines the new file length, reads intermediate bytes, and updates lines in batch."
            },
            new()
            {
                Id = "large-files",
                Category = "1. Core File Ingestion & Real-Time Monitoring",
                Title = "1.4 Large File Engine (>2GB & Chunk Loading)",
                ShortTitle = "Large File Engine (>2GB)",
                Shortcut = "None",
                UiLocation = "Tools > Settings… > Max load size (MB)",
                Overview = "Enables opening multi-gigabyte log files (5GB–50GB) without running out of RAM or locking the UI.",
                DemoScenario = "You open a 14 GB production crash dump.\n" +
                               "With 'Max load size' set to 100 MB, LogTailPro opens the file in 1 second.\n" +
                               "It seeks directly backwards from EOF by 100 MB and displays the latest 600,000 lines, tailing live from that point onwards.",
                StepByStep = "1. Navigate to Tools > Settings….\n" +
                             "2. Configure 'Max load size (MB)' (e.g. 100 MB; 0 = whole file).\n" +
                             "3. Open any multi-gigabyte log file.",
                UnderTheHood = "• If FileInfo.Length > MaxLoadMegabytes * 1024 * 1024, stream position seeks to Length - MaxBytes.\n" +
                               "• Scans forward to the next '\\n' boundary to prevent displaying a cut-off partial line.\n" +
                               "• Uses ring-buffer retention in memory when Max lines in memory is configured."
            },
            new()
            {
                Id = "network-share",
                Category = "1. Core File Ingestion & Real-Time Monitoring",
                Title = "1.5 Network Share Polling & Recovery",
                ShortTitle = "Network Share Polling",
                Shortcut = "None",
                UiLocation = "Tools > Settings… > Network poll (ms)",
                Overview = "Reliable tailing for files on UNC network shares (\\\\server\\logs\\app.log) where OS ReadDirectoryChangesW events are dropped.",
                DemoScenario = "You tail '\\\\192.168.1.50\\logs\\orders.log'.\n" +
                               "If VPN disconnects, a yellow warning banner appears:\n" +
                               "'Network share unavailable. Retrying...'\n" +
                               "Once the VPN reconnects, LogTailPro catches up automatically.",
                StepByStep = "1. Set 'Network poll (ms)' in Tools > Settings… (default 1000ms).\n" +
                             "2. Open any UNC path (\\\\server\\...) or mapped drive (Z:\\...).",
                UnderTheHood = "• Detects UNC paths and starts a high-precision background timer.\n" +
                               "• Queries file metadata using GetFileInformationByHandleEx.\n" +
                               "• Enters exponential backoff retry loop on IOException or network drops."
            },
            new()
            {
                Id = "rotation-truncation",
                Category = "1. Core File Ingestion & Real-Time Monitoring",
                Title = "1.6 File Rotation & Truncation Handling",
                ShortTitle = "Rotation & Truncation",
                Shortcut = "None",
                UiLocation = "Top warning banner (BgWarningBanner)",
                Overview = "Detects when a log file is cleared, rolled over by logrotate/Serilog, or rewritten from scratch, alerting the user and auto-reloading.",
                DemoScenario = "An application rolls 'app.log' at midnight. File size drops from 500 MB to 0 bytes.\n" +
                               "An amber banner appears: '⚠️ File was truncated or rotated — reloaded from beginning'.\n" +
                               "The viewer resets its stream offset to zero and tails the new content seamlessly.",
                StepByStep = "1. No action required — handled automatically.\n" +
                             "2. The amber banner alerts you that the file was rotated or cleared.",
                UnderTheHood = "• If currentLength < lastReadByteOffset, truncation is detected.\n" +
                               "• Reader resets position to 0, signals FileTruncated, and reloads new content."
            },
            new()
            {
                Id = "watch-directory",
                Category = "1. Core File Ingestion & Real-Time Monitoring",
                Title = "1.7 Watch Directory",
                ShortTitle = "Watch Directory",
                Shortcut = "None",
                UiLocation = "File > Watch Directory…",
                Overview = "Monitors a directory for newly created *.log files and opens each one automatically in a new tab.",
                DemoScenario = "A test suite creates a new log file per test case into C:\\Logs\\TestRun\\.\n" +
                               "Select 'Watch Directory'. As tests execute, new tabs pop up automatically and begin tailing immediately.",
                StepByStep = "1. Click File > Watch Directory….\n" +
                             "2. Select a target folder and click OK.",
                UnderTheHood = "• Uses DirectoryMonitorService with FileSystemWatcher listening for Created events.\n" +
                               "• Dispatches MainViewModel.OpenFileTabAsync on UI thread."
            },
            new()
            {
                Id = "open-clipboard",
                Category = "1. Core File Ingestion & Real-Time Monitoring",
                Title = "1.8 Open from Clipboard",
                ShortTitle = "Open from Clipboard",
                Shortcut = "Ctrl+Shift+V",
                UiLocation = "File > Open from Clipboard",
                Overview = "Instantly creates a temporary log tab containing text or stack traces copied from other applications.",
                DemoScenario = "A colleague sends a 200-line JSON exception dump in Teams.\n" +
                               "Copy the text, switch to LogTailPro, and press Ctrl+Shift+V.\n" +
                               "A tab titled 'Clipboard' opens with full highlighting, JSON parsing, and search.",
                StepByStep = "1. Copy text to the Windows Clipboard (Ctrl+C).\n" +
                             "2. In LogTailPro, press Ctrl+Shift+V or click File > Open from Clipboard.",
                UnderTheHood = "• Reads Clipboard.GetText() and writes to %TEMP%\\LogTailPro_clipboard_<timestamp>.log.\n" +
                               "• Opens the tab in memory and cleans up the temp file on close."
            },

            // ── 2. Search, Filtering & Saved Patterns ────────────────────────────────
            new()
            {
                Id = "find-bar",
                Category = "2. Search, Filtering & Saved Patterns",
                Title = "2.1 Find Bar & Query Execution",
                ShortTitle = "Find Bar",
                Shortcut = "Ctrl+F",
                UiLocation = "Search > Find… | Toolbar: 🔍 Search | Esc to close",
                Overview = "Searches loaded lines in memory with visual amber highlights and keyboard jumping.",
                DemoScenario = "Press Ctrl+F. The search panel opens at the bottom.\n" +
                               "Type 'TIMEOUT' and press Enter. All matching lines highlight in amber.\n" +
                               "Press F3 to jump to the next match; Shift+F3 jumps backwards.",
                StepByStep = "1. Press Ctrl+F to open the search bar.\n" +
                             "2. Type query and press Enter.\n" +
                             "3. Use F3 (Next) and Shift+F3 (Previous) to navigate.\n" +
                             "4. Press Esc to close search and clear highlights.",
                UnderTheHood = "• SearchService runs asynchronously on a background thread with CancellationToken.\n" +
                               "• Keystrokes cancel the prior in-flight search to prevent UI stutters."
            },
            new()
            {
                Id = "regex-search",
                Category = "2. Search, Filtering & Saved Patterns",
                Title = "2.2 Text vs Regular Expression Modes",
                ShortTitle = "Text vs Regex Modes",
                Shortcut = "None",
                UiLocation = "Search panel dropdown: Text / Regex | Checkbox: Regex syntax",
                Overview = "Toggle between high-speed literal string searching and regex matching.",
                DemoScenario = "Switch mode to 'Regex' and type: HTTP/\\d\\.\\d\" 5\\d\\d\n" +
                               "Matches all HTTP 500, 502, 503 error lines.\n" +
                               "Invalid regex like [a-z( displays an inline red error message safely.",
                StepByStep = "1. In Search panel, select 'Regex' from the dropdown or check 'Regex syntax'.\n" +
                             "2. Type your regular expression pattern and press Enter.",
                UnderTheHood = "• Text mode uses string.IndexOf(..., StringComparison.OrdinalIgnoreCase).\n" +
                               "• Regex mode uses Regex(pattern, RegexOptions.Compiled, 1sec timeout) to prevent ReDoS."
            },
            new()
            {
                Id = "invert-match",
                Category = "2. Search, Filtering & Saved Patterns",
                Title = "2.3 Case Sensitivity & Invert Matching",
                ShortTitle = "Case & Invert Matching",
                Shortcut = "None",
                UiLocation = "Search panel checkboxes: Ignore case | Invert match",
                Overview = "Invert match displays or highlights only lines that DO NOT match the query.",
                DemoScenario = "Your log is flooded with health checks: GET /health 200 OK.\n" +
                               "Type '/health', check 'Invert match', and check 'Filter tail'.\n" +
                               "All health checks vanish; only real application logs remain.",
                StepByStep = "1. Enter search term.\n" +
                             "2. Check 'Invert match' to find lines NOT containing the term.",
                UnderTheHood = "• Evaluates boolean: isMatch ^ IsInvertMatch."
            },
            new()
            {
                Id = "filter-tail",
                Category = "2. Search, Filtering & Saved Patterns",
                Title = "2.4 Filter Tail Mode (Dynamic Grep)",
                ShortTitle = "Filter Tail Mode",
                Shortcut = "None",
                UiLocation = "Search panel checkbox: Filter tail",
                Overview = "Filters the view so only matching lines are shown. New matching appends appear at the bottom in real time.",
                DemoScenario = "Tailing a log outputting 1,000 lines/sec. You search 'ERROR' and check 'Filter tail'.\n" +
                               "The 200,000 lines collapse down to only the 12 error lines.\n" +
                               "When a new error arrives 10 minutes later, it appends at the bottom instantly.",
                StepByStep = "1. Enter search term or regex.\n" +
                             "2. Check 'Filter tail'.\n" +
                             "3. Uncheck to restore the full log view instantly.",
                UnderTheHood = "• VisibleLines holds filtered items while AllLines retains complete file history in RAM.\n" +
                               "• New appends pass through LinePassesFilter before being added to VisibleLines."
            },
            new()
            {
                Id = "search-results-panel",
                Category = "2. Search, Filtering & Saved Patterns",
                Title = "2.5 Search Results Dock Panel",
                ShortTitle = "Search Results Panel",
                Shortcut = "None",
                UiLocation = "View > Panels > Search Results (bottom dock)",
                Overview = "Tabular grid of all search hits across the entire file with line numbers, timestamps, and regex capture groups.",
                DemoScenario = "Searching regex 'order (?<ID>\\d+) failed' finds 45 matches.\n" +
                               "The Search Results panel lists all 45 occurrences.\n" +
                               "Double-clicking row #12 jumps the main log viewer directly to that line.",
                StepByStep = "1. Perform a search.\n" +
                             "2. Open the Search Results dockable panel.\n" +
                             "3. Double-click any row to jump to its location in the log.",
                UnderTheHood = "• SearchResultRow populates dynamic columns for named regex groups.\n" +
                               "• Double-clicking fires ScrollToLineRequested."
            },
            new()
            {
                Id = "saved-patterns",
                Category = "2. Search, Filtering & Saved Patterns",
                Title = "2.6 Saved Search Patterns & Presets",
                ShortTitle = "Saved Search Patterns",
                Shortcut = "None",
                UiLocation = "Search panel: ＋ Save pattern button",
                Overview = "Save complex regex or text filters with custom names to re-use across sessions.",
                DemoScenario = "Save regex '(FATAL|CRITICAL|NullReference)' as 'Fatal Errors'.\n" +
                               "Next time, select 'Fatal Errors' from the presets to apply it in one click.",
                StepByStep = "1. Configure your search query and options.\n" +
                             "2. Click '＋ Save pattern'. Enter a name and click OK.",
                UnderTheHood = "• Stored in AppSettings.SavedPatterns and serialized to settings.json."
            },
            new()
            {
                Id = "export-filtered",
                Category = "2. Search, Filtering & Saved Patterns",
                Title = "2.7 Export Filtered Lines",
                ShortTitle = "Export Filtered Lines",
                Shortcut = "None",
                UiLocation = "File > Export Filtered Lines…",
                Overview = "Exports the currently visible (filtered) lines into a clean text or log file on disk.",
                DemoScenario = "After filtering a 10 GB file down to 42 error lines, click 'File > Export Filtered Lines…'.\n" +
                               "Save as 'errors.txt'. Only the 42 lines are written.",
                StepByStep = "1. Apply your desired search / filter.\n" +
                             "2. Click File > Export Filtered Lines….\n" +
                             "3. Choose a filename and save.",
                UnderTheHood = "• Writes VisibleLines.Select(l => l.Text) via File.WriteAllLinesAsync."
            },

            // ── 3. Structured JSON Logs & Property Inspector ─────────────────────────
            new()
            {
                Id = "json-detection",
                Category = "3. Structured JSON Logs & Property Inspector",
                Title = "3.1 JSON & CLEF Auto-Detection",
                ShortTitle = "JSON & CLEF Detection",
                Shortcut = "None",
                UiLocation = "Automatic on file load",
                Overview = "Detects line-delimited JSON and Serilog Compact Log Event Format (CLEF: @t, @l, @m, @mt, @x).",
                DemoScenario = "Opening a file with lines like:\n" +
                               "{\"@t\":\"2026-11-06T14:02:11Z\",\"@l\":\"Error\",\"@mt\":\"Failed: {Id}\",\"Id\":123}\n" +
                               "LogTailPro automatically enables the '⛯ Structured' toolbar button.",
                StepByStep = "1. Open any JSON or Serilog CLEF log file.\n" +
                             "2. Notice the '⛯ Structured' button becomes available on the toolbar.",
                UnderTheHood = "• JsonLogParser samples lines with 2MB max-length safety limits.\n" +
                               "• Extracts timestamps, levels, templates, messages, and exceptions."
            },
            new()
            {
                Id = "structured-view",
                Category = "3. Structured JSON Logs & Property Inspector",
                Title = "3.2 Structured Event View vs Raw Text View",
                ShortTitle = "Structured vs Raw View",
                Shortcut = "None",
                UiLocation = "Toolbar: ⛯ Structured | View > Structured View",
                Overview = "Toggles between human-readable rendered event summaries and raw JSON lines.",
                DemoScenario = "Raw: {\"@t\":\"2026-11-06T14:02:11Z\",\"@l\":\"Error\",\"@mt\":\"Order {Id} failed\",\"Id\":\"10\"}\n" +
                               "Structured: [14:02:11] [ERROR] Order 10 failed\n" +
                               "Removes JSON clutter for rapid visual scanning.",
                StepByStep = "1. Click '⛯ Structured' on the toolbar or select View > Structured View.",
                UnderTheHood = "• Switches ListView bindings between Text and StructuredMessage / StructuredLevel."
            },
            new()
            {
                Id = "property-inspector",
                Category = "3. Structured JSON Logs & Property Inspector",
                Title = "3.4 Line Details / Property Inspector Panel",
                ShortTitle = "Line Details Inspector",
                Shortcut = "None",
                UiLocation = "View > Panels > Line Details (right dock)",
                Overview = "Deep inspection panel displaying structured properties, templates, exceptions, and raw/pretty JSON.",
                DemoScenario = "Click any log line.\n" +
                               "Line Details shows:\n" +
                               "1. Header: Severity badge, ISO timestamp, rendered message, template.\n" +
                               "2. Properties: Interactive tree of all JSON fields.\n" +
                               "3. Raw/Formatted Text: Toggle between Pretty JSON and Raw line.",
                StepByStep = "1. Click any line in the log viewer.\n" +
                             "2. Open the 'Line Details' panel to inspect properties.",
                UnderTheHood = "• LogPropertyNode models hierarchical key-values with context menu actions."
            },
            new()
            {
                Id = "correlate-property",
                Category = "3. Structured JSON Logs & Property Inspector",
                Title = "3.5 Hierarchical Property Tree & Correlate by Value",
                ShortTitle = "Correlate by Value",
                Shortcut = "None",
                UiLocation = "Right-click any property in Line Details > Correlate by Value",
                Overview = "Instantly tracks any JSON property value across all open log files.",
                DemoScenario = "In the property tree, find: Headers.X-Correlation-Id = 'c89b-4412'.\n" +
                               "Right-click > 'Correlate by Value'.\n" +
                               "A new multi-source correlation tab opens tracking that exact token.",
                StepByStep = "1. Expand properties in Line Details.\n" +
                             "2. Right-click any value and select 'Correlate by Value'.",
                UnderTheHood = "• Invokes MainViewModel.CorrelateId with the selected property string."
            },
            new()
            {
                Id = "pretty-print",
                Category = "3. Structured JSON Logs & Property Inspector",
                Title = "3.6 JSON / XML Pretty-Printing",
                ShortTitle = "Pretty-Print Selected Line",
                Shortcut = "Ctrl+P",
                UiLocation = "Tools > Pretty-Print Selected Line | Ctrl+P",
                Overview = "Formats minified single-line JSON or XML payloads into clean indented text.",
                DemoScenario = "Select a log line with a minified 5KB JSON blob. Press Ctrl+P.\n" +
                               "The formatted, indented JSON opens in the Line Details panel with a Copy button.",
                StepByStep = "1. Select a log line containing JSON or XML.\n" +
                             "2. Press Ctrl+P or click Tools > Pretty-Print Selected Line.",
                UnderTheHood = "• Parses via JsonDocument or XmlDocument and serializes with indentation."
            },

            // ── 4. Multiline Exception Grouping ──────────────────────────────────────
            new()
            {
                Id = "exception-grouping",
                Category = "4. Multiline Exception Grouping",
                Title = "4.1 Group Multiline Exceptions Toggle",
                ShortTitle = "Exception Grouping Toggle",
                Shortcut = "None",
                UiLocation = "Toolbar: ▶ Group Exceptions | View > Group Multiline Exceptions",
                Overview = "Folds 50+ line stack traces into single expandable rows, keeping log scanning clean.",
                DemoScenario = "An exception creates 80 lines of 'at System.Data.SqlClient...'.\n" +
                               "With Group Exceptions ON, it collapses to:\n" +
                               "'▶ [+79 frames] 14:02:11 [ERROR] SqlException: Connection timeout'\n" +
                               "Clicking ▶ expands the trace; clicking ▼ collapses it back.",
                StepByStep = "1. Click '▶ Group Exceptions' on the toolbar.\n" +
                             "2. Click the ▶ button beside any exception to expand/collapse it.\n" +
                             "3. Use View > Expand All Exceptions to expand all at once.",
                UnderTheHood = "• MultilineGroupingService links frames into LogLine.Continuations.\n" +
                               "• Hidden frames remain in the group parent for search and copying."
            },
            new()
            {
                Id = "trace-detection",
                Category = "4. Multiline Exception Grouping",
                Title = "4.2 Multi-Language Trace Detection (.NET, Java, Python)",
                ShortTitle = "Multi-Language Detection",
                Shortcut = "None",
                UiLocation = "Automatic when exception grouping is enabled",
                Overview = "Automatically recognizes stack traces from .NET (C#), Java, and Python.",
                DemoScenario = "Detects .NET 'at Namespace.Class.Method in File.cs:line 12',\n" +
                               "Java 'Caused by: com.pkg.Class: method (Class.java:42)',\n" +
                               "and Python 'Traceback (most recent call last):' seamlessly.",
                StepByStep = "1. Enable '▶ Group Exceptions' on any tab containing stack traces.",
                UnderTheHood = "• Regex-based heuristic parser with boundary marker detection."
            },
            new()
            {
                Id = "trace-search",
                Category = "4. Multiline Exception Grouping",
                Title = "4.4 Search & Filter Transparency Across Hidden Frames",
                ShortTitle = "Search Inside Traces",
                Shortcut = "None",
                UiLocation = "Automatic during search",
                Overview = "Searching matches terms that exist inside collapsed stack traces without requiring you to expand them first.",
                DemoScenario = "Search 'UserRepository.cs'. It exists on line 35 inside a collapsed trace.\n" +
                               "The collapsed root row highlights as a match so critical errors are never missed.",
                StepByStep = "1. Collapse exceptions.\n" +
                             "2. Search for any term inside the stack trace (e.g. filename or method).\n" +
                             "3. The collapsed root row matches and highlights.",
                UnderTheHood = "• SearchService inspects LogLine.Continuations for collapsed parent rows."
            },

            // ── 5. Cross-Log Request & Trace ID Correlation ──────────────────────────
            new()
            {
                Id = "correlation-workflow",
                Category = "5. Cross-Log Request & Trace ID Correlation",
                Title = "5.1 Correlation Workflow",
                ShortTitle = "Correlation Workflow",
                Shortcut = "None",
                UiLocation = "Edit > Correlate Request / Trace ID… | Line Details: Correlate ID",
                Overview = "Correlates distributed transaction logs across multiple open microservice log files into one synchronized timeline.",
                DemoScenario = "You open Frontend.log, Gateway.log, and Database.log.\n" +
                               "Select a line with TraceId = '4bf92f3577b34da6'. Click 'Correlate ID'.\n" +
                               "A new tab 'Correlation: 4bf92f3577b34da6' opens showing only events matching that transaction across all 3 files in chronological order.",
                StepByStep = "1. Select a log line containing a request ID or trace token.\n" +
                             "2. Click 'Correlate ID' in Line Details or Edit > Correlate Request / Trace ID….\n" +
                             "3. Select the source logs to search and click 'Correlate'.",
                UnderTheHood = "• CorrelationService performs exact-boundary matching.\n" +
                               "• Merges matching records with UTC sorting and continues live tailing."
            },
            new()
            {
                Id = "open-original-line",
                Category = "5. Cross-Log Request & Trace ID Correlation",
                Title = "5.5 Jump to Original Line Navigation",
                ShortTitle = "Jump to Original Line",
                Shortcut = "None",
                UiLocation = "Right-click in Correlation/Timeline tab > Open original line",
                Overview = "Navigates from a correlated or combined timeline row directly back to its original source file tab and line position.",
                DemoScenario = "In a correlation tab, you see a database error from 'db-worker.log'.\n" +
                               "Right-click the row > 'Open original line'.\n" +
                               "LogTailPro switches to the 'db-worker.log' tab and highlights the line.",
                StepByStep = "1. Right-click any row in a Correlation or Timeline tab.\n" +
                             "2. Click 'Open original line'.",
                UnderTheHood = "• Fires OriginalLineRequested to switch MainViewModel.SelectedTab and call NavigateToLine."
            },

            // ── 6. Combined Multi-File Live Timeline ─────────────────────────────────
            new()
            {
                Id = "combined-timeline",
                Category = "6. Combined Multi-File Live Timeline",
                Title = "6.1 Creating a Combined Timeline",
                ShortTitle = "Combined Timeline",
                Shortcut = "None",
                UiLocation = "Edit > Combined Timeline…",
                Overview = "Merges two or more open log files into a single master chronological stream ordered by UTC timestamp.",
                DemoScenario = "Open 'web.log' and 'auth.log'. Click Edit > Combined Timeline….\n" +
                               "Select both files. A new timeline tab displays both streams interleaved by timestamp with dedicated 'Source' columns.",
                StepByStep = "1. Open 2 or more log files.\n" +
                             "2. Click Edit > Combined Timeline….\n" +
                             "3. Select the sources to include and click OK.",
                UnderTheHood = "• Normalizes timestamps across formats to UTC.\n" +
                               "• Timeline keeps running independently of source tab filters."
            },

            // ── 7. Interactive Error Timeline Chart ──────────────────────────────────
            new()
            {
                Id = "error-chart",
                Category = "7. Interactive Error Timeline Chart",
                Title = "7.1 Error Timeline Chart Panel",
                ShortTitle = "Error Timeline Chart",
                Shortcut = "Ctrl+Shift+T",
                UiLocation = "Toolbar: 📊 Chart | View > Panels > Error Timeline Chart | Ctrl+Shift+T",
                Overview = "Vector bar chart visualizing event frequency and severity spikes (Errors, Warnings, Info) over time.",
                DemoScenario = "Open a log with 500,000 lines. Press Ctrl+Shift+T.\n" +
                               "The chart renders stacked bars across the time span.\n" +
                               "A giant red spike at 14:15 indicates a server outage.\n" +
                               "Click the bar to jump the log view directly to 14:15.",
                StepByStep = "1. Press Ctrl+Shift+T or click '📊 Chart' on the toolbar.\n" +
                             "2. Click any bar to jump to that timestamp in the log.",
                UnderTheHood = "• ErrorTimelineChartCanvas renders native vector geometries.\n" +
                               "• ErrorTimelineChartService aggregates records into adaptive time buckets."
            },
            new()
            {
                Id = "chart-marquee",
                Category = "7. Interactive Error Timeline Chart",
                Title = "7.3 Click-and-Drag Time Range Marquee Filtering",
                ShortTitle = "Marquee Time Filtering",
                Shortcut = "None",
                UiLocation = "Click and drag across the Error Timeline Chart",
                Overview = "Selects a time window on the chart to filter the log view to only events occurring in that window.",
                DemoScenario = "Click and drag from 14:10 to 14:20 on the chart.\n" +
                               "A blue selection marquee appears.\n" +
                               "The log viewer filters down to only events in that 10-minute window.\n" +
                               "Click '✕ Clear Filter' to restore the full view.",
                StepByStep = "1. Click and drag horizontally across bars on the chart.\n" +
                             "2. Release mouse. The log filters immediately.\n" +
                             "3. Click '✕ Clear Filter' in the banner to reset.",
                UnderTheHood = "• Sets TimeRangeFilter on LogTabViewModel, evaluating line.Timestamp boundaries."
            },

            // ── 8. Process Explorer & Live Output Capture ────────────────────────────
            new()
            {
                Id = "process-explorer",
                Category = "8. Process Explorer & Live Output Capture",
                Title = "8.1 Process Explorer Panel",
                ShortTitle = "Process Explorer Panel",
                Shortcut = "Ctrl+Shift+P",
                UiLocation = "Toolbar: ⚙ Processes | View > Panels > Process Explorer | Ctrl+Shift+P",
                Overview = "Side panel displaying running Windows processes and services with CPU, RAM, and automated log discovery.",
                DemoScenario = "Press Ctrl+Shift+P.\n" +
                               "The Process Explorer panel docks on the left showing running processes in a tree.\n" +
                               ".NET processes are flagged with an accent 'ᴺ' badge.",
                StepByStep = "1. Press Ctrl+Shift+P or click '⚙ Processes'.\n" +
                             "2. Type in the filter box to find processes by name or PID.\n" +
                             "3. Toggle '.NET', 'Sys', or 'Auto' refresh.",
                UnderTheHood = "• ProcessSnapshotService queries Toolhelp32 snapshots and performance counters."
            },
            new()
            {
                Id = "services-mode",
                Category = "8. Process Explorer & Live Output Capture",
                Title = "8.3 Windows Services Mode",
                ShortTitle = "Windows Services Mode",
                Shortcut = "None",
                UiLocation = "Process Explorer top radio: Services",
                Overview = "Manage Windows services, inspect hosting PIDs, and Start, Stop, or Restart services directly.",
                DemoScenario = "Select 'Services' in Process Explorer.\n" +
                               "Running services display green status dots; stopped services display gray.\n" +
                               "Right-click a service to Start, Stop, Restart, or Open Log Files.",
                StepByStep = "1. Switch radio button to 'Services'.\n" +
                             "2. Right-click any service for control options.",
                UnderTheHood = "• Queries ServiceController APIs and maps services to hosting svchost PIDs."
            },
            new()
            {
                Id = "log-discovery",
                Category = "8. Process Explorer & Live Output Capture",
                Title = "8.4 Automated Log Discovery",
                ShortTitle = "Automated Log Discovery",
                Shortcut = "Enter",
                UiLocation = "Right-click process > Open Log Files… | Press Enter on process",
                Overview = "Automatically locates log files written by any process by inspecting open handles, configs, and log folders.",
                DemoScenario = "Right-click 'w3wp.exe' > 'Open Log Files…'.\n" +
                               "LogTailPro scans open handles and config files, finds 'W3SVC1\\u_ex261106.log',\n" +
                               "marks it with a gold star (★), and opens it in a new tab.",
                StepByStep = "1. Select any process or service in Process Explorer.\n" +
                             "2. Press Enter or right-click > 'Open Log Files…'.\n" +
                             "3. Tick the desired log files and click Open.",
                UnderTheHood = "• ProcessLogDiscoveryService scans: 1) Open file handles via NtQuerySystemInformation, 2) Configs (NLog, log4net, Serilog), 3) Folder heuristic scans."
            },
            new()
            {
                Id = "output-debug-string",
                Category = "8. Process Explorer & Live Output Capture",
                Title = "8.6 Win32 OutputDebugString Live Capture",
                ShortTitle = "OutputDebugString Capture",
                Shortcut = "None",
                UiLocation = "Right-click process > Capture Debug Output (OutputDebugString)",
                Overview = "Captures unmanaged Win32 OutputDebugString calls (DebugView-style) into a live streaming tab.",
                DemoScenario = "Right-click an application > 'Capture Debug Output'.\n" +
                               "A new tab streams all internal Debug.WriteLine and OutputDebugString calls in real time.",
                StepByStep = "1. Right-click target process in Process Explorer.\n" +
                             "2. Click 'Capture Debug Output (OutputDebugString)'.",
                UnderTheHood = "• Listens on Win32 shared memory DBWIN_BUFFER with DBWIN_DATA_READY / DBWIN_BUFFER_READY mutexes."
            },
            new()
            {
                Id = "launch-attach",
                Category = "8. Process Explorer & Live Output Capture",
                Title = "8.8 Launch & Attach stdout/stderr",
                ShortTitle = "Launch & Attach",
                Shortcut = "None",
                UiLocation = "Right-click process > Launch & Attach stdout/stderr…",
                Overview = "Launches an executable and redirects its standard console streams into a live tab.",
                DemoScenario = "Configure: 'dotnet.exe', args: 'run --project MyApi', capture: 'Both'.\n" +
                               "Click '▶ Launch'. Console stdout and stderr stream directly into LogTailPro.",
                StepByStep = "1. Select 'Launch & Attach stdout/stderr…'.\n" +
                             "2. Set executable, arguments, and working directory.\n" +
                             "3. Select stdout, stderr, or Both, then click Launch.",
                UnderTheHood = "• Starts process with ProcessStartInfo.RedirectStandardOutput/Error."
            },

            // ── 9. Navigation, Bookmarking & Live Analytics ──────────────────────────
            new()
            {
                Id = "bookmarks",
                Category = "9. Navigation, Bookmarking & Live Analytics",
                Title = "9.1 Bookmarking System",
                ShortTitle = "Bookmarking System",
                Shortcut = "Ctrl+B",
                UiLocation = "Ctrl+B to toggle | F2 / Shift+F2 to jump | View > Panels > Bookmarks",
                Overview = "Mark critical lines with accent dots and navigate between them instantly.",
                DemoScenario = "Select line 4,120 and press Ctrl+B. A blue accent dot appears in the gutter.\n" +
                               "Later, press F2 anywhere in the file to jump directly back to line 4,120.\n" +
                               "Bookmarks are preserved even if you close and re-open LogTailPro.",
                StepByStep = "1. Select a row and press Ctrl+B to toggle bookmark.\n" +
                             "2. Press F2 to jump to next bookmark; Shift+F2 for previous.\n" +
                             "3. Open the Bookmarks panel to view all bookmarks in a list.",
                UnderTheHood = "• Bookmarks are stored in Settings.Bookmarks by file path and persisted across restarts."
            },
            new()
            {
                Id = "go-to-line",
                Category = "9. Navigation, Bookmarking & Live Analytics",
                Title = "9.2 Go To Line",
                ShortTitle = "Go To Line",
                Shortcut = "Ctrl+G",
                UiLocation = "View > Go To Line… | Ctrl+G",
                Overview = "Direct jump navigation to any line number.",
                DemoScenario = "Press Ctrl+G. Enter '84520' and press Enter.\n" +
                               "The viewer scrolls immediately to line 84,520 and highlights the row.",
                StepByStep = "1. Press Ctrl+G.\n" +
                             "2. Type line number and press Enter.",
                UnderTheHood = "• Locates first LogLine with LineNumber >= target and scrolls into view."
            },
            new()
            {
                Id = "line-wrap",
                Category = "9. Navigation, Bookmarking & Live Analytics",
                Title = "9.3 Line Wrapping Toggle",
                ShortTitle = "Line Wrapping",
                Shortcut = "None",
                UiLocation = "Toolbar: ↩ Wrap | View > Toggle Line Wrap",
                Overview = "Wraps long log lines to fit window width while maintaining gutter alignment.",
                DemoScenario = "A 2,000-character line requires horizontal scrolling.\n" +
                               "Click '↩ Wrap'. The text wraps cleanly within the window boundary without obscuring line numbers.",
                StepByStep = "1. Click '↩ Wrap' on the toolbar.",
                UnderTheHood = "• Switches TextWrapping to Wrap while keeping vertical alignment Top."
            },
            new()
            {
                Id = "elapsed-column",
                Category = "9. Navigation, Bookmarking & Live Analytics",
                Title = "9.4 Elapsed Time Delta Column",
                ShortTitle = "Elapsed Time Delta",
                Shortcut = "Ctrl+Shift+E",
                UiLocation = "View > Toggle Elapsed Column | Ctrl+Shift+E",
                Overview = "Displays a +hh:mm:ss.fff column indicating the time difference between consecutive log lines.",
                DemoScenario = "Press Ctrl+Shift+E.\n" +
                               "A column appears: '+00:00:00.012', '+00:00:04.810'.\n" +
                               "The 4.8-second delta highlights an unexpected database delay immediately.",
                StepByStep = "1. Press Ctrl+Shift+E to toggle elapsed column visibility.",
                UnderTheHood = "• Computes TimeSpan difference between line.Timestamp and previous line's timestamp."
            },
            new()
            {
                Id = "stats-panel",
                Category = "9. Navigation, Bookmarking & Live Analytics",
                Title = "9.5 Live Stats Dashboard Panel",
                ShortTitle = "Live Stats Panel",
                Shortcut = "Ctrl+Shift+S",
                UiLocation = "View > Panels > Stats | Ctrl+Shift+S",
                Overview = "Real-time dashboard reporting total Errors, Warnings, Info counts, and live Lines/Sec throughput.",
                DemoScenario = "Press Ctrl+Shift+S.\n" +
                               "The Stats panel opens showing 14 Errors, 82 Warnings, and live throughput '450 lines/sec'.",
                StepByStep = "1. Press Ctrl+Shift+S to open the Stats dock panel.",
                UnderTheHood = "• Aggregates counts during stream ingestion and computes rolling 1-second lines/sec."
            },

            // ── 10. Custom Highlighting & Log Parsing Engine ─────────────────────────
            new()
            {
                Id = "highlight-rules",
                Category = "10. Custom Highlighting & Log Parser",
                Title = "10.1 Highlight Rules Manager",
                ShortTitle = "Highlight Rules Manager",
                Shortcut = "None",
                UiLocation = "Tools > Highlight Rules… | Toolbar: 🎨 Highlight",
                Overview = "Define custom text or regex rules that colorize entire lines or backgrounds.",
                DemoScenario = "Click Tools > Highlight Rules….\n" +
                               "Add rule: Pattern = 'FATAL', Foreground = White, Background = Dark Red.\n" +
                               "Click OK. Every line containing 'FATAL' immediately renders with dark red background.",
                StepByStep = "1. Click Tools > Highlight Rules….\n" +
                             "2. Click 'Add Rule', set pattern, mode (Text/Regex), and pick colors.\n" +
                             "3. Click OK to apply across all tabs.",
                UnderTheHood = "• HighlightService evaluates rules top-down; first match applies.\n" +
                               "• Persisted to Settings.HighlightRules."
            },
            new()
            {
                Id = "log-parser",
                Category = "10. Custom Highlighting & Log Parser",
                Title = "10.3 Log Parser Regex Configuration",
                ShortTitle = "Log Parser Configuration",
                Shortcut = "None",
                UiLocation = "Tools > Configure Log Parser…",
                Overview = "Configure custom regex patterns with named capture groups (?<Timestamp>...) (?<Level>...) to parse proprietary log formats.",
                DemoScenario = "Select preset 'Apache Common Log' or enter custom regex:\n" +
                               "(?<Timestamp>\\S+) \\[(?<Level>\\w+)\\] (?<Message>.*)\n" +
                               "Paste a sample line into the tester to see parsed groups in real time.",
                StepByStep = "1. Click Tools > Configure Log Parser….\n" +
                             "2. Choose a preset or define custom regex groups.\n" +
                             "3. Test against sample line and click OK.",
                UnderTheHood = "• LogParserService extracts named groups to populate StructuredLogEvent fields."
            },

            // ── 11. Docking, Workspaces & CLI ────────────────────────────────────────
            new()
            {
                Id = "docking-system",
                Category = "11. Docking, Workspaces & Command Line Interface",
                Title = "11.1 AvalonDock Layout & Docking",
                ShortTitle = "Docking & Layouts",
                Shortcut = "None",
                UiLocation = "Drag panel headers to dock, float, or pin",
                Overview = "Full Visual Studio / JetBrains style docking system for all panels and document tabs.",
                DemoScenario = "Grab the 'Search Results' panel tab and drag it to the right screen edge.\n" +
                               "It docks vertically beside the log viewer.\n" +
                               "Drag another tab out to float on a secondary monitor.",
                StepByStep = "1. Click and drag any panel header.\n" +
                             "2. Drop onto docking guide diamonds (Left, Right, Top, Bottom, Centre).",
                UnderTheHood = "• Managed via AvalonDock DockingManager with serialized layout XML."
            },
            new()
            {
                Id = "split-view",
                Category = "11. Docking, Workspaces & Command Line Interface",
                Title = "11.2 Split View Manager",
                ShortTitle = "Split View Manager",
                Shortcut = "None",
                UiLocation = "Toolbar: ◫ Split View… | View > Split View…",
                Overview = "Splits the workspace to view multiple log tabs side-by-side or stacked vertically.",
                DemoScenario = "Click '◫ Split View…'. Select 'orders-service.log' to place next to 'auth.log'.\n" +
                               "Select 'Side by side (left | right)' and click Split.\n" +
                               "Both logs render simultaneously with synchronized follow-tail.",
                StepByStep = "1. Click '◫ Split View…' on the toolbar.\n" +
                             "2. Check the logs to display and choose Side by side or Stacked.\n" +
                             "3. Click Split.",
                UnderTheHood = "• Repositions LayoutDocument items into split LayoutDocumentPanes."
            },
            new()
            {
                Id = "reset-layout",
                Category = "11. Docking, Workspaces & Command Line Interface",
                Title = "11.3 Reset Layout",
                ShortTitle = "Reset Layout",
                Shortcut = "None",
                UiLocation = "Toolbar: ⟲ Reset Layout | View > Reset Layout",
                Overview = "Restores all dockable panels and document panes to their clean factory default positions.",
                DemoScenario = "Panels were accidentally closed or dragged off-screen.\n" +
                               "Click '⟲ Reset Layout'.\n" +
                               "All default panels (Process Explorer, Search, Details, Chart) snap back to standard layout.",
                StepByStep = "1. Click '⟲ Reset Layout' on the toolbar or View > Reset Layout.",
                UnderTheHood = "• Re-injects missing LayoutAnchorable panels and resets pane dimensions."
            },
            new()
            {
                Id = "workspaces",
                Category = "11. Docking, Workspaces & Command Line Interface",
                Title = "11.4 Save & Load Workspace (.ltpw)",
                ShortTitle = "Saved Workspaces (.ltpw)",
                Shortcut = "None",
                UiLocation = "File > Save Workspace… | File > Load Workspace…",
                Overview = "Saves your entire multi-tab investigation state (files, timelines, correlations, searches, layout) into a .ltpw file.",
                DemoScenario = "You have 5 files, 2 timelines, 1 correlation tab, and custom filters configured.\n" +
                               "Click File > Save Workspace… > 'Incident_4891.ltpw'.\n" +
                               "Reopening this workspace on any machine restores every tab and filter exactly.",
                StepByStep = "1. Click File > Save Workspace… to save session state.\n" +
                             "2. Click File > Load Workspace… to restore any previous investigation.",
                UnderTheHood = "• WorkspaceService saves atomic v2 JSON schemas with safe file replacement."
            },
            new()
            {
                Id = "cli-automation",
                Category = "11. Docking, Workspaces & Command Line Interface",
                Title = "11.6 Command Line Arguments & Multi-Window Tiling",
                ShortTitle = "Command Line Arguments",
                Shortcut = "None",
                UiLocation = "Command line: LogTailPro.exe [switches] [files]",
                Overview = "Launch LogTailPro with automated window sizing, monitor tiling, and file opening switches.",
                DemoScenario = "Tile 2 windows side-by-side from PowerShell:\n" +
                               "Start-Process LogTailPro.exe '-tc 2 -ti 0 C:\\Logs\\app1.log'\n" +
                               "Start-Process LogTailPro.exe '-tc 2 -ti 1 C:\\Logs\\app2.log'\n" +
                               "Both windows launch cleanly tiled across your screen.",
                StepByStep = "Supported CLI switches:\n" +
                             "• -wp <left> <top> <width> <height> : Set window bounds\n" +
                             "• -ws <0=Normal, 1=Min, 2=Max> : Set window state\n" +
                             "• -tc <count> -ti <index> : Automatically tile windows across screen\n" +
                             "• <file1> <file2> ... : Files to open",
                UnderTheHood = "• SingleInstanceService forwards paths to active window unless placement flags opt out."
            },

            // ── 12. Master Keyboard Shortcuts Reference ─────────────────────────────
            new()
            {
                Id = "shortcuts",
                Category = "12. Master Keyboard Shortcuts Reference",
                Title = "12.1 Master Keyboard Shortcuts Reference",
                ShortTitle = "Keyboard Shortcuts",
                Shortcut = "F1",
                UiLocation = "Help > Keyboard Shortcuts Reference | Help > Help Guide",
                Overview = "Complete reference of all universal keyboard shortcuts across LogTailPro.",
                DemoScenario = "Press Ctrl+O to open a file, F to toggle follow tail, Ctrl+F to search, F3 to jump matches, Ctrl+B to bookmark, F2 to jump bookmarks, and Ctrl+Shift+P to open Process Explorer.",
                StepByStep = "• Ctrl+O: Open File\n" +
                             "• Ctrl+W: Close Tab\n" +
                             "• Ctrl+Shift+V: Open from Clipboard\n" +
                             "• Ctrl+F: Open Search\n" +
                             "• F3 / Shift+F3: Next / Previous Search Match\n" +
                             "• Esc: Close Search / Clear Highlights\n" +
                             "• F: Toggle Follow Tail\n" +
                             "• Space: Pause / Resume Stream\n" +
                             "• Ctrl+G: Go To Line\n" +
                             "• Ctrl+B: Toggle Bookmark\n" +
                             "• F2 / Shift+F2: Next / Previous Bookmark\n" +
                             "• Ctrl+P: Pretty-Print Selected Line\n" +
                             "• Ctrl+C: Copy Selected Lines (includes hidden exception frames)\n" +
                             "• Ctrl+Shift+C: Copy with Line Numbers\n" +
                             "• Ctrl+A: Select All Visible Lines\n" +
                             "• Ctrl+Shift+P: Toggle Process Explorer\n" +
                             "• Ctrl+Shift+T: Toggle Error Timeline Chart\n" +
                             "• Ctrl+Shift+S: Toggle Stats Panel\n" +
                             "• Ctrl+Shift+E: Toggle Elapsed Column\n" +
                             "• Ctrl+Tab / Ctrl+Shift+Tab: Next / Previous Tab\n" +
                             "• Ctrl+1 ... Ctrl+9: Switch to Tab N\n" +
                             "• Ctrl+Home / Ctrl+End: Scroll to Top / End\n" +
                             "• F1: Open Help Guide",
                UnderTheHood = "• Routed via Window.InputBindings in MainWindow.xaml directly to ViewModel commands."
            }
        };
    }
}
