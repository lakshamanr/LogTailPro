# Performance Optimization — Keep Your UI Snappy

Ensure your WPF application remains responsive and smooth (60 FPS target).

---

## Virtualization — The Game Changer

### ListBox Virtualization

```xaml
<!-- WRONG: No virtualization (freezes with 1000+ items) -->
<ListBox ItemsSource="{Binding Items}"/>

<!-- RIGHT: Virtualized -->
<ListBox ItemsSource="{Binding Items}">
  <ListBox.ItemsPanel>
    <ItemsPanelTemplate>
      <VirtualizingStackPanel VirtualizationMode="Recycling"/>
    </ItemsPanelTemplate>
  </ListBox.ItemsPanel>
</ListBox>
```

**Key settings:**
- `VirtualizingStackPanel` - Only renders visible items
- `VirtualizationMode="Recycling"` - Reuses containers (more efficient)

---

### DataGrid Virtualization

```xaml
<!-- Enable row and column virtualization -->
<DataGrid ItemsSource="{Binding Items}"
          EnableRowVirtualization="True"
          EnableColumnVirtualization="True"
          VirtualizingPanel.IsVirtualizing="True"
          VirtualizingPanel.VirtualizationMode="Recycling">
</DataGrid>
```

**Impact:** 1000 items loads in <100ms (vs 10+ seconds without)

---

### ItemsControl Virtualization (Complex)

```xaml
<ItemsControl ItemsSource="{Binding Items}">
  <ItemsControl.ItemsPanel>
    <ItemsPanelTemplate>
      <VirtualizingStackPanel IsVirtualizing="True" VirtualizationMode="Recycling"/>
    </ItemsPanelTemplate>
  </ItemsControl.ItemsPanel>
</ItemsControl>
```

---

## Binding Performance

### Problem: Binding updates cause UI lag

```csharp
// WRONG: Update binding constantly (every 100ms)
public class ViewModel : INotifyPropertyChanged
{
    private string _value;
    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); } // Fires constantly
    }

    public void MonitorData()
    {
        while (true)
        {
            Thread.Sleep(100);
            Value = GetCurrentValue(); // UI updates 10x/sec (expensive!)
        }
    }
}

// RIGHT: Batch updates, increase update interval
public void MonitorData()
{
    while (true)
    {
        Thread.Sleep(1000); // Update only 1x/sec
        Value = GetCurrentValue();
    }
}

// OR: Only notify when value actually changes
private string _value;
public string Value
{
    get => _value;
    set
    {
        if (_value != value) // Only notify if changed
        {
            _value = value;
            OnPropertyChanged();
        }
    }
}
```

---

### Binding Mode Optimization

```xaml
<!-- WRONG: Two-way binding (more expensive) -->
<TextBlock Text="{Binding Data, Mode=TwoWay}"/>

<!-- RIGHT: One-way when possible -->
<TextBlock Text="{Binding Data, Mode=OneWay}"/>

<!-- ONE-TIME: For data that never changes -->
<TextBlock Text="{Binding Status, Mode=OneTime}"/>
```

**Impact:** OneWay ~30% faster than TwoWay, OneTime ~50% faster

---

## Rendering Performance

### Optimize Visual Tree Depth

```xaml
<!-- WRONG: Deeply nested (12 layers) -->
<Grid>
  <Grid>
    <StackPanel>
      <StackPanel>
        <Border>
          <Border>
            <ContentControl>
              <!-- Content buried 7 levels deep -->
            </ContentControl>
          </Border>
        </Border>
      </StackPanel>
    </StackPanel>
  </Grid>
</Grid>

<!-- RIGHT: Flat structure (3 layers) -->
<Grid>
  <Border>
    <!-- Content here -->
  </Border>
</Grid>
```

**Rule:** Keep visual tree depth < 5 levels. Each level = 20-30% performance hit.

---

### Avoid Layout Thrashing

```csharp
// WRONG: Multiple layout passes
public void UpdateUI()
{
    Element1.Width = 100;      // Layout pass 1
    Element2.Margin = new Thickness(10); // Layout pass 2
    Element3.Padding = new Thickness(5); // Layout pass 3
    // Total: 3 layout cycles
}

// RIGHT: Batch updates, then invalidate once
public void UpdateUI()
{
    Element1.Width = 100;
    Element2.Margin = new Thickness(10);
    Element3.Padding = new Thickness(5);
    // All changes applied together (1 layout cycle)
}
```

---

### Use Freezable Objects (Images, Brushes)

```xaml
<!-- WRONG: Creates new brush every render -->
<Border Background="#0078D4"/>

<!-- RIGHT: Freeze static resources -->
<Window.Resources>
  <SolidColorBrush x:Key="PrimaryBrush" Color="#0078D4" Freezable.IsFrozen="True"/>
</Window.Resources>

<Border Background="{StaticResource PrimaryBrush}"/>
```

**Impact:** Frozen objects ~2x faster rendering

---

## Deferred Loading

### Load Large Lists Asynchronously

