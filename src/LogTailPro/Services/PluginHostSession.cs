using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using LogTailPro.PluginContract;

namespace LogTailPro.Services;

/// <summary>
/// Scoped IPluginHost session wrapping the main PluginHost for an individual loaded plugin.
/// When the plugin is retired or fails initialization, Deactivate() is called to sever all
/// event subscriptions and ignore any subsequent calls from stale or timed-out plugin tasks.
/// </summary>
public sealed class PluginHostSession : IPluginHost, IDisposable
{
    private readonly IPluginHost _innerHost;
    private readonly string _pluginId;
    private readonly Action<string>? _logWarn;
    private volatile bool _isActive = true;

    private readonly List<EventHandler<ILogContext?>> _activeContextHandlers = new();
    private readonly List<EventHandler<LogLinesAppendedEventArgs>> _linesAppendedHandlers = new();
    private readonly List<PluginMenuItem> _pendingMenuItems = new();
    private readonly object _lock = new();

    public PluginHostSession(IPluginHost innerHost, string pluginId, Action<string>? logWarn = null)
    {
        _innerHost = innerHost ?? throw new ArgumentNullException(nameof(innerHost));
        _pluginId = pluginId ?? string.Empty;
        _logWarn = logWarn;

        _innerHost.ActiveContextChanged += OnInnerActiveContextChanged;
        _innerHost.LinesAppended += OnInnerLinesAppended;
    }

    public bool IsActive => _isActive;
    public string PluginId => _pluginId;

    public IReadOnlyList<PluginMenuItem> GetPendingMenuItems()
    {
        lock (_lock)
        {
            return _pendingMenuItems.ToList();
        }
    }

    public void Deactivate()
    {
        if (!_isActive) return;
        _isActive = false;

        _innerHost.ActiveContextChanged -= OnInnerActiveContextChanged;
        _innerHost.LinesAppended -= OnInnerLinesAppended;

        lock (_lock)
        {
            _activeContextHandlers.Clear();
            _linesAppendedHandlers.Clear();
            _pendingMenuItems.Clear();
        }
    }

    public void Dispose() => Deactivate();

    private void OnInnerActiveContextChanged(object? sender, ILogContext? e)
    {
        if (!_isActive) return;
        EventHandler<ILogContext?>[] handlers;
        lock (_lock)
        {
            handlers = _activeContextHandlers.ToArray();
        }
        foreach (var h in handlers)
        {
            try
            {
                h(sender, e);
            }
            catch (Exception ex)
            {
                _logWarn?.Invoke($"[{_pluginId}] ActiveContextChanged handler error: {ex.Message}");
            }
        }
    }

    private void OnInnerLinesAppended(object? sender, LogLinesAppendedEventArgs e)
    {
        if (!_isActive) return;
        EventHandler<LogLinesAppendedEventArgs>[] handlers;
        lock (_lock)
        {
            handlers = _linesAppendedHandlers.ToArray();
        }
        foreach (var h in handlers)
        {
            try
            {
                h(sender, e);
            }
            catch (Exception ex)
            {
                _logWarn?.Invoke($"[{_pluginId}] LinesAppended handler error: {ex.Message}");
            }
        }
    }

    public string LogTailProVersion => _innerHost.LogTailProVersion;

    public ILogContext? GetActiveContext() => _isActive ? _innerHost.GetActiveContext() : null;

    public IReadOnlyList<ILogContext> GetAllOpenContexts() =>
        _isActive ? _innerHost.GetAllOpenContexts() : Array.Empty<ILogContext>();

    public void SwitchToTab(string filePath)
    {
        if (!_isActive) return;
        _innerHost.SwitchToTab(filePath);
    }

    public void OpenLogFile(string filePath)
    {
        if (!_isActive) return;
        _innerHost.OpenLogFile(filePath);
    }

    public void NavigateToLine(int lineNumber)
    {
        if (!_isActive) return;
        _innerHost.NavigateToLine(lineNumber);
    }

    public void NavigateToLine(int lineNumber, string? filePath = null)
    {
        if (!_isActive) return;
        _innerHost.NavigateToLine(lineNumber, filePath);
    }

    public void SetStatusMessage(string message)
    {
        if (!_isActive) return;
        _innerHost.SetStatusMessage(message);
    }

    public void SetStatusMessage(string message, int durationSeconds)
    {
        if (!_isActive) return;
        _innerHost.SetStatusMessage(message, durationSeconds);
    }

    public void RegisterPanel(string panelId, string title, Func<FrameworkElement> viewFactory)
    {
        if (!_isActive) return;
        _innerHost.RegisterPanel(panelId, title, viewFactory);
    }

    public void RegisterMenuItem(string pluginId, PluginMenuItem menuItem)
    {
        if (!_isActive || menuItem == null) return;
        lock (_lock)
        {
            _pendingMenuItems.Add(menuItem);
        }

        if (_innerHost is PluginHost host)
        {
            host.RegisterMenuItem(this, menuItem);
        }
        else
        {
            _innerHost.RegisterMenuItem(_pluginId, menuItem);
        }
    }

    public event EventHandler<ILogContext?> ActiveContextChanged
    {
        add
        {
            if (!_isActive || value == null) return;
            lock (_lock) { _activeContextHandlers.Add(value); }
        }
        remove
        {
            if (value == null) return;
            lock (_lock) { _activeContextHandlers.Remove(value); }
        }
    }

    public event EventHandler<LogLinesAppendedEventArgs> LinesAppended
    {
        add
        {
            if (!_isActive || value == null) return;
            lock (_lock) { _linesAppendedHandlers.Add(value); }
        }
        remove
        {
            if (value == null) return;
            lock (_lock) { _linesAppendedHandlers.Remove(value); }
        }
    }
}
