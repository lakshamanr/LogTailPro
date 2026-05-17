# PHASE 4 — Multi-Tab, File Management & Recent Files

## Goal
By the end of this phase:
- Multiple log files open simultaneously in tabs
- Tab order can be changed by drag-and-drop
- Each tab has an independent search, filter, and follow-tail state
- Recent files list (last 10) in File menu and jump-list
- File status indicators: active (green), error (red), deleted (grey)
- Ctrl+Tab cycles through tabs

---

## Pre-flight Checklist
- [ ] Phase 3 is fully working and built without errors

---

## Step 1: Tab Drag-and-Drop Reordering

WPF's built-in TabControl doesn't support drag-and-drop tab reordering.
Use `Microsoft.Xaml.Behaviors.Wpf` to implement it cleanly.

Add to `MainWindow.xaml.cs`:
```csharp
// Store drag start info
private LogTabViewModel? _draggedTab;
private Point _dragStartPoint;

// On TabItem MouseMove
private void TabItem_MouseMove(object sender, MouseEventArgs e)
{
    if (e.LeftButton != MouseButtonState.Pressed) return;
    if (sender is not FrameworkElement fe) return;
    if (fe.DataContext is not LogTabViewModel tab) return;

    var pos = e.GetPosition(null);
    if (Math.Abs(pos.X - _dragStartPoint.X) > 4 || Math.Abs(pos.Y - _dragStartPoint.Y) > 4)
    {
        _draggedTab = tab;
        DragDrop.DoDragDrop(fe, tab, DragDropEffects.Move);
    }
}

// On TabItem Drop
private void TabItem_Drop(object sender, DragEventArgs e)
{
    if (_draggedTab == null) return;
    if (sender is not FrameworkElement fe) return;
    if (fe.DataContext is not LogTabViewModel target) return;
    if (_draggedTab == target) return;

    var vm = (MainViewModel)DataContext;
    int from = vm.Tabs.IndexOf(_draggedTab);
    int to = vm.Tabs.IndexOf(target);
    vm.Tabs.Move(from, to);
    _draggedTab = null;
}
```

---

## Step 2: File Status Indicators

Add to `LogTabViewModel`:
```csharp
public enum FileStatus { Watching, Paused, Error, Deleted }

[ObservableProperty]
private FileStatus _fileStatus = FileStatus.Watching;

[ObservableProperty]
private string _fileStatusIcon => FileStatus switch
{
    FileStatus.Watching => "●",  // green dot
    FileStatus.Paused   => "⏸",
    FileStatus.Error    => "⚠",  // yellow
    FileStatus.Deleted  => "✕",  // red
    _ => "●"
};
```

Wire up FileWatcherService events:
```csharp
_watcherService.FileDeleted += (_, _) =>
{
    App.Current.Dispatcher.Invoke(() =>
    {
        FileStatus = FileStatus.Deleted;
        StatusText = "File deleted or moved";
    });
};

_watcherService.FileTruncated += (_, _) =>
{
    App.Current.Dispatcher.Invoke(async () =>
    {
        StatusText = "File truncated — reloading...";
        _allLines.Clear();
        VisibleLines.Clear();
        await InitializeAsync();
    });
};
```

Tab header XAML:
```xml
<DataTemplate>
  <StackPanel Orientation="Horizontal">
    <TextBlock Text="{Binding FileStatusIcon}"
               Foreground="{Binding FileStatus, Converter={StaticResource StatusColorConverter}}"
               Margin="0,0,4,0"/>
    <TextBlock Text="{Binding FileName}"/>
    <Button Content="✕"
            Command="{Binding DataContext.CloseTabCommand,
                     RelativeSource={RelativeSource AncestorType=Window}}"
            CommandParameter="{Binding}"
            Style="{StaticResource TabCloseButtonStyle}"/>
  </StackPanel>
</DataTemplate>
```

---

## Step 3: Recent Files

In `SettingsService`, recent files are stored in `AppSettings.RecentFiles` (max 10).

In `MainViewModel`:
```csharp
private void AddToRecentFiles(string filePath)
{
    Settings.RecentFiles.Remove(filePath); // remove if already exists
    Settings.RecentFiles.Insert(0, filePath); // add to front
    if (Settings.RecentFiles.Count > 10)
        Settings.RecentFiles.RemoveAt(10);
    _settingsService.Save(Settings);
}
```

