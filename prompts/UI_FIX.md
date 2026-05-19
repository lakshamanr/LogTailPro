# UI FIX — Full UI Overhaul

## Read First
Before touching a single XAML file, read the full UI_AGENT.md.
It contains the colour system, component specs, anti-patterns, and QA checklist.
Do NOT start coding until you have read it completely.

---

## What Went Wrong (Why This Fix Exists)

The current UI uses default WPF controls with no custom styling.
This means:
- Grey Windows 7-era toolbars, buttons, and scrollbars
- White backgrounds on ComboBox, TextBox, dialogs
- System blue selection highlights
- Raised/beveled button borders
- Thick, grey WPF scrollbars
- No coherent dark theme — just random dark backgrounds with white/grey controls poking through
- GridView column headers visible on the log viewer (they should be hidden)
- Tabs look like default IE tabs, not VS Code-style tabs

The fix must make this look like a professional developer tool.

---

## Step-by-Step Fix Order

### Step 1: Establish Design Tokens

Completely rewrite Themes/Dark.xaml with ALL tokens from UI_AGENT.md.
Do the same for Themes/Light.xaml.

Verify App.xaml merges the theme:
```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <ResourceDictionary Source="Themes/Dark.xaml"/>
    </ResourceDictionary.MergedDictionaries>
    <!-- Global converters registered here -->
    <local:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
    <local:FileStatusColorConverter x:Key="FileStatusColorConverter"/>
    <local:RuleToBackgroundConverter x:Key="RuleToBackgroundConverter"/>
    <local:RuleToForegroundConverter x:Key="RuleToForegroundConverter"/>
    <local:BoolToWrapConverter x:Key="BoolToWrapConverter"/>
    <local:NotEmptyConverter x:Key="NotEmptyConverter"/>
  </ResourceDictionary>
</Application.Resources>
```

Build. Fix any resource-not-found errors before continuing.

---

### Step 2: MainWindow.xaml

Apply the full window spec from UI_AGENT.md section "Window Chrome".

Rebuild the layout:
```xml
<Window Background="{StaticResource BgBase}"
        FontFamily="{StaticResource UiFont}"
        FontSize="{StaticResource UiSize}"
        Foreground="{StaticResource TextPrimary}"
        UseLayoutRounding="True"
        TextOptions.TextFormattingMode="Display"
        RenderOptions.ClearTypeHint="Enabled"
        AllowDrop="True">

  <DockPanel>
    <!-- 1. Menu -->
    <!-- 2. Toolbar -->
    <!-- 3. Tab strip (TabControl) -->
    <!-- 4. Status bar -->
  </DockPanel>
</Window>
```

Apply styled Menu from UI_AGENT.md.
Apply styled Toolbar from UI_AGENT.md (with ToolBar_Loaded to remove grip/overflow).
Apply styled StatusBar from UI_AGENT.md.

For the TabControl:
```xml
<TabControl DockPanel.Dock="Top"
            ItemsSource="{Binding Tabs}"
            SelectedItem="{Binding SelectedTab}"
            Background="{StaticResource BgBase}"
            BorderBrush="{StaticResource BorderNormal}"
            BorderThickness="0,0,0,1"
            Padding="0">
  <TabControl.ItemContainerStyle>
    <!-- Full custom TabItem style from UI_AGENT.md section 4 -->
  </TabControl.ItemContainerStyle>
  <TabControl.ContentTemplate>
    <DataTemplate DataType="{x:Type vm:LogTabViewModel}">
      <views:LogTabView/>
    </DataTemplate>
  </TabControl.ContentTemplate>
</TabControl>
```

---

### Step 3: LogTabView.xaml

Rewrite using the full log viewer spec from UI_AGENT.md.

