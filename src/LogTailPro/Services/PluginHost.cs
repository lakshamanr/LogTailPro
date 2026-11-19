using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using LogTailPro.Models;
using LogTailPro.PluginContract;
using LogTailPro.ViewModels;

namespace LogTailPro.Services;

public sealed class PluginHost : IPluginHost
{
    private readonly MainViewModel _mainViewModel;

    public string LogTailProVersion { get; }

    public event EventHandler<ILogContext?>? ActiveContextChanged;
    public event EventHandler<LogLinesAppendedEventArgs>? LinesAppended;

    public PluginHost(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        
        var asm = Assembly.GetExecutingAssembly();
        var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        LogTailProVersion = !string.IsNullOrWhiteSpace(infoVer) 
            ? infoVer.Split('+')[0] 
            : asm.GetName().Version?.ToString(3) ?? "2.10.0";

        _mainViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedTab))
            {
                RaiseActiveContextChanged();
            }
        };
    }

    public void RaiseActiveContextChanged()
    {
        var ctx = GetActiveContext();
        if (ActiveContextChanged == null) return;
        foreach (EventHandler<ILogContext?> handler in ActiveContextChanged.GetInvocationList())
        {
            try
            {
                handler(this, ctx);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginHost] ActiveContextChanged handler error: {ex.Message}");
            }
        }
    }

    public void RaiseLinesAppended(string filePath, System.Collections.Generic.IReadOnlyList<string> newLines, int startingLineNumber)
    {
        if (LinesAppended == null) return;
        var args = new LogLinesAppendedEventArgs(filePath, newLines, startingLineNumber);
        foreach (EventHandler<LogLinesAppendedEventArgs> handler in LinesAppended.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginHost] LinesAppended handler error: {ex.Message}");
            }
        }
    }

    public ILogContext? GetActiveContext()
    {
        return new PluginLogContext(_mainViewModel.SelectedTab);
    }

    public System.Collections.Generic.IReadOnlyList<ILogContext> GetAllOpenContexts()
    {
        try
        {
            return _mainViewModel.Tabs
                .Select(t => (ILogContext)new PluginLogContext(t))
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PluginHost] GetAllOpenContexts error: {ex.Message}");
            return GetActiveContext() is { } ctx ? new[] { ctx } : Array.Empty<ILogContext>();
        }
    }

    public void SwitchToTab(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        var app = Application.Current;
        if (app?.Dispatcher == null || app.Dispatcher.HasShutdownStarted) return;

        Action action = () =>
        {
            try
            {
                var target = _mainViewModel.Tabs.FirstOrDefault(t =>
                    string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.TempFilePath, filePath, StringComparison.OrdinalIgnoreCase));

                if (target != null)
                {
                    _mainViewModel.SelectedTab = target;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginHost] SwitchToTab error: {ex.Message}");
            }
        };

        if (app.Dispatcher.CheckAccess()) action();
        else app.Dispatcher.InvokeAsync(action);
    }

    public void OpenLogFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        var app = Application.Current;
        if (app?.Dispatcher == null || app.Dispatcher.HasShutdownStarted) return;

        app.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await _mainViewModel.OpenFileTabAsync(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginHost] OpenLogFile error: {ex.Message}");
            }
        });
    }

    public void NavigateToLine(int lineNumber)
    {
        NavigateToLine(lineNumber, null);
    }

    public void NavigateToLine(int lineNumber, string? filePath = null)
    {
        var app = Application.Current;
        if (app?.Dispatcher == null || app.Dispatcher.HasShutdownStarted) return;

        Action action = () =>
        {
            try
            {
                LogTabViewModel? tab = null;
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    tab = _mainViewModel.Tabs.FirstOrDefault(t => 
                        string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
                    if (tab != null)
                    {
                        _mainViewModel.SelectedTab = tab;
                    }
                }

                tab ??= _mainViewModel.SelectedTab;
                if (tab == null) return;

                LogLine? match = null;

                // 1. O(1) direct index lookup if sequential
                if (tab.AllLines.Count > 0)
                {
                    long firstNo = tab.AllLines[0].LineNumber;
                    int idx = (int)(lineNumber - firstNo);
                    if (idx >= 0 && idx < tab.AllLines.Count && tab.AllLines[idx].LineNumber == lineNumber)
                    {
                        match = tab.AllLines[idx];
                    }
                }

                // 2. O(log N) binary search fallback
                if (match == null && tab.AllLines.Count > 0)
                {
                    int low = 0, high = tab.AllLines.Count - 1;
                    while (low <= high)
                    {
                        int mid = low + (high - low) / 2;
                        long midNum = tab.AllLines[mid].LineNumber;
                        if (midNum == lineNumber) { match = tab.AllLines[mid]; break; }
                        if (midNum < lineNumber) low = mid + 1;
                        else high = mid - 1;
                    }
                }

                // 3. Fallback to VisibleLines
                match ??= tab.VisibleLines.FirstOrDefault(l => l.LineNumber == lineNumber);

                if (match != null)
                {
                    tab.RequestScrollToLine(match);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginHost] NavigateToLine error: {ex.Message}");
            }
        };

        if (app.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            app.Dispatcher.InvokeAsync(action);
        }
    }

    public void SetStatusMessage(string message)
    {
        SetStatusMessage(message, 5);
    }

    public void SetStatusMessage(string message, int durationSeconds)
    {
        var app = Application.Current;
        if (app?.Dispatcher == null || app.Dispatcher.HasShutdownStarted) return;

        if (app.Dispatcher.CheckAccess())
        {
            _mainViewModel.StatusMessage = message;
        }
        else
        {
            app.Dispatcher.InvokeAsync(() =>
            {
                _mainViewModel.StatusMessage = message;
            });
        }
    }

    public void RegisterPanel(string panelId, string title, Func<FrameworkElement> viewFactory)
    {
        // Panels can be integrated into AvalonDock layout or docked tool windows
    }

    public void RegisterMenuItem(string pluginId, PluginMenuItem menuItem)
    {
        _mainViewModel.PluginLoaderService?.RegisterDynamicMenuItem(pluginId, menuItem);
    }

    public void RegisterMenuItem(PluginHostSession session, PluginMenuItem menuItem)
    {
        _mainViewModel.PluginLoaderService?.RegisterDynamicMenuItem(session, menuItem);
    }
}