Recent files menu:
```xml
<MenuItem Header="Recent Files" ItemsSource="{Binding Settings.RecentFiles}">
  <MenuItem.ItemContainerStyle>
    <Style TargetType="MenuItem">
      <Setter Property="Header" Value="{Binding}"/>
      <Setter Property="Command" Value="{Binding DataContext.OpenRecentFileCommand,
              RelativeSource={RelativeSource AncestorType=Window}}"/>
      <Setter Property="CommandParameter" Value="{Binding}"/>
    </Style>
  </MenuItem.ItemContainerStyle>
</MenuItem>
```

---

## Step 4: Keyboard Shortcuts for Tabs

```xml
<Window.InputBindings>
  <KeyBinding Key="Tab" Modifiers="Ctrl" Command="{Binding NextTabCommand}"/>
  <KeyBinding Key="Tab" Modifiers="Ctrl+Shift" Command="{Binding PreviousTabCommand}"/>
  <KeyBinding Key="W" Modifiers="Ctrl" Command="{Binding CloseSelectedTabCommand}"/>
  <KeyBinding Key="D1" Modifiers="Ctrl" Command="{Binding SelectTabCommand}" CommandParameter="0"/>
  <KeyBinding Key="D2" Modifiers="Ctrl" Command="{Binding SelectTabCommand}" CommandParameter="1"/>
  <!-- etc up to D9 -->
</Window.InputBindings>
```

---

## Step 5: Window State Persistence

Save/restore window size, position, and maximized state in `App.xaml.cs`:

```csharp
protected override void OnExit(ExitEventArgs e)
{
    base.OnExit(e);
    var window = Application.Current.MainWindow;
    Settings.WindowWidth = window.ActualWidth;
    Settings.WindowHeight = window.ActualHeight;
    Settings.WindowTop = window.Top;
    Settings.WindowLeft = window.Left;
    Settings.WindowMaximized = window.WindowState == WindowState.Maximized;
    _settingsService.Save(Settings);
}
```

Add `WindowTop`, `WindowLeft`, `WindowMaximized` to `AppSettings`.

---

## Step 7: Bookmarks + Navigation Bar

### Bookmarks
Users can bookmark any log line for later reference (Ctrl+B or click gutter).

Add to `LogLine`:
```csharp
public bool IsBookmarked { get; set; } = false;
```

Add to `LogTabViewModel`:
```csharp
public ObservableCollection<LogLine> Bookmarks { get; } = new();

[RelayCommand]
private void ToggleBookmark()
{
    if (SelectedLogLine == null) return;
    SelectedLogLine.IsBookmarked = !SelectedLogLine.IsBookmarked;
    if (SelectedLogLine.IsBookmarked) Bookmarks.Add(SelectedLogLine);
    else Bookmarks.Remove(SelectedLogLine);
}

[RelayCommand]
private void NextBookmark() { /* jump to next bookmarked line */ }

[RelayCommand]
private void PreviousBookmark() { /* jump to previous bookmarked line */ }
```

Show bookmark icon (⭐) in a narrow gutter column left of line numbers.

### Navigation Bar (Minimap)

A narrow panel (30px wide) docked to the RIGHT of the log view.
Represents the entire file as a vertical strip. Shows colored ticks for:
- 🔴 Red tick = ERROR line
- 🟡 Yellow tick = WARN line
- 🔵 Blue tick = Bookmark
- 🟢 Green tick = Search match

Clicking a tick jumps to that line. This is the single most powerful
navigation feature in LogViewPlus.

```csharp
// NavigationBar is a Canvas drawn with tiny rectangles
// Redrawn when: lines load, new lines arrive, search changes
private void DrawNavigationBar()
{
    NavCanvas.Children.Clear();
    double totalLines = _allLines.Count;
    double canvasHeight = NavCanvas.ActualHeight;

    foreach (var line in _allLines)
    {
        double y = (line.LineNumber / totalLines) * canvasHeight;
        Color color = GetNavColor(line);
        if (color == Colors.Transparent) continue;

        var rect = new Rectangle
        {
            Width = 30, Height = 2,
            Fill = new SolidColorBrush(color)
        };
        Canvas.SetTop(rect, y);
        NavCanvas.Children.Add(rect);
    }
}

private Color GetNavColor(LogLine line)
{
    if (line.IsBookmarked) return Colors.DeepSkyBlue;
    if (_activePattern?.IsMatch(line.Text) == true) return Colors.LimeGreen;
    if (line.MatchedRule?.BackgroundColor == "#CC2222") return Colors.Red;
    if (line.MatchedRule?.BackgroundColor == "#FFA500") return Colors.Orange;
    return Colors.Transparent;
}
```

