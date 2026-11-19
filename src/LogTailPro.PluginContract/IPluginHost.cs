using System;
using System.Collections.Generic;
using System.Windows;

namespace LogTailPro.PluginContract;

/// <summary>
/// Host services exposed by LogTailPro to plugins.
/// </summary>
public interface IPluginHost
{
    /// <summary>
    /// The current LogTailPro application version.
    /// </summary>
    string LogTailProVersion { get; }

    /// <summary>
    /// Gets information about the currently active log tab.
    /// </summary>
    ILogContext? GetActiveContext();

    /// <summary>
    /// Opens a log file in a new tab within LogTailPro.
    /// </summary>
    void OpenLogFile(string filePath);

    /// <summary>
    /// Navigates to a specific line in the active file.
    /// </summary>
    void NavigateToLine(int lineNumber) => NavigateToLine(lineNumber, null);

    /// <summary>
    /// Navigates to a specific line in the active or specified file.
    /// </summary>
    void NavigateToLine(int lineNumber, string? filePath = null);

    /// <summary>
    /// Shows a status message in LogTailPro's status bar.
    /// </summary>
    void SetStatusMessage(string message) => SetStatusMessage(message, 5);

    /// <summary>
    /// Shows a status message in LogTailPro's status bar.
    /// </summary>
    void SetStatusMessage(string message, int durationSeconds);

    /// <summary>
    /// Registers a custom dockable AvalonDock panel or tool window.
    /// </summary>
    void RegisterPanel(string panelId, string title, Func<FrameworkElement> viewFactory);

    /// <summary>
    /// Registers a custom menu item dynamically into the Plugins menu.
    /// </summary>
    void RegisterMenuItem(string pluginId, PluginMenuItem menuItem);

    /// <summary>
    /// Event raised when the user switches tabs or changes the active file.
    /// </summary>
    event EventHandler<ILogContext?> ActiveContextChanged;

    /// <summary>
    /// Event raised when new lines are appended in real-time follow-tail mode.
    /// </summary>
    event EventHandler<LogLinesAppendedEventArgs> LinesAppended;
}

public class LogLinesAppendedEventArgs : EventArgs
{
    public string FilePath { get; }
    public IReadOnlyList<string> NewLines { get; }
    public int StartingLineNumber { get; }

    public LogLinesAppendedEventArgs(string filePath, IReadOnlyList<string> newLines, int startingLineNumber)
    {
        FilePath = filePath;
        NewLines = newLines;
        StartingLineNumber = startingLineNumber;
    }
}
