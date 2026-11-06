using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;
using LogTailPro.Services;
using LogTailPro.Views;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace LogTailPro.ViewModels;

/// <summary>Root ViewModel for the application window.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly DirectoryMonitorService _directoryMonitor = new();
    private readonly WorkspaceService _workspaceService = new();

    /// <summary>Delegate invoked by MainViewModel to query the current AvalonDock layout XML.</summary>
    public Func<string>? RequestLayoutXml { get; set; }

    /// <summary>Delegate invoked by MainViewModel to restore an AvalonDock layout XML.</summary>
    public Action<string>? RestoreLayoutXml { get; set; }

    /// <summary>Loaded and persisted application settings.</summary>
    public AppSettings Settings { get; private set; }

    /// <summary>All currently open log file tabs.</summary>
    [ObservableProperty]
    private ObservableCollection<LogTabViewModel> _tabs = new();

    /// <summary>The tab currently displayed in the content area.</summary>
    [ObservableProperty]
    private LogTabViewModel? _selectedTab;

    /// <summary>Text shown in the status bar when no tab is selected.</summary>
    [ObservableProperty]
    private string _statusMessage = "Ready";

    /// <summary>Whether highlight rules are applied to log lines (toolbar checkbox).</summary>
    [ObservableProperty]
    private bool _isHighlightingEnabled = true;

    /// <summary>Process Explorer panel ViewModel.</summary>
    public ProcessExplorerViewModel ProcessExplorer { get; }

    /// <summary>Whether the Process Explorer left panel is visible.</summary>
    [ObservableProperty]
    private bool _isProcessExplorerVisible;

    public MainViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        Settings = settingsService.Load();

        _directoryMonitor.NewFileDetected += async (_, path) =>
            await Application.Current.Dispatcher.InvokeAsync(() => OpenFileTabAsync(path));

        _isProcessExplorerVisible = Settings.IsProcessExplorerVisible;
        ProcessExplorer = new ProcessExplorerViewModel(Settings);
        ProcessExplorer.AttachRequested += (_, e) => OpenProcessTab(e.Info, e.Type);
        ProcessExplorer.LaunchAndAttachRequested += (_, _) => OpenLaunchDialog();
        ProcessExplorer.OpenLogsRequested += async (_, processes) => await OpenLogsAsync(processes);
        _tabs.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (LogTabViewModel t in e.NewItems)
                {
                    t.CorrelationRequested += (_, id) => CorrelateId(id);
                }
            }
        };
    }

    private readonly ProcessLogDiscoveryService _logDiscovery = new();

    /// <summary>
    /// Discovers the log files of the given processes (open handles, config files, log folders),
    /// lets the user pick which to open, and opens each in its own tab.
    /// </summary>
    public async Task OpenLogsAsync(IReadOnlyList<ProcessInfo> processes)
    {
        if (processes.Count == 0) return;
        string label = processes.Count == 1 ? processes[0].DisplayName : $"{processes.Count} processes";
        StatusMessage = $"Looking for log files of {label}…";

        IReadOnlyList<LogCandidate> found;
        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
            found = await _logDiscovery.DiscoverManyAsync(processes, cts.Token);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Log search timed out.";
            return;
        }
        catch (Exception ex)
        {
            StatusMessage = "Log search failed.";
            MessageBox.Show($"Could not search for log files:\n{ex.Message}", "Open Log Files",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (found.Count == 0)
        {
            StatusMessage = $"No log files found for {label}.";
            bool noPath = processes.Any(p => string.IsNullOrEmpty(p.ExecutablePath));
            MessageBox.Show(
                $"No log files were found for {label}.\n\n" +
                "Searched: files the process has open, config files next to its executable " +
                "(NLog, log4net, Serilog, appsettings, ini, yaml …) and its usual log folders.\n\n" +
                (noPath ? "The executable path of some processes is not accessible — try running LogTailPro as administrator.\n\n" : "") +
                "If the process logs elsewhere, use File ▸ Open to open the file directly.",
                "Open Log Files", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        StatusMessage = $"{found.Count} log file(s) found for {label}.";
        LogDiscoveryViewModel picker = new(found, $"Log files for {label}");
        LogDiscoveryDialog dialog = new(picker) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;

        foreach (string path in picker.SelectedPaths)
            await OpenFileTabAsync(path);
    }

    partial void OnIsProcessExplorerVisibleChanged(bool value)
    {
        Settings.IsProcessExplorerVisible = value;
        _settingsService.Save(Settings);
        if (value) _ = ProcessExplorer.RefreshAsync();
    }

    partial void OnIsHighlightingEnabledChanged(bool value)
    {
        foreach (LogTabViewModel tab in Tabs)
            tab.IsHighlightingEnabled = value;
    }

    partial void OnSelectedTabChanged(LogTabViewModel? value)
    {
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Title = value != null
                ? $"{value.FileName} — LogTailPro"
                : "LogTailPro";
        }
    }

    // ── File commands ─────────────────────────────────────────────────────────

    /// <summary>Shows the Open File dialog and opens each selected file in a new tab.</summary>
    [RelayCommand]
    private async Task OpenFileAsync()
    {
        OpenFileDialog dialog = new()
        {
            Filter = "Log Files|*.log;*.txt;*.out;*.err;*.trace|All Files|*.*",
            Multiselect = true,
            Title = "Open Log File"
        };

        if (dialog.ShowDialog() != true) return;
        foreach (string path in dialog.FileNames)
            await OpenFileTabAsync(path);
    }

    /// <summary>Opens a file at the given path in a new tab (also used by drag-drop and CLI args).</summary>
    public async Task OpenFileTabAsync(string filePath)
    {
        // Reuse existing tab if the same file is already open
        LogTabViewModel? existing = Tabs.FirstOrDefault(t =>
            string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (existing != null) { SelectedTab = existing; return; }

        LogTabViewModel tabVm = new(filePath, Settings) { IsHighlightingEnabled = IsHighlightingEnabled };
        Tabs.Add(tabVm);
        SelectedTab = tabVm;

        Settings.RecentFiles.Remove(filePath);
        Settings.RecentFiles.Insert(0, filePath);
        if (Settings.RecentFiles.Count > 10)
            Settings.RecentFiles.RemoveAt(Settings.RecentFiles.Count - 1);
        _settingsService.Save(Settings);

        await tabVm.InitializeAsync();
    }

    // ── Process Explorer ──────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleProcessExplorer() => IsProcessExplorerVisible = !IsProcessExplorerVisible;

    /// <summary>Opens a new tab capturing output from <paramref name="process"/>.</summary>
    public void OpenProcessTab(ProcessInfo process, ProcessAttachmentType type)
    {
        switch (type)
        {
            case ProcessAttachmentType.DebugOutput:
                OpenDebugOutputTab(process);
                break;
            case ProcessAttachmentType.WindowsEventLog:
                OpenEventLogTab(process);
                break;
            default:
                // stdout/stderr can only be captured for processes LogTailPro launches itself.
                OpenLaunchDialog();
                break;
        }
    }

    private void OpenDebugOutputTab(ProcessInfo process)
    {
        DebugOutputMonitorService monitor = new();
        try { monitor.Start(); }
        catch (InvalidOperationException ex)
        {
            monitor.Dispose();
            MessageBox.Show(ex.Message, "Process Explorer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ProcessOutputCaptureService lineSource = new();
        int targetPid = process.Pid;
        monitor.LineReceived += (_, e) =>
        {
            if (targetPid == 0 || e.Pid == targetPid)
                lineSource.Emit($"[{e.Pid}] {e.Message}");
        };

        AddProcessTab(new LogTabViewModel(process, ProcessAttachmentType.DebugOutput,
                                          lineSource, Settings, ownedResource: monitor));
    }

    private void OpenEventLogTab(ProcessInfo process)
    {
        ProcessOutputCaptureService lineSource = new();
        System.Diagnostics.EventLog log;
        try
        {
            log = new System.Diagnostics.EventLog("Application");
            log.EntryWritten += (_, e) =>
            {
                if (e.Entry.Source.Contains(process.Name, StringComparison.OrdinalIgnoreCase))
                    lineSource.Emit($"[{e.Entry.EntryType}] [{e.Entry.TimeWritten:HH:mm:ss}] " +
                                    $"{e.Entry.Source}: {e.Entry.Message}");
            };
            log.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            lineSource.Dispose();
            MessageBox.Show($"Cannot monitor the Windows Event Log:\n{ex.Message}", "Process Explorer",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AddProcessTab(new LogTabViewModel(process, ProcessAttachmentType.WindowsEventLog,
                                          lineSource, Settings, ownedResource: log));
    }

    private void OpenLaunchDialog()
    {
        LaunchProcessDialog dlg = new() { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true) return;

        ProcessOutputCaptureService capture = new();
        try
        {
            capture.Start(dlg.ExecutablePath, dlg.Arguments, dlg.CaptureType, dlg.WorkingDirectory);
        }
        catch (Exception ex)
        {
            capture.Dispose();
            MessageBox.Show($"Could not start process:\n{ex.Message}", "Launch & Attach",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ProcessInfo launched = new()
        {
            Name = Path.GetFileNameWithoutExtension(dlg.ExecutablePath),
            Pid = capture.ProcessId ?? 0,
            ExecutablePath = dlg.ExecutablePath
        };
        AddProcessTab(new LogTabViewModel(launched, dlg.CaptureType, capture, Settings));
    }

    private void AddProcessTab(LogTabViewModel tab)
    {
        tab.IsHighlightingEnabled = IsHighlightingEnabled;
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    [RelayCommand]
    private async Task OpenRecentFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Settings.RecentFiles.Remove(filePath);
            _settingsService.Save(Settings);
            MessageBox.Show($"File not found:\n{filePath}", "LogTailPro",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await OpenFileTabAsync(filePath);
    }

    [RelayCommand]
    private async Task OpenFromClipboardAsync()
    {
        string text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text)) { StatusMessage = "Clipboard is empty"; return; }

        string tempPath = Path.Combine(Path.GetTempPath(),
            $"LogTailPro_clipboard_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        await File.WriteAllTextAsync(tempPath, text);
        await OpenFileTabAsync(tempPath);

        if (SelectedTab != null)
        {
            SelectedTab.DisplayName = "Clipboard";
            SelectedTab.TempFilePath = tempPath;
        }
    }

    [RelayCommand]
    private void WatchDirectory()
    {
        OpenFolderDialog dialog = new()
        {
            Title = "Select directory to monitor for new log files"
        };
        if (dialog.ShowDialog() != true) return;

        _directoryMonitor.StartMonitoring(dialog.FolderName, "*.log");
        StatusMessage = $"Watching directory: {dialog.FolderName}";
    }

    // ── Tab commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    private void CloseTab(LogTabViewModel tab)
    {
        CaptureBookmarks(tab);
        _settingsService.Save(Settings);
        tab.Dispose();
        Tabs.Remove(tab);
        if (SelectedTab == tab)
            SelectedTab = Tabs.Count > 0 ? Tabs[^1] : null;
    }

    [RelayCommand]
    private void CloseSelectedTab()
    {
        if (SelectedTab != null) CloseTab(SelectedTab);
    }

    [RelayCommand]
    private void NextTab()
    {
        if (Tabs.Count == 0) return;
        int idx = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : -1;
        SelectedTab = Tabs[(idx + 1) % Tabs.Count];
    }

    [RelayCommand]
    private void PreviousTab()
    {
        if (Tabs.Count == 0) return;
        int idx = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0;
        SelectedTab = Tabs[(idx - 1 + Tabs.Count) % Tabs.Count];
    }

    [RelayCommand]
    private void SelectTab(string? indexStr)
    {
        if (int.TryParse(indexStr, out int index) && index >= 0 && index < Tabs.Count)
            SelectedTab = Tabs[index];
    }

    [RelayCommand]
    private void MergeTabs()
    {
        LogTabViewModel[] sources = Tabs.Where(tab => tab.IsTimelineSource).ToArray();
        if (sources.Length < 2)
        {
            StatusMessage = "Open at least two log files to create a combined timeline.";
            return;
        }
        TimelineSourcesDialog picker = new(sources) { Owner = Application.Current.MainWindow };
        if (picker.ShowDialog() != true) return;
        LogTabViewModel timeline = new(picker.SelectedSources, Settings)
        {
            IsHighlightingEnabled = IsHighlightingEnabled
        };
        timeline.OriginalLineRequested += (_, line) =>
        {
            LogTabViewModel? source = Tabs.FirstOrDefault(tab => tab.IsTimelineSource &&
                string.Equals(tab.FilePath, line.SourceFilePath, StringComparison.OrdinalIgnoreCase));
            if (source == null || line.OriginalLine == null || !source.AllLines.Contains(line.OriginalLine))
            {
                StatusMessage = "Original line is no longer loaded (source closed, rotated or trimmed).";
                return;
            }
            SelectedTab = source;
            source.NavigateToLine(line.OriginalLine);
        };
        Tabs.Add(timeline);
        SelectedTab = timeline;
    }

    [RelayCommand]
    public void CorrelateId(string? initialId = null)
    {
        LogTabViewModel[] sources = Tabs.Where(tab => tab.IsTimelineSource).ToArray();
        if (sources.Length == 0)
        {
            StatusMessage = "Open at least one log file to correlate.";
            return;
        }
        string? suggested = initialId;
        if (string.IsNullOrWhiteSpace(suggested) && SelectedTab != null)
        {
            suggested = SelectedTab.ExtractSuggestedCorrelationId();
        }
        CorrelateIdDialog dlg = new(sources, suggested) { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true) return;
        string correlationId = dlg.CorrelationId.Trim();
        if (string.IsNullOrEmpty(correlationId) || dlg.SelectedSources.Count == 0) return;
        LogTabViewModel correlationTab = new(dlg.SelectedSources, correlationId, Settings)
        {
            IsHighlightingEnabled = IsHighlightingEnabled
        };
        correlationTab.OriginalLineRequested += (_, line) =>
        {
            LogTabViewModel? source = Tabs.FirstOrDefault(tab => tab.IsTimelineSource &&
                string.Equals(tab.FilePath, line.SourceFilePath, StringComparison.OrdinalIgnoreCase));
            if (source == null || line.OriginalLine == null || !source.AllLines.Contains(line.OriginalLine))
            {
                StatusMessage = "Original line is no longer loaded (source closed, rotated or trimmed).";
                return;
            }
            SelectedTab = source;
            source.NavigateToLine(line.OriginalLine);
        };
        Tabs.Add(correlationTab);
        SelectedTab = correlationTab;
    }
    // ── Workspace ─────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task SaveWorkspaceAsync(string? targetFilePath = null)
    {
        string? savePath = targetFilePath;
        if (string.IsNullOrEmpty(savePath))
        {
            SaveFileDialog dlg = new()
            {
                Filter = "LogTailPro Workspace|*.ltpw|All Files|*.*",
                FileName = "workspace.ltpw",
                Title = "Save Workspace"
            };
            if (dlg.ShowDialog() != true) return;
            savePath = dlg.FileName;
        }

        List<WorkspaceTab> wsTabs = new();
        foreach (LogTabViewModel t in Tabs)
        {
            if (t.IsProcessTab) continue;

            if (t.IsCorrelationTab)
            {
                wsTabs.Add(new WorkspaceTab
                {
                    TabType = WorkspaceTabType.Correlation,
                    FilePath = t.FilePath,
                    FileName = t.FileName,
                    CorrelationId = t.CorrelationId,
                    SourceFilePaths = t.TimelineSourcePaths.ToList(),
                    SearchText = t.SearchText,
                    IsRegex = t.IsRegexMode,
                    IsCaseSensitive = t.IsCaseSensitive,
                    FilterMode = t.FilterMode,
                    BookmarkedLines = t.Bookmarks.Select(b => b.LineNumber).ToList(),
                    IsFollowTail = t.IsFollowTail,
                    ScrollVerticalOffset = t.SavedScrollVertical,
                    ScrollHorizontalOffset = t.SavedScrollHorizontal,
                    IsStructuredViewEnabled = t.IsStructuredViewEnabled
                });
            }
            else if (t.IsTimeline)
            {
                wsTabs.Add(new WorkspaceTab
                {
                    TabType = WorkspaceTabType.Timeline,
                    FilePath = t.FilePath,
                    FileName = t.FileName,
                    SourceFilePaths = t.TimelineSourcePaths.ToList(),
                    SearchText = t.SearchText,
                    IsRegex = t.IsRegexMode,
                    IsCaseSensitive = t.IsCaseSensitive,
                    FilterMode = t.FilterMode,
                    BookmarkedLines = t.Bookmarks.Select(b => b.LineNumber).ToList(),
                    IsFollowTail = t.IsFollowTail,
                    ScrollVerticalOffset = t.SavedScrollVertical,
                    ScrollHorizontalOffset = t.SavedScrollHorizontal,
                    IsStructuredViewEnabled = t.IsStructuredViewEnabled
                });
            }
            else if (!string.IsNullOrEmpty(t.FilePath) && File.Exists(t.FilePath))
            {
                wsTabs.Add(new WorkspaceTab
                {
                    TabType = WorkspaceTabType.File,
                    FilePath = t.FilePath,
                    FileName = t.FileName,
                    SearchText = t.SearchText,
                    IsRegex = t.IsRegexMode,
                    IsCaseSensitive = t.IsCaseSensitive,
                    FilterMode = t.FilterMode,
                    BookmarkedLines = t.Bookmarks.Select(b => b.LineNumber).ToList(),
                    IsFollowTail = t.IsFollowTail,
                    ScrollVerticalOffset = t.SavedScrollVertical,
                    ScrollHorizontalOffset = t.SavedScrollHorizontal,
                    IsStructuredViewEnabled = t.IsStructuredViewEnabled
                });
            }
        }

        string? dockLayoutXml = RequestLayoutXml?.Invoke();

        Workspace ws = new()
        {
            SchemaVersion = WorkspaceService.CurrentSchemaVersion,
            Name = Path.GetFileNameWithoutExtension(savePath),
            ActiveTabId = SelectedTab?.FilePath ?? SelectedTab?.FileName,
            ActiveTabIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0,
            Tabs = wsTabs,
            HighlightRules = Settings.HighlightRules.ToList(),
            DockLayoutXml = dockLayoutXml
        };

        try
        {
            await _workspaceService.SaveAsync(savePath, ws);
            StatusMessage = $"Workspace saved to {Path.GetFileName(savePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save workspace: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task LoadWorkspaceAsync(string? sourceFilePath = null)
    {
        string? openPath = sourceFilePath;
        if (string.IsNullOrEmpty(openPath))
        {
            OpenFileDialog dlg = new()
            {
                Filter = "LogTailPro Workspace|*.ltpw|All Files|*.*",
                Title = "Load Workspace"
            };
            if (dlg.ShowDialog() != true) return;
            openPath = dlg.FileName;
        }

        WorkspaceLoadResult result = await _workspaceService.LoadAsync(openPath);
        if (!result.Success || result.Workspace == null)
        {
            StatusMessage = $"Failed to load workspace: {result.ErrorMessage ?? "Invalid format"}";
            return;
        }

        Workspace ws = result.Workspace;

        List<WorkspaceTab> fileTabs = ws.Tabs.Where(t => t.TabType == WorkspaceTabType.File).ToList();
        List<WorkspaceTab> dependentTabs = ws.Tabs.Where(t => t.TabType is WorkspaceTabType.Timeline or WorkspaceTabType.Correlation).ToList();

        List<string> missingFiles = result.MissingFiles.ToList();
        List<WorkspaceTab> validFileTabs = fileTabs.Where(t => File.Exists(t.FilePath)).ToList();

        if (validFileTabs.Count == 0 && dependentTabs.Count == 0 && missingFiles.Count > 0)
        {
            StatusMessage = $"Workspace load cancelled: All {missingFiles.Count} log file(s) are missing.";
            return;
        }

        // Close all current tabs
        foreach (LogTabViewModel tab in Tabs.ToList()) tab.Dispose();
        Tabs.Clear();
        SelectedTab = null;

        // Restore custom highlight rules if present
        if (ws.HighlightRules != null && ws.HighlightRules.Count > 0)
        {
            Settings.HighlightRules = ws.HighlightRules.ToList();
            _settingsService.Save(Settings);
            foreach (LogTabViewModel tab in Tabs) tab.RefreshHighlighting();
        }

        // 1. Restore File tabs first
        Dictionary<string, LogTabViewModel> openedFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (WorkspaceTab wt in validFileTabs)
        {
            await OpenFileTabAsync(wt.FilePath);
            LogTabViewModel? tab = Tabs.FirstOrDefault(t => string.Equals(t.FilePath, wt.FilePath, StringComparison.OrdinalIgnoreCase));
            if (tab == null) continue;

            openedFiles[wt.FilePath] = tab;
            tab.SearchText = wt.SearchText;
            tab.IsRegexMode = wt.IsRegex;
            tab.IsCaseSensitive = wt.IsCaseSensitive;
            tab.FilterMode = wt.FilterMode;
            tab.IsFollowTail = wt.IsFollowTail;
            tab.SavedScrollVertical = wt.ScrollVerticalOffset;
            tab.SavedScrollHorizontal = wt.ScrollHorizontalOffset;
            tab.IsStructuredViewEnabled = wt.IsStructuredViewEnabled;
            if (wt.BookmarkedLines.Count > 0)
            {
                tab.RestoreBookmarks(wt.BookmarkedLines);
            }
        }

        // 2. Restore Dependent tabs (Timelines and Correlations)
        List<string> unrestorableTabs = new();
        foreach (WorkspaceTab dt in dependentTabs)
        {
            List<LogTabViewModel> matchingSources = new();
            foreach (string sp in dt.SourceFilePaths)
            {
                if (openedFiles.TryGetValue(sp, out LogTabViewModel? src))
                {
                    matchingSources.Add(src);
                }
            }

            if (dt.TabType == WorkspaceTabType.Timeline)
            {
                if (matchingSources.Count >= 2)
                {
                    LogTabViewModel timeline = new(matchingSources, Settings)
                    {
                        IsHighlightingEnabled = IsHighlightingEnabled,
                        SearchText = dt.SearchText,
                        IsRegexMode = dt.IsRegex,
                        IsCaseSensitive = dt.IsCaseSensitive,
                        FilterMode = dt.FilterMode,
                        IsFollowTail = dt.IsFollowTail,
                        SavedScrollVertical = dt.ScrollVerticalOffset,
                        SavedScrollHorizontal = dt.ScrollHorizontalOffset,
                        IsStructuredViewEnabled = dt.IsStructuredViewEnabled
                    };
                    timeline.OriginalLineRequested += (_, line) =>
                    {
                        LogTabViewModel? source = Tabs.FirstOrDefault(tab => tab.IsTimelineSource &&
                            string.Equals(tab.FilePath, line.SourceFilePath, StringComparison.OrdinalIgnoreCase));
                        if (source == null || line.OriginalLine == null || !source.AllLines.Contains(line.OriginalLine))
                        {
                            StatusMessage = "Original line is no longer loaded (source closed, rotated or trimmed).";
                            return;
                        }
                        SelectedTab = source;
                        source.NavigateToLine(line.OriginalLine);
                    };
                    if (dt.BookmarkedLines.Count > 0) timeline.RestoreBookmarks(dt.BookmarkedLines);
                    Tabs.Add(timeline);
                }
                else
                {
                    unrestorableTabs.Add(dt.FileName);
                }
            }
            else if (dt.TabType == WorkspaceTabType.Correlation)
            {
                if (matchingSources.Count >= 1 && !string.IsNullOrEmpty(dt.CorrelationId))
                {
                    LogTabViewModel corr = new(matchingSources, dt.CorrelationId, Settings)
                    {
                        IsHighlightingEnabled = IsHighlightingEnabled,
                        SearchText = dt.SearchText,
                        IsRegexMode = dt.IsRegex,
                        IsCaseSensitive = dt.IsCaseSensitive,
                        FilterMode = dt.FilterMode,
                        IsFollowTail = dt.IsFollowTail,
                        SavedScrollVertical = dt.ScrollVerticalOffset,
                        SavedScrollHorizontal = dt.ScrollHorizontalOffset,
                        IsStructuredViewEnabled = dt.IsStructuredViewEnabled
                    };
                    corr.OriginalLineRequested += (_, line) =>
                    {
                        LogTabViewModel? source = Tabs.FirstOrDefault(tab => tab.IsTimelineSource &&
                            string.Equals(tab.FilePath, line.SourceFilePath, StringComparison.OrdinalIgnoreCase));
                        if (source == null || line.OriginalLine == null || !source.AllLines.Contains(line.OriginalLine))
                        {
                            StatusMessage = "Original line is no longer loaded (source closed, rotated or trimmed).";
                            return;
                        }
                        SelectedTab = source;
                        source.NavigateToLine(line.OriginalLine);
                    };
                    if (dt.BookmarkedLines.Count > 0) corr.RestoreBookmarks(dt.BookmarkedLines);
                    Tabs.Add(corr);
                }
                else
                {
                    unrestorableTabs.Add(dt.FileName);
                }
            }
        }

        // 3. Restore Active Tab
        LogTabViewModel? active = null;
        if (!string.IsNullOrEmpty(ws.ActiveTabId))
        {
            active = Tabs.FirstOrDefault(t =>
                string.Equals(t.FilePath, ws.ActiveTabId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.FileName, ws.ActiveTabId, StringComparison.OrdinalIgnoreCase));
        }

        if (active == null && ws.ActiveTabIndex >= 0 && ws.ActiveTabIndex < Tabs.Count)
        {
            active = Tabs[ws.ActiveTabIndex];
        }

        SelectedTab = active ?? (Tabs.Count > 0 ? Tabs[0] : null);

        // 4. Restore Docking Layout
        if (!string.IsNullOrWhiteSpace(ws.DockLayoutXml))
        {
            try { RestoreLayoutXml?.Invoke(ws.DockLayoutXml); } catch { }
        }

        // 5. Status message
        if (missingFiles.Count > 0 || unrestorableTabs.Count > 0)
        {
            string detail = missingFiles.Count > 0
                ? $"{missingFiles.Count} file(s) missing"
                : $"{unrestorableTabs.Count} view(s) could not be restored";
            StatusMessage = $"Workspace loaded with warnings ({detail}): {Path.GetFileName(openPath)}";
        }
        else
        {
            StatusMessage = $"Workspace loaded: {Path.GetFileName(openPath)}";
        }
    }
    // ── Tools ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenLogParser()
    {
        LogParserDialog dlg = new(Settings.LogParserPreset)
        {
            Owner = Application.Current.MainWindow
        };
        if (dlg.ShowDialog() != true) return;

        Settings.LogParserPreset = dlg.ClearParser ? string.Empty
            : (dlg.ViewModel.SelectedPreset?.Name ?? string.Empty);
        _settingsService.Save(Settings);
        StatusMessage = dlg.ClearParser
            ? "Log parser disabled."
            : $"Log parser set to: {Settings.LogParserPreset}";
    }

    [RelayCommand]
    private void OpenHighlightRules()
    {
        HighlightRulesDialog dialog = new(Settings.HighlightRules)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true) return;

        Settings.HighlightRules = dialog.ViewModel.GetResultRules();
        _settingsService.Save(Settings);

        foreach (LogTabViewModel tab in Tabs)
            tab.RefreshHighlighting();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsDialog dialog = new(Settings)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true) return;

        dialog.ViewModel.ApplyTo(Settings);
        _settingsService.Save(Settings);

        // Refresh all tabs to pick up font/highlight changes
        foreach (LogTabViewModel tab in Tabs)
            tab.RefreshHighlighting();
    }

    // ── App exit ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Exit()
    {
        ProcessExplorer.Dispose();
        SaveSettings();
        Application.Current.Shutdown();
    }

    /// <summary>Persists current settings (called on window close).</summary>
    public void SaveSettings()
    {
        foreach (LogTabViewModel tab in Tabs) CaptureBookmarks(tab);
        _settingsService.Save(Settings);
    }

    /// <summary>Copies a tab's bookmarks into settings (only for fully loaded, real files).</summary>
    private void CaptureBookmarks(LogTabViewModel tab)
    {
        if (tab.IsProcessTab || !tab.IsLoaded || !System.IO.File.Exists(tab.FilePath)) return;
        string key = LogTabViewModel.BookmarkKey(tab.FilePath);
        if (tab.Bookmarks.Count > 0) Settings.Bookmarks[key] = tab.ExportBookmarks();
        else Settings.Bookmarks.Remove(key);
    }
}
