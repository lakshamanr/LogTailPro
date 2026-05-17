using System.Collections.ObjectModel;

namespace LogTailPro.Models;

/// <summary>Persisted application settings stored in %AppData%\LogTailPro\settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>"Dark" or "Light".</summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>Maximum number of log lines kept in memory per tab.</summary>
    public int MaxLinesInMemory { get; set; } = 100_000;

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
}
