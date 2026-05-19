# WPF Troubleshooting — Common Issues & Solutions

Fix the 20 most common WPF design and layout problems.

---

## Layout & Spacing Issues

### Problem: Text too close to edge / insufficient padding

```xaml
<!-- WRONG -->
<TextBlock Text="Hello" Margin="0"/>

<!-- RIGHT -->
<TextBlock Text="Hello" Margin="16"/> <!-- or use Padding on parent -->
<Border Padding="16">
  <TextBlock Text="Hello"/>
</Border>
```

---

### Problem: Buttons not aligned properly

```xaml
<!-- WRONG: Using Margin -->
<Button Content="Save" Margin="0,5,0,0"/>
<Button Content="Cancel" Margin="0,5,0,0"/>

<!-- RIGHT: Using Spacing in StackPanel -->
<StackPanel Orientation="Horizontal" Spacing="8">
  <Button Content="Save"/>
  <Button Content="Cancel"/>
</StackPanel>
```

---

### Problem: Content not stretching to fill available space

```xaml
<!-- WRONG -->
<Grid>
  <ListBox Width="300" Height="500"/>
</Grid>

<!-- RIGHT: Use * to fill -->
<Grid>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="*"/>
  </Grid.ColumnDefinitions>
  <ListBox Grid.Column="0"/>
</Grid>
```

---

### Problem: StackPanel not resizing properly

```xaml
<!-- WRONG -->
<StackPanel>
  <TextBlock Text="Long text that should wrap" Width="100"/>
</StackPanel>

<!-- RIGHT: Set TextWrapping -->
<StackPanel>
  <TextBlock Text="Long text that should wrap" Width="100" TextWrapping="Wrap"/>
</StackPanel>
```

---

## Styling & Appearance Issues

### Problem: Button text/colors not showing

```xaml
<!-- WRONG: Conflicting styles -->
<Button Content="Click" Foreground="White" Background="White"/>

<!-- RIGHT: Sufficient contrast -->
<Button Content="Click" Foreground="White" Background="#0078D4"/>
```

---

### Problem: Focus rectangle not visible

```xaml
<!-- WRONG: Default focus hidden -->
<Style TargetType="Button">
  <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
</Style>

<!-- RIGHT: Visible focus indicator -->
<Style TargetType="Button">
  <Setter Property="FocusVisualStyle">
    <Setter.Value>
      <Style>
        <Setter Property="Control.Template">
          <Setter.Value>
            <ControlTemplate>
              <Border BorderBrush="#0078D4" BorderThickness="2" CornerRadius="4"/>
            </ControlTemplate>
          </Setter.Value>
        </Setter>
      </Style>
    </Setter.Value>
  </Setter>
</Style>
```

---

### Problem: Borders look harsh/pixelated

```xaml
<!-- WRONG: Sharp corners, heavy border -->
<Border BorderBrush="Black" BorderThickness="3" CornerRadius="0">
  <TextBlock Text="Content"/>
</Border>

<!-- RIGHT: Soft, professional -->
<Border BorderBrush="#D0D0D0" BorderThickness="1" CornerRadius="8">
  <TextBlock Text="Content" Padding="16"/>
</Border>
```

---

### Problem: Text too small / hard to read

```xaml
<!-- WRONG -->
<TextBlock Text="Body text" FontSize="10"/>

<!-- RIGHT -->
<TextBlock Text="Body text" FontSize="14"/>
```

Minimum 12px for captions, 14px for body text.

---

## Data & List Issues

### Problem: DataGrid slow with 1000+ items

```xaml
<!-- WRONG: No virtualization -->
<DataGrid ItemsSource="{Binding Items}"/>

<!-- RIGHT: Enable virtualization -->
<DataGrid ItemsSource="{Binding Items}" 
          EnableRowVirtualization="True"
          EnableColumnVirtualization="True"
          VirtualizingPanel.IsVirtualizing="True"/>
```

---

### Problem: ListBox scrolling jerky

```xaml
<!-- WRONG: No virtualization -->
<ListBox ItemsSource="{Binding LargeList}"/>

<!-- RIGHT: Enable virtualization -->
<ListBox ItemsSource="{Binding LargeList}">
  <ListBox.ItemsPanel>
    <ItemsPanelTemplate>
      <VirtualizingStackPanel VirtualizationMode="Recycling"/>
    </ItemsPanelTemplate>
  </ListBox.ItemsPanel>
</ListBox>
```

---

### Problem: DataGrid columns too narrow / overlapping

```xaml
<!-- WRONG: Fixed widths -->
<DataGrid.Columns>
  <DataGridTextColumn Header="Name" Width="100"/>
  <DataGridTextColumn Header="Email" Width="100"/>
</DataGrid.Columns>

<!-- RIGHT: Use * for flexible sizing -->
<DataGrid.Columns>
  <DataGridTextColumn Header="Name" Width="*" MinWidth="150"/>
  <DataGridTextColumn Header="Email" Width="2*" MinWidth="200"/>
</DataGrid.Columns>
```

