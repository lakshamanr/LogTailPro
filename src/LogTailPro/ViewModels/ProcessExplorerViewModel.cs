using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;
using LogTailPro.Services;

namespace LogTailPro.ViewModels;

/// <summary>Root ViewModel for the Process Explorer side panel.</summary>
public sealed partial class ProcessExplorerViewModel : ObservableObject, IDisposable
{
    private readonly ProcessSnapshotService _snapshot = new();
    private readonly ServiceSnapshotService _serviceSnapshot = new();
    private readonly object _snapshotLock = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly AppSettings _settings;
    private readonly HashSet<int> _expandedPids = new();
    private bool _refreshing;
    private bool _refreshPending;
    private bool _disposed;

    /// <summary>Creates the panel ViewModel, restoring filter options from <paramref name="settings"/>.</summary>
    public ProcessExplorerViewModel(AppSettings settings)
    {
        _settings = settings;
        _showDotNetOnly = settings.ProcessExplorerDotNetOnly;
        _showSystemProcesses = settings.ProcessExplorerShowSystem;
        _isRegexSearch = settings.ProcessExplorerRegexSearch;
        _isServicesMode = settings.ProcessExplorerServicesMode;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();

        // Assign through the property so the timer starts when auto-refresh was persisted on.
        IsAutoRefresh = settings.ProcessExplorerAutoRefresh;
    }

    /// <summary>Root nodes of the process tree.</summary>
    [ObservableProperty] private ObservableCollection<ProcessItemViewModel> _processes = new();

    /// <summary>Currently selected node.</summary>
    [ObservableProperty] private ProcessItemViewModel? _selectedProcess;

    /// <summary>
    /// Search pattern: plain text, wildcards (<c>*</c>, <c>?</c>, comma-separated alternatives) or,
    /// when <see cref="IsRegexSearch"/> is on, a regular expression. Matches name, PID, path and user.
    /// </summary>
    [ObservableProperty] private string _searchText = string.Empty;

    /// <summary>Treat <see cref="SearchText"/> as a regular expression.</summary>
    [ObservableProperty] private bool _isRegexSearch;

    /// <summary>Regex syntax error for the current pattern (empty when valid).</summary>
    [ObservableProperty] private string _searchError = string.Empty;

    /// <summary>Number of processes currently matching the search (drives the "Logs (N)" button).</summary>
    [ObservableProperty] private int _matchCount;

    /// <summary>Whether the list refreshes every 2 seconds.</summary>
    [ObservableProperty] private bool _isAutoRefresh;

    /// <summary>Whether system/service processes are included.</summary>
    [ObservableProperty] private bool _showSystemProcesses;

    /// <summary>Whether only .NET processes are listed.</summary>
    [ObservableProperty] private bool _showDotNetOnly;

    /// <summary>Status line shown at the bottom of the panel.</summary>
    [ObservableProperty] private string _statusText = "Click Refresh to load processes";

    /// <summary>True when the Windows Services list is shown instead of the process tree.</summary>
    [ObservableProperty] private bool _isServicesMode;

    /// <summary>Rows of the services list.</summary>
    [ObservableProperty] private ObservableCollection<ServiceItemViewModel> _services = new();

    /// <summary>Currently selected service row.</summary>
    [ObservableProperty] private ServiceItemViewModel? _selectedService;

    /// <summary>Services matching the current filter, for "open logs for all matches".</summary>
    private IReadOnlyList<ServiceItemViewModel> _serviceMatches = Array.Empty<ServiceItemViewModel>();

    /// <summary>Inverse of <see cref="IsServicesMode"/>; lets the "Processes" radio button bind two-way.</summary>
    public bool IsProcessesMode
    {
        get => !IsServicesMode;
        set => IsServicesMode = !value;
    }