```csharp
public async void LoadLargeDataSet()
{
    // Show loading state
    LoadingIndicator.Visibility = Visibility.Visible;
    
    // Load on background thread
    var items = await Task.Run(() =>
    {
        return FetchMillionRecords(); // 5-10 second operation
    });
    
    // Update UI on main thread
    ItemsSource = items;
    LoadingIndicator.Visibility = Visibility.Collapsed;
}
```

---

### Lazy Load on Scroll

```csharp
public class VirtualizedViewModel
{
    private ObservableCollection<Item> _items = new();
    
    public void OnScrollToBottom()
    {
        if (_items.Count % 100 == 0) // Load next batch
        {
            var nextBatch = Task.Run(() => FetchNextBatch(_items.Count, 100));
            _items.AddRange(nextBatch);
        }
    }
}
```

---

## Memory Management

### Dispose Resources Properly

```csharp
// WRONG: Memory leak - image never unloaded
public class MyViewModel
{
    public BitmapImage LargeImage { get; set; }
    
    public MyViewModel()
    {
        var uri = new Uri("very-large-image.jpg");
        LargeImage = new BitmapImage(uri);
        LargeImage.CacheOption = BitmapCacheOption.OnLoad;
        LargeImage.EndInit();
        // Image stays in memory forever
    }
}

// RIGHT: Unload when not needed
public class MyViewModel : IDisposable
{
    public BitmapImage LargeImage { get; set; }
    
    public void Dispose()
    {
        LargeImage = null;
        GC.Collect(); // Force cleanup of large objects
    }
}
```

---

### Unsubscribe from Events

```csharp
// WRONG: Memory leak
public MyWindow()
{
    InitializeComponent();
    DataService.DataChanged += OnDataChanged;
    // Event never unsubscribed - MyWindow stays in memory!
}

// RIGHT: Unsubscribe on close
public MyWindow()
{
    InitializeComponent();
    DataService.DataChanged += OnDataChanged;
    Closed += (s,e) => DataService.DataChanged -= OnDataChanged;
}
```

---

## Animation Performance

### GPU vs CPU Rendering

```xaml
<!-- WRONG: Animates Margin (CPU expensive) -->
<StackPanel Margin="0,0,0,0">
  <Button Content="Slide"/>
</StackPanel>

<!-- RIGHT: Animates RenderTransform (GPU accelerated) -->
<Button Content="Slide">
  <Button.RenderTransform>
    <TranslateTransform X="0"/>
  </Button.RenderTransform>
</Button>
```

**Performance difference:** GPU ~10x faster

---

### Disable Animations When Possible

```csharp
// Check if animations should be disabled
public class AnimationSettings
{
    public static bool AreAnimationsEnabled
    {
        get
        {
            // Disable if performance mode, high CPU load, or accessibility mode
            try
            {
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                var value = key?.GetValue("TaskbarAnimations", 1);
                return (int)value == 1;
            }
            catch { return true; }
        }
    }
}

// Usage
if (AnimationSettings.AreAnimationsEnabled)
{
    AnimateElement(MyElement, 300);
}
else
{
    MyElement.Opacity = 1; // Instant (no animation)
}
```

---

## Profiling & Diagnostics

### Use WPF Profiler

```csharp
// Built-in Visual Studio profiler
// Menu > Debug > Start Diagnostic Tools (Ctrl+Alt+F2)
// Measure:
// - Frame rate
// - CPU usage
// - Memory consumption
// - Event handling time
```

### Manual Performance Timing

```csharp
public class PerformanceMonitor
{
    public static void MeasureAction(string name, Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        Debug.WriteLine($"{name}: {sw.ElapsedMilliseconds}ms");
    }
}

// Usage
PerformanceMonitor.MeasureAction("Load data", () =>
{
    var items = LoadMillionItems();
});
// Output: Load data: 2543ms
```

---

## Performance Checklist

- [ ] DataGrid/ListBox virtualization enabled
- [ ] Binding mode set to OneWay (not TwoWay) where possible
- [ ] Visual tree depth < 5 levels
- [ ] No layout thrashing (batch property updates)
- [ ] Large images frozen or cached
- [ ] Events unsubscribed on window close
- [ ] Animations use RenderTransform (GPU)
- [ ] Deferred loading for large datasets
- [ ] No memory leaks (test with profiler)
- [ ] 60 FPS maintained (test while scrolling)
- [ ] Startup time < 2 seconds
- [ ] Responsive to user input (no lag)

---

## Performance Targets

| Metric | Target | Critical |
|--------|--------|----------|
| App startup | < 2 seconds | < 5 seconds |
| Page transition | < 300ms | < 1000ms |
| List scroll (1000 items) | 60 FPS | > 30 FPS |
| Data binding update | < 16ms | < 100ms |
| Modal open | < 200ms | < 500ms |
| Search response | < 500ms | < 2000ms |

---

## Summary

Professional WPF performance:
- ✅ Virtualization for lists 100+
- ✅ OneWay binding (TwoWay only when needed)
- ✅ Shallow visual tree (<5 levels)
- ✅ GPU-accelerated animations
- ✅ Deferred loading for large data
- ✅ 60 FPS frame rate
- ✅ < 2 second startup
