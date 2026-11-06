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
    private async Task MergeTabsAsync()
    {
        if (Tabs.Count < 2) { StatusMessage = "Need at least 2 tabs to merge"; return; }

        List<LogLine> allLines = new();
        foreach (LogTabViewModel tab in Tabs)
        {
            allLines.AddRange(tab.AllLines.Select(l => new LogLine
            {
                LineNumber = l.LineNumber,
                Text = l.Text,
                Timestamp = l.Timestamp,
                ElapsedSincePrevious = l.ElapsedSincePrevious,
                ByteOffset = l.ByteOffset,
                SourceFileName = tab.FileName
            }));
        }

        List<LogLine> sorted = allLines
            .OrderBy(l => l.Timestamp ?? DateTime.MaxValue)
            .ThenBy(l => l.LineNumber)
            .ToList();

        LogTabViewModel mergedTab = new("Merged View", sorted, Settings);
        Tabs.Add(mergedTab);
        SelectedTab = mergedTab;
        await Task.CompletedTask;
    }

    // ── Theme ─────────────────────────────────────────────────────────────────

    public bool IsThemeDark  => Settings.Theme == "Dark";
    public bool IsThemeLight => Settings.Theme == "Light";

    [RelayCommand]
    private void SwitchTheme(string themeName)
    {
        ResourceDictionary dict = new()
        {
            Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative)
        };
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(dict);
        Settings.Theme = themeName;
        _settingsService.Save(Settings);
        OnPropertyChanged(nameof(IsThemeDark));
        OnPropertyChanged(nameof(IsThemeLight));
    }

    // ── Workspace ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveWorkspaceAsync()
    {
        SaveFileDialog dlg = new()
        {
            Filter = "LogTailPro Workspace|*.ltpw|All Files|*.*",
            FileName = "workspace.ltpw",
            Title = "Save Workspace"
        };
        if (dlg.ShowDialog() != true) return;

        Workspace ws = new()
        {
            ActiveTabIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0,
            Tabs = Tabs.Where(t => File.Exists(t.FilePath)).Select(t => new WorkspaceTab
            {
                FilePath = t.FilePath,
                SearchText = t.SearchText,
                IsRegex = t.IsRegexMode,
                IsCaseSensitive = t.IsCaseSensitive,
                FilterMode = t.FilterMode,
                BookmarkedLines = t.Bookmarks.Select(b => b.LineNumber).ToList()
            }).ToList()
        };

        await File.WriteAllTextAsync(dlg.FileName,
            JsonConvert.SerializeObject(ws, Formatting.Indented));
        StatusMessage = $"Workspace saved to {Path.GetFileName(dlg.FileName)}";
    }

    [RelayCommand]
    private async Task LoadWorkspaceAsync()
    {
        OpenFileDialog dlg = new()
        {
            Filter = "LogTailPro Workspace|*.ltpw|All Files|*.*",
            Title = "Load Workspace"
        };
        if (dlg.ShowDialog() != true) return;

        Workspace? ws = JsonConvert.DeserializeObject<Workspace>(
            await File.ReadAllTextAsync(dlg.FileName));
        if (ws == null) return;

        // Close all current tabs
        foreach (LogTabViewModel tab in Tabs.ToList()) tab.Dispose();
        Tabs.Clear();
        SelectedTab = null;

        foreach (WorkspaceTab wt in ws.Tabs)
        {
            if (!File.Exists(wt.FilePath)) continue;
            await OpenFileTabAsync(wt.FilePath);
            if (SelectedTab == null) continue;
            SelectedTab.SearchText = wt.SearchText;
            SelectedTab.IsRegexMode = wt.IsRegex;
            SelectedTab.IsCaseSensitive = wt.IsCaseSensitive;
            SelectedTab.FilterMode = wt.FilterMode;
        }

        if (ws.ActiveTabIndex >= 0 && ws.ActiveTabIndex < Tabs.Count)
            SelectedTab = Tabs[ws.ActiveTabIndex];

        StatusMessage = $"Workspace loaded: {Path.GetFileName(dlg.FileName)}";
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

        string previousTheme = Settings.Theme;
        dialog.ViewModel.ApplyTo(Settings);
        _settingsService.Save(Settings);

        // Apply theme if it changed
        if (Settings.Theme != previousTheme)
            SwitchTheme(Settings.Theme);

        // Refresh all tabs to pick up font/highlight changes
        foreach (LogTabViewModel tab in Tabs)
            tab.RefreshHighlighting();
    }

    // ── Tab strip placement ───────────────────────────────────────────────────

    [RelayCommand]
    private void SetTabPlacement(string side)
    {
        System.Windows.Controls.Dock dock = side switch
        {
            "Bottom" => System.Windows.Controls.Dock.Bottom,
            "Left"   => System.Windows.Controls.Dock.Left,
            "Right"  => System.Windows.Controls.Dock.Right,
            _        => System.Windows.Controls.Dock.Top
        };
        Settings.TabStripPlacement = dock;
        _settingsService.Save(Settings);
    }

    // ── App exit ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Exit()
    {
        ProcessExplorer.Dispose();
        _settingsService.Save(Settings);
        Application.Current.Shutdown();
    }

    /// <summary>Persists current settings (called on window close).</summary>
    public void SaveSettings() => _settingsService.Save(Settings);
}
