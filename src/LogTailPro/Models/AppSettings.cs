using System.Collections.ObjectModel;

namespace LogTailPro.Models;

/// <summary>Persisted application settings stored in %AppData%\LogTailPro\settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>"Dark" or "Light".</summary>
    public string Theme { get; set; } = "Light"; // legacy; single soft-light theme is always used

    /// <summary>Maximum number of log lines kept in memory per tab. 0 (default) = unlimited.</summary>
    public int MaxLinesInMemory { get; set; } = 0;

    /// <summary>
    /// Largest part of a file (in MB, counted from the end) that is loaded when it is opened.
    /// Bigger files show their last N MB with a notice. 0 = always load the whole file.
    /// </summary>
    public int MaxLoadMegabytes { get; set; } = 1024;

    /// <summary>Whether new tabs should start in follow-tail mode.</summary>
    public bool FollowTailOnOpen { get; set; } = true;

    /// <summary>Monospace font used in the log viewer.</summary>
    public string FontFamily { get; set; } = "Consolas";

    /// <summary>Font size in points used in the log viewer.</summary>
    public double FontSize { get; set; } = 13.0;

    /// <summary>All user-defined highlight rules, ordered by <see cref="HighlightRule.Priority"/>.</summary>
    public List<HighlightRule> HighlightRules { get; set; } = new();

    /// <summary>Most-recently-opened files (newest first).</summary>
    public List<string> RecentFiles { get; set; } = new();

    /// <summary>Saved filter profiles.</summary>
    public List<FilterProfile> FilterProfiles { get; set; } = new();

    /// <summary>Main window width in device-independent pixels.</summary>
    public double WindowWidth { get; set; } = 1280;

    /// <summary>Main window height in device-independent pixels.</summary>
    public double WindowHeight { get; set; } = 800;

    /// <summary>Main window top position (NaN = use OS default).</summary>
    public double WindowTop { get; set; } = double.NaN;

    /// <summary>Main window left position (NaN = use OS default).</summary>
    public double WindowLeft { get; set; } = double.NaN;

    /// <summary>Whether the main window was maximized on last close.</summary>
    public bool WindowMaximized { get; set; }

    /// <summary>Number of spaces used to expand tab characters in log lines.</summary>
    public int TabWidth { get; set; } = 8;

    /// <summary>User-saved search patterns.</summary>
    public ObservableCollection<SavedSearchPattern> SavedPatterns { get; set; } = new();

    /// <summary>Where the tab strip is placed relative to the content area.</summary>
    public System.Windows.Controls.Dock TabStripPlacement { get; set; } = System.Windows.Controls.Dock.Top;

    // ── Phase 5 additions ─────────────────────────────────────────────────────

    /// <summary>Re-open the most-recently-used file on startup.</summary>
    public bool AutomaticallyOpenLastFileOnStartup { get; set; } = true;

    /// <summary>Polling interval (ms) used when watching files over a network share.</summary>
    public int NetworkPollingIntervalMs { get; set; } = 2000;

    /// <summary>Line spacing multiplier applied to the log viewer (0.8–2.0).</summary>
    public double LineSpacingMultiplier { get; set; } = 1.0;

    /// <summary>Vertical pixel offset applied to characters in the log viewer font (-5..5).</summary>
    public int FontCharacterOffsetPx { get; set; } = 0;

    /// <summary>Where preferences are persisted.</summary>
    public PreferenceStorage PreferenceStorage { get; set; } = PreferenceStorage.File;

    /// <summary>Active log parser preset name (empty = none).</summary>
    public string LogParserPreset { get; set; } = string.Empty;

    /// <summary>Saved bookmarks per log file (key = upper-cased full path).</summary>
    public Dictionary<string, List<BookmarkEntry>> Bookmarks { get; set; } = new();

    // ── Phase 7: Process Explorer ─────────────────────────────────────────────

    /// <summary>Whether the Process Explorer panel is open.</summary>
    public bool IsProcessExplorerVisible { get; set; }

    /// <summary>Width of the Process Explorer panel in device-independent pixels.</summary>
    public double ProcessExplorerPanelWidth { get; set; } = 280.0;

    /// <summary>Whether the Process Explorer auto-refreshes every 2 seconds.</summary>
    public bool ProcessExplorerAutoRefresh { get; set; }

    /// <summary>Whether system processes (PID &lt;= 4, or service accounts) are shown.</summary>
    public bool ProcessExplorerShowSystem { get; set; }

    /// <summary>Whether only .NET processes are shown in the Process Explorer.</summary>
    public bool ProcessExplorerDotNetOnly { get; set; }

    /// <summary>Whether the Process Explorer search box treats its text as a regular expression.</summary>
    public bool ProcessExplorerRegexSearch { get; set; }

    /// <summary>Whether the Process Explorer shows the Windows Services list instead of the process tree.</summary>
    public bool ProcessExplorerServicesMode { get; set; }
}