    partial void OnIsServicesModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsProcessesMode));
        _settings.ProcessExplorerServicesMode = value;
        if (value) SelectedProcess = null;   // hides the process details strip
        _ = RefreshAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(LogsButtonText));
        OpenLogsForMatchesCommand.NotifyCanExecuteChanged();
        _ = RefreshAsync();
    }

    partial void OnIsRegexSearchChanged(bool value)
    {
        _settings.ProcessExplorerRegexSearch = value;
        _ = RefreshAsync();
    }

    partial void OnMatchCountChanged(int value)
    {
        OnPropertyChanged(nameof(LogsButtonText));
        OpenLogsForMatchesCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchErrorChanged(string value) => OnPropertyChanged(nameof(HasSearchError));

    /// <summary>True when the regex pattern is invalid.</summary>
    public bool HasSearchError => SearchError.Length > 0;

    /// <summary>Caption of the "open logs for all matches" button.</summary>
    public string LogsButtonText => SearchText.Trim().Length > 0 ? $"📄 Logs ({MatchCount})" : "📄 Logs";

    partial void OnShowSystemProcessesChanged(bool value)
    {
        _settings.ProcessExplorerShowSystem = value;
        _ = RefreshAsync();
    }

    partial void OnShowDotNetOnlyChanged(bool value)
    {
        _settings.ProcessExplorerDotNetOnly = value;
        _ = RefreshAsync();
    }

    partial void OnIsAutoRefreshChanged(bool value)
    {
        _settings.ProcessExplorerAutoRefresh = value;
        if (value) _refreshTimer.Start();
        else       _refreshTimer.Stop();
    }

    /// <summary>Raised when the user picks a process + capture type. Handled by MainViewModel.</summary>
    public event EventHandler<(ProcessInfo Info, ProcessAttachmentType Type)>? AttachRequested;

    /// <summary>Raised when the user clicks Launch &amp; Attach. Handled by MainViewModel.</summary>
    public event EventHandler? LaunchAndAttachRequested;

    /// <summary>Raised when the user wants the log files of one or more processes. Handled by MainViewModel.</summary>
    public event EventHandler<IReadOnlyList<ProcessInfo>>? OpenLogsRequested;

    /// <summary>Maximum number of processes whose logs are discovered in one go.</summary>
    private const int MaxLogProcesses = 25;

    /// <summary>Processes matching the current filter (flat), for "open logs for all matches".</summary>
    private IReadOnlyList<ProcessInfo> _matches = Array.Empty<ProcessInfo>();

    /// <summary>Last successfully compiled search; kept while the user is mid-way through typing a regex.</summary>
    private ProcessSearchMatcher _matcher = ProcessSearchMatcher.MatchAll;

    /// <summary>Re-reads the process list. Overlapping calls are coalesced into one follow-up refresh.</summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (_disposed) return;
        if (_refreshing) { _refreshPending = true; return; }
        _refreshing = true;

        try
        {
            do
            {
                _refreshPending = false;
                await RefreshCoreAsync();
            }
            while (_refreshPending && !_disposed);
        }
        catch (Exception ex)
        {
            StatusText = $"Refresh failed: {ex.Message}";
        }
        finally { _refreshing = false; }
    }

    private async Task RefreshCoreAsync()
    {
        // Capture UI-bound values on the UI thread before going to the pool.
        bool showSystem = ShowSystemProcesses;
        bool dotNetOnly = ShowDotNetOnly;
        string search = SearchText.Trim();
        int? selectedPid = SelectedProcess?.Pid;

        // Compile the pattern on the UI thread; an invalid regex keeps the previous good matcher.
        ProcessSearchMatcher? compiled = ProcessSearchMatcher.Create(search, IsRegexSearch, out string? error);
        if (compiled != null) _matcher = compiled;
        SearchError = error ?? string.Empty;
        ProcessSearchMatcher matcher = _matcher;

        if (IsServicesMode)
        {
            await RefreshServicesAsync(matcher, dotNetOnly);
            return;
        }

        (IReadOnlyList<ProcessInfo> roots, IReadOnlyList<ProcessInfo> matches, int total) = await Task.Run(() =>
        {
            lock (_snapshotLock)
            {
                IReadOnlyList<ProcessInfo> flat = _snapshot.GetAll();
                IEnumerable<ProcessInfo> filtered = flat;
                if (!showSystem)  filtered = filtered.Where(p => !p.IsSystemProcess);
                if (dotNetOnly)   filtered = filtered.Where(p => p.IsDotNet);
                List<ProcessInfo> kept = filtered.Where(matcher.IsMatch).ToList();
                return (_snapshot.BuildTree(kept), (IReadOnlyList<ProcessInfo>)kept, flat.Count);
            }
        });

        _matches = matches;
        MatchCount = matches.Count;

        bool expandAll = search.Length > 0;
        ObservableCollection<ProcessItemViewModel> newRoots = new();
        ProcessItemViewModel? reselect = null;
        foreach (ProcessInfo root in roots)
            newRoots.Add(BuildItemVm(root, expandAll, selectedPid, ref reselect));

        Processes = newRoots;
        if (reselect != null) reselect.IsSelected = true;
        else SelectedProcess = null;

        StatusText = $"{total} processes  •  {DateTime.Now:HH:mm:ss}";
    }

    /// <summary>Re-reads the Windows services list and applies the current filter.</summary>
    private async Task RefreshServicesAsync(ProcessSearchMatcher matcher, bool dotNetOnly)
    {
        string? selectedName = SelectedService?.Name;

        // Each service is joined with a live snapshot of its hosting process (CPU, memory, .NET, user, path),
        // so the .NET filter and the details strip work the same as in the process tree.
        (List<(ServiceInfo Service, ProcessInfo? Host)> kept, int total) = await Task.Run(() =>
        {
            lock (_snapshotLock)
            {
                IReadOnlyList<ServiceInfo> all = _serviceSnapshot.GetAll();
                Dictionary<int, ProcessInfo> hosts = _snapshot.GetAll().ToDictionary(p => p.Pid);

                List<(ServiceInfo, ProcessInfo?)> rows = all
                    .Where(matcher.IsMatch)
                    .Select(s => (s, s.IsRunning && hosts.TryGetValue(s.Pid, out ProcessInfo? h) ? h : null))
                    .Where(r => !dotNetOnly || r.Item2?.IsDotNet == true)
                    .ToList();
                return (rows, all.Count);
            }
        });

        ObservableCollection<ServiceItemViewModel> rowVms = new();
        ServiceItemViewModel? reselect = null;
        foreach ((ServiceInfo s, ProcessInfo? host) in kept)
        {
            ServiceItemViewModel row = new(s, host, OnOpenLogs, OnAttach, OnServiceControl, OnLaunch, OnKill);
            row.PropertyChanged += OnServiceRowPropertyChanged;
            if (s.Name == selectedName) reselect = row;
            rowVms.Add(row);
        }

        _serviceMatches = rowVms.ToList();
        MatchCount = rowVms.Count;

        Services = rowVms;
        if (reselect != null) reselect.IsSelected = true;
        else SelectedService = null;

        int running = kept.Count(r => r.Service.IsRunning);
        StatusText = $"{kept.Count} of {total} services  •  {running} running  •  {DateTime.Now:HH:mm:ss}";
    }

    private void OnServiceRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ServiceItemViewModel row && e.PropertyName == nameof(ServiceItemViewModel.IsSelected) && row.IsSelected)
            SelectedService = row;
    }

    private async void OnServiceControl(ServiceInfo service, ServiceAction action)
    {
        StatusText = $"{action} {service.Name}…";
        try
        {
            await Task.Run(() =>
            {
                lock (_snapshotLock)
                {
                    switch (action)
                    {
                        case ServiceAction.Start:   _serviceSnapshot.Start(service.Name);   break;
                        case ServiceAction.Stop:    _serviceSnapshot.Stop(service.Name);    break;
                        default:                    _serviceSnapshot.Restart(service.Name); break;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, $"{action} Service", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        await RefreshAsync();
    }

    private ProcessItemViewModel BuildItemVm(ProcessInfo info, bool expandAll, int? selectedPid,
                                             ref ProcessItemViewModel? reselect)
    {
        ProcessItemViewModel vm = new(info, OnAttach, OnLaunch, OnKill, OnOpenLogs)
        {
            IsExpanded = expandAll || _expandedPids.Contains(info.Pid)
        };

        // Remember the user's expand/collapse choices across refreshes.
        vm.PropertyChanged += OnItemPropertyChanged;

        if (selectedPid == info.Pid) reselect = vm;

        foreach (ProcessInfo child in info.Children)
            vm.Children.Add(BuildItemVm(child, expandAll, selectedPid, ref reselect));
        return vm;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ProcessItemViewModel item) return;

        if (e.PropertyName == nameof(ProcessItemViewModel.IsExpanded) && SearchText.Length == 0)
        {
            if (item.IsExpanded) _expandedPids.Add(item.Pid);
            else _expandedPids.Remove(item.Pid);
        }
        else if (e.PropertyName == nameof(ProcessItemViewModel.IsSelected) && item.IsSelected)
        {
            SelectedProcess = item;
        }
    }

    private void OnAttach(ProcessInfo info, ProcessAttachmentType type) =>
        AttachRequested?.Invoke(this, (info, type));

    private void OnLaunch() => LaunchAndAttachRequested?.Invoke(this, EventArgs.Empty);

    private void OnOpenLogs(ProcessInfo info) =>
        OpenLogsRequested?.Invoke(this, new[] { info });

    /// <summary>Finds and opens the logs of every process matching the current search (up to 25).</summary>
    [RelayCommand(CanExecute = nameof(CanOpenLogsForMatches))]
    private void OpenLogsForMatches()
    {
        if (!IsServicesMode)
        {
            OpenLogsRequested?.Invoke(this, _matches.Take(MaxLogProcesses).ToList());
            return;
        }

        // Services (running or stopped): several services can share one host (svchost), so a running
        // host is searched once; stopped services are searched by their own executable.
        List<ProcessInfo> targets = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (ServiceItemViewModel s in _serviceMatches)
        {
            string key = s.Target.Pid > 0 ? $"pid:{s.Target.Pid}" : $"svc:{s.Name}";
            if (!seen.Add(key)) continue;
            targets.Add(s.Target);
            if (targets.Count >= MaxLogProcesses) break;
        }
        if (targets.Count > 0) OpenLogsRequested?.Invoke(this, targets);
    }

    private bool CanOpenLogsForMatches() => SearchText.Trim().Length > 0 && MatchCount > 0;

    private void OnKill(ProcessInfo info)
    {
        try
        {
            using Process p = Process.GetProcessById(info.Pid);
            p.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
            // Already gone — just refresh.
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not kill {info.DisplayName}:\n{ex.Message}", "Kill Process",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        _ = RefreshAsync();
    }

    [RelayCommand]
    private void LaunchAndAttach() => OnLaunch();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer.Stop();
    }
}
