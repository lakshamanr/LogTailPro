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
        ActiveContextChanged?.Invoke(this, ctx);
    }

    public void RaiseLinesAppended(string filePath, System.Collections.Generic.IReadOnlyList<string> newLines, int startingLineNumber)
    {
        LinesAppended?.Invoke(this, new LogLinesAppendedEventArgs(filePath, newLines, startingLineNumber));
    }

    public ILogContext? GetActiveContext()
    {
        return new PluginLogContext(_mainViewModel.SelectedTab);
    }

    public void OpenLogFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        Application.Current?.Dispatcher?.InvokeAsync(async () =>
        {
            await _mainViewModel.OpenFileTabAsync(filePath);
        });
    }

    public void NavigateToLine(int lineNumber)
    {
        NavigateToLine(lineNumber, null);
    }

    public void NavigateToLine(int lineNumber, string? filePath = null)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
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

            var match = tab.VisibleLines.FirstOrDefault(l => l.LineNumber == lineNumber)
                        ?? tab.AllLines.FirstOrDefault(l => l.LineNumber == lineNumber);
            if (match != null)
            {
                tab.RequestScrollToLine(match);
            }
        });
    }

    public void SetStatusMessage(string message)
    {
        SetStatusMessage(message, 5);
    }

    public void SetStatusMessage(string message, int durationSeconds)
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == true)
        {
            _mainViewModel.StatusMessage = message;
        }
        else
        {
            Application.Current?.Dispatcher?.InvokeAsync(() =>
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
}