---

## Step 8: Merge Log Files

Combine two or more open tabs into a single merged view, sorted by timestamp.

```csharp
[RelayCommand]
private async Task MergeTabsAsync()
{
    // Show dialog to select which tabs to merge
    var mergeable = Tabs.Where(t => t != SelectedTab).ToList();
    var dialog = new MergeTabsDialog(mergeable);
    if (dialog.ShowDialog() != true) return;

    var selectedTabs = dialog.SelectedTabs;
    var allLines = new List<LogLine>(SelectedTab!.AllLines);

    foreach (var tab in selectedTabs)
        allLines.AddRange(tab.AllLines.Select(l => l with
        {
            Text = $"[{tab.FileName}] {l.Text}"  // prefix with source file
        }));

    // Sort by parsed timestamp, fallback to original order
    var sorted = allLines
        .OrderBy(l => l.Timestamp ?? DateTime.MaxValue)
        .ThenBy(l => l.LineNumber)
        .ToList();

    // Create a new "Merged" tab
    var mergedTab = new LogTabViewModel("Merged View", sorted, Settings);
    Tabs.Add(mergedTab);
    SelectedTab = mergedTab;
}
```

Add: Edit → Merge Selected Tabs.
The merged tab is read-only (no file watcher).

---

## Step 9: Directory Monitor

Watches a folder and automatically opens any new `.log` or `.txt` files that appear.

```csharp
public sealed class DirectoryMonitorService : IDisposable
{
    private FileSystemWatcher? _watcher;
    public event EventHandler<string>? NewFileDetected;

    public void StartMonitoring(string directoryPath, string filter = "*.log")
    {
        _watcher = new FileSystemWatcher(directoryPath, filter)
        {
            NotifyFilters = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };
        _watcher.Created += (_, e) => NewFileDetected?.Invoke(this, e.FullPath);
    }

    public void Dispose() => _watcher?.Dispose();
}
```

In `MainViewModel`:
```csharp
[RelayCommand]
private void WatchDirectory()
{
    var dialog = new FolderBrowserDialog();
    if (dialog.ShowDialog() != DialogResult.OK) return;

    _directoryMonitor.StartMonitoring(dialog.SelectedPath);
    _directoryMonitor.NewFileDetected += async (_, path) =>
        await App.Current.Dispatcher.InvokeAsync(() => OpenFileTabAsync(path));

    StatusMessage = $"Watching directory: {dialog.SelectedPath}";
}
```

Add: File → Watch Directory.

---

## Step 10: Build and Test

```bash
dotnet build src/LogTailPro/LogTailPro.csproj
```

### Manual Test Checklist
- [ ] Open 3 different log files — each gets its own tab
- [ ] Each tab has independent search, filter, follow-tail state
- [ ] Tabs can be reordered by dragging
- [ ] Ctrl+Tab cycles through tabs
- [ ] Ctrl+1, Ctrl+2 etc. select specific tabs
- [ ] File menu shows recent files (last 10)
- [ ] Clicking a recent file opens it in a new tab
- [ ] Tab shows green dot while file is being watched
- [ ] Tab shows ✕ red icon when file is deleted
- [ ] Log rotation (truncation) triggers automatic reload
- [ ] Window size and position are restored on restart
- [ ] Ctrl+B bookmarks/unbookmarks a line
- [ ] Bookmarked lines show a star icon in the gutter
- [ ] F2 / Shift+F2 jumps between bookmarks
- [ ] Navigation bar appears on right side of log view
- [ ] Navigation bar shows red ticks for ERROR lines, orange for WARN, blue for bookmarks
- [ ] Clicking a tick in the navigation bar jumps to that line
- [ ] Edit → Merge Selected Tabs creates a new merged tab sorted by timestamp
- [ ] File → Watch Directory opens a folder picker and auto-opens new log files

---

## Phase 4 Complete When
All checkboxes above are ticked. Then read `PHASE_5.md`.