---

## Modal & Dialog Issues

### Problem: Modal doesn't appear / behind other windows

```csharp
// WRONG: Not setting owner
var dialog = new Window();
dialog.Show();

// RIGHT: Set owner and modal
var dialog = new Window();
dialog.Owner = this;
dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
dialog.ShowDialog(); // Modal (blocks parent)
```

---

### Problem: Backdrop doesn't cover entire window

```xaml
<!-- WRONG: Fixed size -->
<Border Name="Backdrop" Width="500" Height="500" Background="Black" Opacity="0.3"/>

<!-- RIGHT: Fill entire window -->
<Border Name="Backdrop" Background="Black" Opacity="0.3"/>
<!-- (Parent Grid will size this) -->
```

---

## Binding & Data Issues

### Problem: Binding not updating UI

```csharp
// WRONG: Plain property
public string Name { get; set; }

// RIGHT: INotifyPropertyChanged
public class ViewModel : INotifyPropertyChanged
{
    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string prop = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
```

---

### Problem: Converter not being called

```xaml
<!-- WRONG: Missing namespace -->
<TextBlock Text="{Binding Date, Converter={StaticResource DateConverter}}"/>

<!-- RIGHT: Define converter in resources -->
<Window.Resources>
    <local:DateFormatConverter x:Key="DateConverter"/>
</Window.Resources>

<TextBlock Text="{Binding Date, Converter={StaticResource DateConverter}}"/>
```

---

## Performance Issues

### Problem: App freezes when loading data

```csharp
// WRONG: Blocking UI thread
public void LoadData()
{
    var data = FetchDataFromServer(); // 5 second wait
    ItemsSource = data;
}

// RIGHT: Load on background thread
public async void LoadData()
{
    var data = await Task.Run(() => FetchDataFromServer());
    ItemsSource = data;
}
```

---

### Problem: Memory leaks after closing windows

```csharp
// WRONG: Event handlers not unsubscribed
public MyWindow()
{
    InitializeComponent();
    SomeService.DataChanged += OnDataChanged;
}

// RIGHT: Unsubscribe on close
public MyWindow()
{
    InitializeComponent();
    SomeService.DataChanged += OnDataChanged;
    Closed += (s,e) => SomeService.DataChanged -= OnDataChanged;
}
```

---

## Color & Theme Issues

### Problem: Colors too bright / hurts eyes

```xaml
<!-- WRONG: Pure colors -->
<TextBlock Text="Text" Foreground="#FF0000"/> <!-- Pure red -->

<!-- RIGHT: Muted colors -->
<TextBlock Text="Text" Foreground="#C50E1F"/> <!-- Professional red -->
```

---

### Problem: Text not readable in dark theme

```xaml
<!-- WRONG: Dark text on dark background -->
<Border Background="#2D2D2D">
  <TextBlock Text="Dark text" Foreground="#161616"/>
</Border>

<!-- RIGHT: Light text on dark background -->
<Border Background="#2D2D2D">
  <TextBlock Text="Light text" Foreground="#FFFFFF"/>
</Border>
```

---

## Mouse & Interaction Issues

### Problem: Buttons don't feel clickable / no cursor change

```xaml
<!-- WRONG: No visual feedback -->
<Button Content="Click me"/>

<!-- RIGHT: Clear visual feedback -->
<Button Content="Click me" Cursor="Hand"
        Background="#0078D4" Foreground="White" Padding="16,10"/>
```

---

### Problem: Double-click not working

```csharp
// WRONG: Using single click
<Button Click="Button_Click"/>

// RIGHT: Use MouseDoubleClick
<Border MouseDoubleClick="Border_MouseDoubleClick">
  <!-- Content -->
</Border>

// Code-behind
private void Border_MouseDoubleClick(object sender, MouseButtonEventArgs e)
{
    // Handle double click
}
```

---

## Quick Diagnosis Checklist

**When UI looks "wrong":**

1. **Spacing**: Is padding/margin consistent? (Use 8px multiples)
2. **Colors**: Do colors meet contrast ratio? (Test with WAVE)
3. **Alignment**: Are elements aligned to grid?
4. **Typography**: Correct font size? (14px for body min)
5. **Borders**: Too thick or harsh? (Use 1px, soft gray)
6. **States**: Button hover/focus states working?
7. **Performance**: Smooth 60 FPS? (Profile with Performance Monitor)
8. **Responsive**: Works on narrow and wide windows?
9. **Accessibility**: Focus visible? Tab order correct?
10. **Testing**: Tested on actual machine (not just designer)?

---

## Summary

Most WPF design issues come from:
- Inconsistent spacing (fix: use 8px grid)
- Poor contrast (fix: use color checker)
- Missing focus indicators (fix: define FocusVisualStyle)
- Performance (fix: enable virtualization)
- Conflicting styles (fix: centralize in App.xaml)

Reference this guide when something "doesn't look right."
