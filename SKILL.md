---
name: logtailpro-wpf
description: >
  Complete technical skill for building LogTailPro — a C# WPF .NET 8 real-time
  log file viewer. Use this skill for every coding decision in this project.
  Contains architecture patterns, WPF-specific techniques, C# patterns, and
  critical constraints for large-file handling, virtual scrolling, and real-time
  file watching. Always consult this before writing any class or XAML file.
---

# LogTailPro — Technical Skill Reference

## 1. Project File (LogTailPro.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationIcon>Assets\app.ico</ApplicationIcon>
    <AssemblyName>LogTailPro</AssemblyName>
    <RootNamespace>LogTailPro</RootNamespace>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
    <Optimize>true</Optimize>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.3.2" />
    <PackageReference Include="Microsoft.Xaml.Behaviors.Wpf" Version="1.1.77" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

---

## 2. Models

### LogLine.cs
```csharp
/// <summary>Represents a single line in the log file.</summary>
public sealed class LogLine
{
    public long LineNumber { get; init; }
    public string Text { get; init; } = string.Empty;
    public long ByteOffset { get; init; }  // position in file for seeking
    public bool IsVisible { get; set; } = true;
    public HighlightRule? MatchedRule { get; set; }
}
```

### LogFile.cs
```csharp
public sealed class LogFile
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public long FileSizeBytes { get; set; }
    public Encoding Encoding { get; set; } = Encoding.UTF8;
    public bool IsActive { get; set; } = true;
    public DateTime LastModified { get; set; }
}
```

### HighlightRule.cs
```csharp
public sealed class HighlightRule
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Pattern { get; set; } = string.Empty;
    public bool IsRegex { get; set; }
    public bool IsCaseSensitive { get; set; }
    public string ForegroundColor { get; set; } = "#FFFFFF";
    public string BackgroundColor { get; set; } = "#FF0000";
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; }  // lower = evaluated first
}
```

### AppSettings.cs
```csharp
public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public int MaxLinesInMemory { get; set; } = 100_000;
    public bool FollowTailOnOpen { get; set; } = true;
    public string FontFamily { get; set; } = "Consolas";
    public double FontSize { get; set; } = 13.0;
    public List<HighlightRule> HighlightRules { get; set; } = new();
    public List<string> RecentFiles { get; set; } = new();
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 800;
}
```

---

## 3. Services

### FileWatcherService.cs — Critical Patterns

```csharp
/// <summary>
/// Watches a log file for new content using FileSystemWatcher.
/// Uses byte-offset seeking to read only new content efficiently.
/// Never reads the whole file again after initial load.
/// </summary>
public sealed class FileWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private long _lastReadPosition = 0;
    private readonly string _filePath;
    private readonly Encoding _encoding;

    public event EventHandler<IReadOnlyList<string>>? NewLinesAvailable;
    public event EventHandler? FileDeleted;
    public event EventHandler? FileTruncated;

    public void StartWatching(string filePath)
    {
        // CRITICAL: Use NotifyFilters.LastWrite | NotifyFilters.Size
        // Do NOT use NotifyFilters.LastAccess — it fires too often
        _watcher = new FileSystemWatcher(
            Path.GetDirectoryName(filePath)!,
            Path.GetFileName(filePath))
        {
            NotifyFilters = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Deleted += OnFileDeleted;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // CRITICAL: Use FileShare.ReadWrite | FileShare.Delete
        // so we don't lock the file being written by another process
        using var stream = new FileStream(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        // Detect file truncation (log rotation)
        if (stream.Length < _lastReadPosition)
        {
            _lastReadPosition = 0;
            FileTruncated?.Invoke(this, EventArgs.Empty);
        }

        stream.Seek(_lastReadPosition, SeekOrigin.Begin);

        var newLines = new List<string>();
        using var reader = new StreamReader(stream, _encoding, leaveOpen: true);

        string? line;
        while ((line = reader.ReadLine()) != null)
            newLines.Add(line);

        _lastReadPosition = stream.Position;

        if (newLines.Count > 0)
            NewLinesAvailable?.Invoke(this, newLines);
    }

    public void Dispose() => _watcher?.Dispose();
}
```

### Large File Initial Load — CRITICAL

