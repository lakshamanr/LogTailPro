using System.Collections.Generic;

namespace LogTailPro.PluginContract;

/// <summary>
/// Provides a snapshot of the active log tab and editor state at the moment a command is executed.
/// </summary>
public interface ILogContext
{
    /// <summary>
    /// Absolute path of the active log file, or null if clipboard/transient.
    /// </summary>
    string? FilePath { get; }

    /// <summary>
    /// Title of the currently active log tab.
    /// </summary>
    string TabTitle { get; }

    /// <summary>
    /// Currently focused or selected line number (1-based), or -1 if none.
    /// </summary>
    int CurrentLineNumber { get; }

    /// <summary>
    /// The text of the currently selected line or highlighted span.
    /// </summary>
    string? SelectedText { get; }

    /// <summary>
    /// All lines currently selected by the user.
    /// </summary>
    IReadOnlyList<string> SelectedLines { get; }

    /// <summary>
    /// Active search or filter expression in the search bar, if any.
    /// </summary>
    string? CurrentFilterExpression { get; }

    /// <summary>
    /// Whether the active tab is parsed in structured JSON mode.
    /// </summary>
    bool IsStructuredJson { get; }

    /// <summary>
    /// Total count of loaded lines in the current tab.
    /// </summary>
    int TotalLineCount { get; }
}
