# WPF Quick Reference for LogTailPro

## Data Binding Cheat Sheet

### Bind to ViewModel property
```xml
<TextBlock Text="{Binding MyProperty}"/>
```

### Two-way binding (for TextBox, ToggleButton)
```xml
<TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged, Mode=TwoWay}"/>
```

### Bind to command
```xml
<Button Command="{Binding MyCommand}" CommandParameter="{Binding SomeValue}"/>
```

### Bind from a nested DataContext (e.g. inside ItemsControl)
```xml
<Button Command="{Binding DataContext.ParentCommand,
                 RelativeSource={RelativeSource AncestorType=Window}}"/>
```

### Bind ListView SelectedItem
```xml
<ListView SelectedItem="{Binding SelectedLogLine, Mode=TwoWay}"/>
```

---

## CommunityToolkit.Mvvm Quick Reference

### ViewModel base + ObservableProperty
```csharp
[ObservableObject]
public partial class MyViewModel
{
    [ObservableProperty]
    private string _myProp = string.Empty;

    // Auto-called when _myProp changes
    partial void OnMyPropChanged(string value) { /* react */ }

    [RelayCommand]
    private async Task DoSomethingAsync()
    {
        // async command — auto-generates DoSomethingCommand
    }

    [RelayCommand(CanExecute = nameof(CanDoSomething))]
    private void DoOther() { }
    private bool CanDoSomething() => !string.IsNullOrEmpty(_myProp);
}
```

---

## Common WPF Patterns

### BoolToVisibilityConverter (built-in)
```xml
<!-- In Resources -->
<BooleanToVisibilityConverter x:Key="BoolToVisibilityConverter"/>

<!-- Usage -->
<Grid Visibility="{Binding IsVisible, Converter={StaticResource BoolToVisibilityConverter}}"/>
```

### Inverse bool (hidden when true)
```csharp
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (bool)value ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(...) => throw new NotImplementedException();
}
```

### SolidColorBrush from hex string
```csharp
var brush = new SolidColorBrush(
    (Color)ColorConverter.ConvertFromString("#FF4444"));
```

### Dispatcher.InvokeAsync (update UI from background thread)
```csharp
await Application.Current.Dispatcher.InvokeAsync(() =>
{
    MyCollection.Add(item);
}, DispatcherPriority.Background);
```

---

## FileSystemWatcher Gotchas

1. **Changed fires multiple times** — debounce with a Timer:
```csharp
private Timer? _debounceTimer;
private void OnFileChanged(object sender, FileSystemEventArgs e)
{
    _debounceTimer?.Dispose();
    _debounceTimer = new Timer(_ => ProcessChange(), null, 100, Timeout.Infinite);
}
```

2. **Changed may not fire for all writes** — also subscribe to `Renamed`

3. **EnableRaisingEvents must be true** after setting all properties

4. **InternalBufferSize** default is 8KB — increase for busy directories:
```csharp
_watcher.InternalBufferSize = 65536;
```

---

## WPF Performance

### VirtualizingStackPanel must-haves
```xml
VirtualizingPanel.IsVirtualizing="True"
VirtualizingPanel.VirtualizationMode="Recycling"
ScrollViewer.CanContentScroll="True"
```
Without `CanContentScroll="True"`, virtualization is disabled!

### Avoid these in item templates (forces measure of all items)
- `Grid` with `Auto` row/column — use `StackPanel` instead
- `TextBlock` with `TextWrapping="Wrap"` — disable unless user toggles it

### Batch ObservableCollection updates
Instead of adding items one-by-one (fires CollectionChanged per item):
```csharp
// Add all at once then replace
var newList = new ObservableCollection<LogLine>(allItems);
VisibleLines = newList;
```
Or suppress notifications:
```csharp
// Use a custom ObservableCollection with AddRange
public void AddRange(IEnumerable<T> items)
{
    foreach (var item in items) Items.Add(item);
    OnCollectionChanged(new NotifyCollectionChangedEventArgs(
        NotifyCollectionChangedAction.Reset));
}
```

---

## App.xaml Startup
```xml
<Application x:Class="LogTailPro.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="">  <!-- Leave empty — we create window in OnStartup -->
```

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    // Create dependencies, create window, show
}
```

---

## Testing File Tailing (PowerShell)

```powershell
# Generate a test log that grows forever
while ($true) {
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $level = @("INFO","WARN","ERROR","DEBUG") | Get-Random
    Add-Content -Path "test.log" -Value "[$ts] $level This is a test log message"
    Start-Sleep -Milliseconds 500
}
```

```powershell
# Generate a large static log file (200MB)
$sw = [System.IO.StreamWriter]::new("bigtest.log")
for ($i = 1; $i -le 2000000; $i++) {
    $level = @("INFO","WARN","ERROR","DEBUG")[$i % 4]
    $sw.WriteLine("[$i] 2024-01-01 00:00:00.000 $level Test log line $i with content")
}
$sw.Close()
```

```powershell
# Simulate log rotation (truncate file)
Clear-Content -Path "test.log"
```