```csharp
/// <summary>
/// For initial load of large files, read in 64KB chunks.
/// Never load the entire file into a List<string> if > 50MB.
/// Instead, load only the LAST N lines for display,
/// and build a line-index for seeking.
/// </summary>
public async Task<InitialLoadResult> LoadLastNLinesAsync(
    string filePath,
    int lineCount = 5000,
    CancellationToken ct = default)
{
    // Strategy: seek from end, read backwards in chunks
    // until we have lineCount lines or reach start of file
    const int ChunkSize = 65536; // 64KB
    var lines = new List<string>(lineCount);
    long fileSize = new FileInfo(filePath).Length;
    long position = fileSize;

    using var stream = new FileStream(filePath, FileMode.Open,
        FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    var buffer = new byte[ChunkSize];
    var leftover = string.Empty;

    while (position > 0 && lines.Count < lineCount)
    {
        ct.ThrowIfCancellationRequested();
        long readFrom = Math.Max(0, position - ChunkSize);
        int bytesToRead = (int)(position - readFrom);
        stream.Seek(readFrom, SeekOrigin.Begin);
        int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bytesToRead), ct);
        // Parse lines from chunk + leftover, add to front of list
        // ... (reverse parsing logic)
        position = readFrom;
    }

    return new InitialLoadResult { Lines = lines, StartByteOffset = position };
}
```

### SearchService.cs

```csharp
public sealed class SearchService
{
    /// <summary>
    /// Compiles regex once, reuses for all lines.
    /// ALWAYS compile with Timeout to prevent ReDoS attacks from bad patterns.
    /// </summary>
    public Regex? CompilePattern(string pattern, bool isRegex, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(pattern)) return null;

        var options = caseSensitive
            ? RegexOptions.Compiled
            : RegexOptions.Compiled | RegexOptions.IgnoreCase;

        string finalPattern = isRegex ? pattern : Regex.Escape(pattern);

        // CRITICAL: Always set matchTimeout to prevent UI freeze from pathological regex
        return new Regex(finalPattern, options, TimeSpan.FromSeconds(2));
    }

    public IEnumerable<LogLine> Filter(
        IEnumerable<LogLine> lines,
        Regex pattern,
        FilterMode mode)
    {
        return mode switch
        {
            FilterMode.IncludeMatching => lines.Where(l => pattern.IsMatch(l.Text)),
            FilterMode.ExcludeMatching => lines.Where(l => !pattern.IsMatch(l.Text)),
            _ => lines
        };
    }
}

public enum FilterMode { None, IncludeMatching, ExcludeMatching }
```

### HighlightService.cs

```csharp
public sealed class HighlightService
{
    /// <summary>
    /// Returns the first matching rule for a line (by priority order).
    /// Called per-line during rendering — must be fast.
    /// Pre-compile all rules on load, not per-line.
    /// </summary>
    public HighlightRule? GetMatchingRule(
        string lineText,
        IReadOnlyList<(HighlightRule Rule, Regex CompiledPattern)> compiledRules)
    {
        foreach (var (rule, pattern) in compiledRules)
        {
            if (!rule.IsEnabled) continue;
            try
            {
                if (pattern.IsMatch(lineText)) return rule;
            }
            catch (RegexMatchTimeoutException)
            {
                // Silently skip timed-out rules — never crash the viewer
            }
        }
        return null;
    }
}
```

### SettingsService.cs

```csharp
public sealed class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LogTailPro", "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath)) return new AppSettings();
        try
        {
            string json = File.ReadAllText(SettingsPath);
            return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
    }
}
```

---

## 4. ViewModels

### MainViewModel.cs — Skeleton

```csharp
[ObservableObject]
public partial class MainViewModel
{
    [ObservableProperty]
    private ObservableCollection<LogTabViewModel> _tabs = new();

    [ObservableProperty]
    private LogTabViewModel? _selectedTab;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    private readonly SettingsService _settingsService;
    public AppSettings Settings { get; private set; }

    public MainViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        Settings = settingsService.Load();
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Log Files|*.log;*.txt;*.out|All Files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true) return;

        foreach (string path in dialog.FileNames)
            await OpenFileTabAsync(path);
    }

    private async Task OpenFileTabAsync(string filePath)
    {
        var tabVm = new LogTabViewModel(filePath, Settings);
        Tabs.Add(tabVm);
        SelectedTab = tabVm;
        await tabVm.InitializeAsync();
    }

    [RelayCommand]
    private void CloseTab(LogTabViewModel tab)
    {
        tab.Dispose();
        Tabs.Remove(tab);
    }
}
```

### LogTabViewModel.cs — Key Properties