Layout structure:
```xml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>  <!-- Search bar (collapsed by default) -->
    <RowDefinition Height="*"/>     <!-- Log viewer -->
    <RowDefinition Height="Auto"/>  <!-- Splitter -->
    <RowDefinition Height="Auto"/>  <!-- Search results table -->
    <!-- Loading overlay (absolute positioned) -->
  </Grid.RowDefinitions>

  <!-- Row 0: Search bar -->
  <!-- Row 1: Log ListView with full spec from UI_AGENT.md section 5 -->
  <!-- Row 2: GridSplitter (only visible when search results showing) -->
  <!-- Row 3: Search results table from UI_AGENT.md section 7 -->

  <!-- Absolute: Loading overlay -->
  <Grid Grid.RowSpan="4"
        Visibility="{Binding IsLoading, Converter={StaticResource BoolToVisibilityConverter}}"
        Background="#CC0D0D0D">
    <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
      <TextBlock Text="Loading file..." Foreground="{StaticResource TextSecondary}"
                 HorizontalAlignment="Center" FontSize="14"/>
      <ProgressBar Value="{Binding LoadProgress}" Maximum="100"
                   Width="280" Height="4" Margin="0,16,0,0"
                   Background="{StaticResource BgOverlay}"
                   Foreground="{StaticResource Accent}"
                   BorderThickness="0"/>
      <TextBlock Text="{Binding LoadProgress, StringFormat='{}{0}%'}"
                 Foreground="{StaticResource TextSecondary}"
                 HorizontalAlignment="Center" Margin="0,8,0,0"/>
    </StackPanel>
  </Grid>
</Grid>
```

Implement the search bar fade-in animation from UI_AGENT.md section 6.

---

### Step 4: Required New Converters

Create these converters if they don't exist:

```csharp
// Converters/FileStatusColorConverter.cs
public sealed class FileStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var brush = (FileStatus)value switch
        {
            FileStatus.Watching => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)), // green
            FileStatus.Error    => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)), // red
            FileStatus.Deleted  => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)), // red
            FileStatus.Paused   => new SolidColorBrush(Color.FromRgb(0xF5, 0x7F, 0x17)), // yellow
            _ => new SolidColorBrush(Colors.Gray)
        };
        return brush;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// Converters/RuleToBackgroundConverter.cs
public sealed class RuleToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not HighlightRule rule) return DependencyProperty.UnsetValue;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(rule.BackgroundColor);
            return new SolidColorBrush(color);
        }
        catch { return DependencyProperty.UnsetValue; }
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// Converters/BoolToWrapConverter.cs
public sealed class BoolToWrapConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (bool)value ? TextWrapping.Wrap : TextWrapping.NoWrap;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// Converters/NotEmptyConverter.cs
public sealed class NotEmptyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s);
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
```

---

### Step 5: Add Missing ViewModel Properties for UI

The UI spec requires these properties that may not exist yet:

```csharp
// In LogTabViewModel:
public string FileSizeText => FileSizeBytes switch
{
    < 1024 => $"{FileSizeBytes} B",
    < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
    < 1024 * 1024 * 1024 => $"{FileSizeBytes / (1024.0 * 1024):F1} MB",
    _ => $"{FileSizeBytes / (1024.0 * 1024 * 1024):F2} GB"
};

public string EncodingName => _logFile.Encoding.WebName.ToUpperInvariant();

public string FileStatusText => FileStatus switch
{
    FileStatus.Watching => "Watching",
    FileStatus.Paused   => "Paused",
    FileStatus.Error    => "Error",
    FileStatus.Deleted  => "Deleted",
    _ => "Unknown"
};
```

---

### Step 6: Restyle Dialogs

For HighlightRulesDialog.xaml and SettingsDialog.xaml:
- Window Background: BgElevated
- All TextBox controls: DarkTextBox style from UI_AGENT.md
- All ComboBox controls: DarkComboBox style from UI_AGENT.md
- OK button: PrimaryButton style from UI_AGENT.md
- Cancel button: secondary button (BgOverlay bg, TextPrimary fg)
- ListBox/ListView for rules: BgBase background, no white

---

### Step 7: Build and Visual QA

```bash
dotnet build src/LogTailPro/LogTailPro.csproj
```

Run through every item in the Visual QA Checklist at the bottom of UI_AGENT.md.
Do not commit until all checkboxes pass.

---

### Step 8: Git Commit

```bash
git add -A
git commit -m "UI overhaul: full dark theme, design system, professional developer tool aesthetic"
```

---

## If You Get Stuck on a Specific Component

Refer back to UI_AGENT.md for the full XAML spec of that component.
The spec is the authority. If there is a conflict between old XAML and UI_AGENT.md, UI_AGENT.md wins.
