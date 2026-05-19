# LogTailPro — UI Agent (v2 — corrected from real screenshot)

## CRITICAL CORRECTIONS FROM SCREENSHOT

The previous UI_AGENT had 5 major layout errors. Fix these first:

| What was wrong | What is correct |
|---|---|
| Search bar at TOP of log view | Search bar is at the BOTTOM of the window |
| Search results split pane in middle | Results table is BELOW the search bar, at very bottom |
| 4-column results table (Timestamp/Line#/Text/Groups) | Only 2 columns: Line# and Text |
| Line numbers as a separate ListView column | NO separate line number column — log is plain full-width text |
| Follow_Tail as toolbar toggle button | Follow_Tail is a CHECKBOX directly on the toolbar |

---

## Aesthetic Direction

**"Dense Developer Tool — Faithful BareTailPro"**
- Black background log area (#000000 or #0A0A10)
- Monospace log content, full width, no gutter
- Everything else is dark panels with flat controls
- No rounded-corner cards, no VS Code side panels, no modern SaaS feel
- Faithful to the original: compact, information-dense, utilitarian

---

## Colour System

```
Log area background:      #000000  (pure black)
Panel/toolbar background: #1A1A2A  (very dark blue-grey)
Elevated surface:         #252535  (menu bar)
Input background:         #111118  (text boxes, file path)
Active tab background:    #0D0D1A  (same as log area)
Inactive tab background:  #1E1E2E

Border default:           #444455
Border subtle:            #333344
Accent:                   #0078D4  (Windows blue, active tab border, Search button)

Text primary:             #D4D4D4
Text secondary:           #888899
Text muted:               #666677
Text log default:         #5A9A5A  (green — matches BareTailPro default)

Search results row:       #6AAFFF  (light blue, matching lines in results)
```

---

## Window Layout — Top to Bottom (In Exact Order)

```
1. TITLE BAR          [OS chrome — "filename (size) — BareTailPro"]
2. MENU BAR           [File | Edit | View | Preferences | Help]          ~18px
3. TOOLBAR            [Open][Highlighting cb][Follow_Tail cb][ANSI▾][filepath...]  ~26px
4. TAB STRIP          [tab1 x][tab2 x][tab3 x][active tab x]             ~22px
5. LOG VIEW           [plain monospace text, full width, BLACK bg]        flex *
6. SEARCH BAR         [Text▾][input...][Search][Stop][Pause][Clear]       ~28px
                      [FilterTail cb][Found N lines.][RegexSyntax][IgnoreCase][Add]
7. SEARCH RESULTS     [Line | Text] table — only 2 columns                ~120px
```

**There is NO status bar.** BareTailPro does not show one. Remove it.
**There is NO stats panel on the right.** Remove it.
**The log view has no left-side line number gutter.**

---

## Component 1: Menu Bar

```xml
<Menu DockPanel.Dock="Top"
      Background="#252535"
      Foreground="#D4D4D4"
      BorderThickness="0,0,0,1"
      BorderBrush="#444455">
  <Menu.Resources>
    <Style TargetType="MenuItem">
      <Setter Property="Background" Value="Transparent"/>
      <Setter Property="Foreground" Value="#D4D4D4"/>
      <Setter Property="Padding" Value="10,4"/>
      <Style.Triggers>
        <Trigger Property="IsHighlighted" Value="True">
          <Setter Property="Background" Value="#2E2E3E"/>
        </Trigger>
      </Style.Triggers>
    </Style>
  </Menu.Resources>
  <MenuItem Header="_File">
    <MenuItem Header="_Open..."     Command="{Binding OpenFileCommand}" InputGestureText="Ctrl+O"/>
    <MenuItem Header="Recent Files" ItemsSource="{Binding Settings.RecentFiles}"/>
    <Separator/>
    <MenuItem Header="E_xit"        Command="{Binding ExitCommand}"/>
  </MenuItem>
  <MenuItem Header="_Edit">
    <MenuItem Header="_Copy"    Command="{Binding SelectedTab.CopySelectedCommand}" InputGestureText="Ctrl+C"/>
    <MenuItem Header="Select _All" Command="{Binding SelectedTab.SelectAllCommand}" InputGestureText="Ctrl+A"/>
  </MenuItem>
  <MenuItem Header="_View">
    <MenuItem Header="Line Wrap"   Command="{Binding SelectedTab.ToggleLineWrapCommand}"/>
    <MenuItem Header="Tab Position">
      <MenuItem Header="Top"    Command="{Binding SetTabPlacementCommand}" CommandParameter="Top"/>
      <MenuItem Header="Bottom" Command="{Binding SetTabPlacementCommand}" CommandParameter="Bottom"/>
      <MenuItem Header="Left"   Command="{Binding SetTabPlacementCommand}" CommandParameter="Left"/>
      <MenuItem Header="Right"  Command="{Binding SetTabPlacementCommand}" CommandParameter="Right"/>
    </MenuItem>
  </MenuItem>
  <MenuItem Header="_Preferences">
    <MenuItem Header="Highlight Rules..." Command="{Binding OpenHighlightRulesCommand}"/>
    <MenuItem Header="Settings..."        Command="{Binding OpenSettingsCommand}"/>
  </MenuItem>
  <MenuItem Header="_Help">
    <MenuItem Header="About LogTailPro"/>
  </MenuItem>
</Menu>
```

---

## Component 2: Toolbar

Exact items from screenshot, in exact order:

```xml
<ToolBar DockPanel.Dock="Top"
         Background="#1A1A2A"
         Height="28"
         Loaded="ToolBar_Loaded">

  <!-- Open button -->
  <Button Content="Open"
          Command="{Binding OpenFileCommand}"
          Style="{StaticResource FlatToolbarButton}"/>

  <Separator/>

  <!-- Highlighting checkbox — not a toggle button -->
  <CheckBox Content="Highlighting"
            IsChecked="{Binding IsHighlightingEnabled}"
            Foreground="#D4D4D4"
            VerticalAlignment="Center"/>

  <!-- Follow_Tail checkbox — directly on toolbar, per screenshot -->
  <CheckBox Content="Follow_Tail"
            IsChecked="{Binding SelectedTab.IsFollowTail}"
            Foreground="#D4D4D4"
            Margin="8,0,0,0"
            VerticalAlignment="Center"/>

  <!-- Encoding selector -->
  <ComboBox Width="52"
            SelectedValue="{Binding SelectedTab.EncodingName}"
            Style="{StaticResource DarkComboBox}"
            Margin="8,0,0,0">
    <ComboBoxItem Content="ANSI"/>
    <ComboBoxItem Content="UTF-8"/>
    <ComboBoxItem Content="UTF-16"/>
    <ComboBoxItem Content="ASCII"/>
  </ComboBox>

  <!-- File path display — read-only, fills remaining space -->
  <TextBox IsReadOnly="True"
           Text="{Binding SelectedTab.FilePathWithSize}"
           Background="#111118"
           Foreground="#666677"
           BorderBrush="#333344"
           BorderThickness="1"
           Padding="4,2"
           FontFamily="Segoe UI"
           FontSize="11"
           Width="340"
           Margin="6,0,0,0"/>
</ToolBar>
```

Remove toolbar grip and overflow:
```csharp
private void ToolBar_Loaded(object sender, RoutedEventArgs e)
{
    if (sender is not ToolBar tb) return;
    if (tb.Template.FindName("OverflowGrid", tb) is FrameworkElement ov)
        ov.Visibility = Visibility.Collapsed;
    if (tb.Template.FindName("MainPanelBorder", tb) is FrameworkElement mp)
        mp.Margin = new Thickness(0);
}
```

Add to LogTabViewModel:
```csharp
public string FilePathWithSize =>
    $"{_logFile.FilePath} ({FileSizeText})";
```

---

## Component 3: Tab Strip

Tabs are horizontal at the top. Active tab has no special color indicator line —
it just uses a slightly brighter background and a thin accent border.
Close X is always visible (not hover-only, per screenshot).

```xml
<TabControl DockPanel.Dock="Top"
            Background="#0D0D1A"
            BorderBrush="#444455"
            BorderThickness="0,0,0,1"
            Padding="0"
            TabStripPlacement="{Binding Settings.TabStripPlacement}">

  <TabControl.Resources>
    <Style TargetType="TabItem">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="TabItem">
            <Border x:Name="border"
                    Background="#1E1E2E"
                    BorderBrush="#444455"
                    BorderThickness="1,1,1,0"
                    Padding="8,0,4,0"
                    MinWidth="80" MaxWidth="200">
              <StackPanel Orientation="Horizontal" Height="22" VerticalAlignment="Center">
                <TextBlock x:Name="label"
                           Text="{Binding FileName}"
                           Foreground="#888899"
                           VerticalAlignment="Center"
                           TextTrimming="CharacterEllipsis"
                           FontFamily="Segoe UI"
                           FontSize="11"/>
                <!-- Close button — always visible per screenshot -->
                <Button Content="x"
                        Margin="5,0,0,0"
                        Width="14" Height="14"
                        FontSize="9"
                        Foreground="#666677"
                        Background="Transparent"
                        BorderThickness="0"
                        VerticalAlignment="Center"
                        Command="{Binding DataContext.CloseTabCommand,
                                 RelativeSource={RelativeSource AncestorType=Window}}"
                        CommandParameter="{Binding}"/>
              </StackPanel>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsSelected" Value="True">
                <Setter TargetName="border" Property="Background" Value="#0D0D1A"/>
                <Setter TargetName="border" Property="BorderBrush" Value="#0078D4"/>
                <Setter TargetName="label" Property="Foreground" Value="#D4D4D4"/>
              </Trigger>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="border" Property="Background" Value="#252535"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </TabControl.Resources>
</TabControl>
```

---

## Component 4: Log View (CRITICAL — No Line Number Column)

The log view is plain monospace text, full width, BLACK background.
There is NO separate line number GridView column.
Line numbers appear in the text itself (many log formats already include them).

```xml
<ListView x:Name="LogListView"
          Background="#000000"
          Foreground="#5A9A5A"
          BorderThickness="0"
          FontFamily="Consolas"
          FontSize="13"
          VirtualizingPanel.IsVirtualizing="True"
          VirtualizingPanel.VirtualizationMode="Recycling"
          VirtualizingPanel.CacheLengthUnit="Item"
          VirtualizingPanel.CacheLength="200,200"
          ScrollViewer.CanContentScroll="True"
          ScrollViewer.HorizontalScrollBarVisibility="Auto"
          SelectionMode="Extended">

  <!-- Hide GridView column headers entirely -->
  <ListView.Resources>
    <Style TargetType="GridViewColumnHeader">
      <Setter Property="Visibility" Value="Collapsed"/>
      <Setter Property="Height" Value="0"/>
    </Style>
    <!-- Thin dark scrollbar -->
    <Style TargetType="ScrollBar">
      <Setter Property="Width" Value="6"/>
      <Setter Property="Background" Value="#1A1A2A"/>
    </Style>
  </ListView.Resources>

  <ListView.ItemContainerStyle>
    <Style TargetType="ListViewItem">
      <Setter Property="Padding" Value="4,0"/>
      <Setter Property="Margin" Value="0"/>
      <Setter Property="BorderThickness" Value="0"/>
      <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
      <!-- Highlight rule colors applied here -->
      <Setter Property="Background"
              Value="{Binding MatchedRule,
                     Converter={StaticResource RuleToBackgroundConverter}}"/>
      <Setter Property="Foreground"
              Value="{Binding MatchedRule,
                     Converter={StaticResource RuleToForegroundConverter}}"/>
      <Style.Triggers>
        <Trigger Property="IsSelected" Value="True">
          <Setter Property="Background" Value="#1A2A4A"/>
          <Setter Property="Foreground" Value="#FFFFFF"/>
        </Trigger>
        <Trigger Property="IsMouseOver" Value="True">
          <Setter Property="Background" Value="#0F0F20"/>
        </Trigger>
        <!-- No rule match = default green text on black -->
        <DataTrigger Binding="{Binding MatchedRule}" Value="{x:Null}">
          <Setter Property="Foreground" Value="#5A9A5A"/>
        </DataTrigger>
      </Style.Triggers>
    </Style>
  </ListView.ItemContainerStyle>

  <!-- SINGLE column — no line number column -->
  <ListView.View>
    <GridView>
      <GridViewColumn Width="4000">
        <GridViewColumn.CellTemplate>
          <DataTemplate>
            <TextBlock Text="{Binding Text}"
                       FontFamily="Consolas"
                       FontSize="13"
                       TextWrapping="{Binding DataContext.IsLineWrapEnabled,
                                     RelativeSource={RelativeSource AncestorType=Window},
                                     Converter={StaticResource BoolToWrapConverter}}"/>
          </DataTemplate>
        </GridViewColumn.CellTemplate>
      </GridViewColumn>
    </GridView>
  </ListView.View>
</ListView>
```

---

## Component 5: Search Bar (AT THE BOTTOM — DockPanel.Dock="Bottom")

This is the MOST IMPORTANT correction. The search bar is docked to the BOTTOM,
not placed above the log view.

Exact controls from screenshot, left to right:
1. [Text▾] — dropdown to switch between Text mode and Regex mode
2. [search input field]
3. [Search] button — green tinted
4. [Stop] button
5. [Pause] button
6. [Clear] button
7. [Filter Tail] checkbox
8. "Found N matching lines." text
9. — (gap)
10. [+] [Add] button on far right
11. [Regex Syntax] checkbox
12. [Ignore Case] checkbox
13. [Invert Mat...] checkbox (Invert Match)

```xml
<!-- IMPORTANT: DockPanel.Dock="Bottom" — below the log view -->
<Border DockPanel.Dock="Bottom"
        Background="#1A1A2A"
        BorderBrush="#555566"
        BorderThickness="0,1,0,0"
        Height="30"
        Visibility="{Binding SelectedTab.IsSearchPanelVisible,
                     Converter={StaticResource BoolToVisibilityConverter}}">

  <DockPanel Margin="4,0" LastChildFill="False">

    <!-- Left side -->
    <!-- Mode selector: Text vs Regex -->
    <ComboBox DockPanel.Dock="Left" Width="50"
              SelectedValue="{Binding SelectedTab.IsRegexMode}"
              SelectedValuePath="Tag"
              Style="{StaticResource DarkComboBox}"
              VerticalAlignment="Center" Margin="0,0,3,0">
      <ComboBoxItem Content="Text"  Tag="False"/>
      <ComboBoxItem Content="Regex" Tag="True"/>
    </ComboBox>

    <!-- Search input -->
    <TextBox DockPanel.Dock="Left"
             Width="140"
             Text="{Binding SelectedTab.SearchText, UpdateSourceTrigger=PropertyChanged}"
             Background="#111118"
             Foreground="#D4D4D4"
             CaretBrush="#D4D4D4"
             BorderBrush="#444455"
             BorderThickness="1"
             Padding="4,3"
             FontFamily="Consolas"
             FontSize="12"
             VerticalContentAlignment="Center"
             VerticalAlignment="Center"
             Margin="0,0,3,0">
      <TextBox.Style>
        <Style TargetType="TextBox">
          <Style.Triggers>
            <Trigger Property="IsFocused" Value="True">
              <Setter Property="BorderBrush" Value="#0078D4"/>
            </Trigger>
            <DataTrigger Binding="{Binding SelectedTab.SearchError,
                         Converter={StaticResource NotEmptyConverter}}" Value="True">
              <Setter Property="BorderBrush" Value="#CC3333"/>
            </DataTrigger>
          </Style.Triggers>
        </Style>
      </TextBox.Style>
    </TextBox>

    <!-- Search button — green tinted per screenshot -->
    <Button DockPanel.Dock="Left"
            Content="Search"
            Command="{Binding SelectedTab.ExecuteSearchCommand}"
            Background="#1A3A1A"
            Foreground="#88CC88"
            BorderBrush="#3A6A3A"
            BorderThickness="1"
            Padding="6,3"
            VerticalAlignment="Center"
            Margin="0,0,3,0"
            Style="{StaticResource FlatSearchButton}"/>

    <!-- Stop -->
    <Button DockPanel.Dock="Left" Content="Stop"
            Command="{Binding SelectedTab.StopSearchCommand}"
            Style="{StaticResource FlatToolbarButton}"
            Margin="0,0,3,0"/>

    <!-- Pause -->
    <Button DockPanel.Dock="Left" Content="Pause"
            Command="{Binding SelectedTab.PauseSearchCommand}"
            Style="{StaticResource FlatToolbarButton}"
            Margin="0,0,3,0"/>

    <!-- Clear -->
    <Button DockPanel.Dock="Left" Content="Clear"
            Command="{Binding SelectedTab.ClearSearchCommand}"
            Style="{StaticResource FlatToolbarButton}"
            Margin="0,0,6,0"/>

    <!-- Filter Tail checkbox -->
    <CheckBox DockPanel.Dock="Left"
              Content="Filter Tail"
              IsChecked="{Binding SelectedTab.IsFilterTailMode}"
              Foreground="#D4D4D4"
              VerticalAlignment="Center"
              Margin="0,0,8,0"/>

    <!-- Found count -->
    <TextBlock DockPanel.Dock="Left"
               Text="{Binding SelectedTab.MatchCount, StringFormat='Found {0} matching lines.'}"
               Foreground="#AAAAAA"
               FontSize="11"
               VerticalAlignment="Center"
               Margin="0,0,8,0"/>

    <!-- Right side items (DockPanel.Dock="Right" — order matters, reversed) -->
    <!-- Add button — far right -->
    <Button DockPanel.Dock="Right"
            Content="Add"
            Command="{Binding SelectedTab.AddSavedPatternCommand}"
            Background="#0078D4"
            Foreground="White"
            BorderThickness="0"
            Padding="8,3"
            VerticalAlignment="Center"
            Margin="3,0,0,0"/>

    <!-- Invert Match -->
    <CheckBox DockPanel.Dock="Right"
              Content="Invert Mat."
              IsChecked="{Binding SelectedTab.IsInvertMatch}"
              Foreground="#D4D4D4"
              VerticalAlignment="Center"
              Margin="0,0,6,0"/>

    <!-- Ignore Case -->
    <CheckBox DockPanel.Dock="Right"
              Content="Ignore Case"
              IsChecked="{Binding SelectedTab.IsIgnoreCase}"
              Foreground="#D4D4D4"
              VerticalAlignment="Center"
              Margin="0,0,6,0"/>

    <!-- Regex Syntax -->
    <CheckBox DockPanel.Dock="Right"
              Content="Regex Syntax"
              IsChecked="{Binding SelectedTab.IsRegexMode}"
              Foreground="#D4D4D4"
              VerticalAlignment="Center"
              Margin="0,0,6,0"/>

  </DockPanel>
</Border>
```

New ViewModel properties needed:
```csharp
[ObservableProperty] private bool _isFilterTailMode = false;  // Filter Tail checkbox
[ObservableProperty] private bool _isIgnoreCase = false;      // separate from IsRegexMode
[ObservableProperty] private bool _isInvertMatch = false;     // Invert Match

// IsCaseSensitive is now driven by IsIgnoreCase:
public bool IsCaseSensitive => !IsIgnoreCase;
```

---

## Component 6: Search Results Table (2 columns: Line# and Text ONLY)

```xml
<!-- DockPanel.Dock="Bottom" — below the search bar -->
<ListView DockPanel.Dock="Bottom"
          ItemsSource="{Binding SelectedTab.SearchResults}"
          Background="#0D0D18"
          Foreground="#6AAFFF"
          BorderBrush="#444455"
          BorderThickness="0,1,0,0"
          FontFamily="Consolas"
          FontSize="12"
          MaxHeight="150"
          Visibility="{Binding SelectedTab.IsSearchResultsVisible,
                       Converter={StaticResource BoolToVisibilityConverter}}">

  <ListView.Resources>
    <Style TargetType="GridViewColumnHeader">
      <Setter Property="Background" Value="#1A1A2E"/>
      <Setter Property="Foreground" Value="#888899"/>
      <Setter Property="BorderBrush" Value="#444455"/>
      <Setter Property="BorderThickness" Value="0,0,1,1"/>
      <Setter Property="Padding" Value="6,2"/>
      <Setter Property="FontFamily" Value="Segoe UI"/>
      <Setter Property="FontSize" Value="11"/>
      <Setter Property="HorizontalContentAlignment" Value="Left"/>
    </Style>
  </ListView.Resources>

  <ListView.ItemContainerStyle>
    <Style TargetType="ListViewItem">
      <Setter Property="Padding" Value="0"/>
      <Setter Property="BorderThickness" Value="0"/>
      <Setter Property="Background" Value="Transparent"/>
      <Setter Property="Foreground" Value="#6AAFFF"/>
      <Style.Triggers>
        <Trigger Property="IsSelected" Value="True">
          <Setter Property="Background" Value="#1A2A4A"/>
          <Setter Property="Foreground" Value="#FFFFFF"/>
        </Trigger>
        <Trigger Property="IsMouseOver" Value="True">
          <Setter Property="Background" Value="#0F1525"/>
        </Trigger>
      </Style.Triggers>
    </Style>
  </ListView.ItemContainerStyle>

  <ListView.View>
    <GridView>
      <!-- Column 1: Line number — narrow, right-aligned -->
      <GridViewColumn Header="Line" Width="60">
        <GridViewColumn.CellTemplate>
          <DataTemplate>
            <TextBlock Text="{Binding LineNumber}"
                       TextAlignment="Right"
                       Foreground="#888899"
                       FontFamily="Consolas"
                       FontSize="12"/>
          </DataTemplate>
        </GridViewColumn.CellTemplate>
      </GridViewColumn>
      <!-- Column 2: Text — full remaining width -->
      <GridViewColumn Header="Text" Width="2000">
        <GridViewColumn.CellTemplate>
          <DataTemplate>
            <TextBlock Text="{Binding Text}"
                       FontFamily="Consolas"
                       FontSize="12"
                       TextTrimming="CharacterEllipsis"/>
          </DataTemplate>
        </GridViewColumn.CellTemplate>
      </GridViewColumn>
    </GridView>
  </ListView.View>
</ListView>
```

NOTE: SearchResultRow only needs LineNumber and Text for the results table.
Timestamp and capturing groups are NOT shown in the table (not in BareTailPro).
Keep them on the model for future extension, but do not display them.

---

## MainWindow.xaml Full DockPanel Order

```xml
<DockPanel>
  <!-- 1. Menu — top -->
  <Menu DockPanel.Dock="Top" .../>

  <!-- 2. Toolbar — top -->
  <ToolBar DockPanel.Dock="Top" .../>

  <!-- 3. Tab strip — top -->
  <TabControl DockPanel.Dock="Top" .../>

  <!-- 4. Search results — BOTTOM (must dock before search bar) -->
  <ListView DockPanel.Dock="Bottom" ... />  <!-- results table -->

  <!-- 5. Search bar — BOTTOM (above results, below log) -->
  <Border DockPanel.Dock="Bottom" ... />    <!-- search bar -->

  <!-- 6. Log view — fills remaining space (LastChild) -->
  <ContentControl Content="{Binding SelectedTab}">
    <ContentControl.ContentTemplate>
      <DataTemplate DataType="{x:Type vm:LogTabViewModel}">
        <views:LogTabView/>
      </DataTemplate>
    </ContentControl.ContentTemplate>
  </ContentControl>
</DockPanel>
```

IMPORTANT: In DockPanel, Bottom-docked items must be declared before the
LastChild (the log view). DockPanel processes children top to bottom in source order —
if log view is declared before the search bar, the search bar gets no space.

---

## LogTabView.xaml Structure

LogTabView is ONLY the log ListView. The search bar and results table live
in MainWindow, not inside each tab. This matches BareTailPro's behavior
where search applies to the currently active tab.

```xml
<Grid>
  <!-- Loading overlay -->
  <Grid Visibility="{Binding IsLoading, Converter={StaticResource BoolToVisibilityConverter}}"
        Background="#CC000000"
        Panel.ZIndex="100">
    <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
      <TextBlock Text="Loading file..." Foreground="#AAAAAA"
                 HorizontalAlignment="Center" FontSize="14" FontFamily="Segoe UI"/>
      <ProgressBar Value="{Binding LoadProgress}" Maximum="100"
                   Width="280" Height="4" Margin="0,16,0,0"
                   Background="#2A2A3A" Foreground="#0078D4" BorderThickness="0"/>
      <TextBlock Text="{Binding LoadProgress, StringFormat='{}{0}%'}"
                 Foreground="#888899" HorizontalAlignment="Center"
                 Margin="0,8,0,0" FontFamily="Segoe UI" FontSize="11"/>
    </StackPanel>
  </Grid>

  <!-- Log ListView (full spec above) -->
  <ListView x:Name="LogListView" ... />
</Grid>
```

---

## Button Styles

```xml
<Style x:Key="FlatToolbarButton" TargetType="Button">
  <Setter Property="Background" Value="Transparent"/>
  <Setter Property="Foreground" Value="#D4D4D4"/>
  <Setter Property="BorderBrush" Value="#444455"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="Padding" Value="7,3"/>
  <Setter Property="FontFamily" Value="Segoe UI"/>
  <Setter Property="FontSize" Value="11"/>
  <Setter Property="VerticalAlignment" Value="Center"/>
  <Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Border Background="{TemplateBinding Background}"
                BorderBrush="{TemplateBinding BorderBrush}"
                BorderThickness="{TemplateBinding BorderThickness}"
                Padding="{TemplateBinding Padding}"
                CornerRadius="2">
          <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="#2A2A3A"/>
          </Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Setter Property="Background" Value="#1A1A2A"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

---

## CheckBox Dark Style

```xml
<Style TargetType="CheckBox">
  <Setter Property="Foreground" Value="#D4D4D4"/>
  <Setter Property="VerticalAlignment" Value="Center"/>
  <Setter Property="FontFamily" Value="Segoe UI"/>
  <Setter Property="FontSize" Value="11"/>
</Style>
```

---

## Theme Files

### Themes/Dark.xaml
```xml
<ResourceDictionary>
  <SolidColorBrush x:Key="BgLog"      Color="#000000"/>
  <SolidColorBrush x:Key="BgPanel"    Color="#1A1A2A"/>
  <SolidColorBrush x:Key="BgMenu"     Color="#252535"/>
  <SolidColorBrush x:Key="BgInput"    Color="#111118"/>
  <SolidColorBrush x:Key="BgTabActive"   Color="#0D0D1A"/>
  <SolidColorBrush x:Key="BgTabInactive" Color="#1E1E2E"/>
  <SolidColorBrush x:Key="BorderDefault" Color="#444455"/>
  <SolidColorBrush x:Key="BorderSubtle"  Color="#333344"/>
  <SolidColorBrush x:Key="Accent"        Color="#0078D4"/>
  <SolidColorBrush x:Key="TextPrimary"   Color="#D4D4D4"/>
  <SolidColorBrush x:Key="TextSecondary" Color="#888899"/>
  <SolidColorBrush x:Key="TextMuted"     Color="#666677"/>
  <SolidColorBrush x:Key="TextLog"       Color="#5A9A5A"/>
  <SolidColorBrush x:Key="TextResults"   Color="#6AAFFF"/>
  <FontFamily x:Key="MonoFont">Consolas</FontFamily>
  <FontFamily x:Key="UiFont">Segoe UI</FontFamily>
</ResourceDictionary>
```

---

## Visual QA Checklist

Run through every item before committing:

### Layout
- [ ] Search bar is at the BOTTOM of the window, NOT above the log
- [ ] Search results table is BELOW the search bar
- [ ] Log view fills all remaining vertical space between tabs and search bar
- [ ] No status bar (remove it completely)
- [ ] No stats side panel

### Log View
- [ ] Background is pure black (#000000)
- [ ] Default text is green (#5A9A5A)
- [ ] No line number column — just a single full-width text column
- [ ] No GridView column headers visible
- [ ] Scrollbar is thin (6px) and dark

### Toolbar
- [ ] "Highlighting" is a CheckBox, not a Button
- [ ] "Follow_Tail" is a CheckBox on the toolbar
- [ ] Encoding selector is a ComboBox (ANSI/UTF-8/etc)
- [ ] File path shows in a read-only TextBox with full path + size
- [ ] No toolbar grip or overflow arrow

### Tab Strip
- [ ] Tabs are horizontal
- [ ] Active tab has blue (#0078D4) border
- [ ] Close X is always visible on each tab (not hover-only)
- [ ] Inactive tabs darker, text muted

### Search Bar
- [ ] Mode selector is a dropdown (Text / Regex), not toggle buttons
- [ ] Search button is green-tinted
- [ ] Stop, Pause, Clear are separate buttons
- [ ] "Filter Tail" is a checkbox
- [ ] "Found N matching lines." text shows match count
- [ ] "Regex Syntax", "Ignore Case", "Invert Mat." are checkboxes on the right
- [ ] "Add" is a blue button on the far right

### Search Results
- [ ] Only 2 columns: Line# and Text
- [ ] Line numbers right-aligned in narrow column
- [ ] Text in light blue (#6AAFFF)
- [ ] Background is very dark (#0D0D18)
- [ ] Table header row visible with "Line" and "Text" labels