```csharp
[ObservableObject]
public partial class LogTabViewModel : IDisposable
{
    // Displayed lines (filtered subset of all loaded lines)
    [ObservableProperty]
    private ObservableCollection<LogLine> _visibleLines = new();

    [ObservableProperty]
    private bool _isFollowTail = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isRegexSearch;

    [ObservableProperty]
    private FilterMode _filterMode = FilterMode.None;

    [ObservableProperty]
    private string _statusText = string.Empty;

    // Total line count (including lines not in memory)
    public long TotalLineCount { get; private set; }
    public long FileSizeBytes { get; private set; }

    // IMPORTANT: SearchText change triggers incremental search
    partial void OnSearchTextChanged(string value) => ApplySearchAndFilter();
    partial void OnFilterModeChanged(FilterMode value) => ApplySearchAndFilter();
}
```

---

## 5. WPF XAML Patterns

### Virtual Scrolling for Log Lines — CRITICAL

```xml
<!-- Use VirtualizingStackPanel with recycling for performance -->
<!-- This is what allows 1M+ lines without freezing -->
<ListView x:Name="LogListView"
          ItemsSource="{Binding VisibleLines}"
          VirtualizingPanel.IsVirtualizing="True"
          VirtualizingPanel.VirtualizationMode="Recycling"
          VirtualizingPanel.CacheLengthUnit="Item"
          VirtualizingPanel.CacheLength="200,200"
          ScrollViewer.CanContentScroll="True"
          ScrollViewer.HorizontalScrollBarVisibility="Auto">
  <ListView.ItemContainerStyle>
    <Style TargetType="ListViewItem">
      <Setter Property="FontFamily" Value="{Binding DataContext.Settings.FontFamily,
              RelativeSource={RelativeSource AncestorType=Window}}"/>
      <Setter Property="FontSize" Value="{Binding DataContext.Settings.FontSize,
              RelativeSource={RelativeSource AncestorType=Window}}"/>
      <Setter Property="Padding" Value="4,1"/>
      <Setter Property="Background" Value="{Binding MatchedRule,
              Converter={StaticResource HighlightColorConverter},
              ConverterParameter=Background}"/>
      <Setter Property="Foreground" Value="{Binding MatchedRule,
              Converter={StaticResource HighlightColorConverter},
              ConverterParameter=Foreground}"/>
    </Style>
  </ListView.ItemContainerStyle>
  <ListView.View>
    <GridView>
      <GridViewColumn Header="Line" Width="70"
                      DisplayMemberBinding="{Binding LineNumber}"/>
      <GridViewColumn Header="Content" Width="2000"
                      DisplayMemberBinding="{Binding Text}"/>
    </GridView>
  </ListView.View>
</ListView>
```

### Auto-Scroll (Follow Tail) Implementation

```csharp
// In LogTabView.xaml.cs — one of the few things allowed in code-behind
private void OnVisibleLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
{
    if (ViewModel.IsFollowTail && e.Action == NotifyCollectionChangedAction.Add)
    {
        // Scroll to end on UI thread
        Dispatcher.InvokeAsync(() =>
        {
            if (LogListView.Items.Count > 0)
                LogListView.ScrollIntoView(LogListView.Items[^1]);
        }, DispatcherPriority.Background);
    }
}
```

### Main Window Layout

```xml
<Window>
  <DockPanel>
    <!-- Menu -->
    <Menu DockPanel.Dock="Top">
      <MenuItem Header="_File">
        <MenuItem Header="_Open..." Command="{Binding OpenFileCommand}" InputGestureText="Ctrl+O"/>
        <Separator/>
        <MenuItem Header="Recent Files" ItemsSource="{Binding Settings.RecentFiles}"/>
        <Separator/>
        <MenuItem Header="E_xit" Command="{Binding ExitCommand}"/>
      </MenuItem>
      <MenuItem Header="_View">
        <MenuItem Header="Toggle Follow Tail" Command="{Binding SelectedTab.ToggleFollowTailCommand}" InputGestureText="F"/>
        <MenuItem Header="Dark Theme" Command="{Binding SwitchThemeCommand}" CommandParameter="Dark"/>
        <MenuItem Header="Light Theme" Command="{Binding SwitchThemeCommand}" CommandParameter="Light"/>
      </MenuItem>
      <MenuItem Header="_Search">
        <MenuItem Header="Find..." Command="{Binding SelectedTab.FocusSearchCommand}" InputGestureText="Ctrl+F"/>
        <MenuItem Header="Filter Mode" Command="{Binding SelectedTab.ToggleFilterCommand}" InputGestureText="Ctrl+Shift+F"/>
      </MenuItem>
      <MenuItem Header="_Tools">
        <MenuItem Header="Highlight Rules..." Command="{Binding OpenHighlightRulesCommand}"/>
        <MenuItem Header="Settings..." Command="{Binding OpenSettingsCommand}"/>
      </MenuItem>
    </Menu>

    <!-- Tab bar -->
    <TabControl DockPanel.Dock="Top" ItemsSource="{Binding Tabs}"
                SelectedItem="{Binding SelectedTab}">
      <TabControl.ItemTemplate>
        <DataTemplate>
          <StackPanel Orientation="Horizontal">
            <TextBlock Text="{Binding FileName}"/>
            <Button Content="✕" Command="{Binding DataContext.CloseTabCommand,
                    RelativeSource={RelativeSource AncestorType=Window}}"
                    CommandParameter="{Binding}"/>
          </StackPanel>
        </DataTemplate>
      </TabControl.ItemTemplate>
    </TabControl>

    <!-- Search/Filter Bar (shown per tab) -->
    <!-- Log Viewer (VirtualizingStackPanel) -->
    <!-- Status Bar -->
    <StatusBar DockPanel.Dock="Bottom">
      <StatusBarItem Content="{Binding SelectedTab.StatusText}"/>
      <Separator/>
      <StatusBarItem Content="{Binding SelectedTab.TotalLineCount, StringFormat='Lines: {0:N0}'}"/>
      <Separator/>
      <StatusBarItem Content="{Binding SelectedTab.FileSizeBytes, StringFormat='Size: {0:N0} bytes'}"/>
    </StatusBar>
  </DockPanel>
</Window>
```

