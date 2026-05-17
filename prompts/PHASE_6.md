# PHASE 6 — Extensions Beyond BareTailPro + Single EXE Publish

## Goal
By the end of this phase, LogTailPro exceeds BareTailPro with:
- Desktop toast notifications when a pattern matches
- Export filtered lines to a file
- Log stats mini-dashboard (error count, warn count over time)
- Copy line(s) to clipboard
- Line wrap toggle
- Packaged as a single self-contained EXE, no installer needed

---

## Pre-flight Checklist
- [ ] Phase 5 is fully working and built without errors
- [ ] All 5 phase manual test checklists are 100% complete

---

## Step 1: Desktop Notifications

When a highlight rule with `NotifyOnMatch = true` matches a new incoming line, show a Windows toast.

Add `NotifyOnMatch` to `HighlightRule`:
```csharp
public bool NotifyOnMatch { get; set; } = false;
public string NotifyTitle { get; set; } = "LogTailPro Alert";
```

Use `System.Windows.Notifications` (WPF) for basic notifications, or use the
`Microsoft.Toolkit.Uwp.Notifications` NuGet package for rich Windows 10/11 toasts:

```bash
dotnet add package Microsoft.Toolkit.Uwp.Notifications --version 7.1.3
```

```csharp
// In LogTabViewModel, when new lines arrive from watcher:
private void CheckForNotifications(IReadOnlyList<LogLine> newLines)
{
    var notifyRules = Settings.HighlightRules.Where(r => r.IsEnabled && r.NotifyOnMatch);
    foreach (var rule in notifyRules)
    {
        var match = newLines.FirstOrDefault(l =>
            _highlightService.RuleMatches(rule, l.Text));
        if (match != null)
            ShowToast(rule.NotifyTitle, match.Text, _logFile.FileName);
    }
}

private static void ShowToast(string title, string message, string fileName)
{
    new ToastContentBuilder()
        .AddText(title)
        .AddText($"[{fileName}] {message.TruncateTo(100)}")
        .Show();
}
```

Add checkbox "Alert me" per rule in `HighlightRulesDialog`.

---

## Step 2: Export Filtered Lines

```csharp
[RelayCommand]
private async Task ExportFilteredLinesAsync()
{
    var dialog = new SaveFileDialog
    {
        Filter = "Text Files|*.txt|Log Files|*.log|All Files|*.*",
        FileName = $"export_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
    };
    if (dialog.ShowDialog() != true) return;

    await File.WriteAllLinesAsync(
        dialog.FileName,
        VisibleLines.Select(l => l.Text));

    StatusText = $"Exported {VisibleLines.Count:N0} lines to {dialog.FileName}";
}
```

Add to toolbar and Edit menu.

---

## Step 2: Export to CSV

Extend the export feature to support structured CSV when a log parser is active:

```csharp
[RelayCommand]
private async Task ExportAsync(string format)
{
    var dialog = new SaveFileDialog
    {
        Filter = format == "csv"
            ? "CSV Files|*.csv"
            : "Text Files|*.txt|Log Files|*.log",
        FileName = $"export_{DateTime.Now:yyyyMMdd_HHmmss}.{format}"
    };
    if (dialog.ShowDialog() != true) return;

    if (format == "csv" && _parserConfig != null)
    {
        // Write header row + parsed column data
        var headers = string.Join(",", _parserConfig.ColumnOrder);
        var rows = VisibleLines.Select(l =>
        {
            var parsed = _parserService.ParseLine(l.Text, _parserConfig);
            return string.Join(",", _parserConfig.ColumnOrder
                .Select(col => $"\"{parsed.Columns.GetValueOrDefault(col)}\""));
        });
        await File.WriteAllLinesAsync(dialog.FileName,
            new[] { headers }.Concat(rows));
    }
    else
    {
        await File.WriteAllLinesAsync(dialog.FileName,
            VisibleLines.Select(l => l.Text));
    }

    StatusText = $"Exported {VisibleLines.Count:N0} lines";
}
```

