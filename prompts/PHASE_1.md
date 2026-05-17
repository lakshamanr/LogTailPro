# PHASE 1 — Solution Scaffold + Core Log Viewer

## Goal
By the end of this phase, the user can:
- Launch the app
- Open a single log file via File menu or drag-and-drop
- See log lines displayed in a virtualized list with line numbers
- Watch new lines appear in real-time as the file grows
- Toggle follow-tail mode (auto-scroll to bottom)
- See file name, line count, and file size in the status bar

No search, no filter, no highlight rules yet. Just a working, fast, real-time log viewer.

---

## Pre-flight Checklist (Do These First)
- [ ] Verify .NET 8 SDK is installed: `dotnet --version` (must be 8.x)
- [ ] Verify the target is Windows (WPF is Windows-only)
- [ ] Read `../SKILL.md` sections 1–5 before writing any code

---

## Step 1: Create Solution and Project

```bash
# Create solution
dotnet new sln -n LogTailPro -o .

# Create WPF project
dotnet new wpf -n LogTailPro -o src/LogTailPro --framework net10.0-windows

# Add to solution
dotnet sln add src/LogTailPro/LogTailPro.csproj

# Add NuGet packages
cd src/LogTailPro
dotnet add package CommunityToolkit.Mvvm --version 8.3.2
dotnet add package Microsoft.Xaml.Behaviors.Wpf --version 1.1.77
dotnet add package Newtonsoft.Json --version 13.0.3
cd ../..
```

---

## Step 2: Create Folder Structure
Create all directories listed in `../CLAUDE.md` under "Project Structure".

---

## Step 3: Implement in This Order

### 3a. Models (no dependencies, build first)
Create these exactly as specified in `../SKILL.md` section 2:
- `Models/LogLine.cs`
- `Models/LogFile.cs`
- `Models/HighlightRule.cs`
- `Models/AppSettings.cs`

### 3b. Services (depend only on Models)
- `Services/SettingsService.cs` — load/save JSON settings
- `Services/FileWatcherService.cs` — real-time file tailing

**Critical for FileWatcherService:**
- Use `FileShare.ReadWrite | FileShare.Delete` when opening the file
- Track `_lastReadPosition` byte offset — never re-read the whole file
- Detect file truncation (log rotation): if `stream.Length < _lastReadPosition`, reset to 0
- Raise `NewLinesAvailable` event with only the new lines
- Raise `FileTruncated` event when truncation is detected

### 3c. ViewModels
- `ViewModels/MainViewModel.cs`
  - `ObservableCollection<LogTabViewModel> Tabs`
  - `LogTabViewModel? SelectedTab`
  - `OpenFileCommand` — opens OpenFileDialog, creates LogTabViewModel
  - `CloseTabCommand` — disposes and removes tab
  - `ExitCommand`

- `ViewModels/LogTabViewModel.cs`
  - `ObservableCollection<LogLine> VisibleLines`
  - `bool IsFollowTail`
  - `string FileName`
  - `long TotalLineCount`
  - `long FileSizeBytes`
  - `string StatusText` — formatted "Watching | Lines: 1,234 | 2.3 MB"
  - `ToggleFollowTailCommand`
  - `InitializeAsync()` — loads last 5000 lines, starts watcher
  - `Dispose()` — stops watcher

**Threading rule:** FileWatcherService raises events on a background thread.
LogTabViewModel must marshal new lines to UI thread:
```csharp
App.Current.Dispatcher.InvokeAsync(() =>
{
    foreach (var line in newLines)
        VisibleLines.Add(new LogLine { ... });
    UpdateStatus();
}, DispatcherPriority.Background);
```

**Memory rule:** If `VisibleLines.Count > Settings.MaxLinesInMemory`, remove oldest lines from the front.

### 3d. Views

**MainWindow.xaml:**
- Set `DataContext` to `MainViewModel` via App.xaml.cs
- DockPanel layout: Menu → TabControl → StatusBar
- TabControl bound to `Tabs`, `SelectedItem` = `SelectedTab`
- Each tab header shows `FileName` + close button
- Content area shows `LogTabView` for selected tab
- Use `AllowDrop="True"` and wire `Drop` event for drag-and-drop

**LogTabView.xaml:**
- ListView with `VirtualizingStackPanel.IsVirtualizing="True"` and `VirtualizationMode="Recycling"`
- Two columns: Line Number (70px) and Content (auto)
- Follow tail: in code-behind, subscribe to `VisibleLines.CollectionChanged`
  - When `IsFollowTail` is true and new items added → `ScrollIntoView(Items[^1])`

**App.xaml.cs:**
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    var settingsService = new SettingsService();
    var mainVm = new MainViewModel(settingsService);
    var window = new MainWindow { DataContext = mainVm };
    window.Show();

    // Handle command-line file argument
    if (e.Args.Length > 0 && File.Exists(e.Args[0]))
        mainVm.OpenFileCommand.Execute(e.Args[0]);
}
```

**Drag-and-drop in MainWindow.xaml.cs:**
```csharp
private async void Window_Drop(object sender, DragEventArgs e)
{
    if (e.Data.GetDataPresent(DataFormats.FileDrop))
    {
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (string file in files)
            await ViewModel.OpenFileTabAsync(file);
    }
}
```

---

## Step 4: Apply Dark Theme

Create `Themes/Dark.xaml` as shown in SKILL.md section 6.
Apply in `App.xaml`:
```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <ResourceDictionary Source="Themes/Dark.xaml"/>
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

---

## Step 5: Build and Test

```bash
dotnet build src/LogTailPro/LogTailPro.csproj
```

**Fix all warnings and errors before moving to Phase 2.**

### Manual Test Checklist
- [ ] App launches without errors
- [ ] File → Open shows dialog, opens file
- [ ] Log lines display with line numbers
- [ ] Dragging a .log file onto the window opens it
- [ ] Status bar shows filename, line count, size
- [ ] New lines appear when another process appends to the file
  - Test: `while ($true) { Add-Content test.log "Line $(Get-Date)"; Start-Sleep 1 }` in PowerShell
- [ ] Follow-tail keeps latest line visible
- [ ] Pressing F toggles follow-tail
- [ ] Files > 100MB open without freezing (loads tail only)

---

## Phase 1 Complete When
All checkboxes above are ticked. Then read `PHASE_2.md`.