---

## 6. Theme System

```xml
<!-- Themes/Dark.xaml -->
<ResourceDictionary>
  <Color x:Key="BackgroundColor">#1E1E1E</Color>
  <Color x:Key="ForegroundColor">#D4D4D4</Color>
  <Color x:Key="AccentColor">#007ACC</Color>
  <Color x:Key="TabBackground">#2D2D2D</Color>
  <Color x:Key="SearchBarBackground">#252526</Color>
  <SolidColorBrush x:Key="BackgroundBrush" Color="{StaticResource BackgroundColor}"/>
  <SolidColorBrush x:Key="ForegroundBrush" Color="{StaticResource ForegroundColor}"/>
</ResourceDictionary>
```

Switch theme at runtime:
```csharp
private void SwitchTheme(string themeName)
{
    var dict = new ResourceDictionary
    {
        Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative)
    };
    Application.Current.Resources.MergedDictionaries.Clear();
    Application.Current.Resources.MergedDictionaries.Add(dict);
}
```

---

## 7. Keyboard Shortcuts

| Key | Action |
|---|---|
| Ctrl+O | Open file |
| Ctrl+W | Close current tab |
| Ctrl+F | Focus search bar |
| Ctrl+Shift+F | Toggle filter mode |
| F | Toggle follow tail |
| Ctrl+G | Go to line |
| Ctrl+End | Jump to end |
| Ctrl+Home | Jump to start |
| Escape | Clear search / close panel |
| Ctrl+Tab | Switch tabs |

---

## 8. Common Pitfalls — Read These

### ❌ Never Do This
```csharp
// WRONG: Blocks UI thread reading large file
var allLines = File.ReadAllLines(filePath); // freezes on 500MB file

// WRONG: Updates ObservableCollection from background thread
_visibleLines.Add(line); // crashes with cross-thread exception

// WRONG: No timeout on regex
var r = new Regex(userPattern); // ReDoS vulnerability

// WRONG: Locks the file being written
new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
```

### ✅ Always Do This
```csharp
// RIGHT: Stream file async
await foreach (var line in ReadLinesAsync(filePath, ct)) { ... }

// RIGHT: Marshal to UI thread
await Application.Current.Dispatcher.InvokeAsync(() => _visibleLines.Add(line));

// RIGHT: Always set regex timeout
new Regex(pattern, options, TimeSpan.FromSeconds(2));

// RIGHT: Share the file with writers
new FileStream(path, FileMode.Open, FileAccess.Read,
               FileShare.ReadWrite | FileShare.Delete);
```

---

## 9. Performance Targets

| Metric | Target |
|---|---|
| App cold start | < 2 seconds |
| Open 100MB log file | < 3 seconds (tail load) |
| New line render latency | < 100ms |
| Search across 100K lines | < 500ms |
| Memory usage (100K lines loaded) | < 150MB |
| UI framerate during tailing | 60fps |

---

## 10. Phase Sequence

Read each phase file from `./prompts/` in order:
1. `PHASE_1.md` — Solution scaffold + core viewer
2. `PHASE_2.md` — Search and filter
3. `PHASE_3.md` — Large file + virtual scroll
4. `PHASE_4.md` — Multi-tab + file management
5. `PHASE_5.md` — Highlight rules + settings UI
6. `PHASE_6.md` — Extensions + publish