---

## Step 3: Clipboard → Temp Log File

Paste clipboard text as a temporary log — great for analysing snippets shared over Slack/email:

```csharp
[RelayCommand]
private async Task OpenFromClipboardAsync()
{
    string text = Clipboard.GetText();
    if (string.IsNullOrWhiteSpace(text)) { StatusMessage = "Clipboard is empty"; return; }
    string tempPath = Path.Combine(Path.GetTempPath(),
        $"LogTailPro_clipboard_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    await File.WriteAllTextAsync(tempPath, text);
    await OpenFileTabAsync(tempPath);
    SelectedTab!.DisplayName = "📋 Clipboard";
}
```

Add: File → Open from Clipboard (Ctrl+Shift+V).

---

## Step 4: Copy Selected Lines to Clipboard

```csharp
[RelayCommand]
private void CopySelectedLines()
{
    if (SelectedLogLines == null || !SelectedLogLines.Any()) return;
    string text = string.Join(Environment.NewLine, SelectedLogLines.Select(l => l.Text));
    Clipboard.SetText(text);
}
```

Wire to Ctrl+C in `LogTabView.xaml` InputBindings.

Enable multi-select on the ListView:
```xml
<ListView SelectionMode="Extended" ...>
```

Bind `SelectedItems` via behavior (WPF SelectedItems isn't directly bindable):
```csharp
// In LogTabView.xaml.cs
LogListView.SelectionChanged += (_, e) =>
    ViewModel.SelectedLogLines = LogListView.SelectedItems.Cast<LogLine>().ToList();
```

---

## Step 4: Line Wrap Toggle

Add to `LogTabViewModel`:
```csharp
[ObservableProperty] private bool _isLineWrapEnabled = false;

[RelayCommand]
private void ToggleLineWrap() => IsLineWrapEnabled = !IsLineWrapEnabled;
```

In `LogTabView.xaml`, the Content column uses a DataTemplate:
```xml
<GridViewColumn Header="Content">
  <GridViewColumn.CellTemplate>
    <DataTemplate>
      <TextBlock Text="{Binding Text}"
                 TextWrapping="{Binding DataContext.IsLineWrapEnabled,
                               RelativeSource={RelativeSource AncestorType=Window},
                               Converter={StaticResource BoolToWrapConverter}}"/>
    </DataTemplate>
  </GridViewColumn.CellTemplate>
</GridViewColumn>
```

Where `BoolToWrapConverter` converts `true` → `TextWrapping.Wrap`, `false` → `TextWrapping.NoWrap`.

---

## Step 5: Log Stats Panel (Mini Dashboard)

Add a collapsible stats panel (toggled by Ctrl+Shift+S):

```csharp
public sealed class LogStats
{
    public int TotalLines { get; set; }
    public int ErrorCount { get; set; }
    public int WarnCount { get; set; }
    public int InfoCount { get; set; }
    public DateTime? FirstTimestamp { get; set; }
    public DateTime? LastTimestamp { get; set; }
    public string NewLinesPerSecond { get; set; } = "0";
}
```

```xml
<!-- Stats panel — DockPanel.Dock="Right", Width=200 -->
<StackPanel Visibility="{Binding IsStatsPanelVisible,
            Converter={StaticResource BoolToVisibilityConverter}}">
  <TextBlock Text="Log Stats" FontWeight="Bold" Margin="8"/>
  <Separator/>
  <TextBlock Text="{Binding Stats.TotalLines, StringFormat='Total: {0:N0}'}"/>
  <TextBlock Text="{Binding Stats.ErrorCount, StringFormat='Errors: {0:N0}'}" Foreground="#FF4444"/>
  <TextBlock Text="{Binding Stats.WarnCount, StringFormat='Warnings: {0:N0}'}" Foreground="#FFA500"/>
  <TextBlock Text="{Binding Stats.InfoCount, StringFormat='Info: {0:N0}'}" Foreground="#A8D8A8"/>
  <Separator/>
  <TextBlock Text="{Binding Stats.NewLinesPerSecond, StringFormat='Rate: {0} lines/s'}"/>
</StackPanel>
```

Update stats every time new lines arrive (count lines matching default patterns).

---

## Step 6: Final Polish

### Toolbar

```xml
<ToolBar DockPanel.Dock="Top">
  <Button Command="{Binding OpenFileCommand}" ToolTip="Open (Ctrl+O)">📂</Button>
  <Separator/>
  <ToggleButton IsChecked="{Binding SelectedTab.IsFollowTail}" ToolTip="Follow Tail (F)">⬇</ToggleButton>
  <Button Command="{Binding SelectedTab.ShowSearchCommand}" ToolTip="Search (Ctrl+F)">🔍</Button>
  <Button Command="{Binding SelectedTab.ExportFilteredLinesCommand}" ToolTip="Export">💾</Button>
  <Separator/>
  <ToggleButton IsChecked="{Binding SelectedTab.IsLineWrapEnabled}" ToolTip="Line Wrap">↵</ToggleButton>
  <ToggleButton IsChecked="{Binding SelectedTab.IsStatsPanelVisible}" ToolTip="Stats">📊</ToggleButton>
  <Separator/>
  <Button Command="{Binding OpenHighlightRulesCommand}" ToolTip="Highlight Rules">🎨</Button>
  <Button Command="{Binding OpenSettingsCommand}" ToolTip="Settings">⚙</Button>
</ToolBar>
```

### Window Title
```csharp
// In MainViewModel, update whenever SelectedTab changes
partial void OnSelectedTabChanged(LogTabViewModel? value)
{
    Application.Current.MainWindow.Title = value != null
        ? $"{value.FileName} — LogTailPro"
        : "LogTailPro";
}
```

### Error Handling
Wrap all file operations in try-catch. Show non-blocking error in StatusBar, not a MessageBox:
```csharp
StatusText = $"⚠ Error reading file: {ex.Message}";
FileStatus = FileStatus.Error;
```

---

## Step 7: Single EXE Publish

```bash
dotnet publish src/LogTailPro/LogTailPro.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none \
  -o ./publish
```

Expected output:
- `publish/LogTailPro.exe` — single file, ~80-120MB, no installer
- Runs on any Windows 10/11 x64 machine with no .NET installed

---

## Step 8: Final Build and Full Test

```bash
dotnet build src/LogTailPro/LogTailPro.csproj --configuration Release
dotnet test  # (if unit tests were added)
dotnet publish ...
```

### Final Acceptance Checklist

**Core (BareTailPro parity):**
- [ ] Open any log file — dialog or drag-drop
- [ ] Real-time tailing with follow-tail toggle
- [ ] Files > 2GB open without freeze
- [ ] Regex and plain-text search, incremental
- [ ] Filter: include-only and exclude modes
- [ ] Configurable color highlight rules, persist
- [ ] Multiple files in tabs simultaneously
- [ ] Visual file status (watching / deleted / error)
- [ ] Dark and Light themes
- [ ] Recent files in menu
- [ ] All keyboard shortcuts work
- [ ] Settings dialog (font, size, theme, max lines)

**Extensions (beyond BareTailPro):**
- [ ] Desktop toast notification on pattern match
- [ ] Export filtered lines to file
- [ ] Copy selected lines (multi-select) to clipboard
- [ ] Line wrap toggle
- [ ] Log stats panel (error/warn counts, rate)
- [ ] Toolbar with all key actions

**Quality:**
- [ ] App launches in < 2 seconds
- [ ] Memory < 150MB with 100K lines loaded
- [ ] Smooth scrolling (no stutter)
- [ ] No unhandled exceptions during 30-minute use
- [ ] Single EXE runs on a clean Windows machine

---

## 🎉 Build Complete

When this checklist is fully ticked, LogTailPro is done.
The EXE is at `./publish/LogTailPro.exe`.
