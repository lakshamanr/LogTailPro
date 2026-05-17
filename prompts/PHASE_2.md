# PHASE 2 - Search, Filter, Incremental Find & Search Results Table

## Goal
By the end of this phase, the user can:
- Open a search bar with Ctrl+F
- Type text or regex and see matching lines highlighted in real-time (incremental)
- Get live regex syntax error feedback while typing
- Switch between plain text and regex mode
- Toggle filter mode: show ONLY matching lines OR hide matching lines
- See filter results with a TIMESTAMP column (when line arrived) -- BareTailPro feature
- See search results in a SORTABLE TABLE with columns: Timestamp | Line No | Text | Capturing Groups
- Save, name and reuse frequently used search patterns
- Clear search and return to full log view
- Jump between search matches with F3 / Shift+F3

---

## Pre-flight Checklist
- [ ] Phase 1 fully working and built without errors
- [ ] Re-read SKILL.md sections 3 (SearchService) and 4 (LogTabViewModel)

---

## Step 1: SearchService

Create Services/SearchService.cs:

```csharp
public sealed class SearchService
{
    public (Regex? Pattern, string? Error) CompilePattern(
        string text, bool isRegex, bool caseSensitive)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, null);

        var options = RegexOptions.Compiled;
        if (!caseSensitive) options |= RegexOptions.IgnoreCase;
        string pattern = isRegex ? text : Regex.Escape(text);

        try
        {
            return (new Regex(pattern, options, TimeSpan.FromSeconds(2)), null);
        }
        catch (ArgumentException ex)
        {
            return (null, $"Invalid regex: {ex.Message}");
        }
    }

    public bool SafeIsMatch(Regex pattern, string text)
    {
        try { return pattern.IsMatch(text); }
        catch (RegexMatchTimeoutException) { return false; }
    }

    /// <summary>
    /// Extract named/numbered capturing group values from a match.
    /// Returns empty list if no groups or no match.
    /// </summary>
    public List<string> GetCapturingGroups(Regex pattern, string text)
    {
        try
        {
            var match = pattern.Match(text);
            if (!match.Success) return new List<string>();

            var groups = new List<string>();
            for (int i = 1; i < match.Groups.Count; i++)
                groups.Add(match.Groups[i].Value);
            return groups;
        }
        catch { return new List<string>(); }
    }

    public IEnumerable<LogLine> ApplyFilter(
        IEnumerable<LogLine> lines, Regex pattern, FilterMode mode)
    {
        return mode switch
        {
            FilterMode.IncludeMatching => lines.Where(l => SafeIsMatch(pattern, l.Text)),
            FilterMode.ExcludeMatching => lines.Where(l => !SafeIsMatch(pattern, l.Text)),
            _ => lines
        };
    }
}

public enum FilterMode { None, IncludeMatching, ExcludeMatching }
```

---

## Step 2: SearchResultRow Model

Add Models/SearchResultRow.cs:

```csharp
/// <summary>
/// A row in the search results table.
/// BareTailPro shows: Timestamp | Line Number | Text | Capturing Groups
/// </summary>
public sealed class SearchResultRow
{
    public DateTime Timestamp { get; init; }       // when line arrived (filter mode) or DateTime.MinValue
    public long LineNumber { get; init; }
    public string Text { get; init; } = string.Empty;
    public List<string> CapturingGroups { get; init; } = new();

    // For display: group columns as "Group 1", "Group 2" etc
    public string Group1 => CapturingGroups.Count > 0 ? CapturingGroups[0] : string.Empty;
    public string Group2 => CapturingGroups.Count > 1 ? CapturingGroups[1] : string.Empty;
    public string Group3 => CapturingGroups.Count > 2 ? CapturingGroups[2] : string.Empty;
}
```

---

## Step 3: SavedSearchPattern Model

Add Models/SavedSearchPattern.cs:

```csharp
public sealed class SavedSearchPattern
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public bool IsRegex { get; set; }
    public bool IsCaseSensitive { get; set; }
    public FilterMode FilterMode { get; set; } = FilterMode.None;
}
```

Add List<SavedSearchPattern> SavedPatterns to AppSettings.

---

## Step 4: Extend LogTabViewModel

```csharp
// Search state
[ObservableProperty] private string _searchText = string.Empty;
[ObservableProperty] private bool _isRegexMode = false;
[ObservableProperty] private bool _isCaseSensitive = false;
[ObservableProperty] private bool _isSearchPanelVisible = false;
[ObservableProperty] private string _searchError = string.Empty;   // live regex error
[ObservableProperty] private FilterMode _filterMode = FilterMode.None;
[ObservableProperty] private int _matchCount = 0;

// Search results table (shown below main log view in filter/search mode)
[ObservableProperty] private ObservableCollection<SearchResultRow> _searchResults = new();
[ObservableProperty] private bool _isSearchResultsVisible = false;

private readonly List<LogLine> _allLines = new();
private Regex? _activePattern;
private CancellationTokenSource? _searchCts;
private readonly SearchService _searchService = new();

partial void OnSearchTextChanged(string value) => _ = ApplySearchAsync();
partial void OnIsRegexModeChanged(bool value) => _ = ApplySearchAsync();
partial void OnIsCaseSensitiveChanged(bool value) => _ = ApplySearchAsync();
partial void OnFilterModeChanged(FilterMode value) => _ = ApplySearchAsync();

private async Task ApplySearchAsync()
{
    _searchCts?.Cancel();
    _searchCts = new CancellationTokenSource();
    var ct = _searchCts.Token;
    SearchError = string.Empty;

    if (string.IsNullOrWhiteSpace(SearchText))
    {
        _activePattern = null;
        await RefreshVisibleLinesAsync(ct);
        SearchResults.Clear();
        IsSearchResultsVisible = false;
        MatchCount = 0;
        return;
    }

    var (pattern, error) = _searchService.CompilePattern(SearchText, IsRegexMode, IsCaseSensitive);
    if (error != null) { SearchError = error; return; }

    _activePattern = pattern;

    await Task.Run(() =>
    {
        if (ct.IsCancellationRequested) return;

        var filtered = FilterMode != FilterMode.None && pattern != null
            ? _searchService.ApplyFilter(_allLines, pattern, FilterMode).ToList()
            : _allLines.ToList();

        // Build search results table rows with capturing groups
        var resultRows = filtered
            .Where(l => pattern != null && _searchService.SafeIsMatch(pattern, l.Text))
            .Select(l => new SearchResultRow
            {
                Timestamp = l.ArrivedAt,     // add ArrivedAt to LogLine model
                LineNumber = l.LineNumber,
                Text = l.Text,
                CapturingGroups = pattern != null
                    ? _searchService.GetCapturingGroups(pattern, l.Text)
                    : new List<string>()
            })
            .ToList();

        if (ct.IsCancellationRequested) return;

        App.Current.Dispatcher.Invoke(() =>
        {
            VisibleLines.Clear();
            foreach (var line in filtered) VisibleLines.Add(line);

            SearchResults.Clear();
            foreach (var row in resultRows) SearchResults.Add(row);

            MatchCount = resultRows.Count;
            IsSearchResultsVisible = resultRows.Count > 0;
        });
    }, ct);
}
```

IMPORTANT: Add ArrivedAt property to LogLine:
```csharp
public DateTime ArrivedAt { get; set; } = DateTime.Now;
```
Set this when a line arrives from the file watcher (not for initial load -- use DateTime.MinValue or file modified time).

---

## Step 5: Saved Patterns Manager

Add ViewModels/SavedPatternsViewModel.cs:

```csharp
[ObservableObject]
public partial class SavedPatternsViewModel
{
    [ObservableProperty]
    private ObservableCollection<SavedSearchPattern> _patterns;

    [ObservableProperty]
    private SavedSearchPattern? _selectedPattern;

    public event EventHandler<SavedSearchPattern>? PatternSelected;

    public SavedPatternsViewModel(List<SavedSearchPattern> existing)
    {
        _patterns = new ObservableCollection<SavedSearchPattern>(existing);
    }

    [RelayCommand]
    private void SaveCurrentPattern(LogTabViewModel tab)
    {
        // Save the current search text/mode as a named pattern
        var name = PromptForName();
        if (string.IsNullOrEmpty(name)) return;

        Patterns.Add(new SavedSearchPattern
        {
            Name = name,
            Pattern = tab.SearchText,
            IsRegex = tab.IsRegexMode,
            IsCaseSensitive = tab.IsCaseSensitive,
            FilterMode = tab.FilterMode
        });
    }

    [RelayCommand]
    private void LoadPattern()
    {
        if (SelectedPattern != null)
            PatternSelected?.Invoke(this, SelectedPattern);
    }

    [RelayCommand]
    private void DeletePattern()
    {
        if (SelectedPattern != null)
            Patterns.Remove(SelectedPattern);
    }
}
```

Expose saved patterns as a dropdown in the search bar.

---

## Step 6: Search Results Table UI

Add below the main log ListView in LogTabView.xaml (in a Grid splitter layout):

```xml
<!-- Splitter between log view and search results -->
<GridSplitter Grid.Row="2" Height="4" HorizontalAlignment="Stretch"
              Visibility="{Binding IsSearchResultsVisible,
                           Converter={StaticResource BoolToVisibilityConverter}}"/>

<!-- Search Results Table -->
<ListView Grid.Row="3"
          ItemsSource="{Binding SearchResults}"
          Visibility="{Binding IsSearchResultsVisible,
                       Converter={StaticResource BoolToVisibilityConverter}}"
          MaxHeight="250">
  <ListView.View>
    <GridView>
      <GridViewColumn Header="Timestamp" Width="160"
                      DisplayMemberBinding="{Binding Timestamp, StringFormat='HH:mm:ss.fff'}"/>
      <GridViewColumn Header="Line #" Width="70"
                      DisplayMemberBinding="{Binding LineNumber}"/>
      <GridViewColumn Header="Text" Width="600"
                      DisplayMemberBinding="{Binding Text}"/>
      <GridViewColumn Header="Group 1" Width="120"
                      DisplayMemberBinding="{Binding Group1}"/>
      <GridViewColumn Header="Group 2" Width="120"
                      DisplayMemberBinding="{Binding Group2}"/>
    </GridView>
  </ListView.View>
</ListView>
```

Make GridView columns sortable by clicking header (use ListViewSortHelper or GridViewColumnHeader click).

---

## Step 7: SearchPanel.xaml

```xml
<UserControl Visibility="{Binding IsSearchPanelVisible,
             Converter={StaticResource BoolToVisibilityConverter}}">
  <DockPanel Background="{DynamicResource SearchBarBackground}" LastChildFill="True">

    <!-- Saved patterns dropdown -->
    <ComboBox DockPanel.Dock="Right" Width="180"
              ItemsSource="{Binding SavedPatterns}"
              DisplayMemberPath="Name"
              ToolTip="Load a saved pattern">
      <ComboBox.ItemContainerStyle>
        <Style TargetType="ComboBoxItem">
          <Setter Property="Command" Value="{Binding DataContext.LoadPatternCommand,
                  RelativeSource={RelativeSource AncestorType=UserControl}}"/>
          <Setter Property="CommandParameter" Value="{Binding}"/>
        </Style>
      </ComboBox.ItemContainerStyle>
    </ComboBox>

    <Button DockPanel.Dock="Right" Content="Save" ToolTip="Save current pattern"
            Command="{Binding SavePatternCommand}" Margin="4,0"/>

    <Button DockPanel.Dock="Right" Content="X" ToolTip="Close (Escape)"
            Command="{Binding CloseSearchCommand}"/>

    <StackPanel Orientation="Horizontal">
      <Label Content="Search:"/>

      <!-- Search box - red border on regex error -->
      <TextBox Width="300" Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}">
        <TextBox.Style>
          <Style TargetType="TextBox">
            <Style.Triggers>
              <DataTrigger Binding="{Binding SearchError, Converter={StaticResource NotEmptyConverter}}" Value="True">
                <Setter Property="BorderBrush" Value="Red"/>
                <Setter Property="ToolTip" Value="{Binding SearchError}"/>
              </DataTrigger>
            </Style.Triggers>
          </Style>
        </TextBox.Style>
      </TextBox>

      <ToggleButton Content=".*" IsChecked="{Binding IsRegexMode}" ToolTip="Regular expression mode" Margin="4,0"/>
      <ToggleButton Content="Aa" IsChecked="{Binding IsCaseSensitive}" ToolTip="Case sensitive" Margin="2,0"/>

      <ComboBox SelectedValue="{Binding FilterMode}" SelectedValuePath="Tag" Margin="8,0">
        <ComboBoxItem Content="Highlight" Tag="{x:Static local:FilterMode.None}"/>
        <ComboBoxItem Content="Show matching" Tag="{x:Static local:FilterMode.IncludeMatching}"/>
        <ComboBoxItem Content="Hide matching" Tag="{x:Static local:FilterMode.ExcludeMatching}"/>
      </ComboBox>

      <TextBlock Text="{Binding MatchCount, StringFormat='{}{0} matches'}"
                 VerticalAlignment="Center" Margin="8,0"/>

      <Button Content="Prev" Command="{Binding PreviousMatchCommand}" ToolTip="Shift+F3"/>
      <Button Content="Next" Command="{Binding NextMatchCommand}" ToolTip="F3" Margin="2,0"/>
    </StackPanel>
  </DockPanel>
</UserControl>
```

---

## Step 8: Keyboard Shortcuts

```xml
<Window.InputBindings>
  <KeyBinding Key="F" Command="{Binding SelectedTab.ToggleFollowTailCommand}"/>
  <KeyBinding Key="F" Modifiers="Ctrl" Command="{Binding SelectedTab.ShowSearchCommand}"/>
  <KeyBinding Key="F3" Command="{Binding SelectedTab.NextMatchCommand}"/>
  <KeyBinding Key="F3" Modifiers="Shift" Command="{Binding SelectedTab.PreviousMatchCommand}"/>
  <KeyBinding Key="Escape" Command="{Binding SelectedTab.CloseSearchCommand}"/>
  <KeyBinding Key="O" Modifiers="Ctrl" Command="{Binding OpenFileCommand}"/>
  <KeyBinding Key="W" Modifiers="Ctrl" Command="{Binding CloseSelectedTabCommand}"/>
</Window.InputBindings>
```

---

## Step 9: Build and Test

```bash
dotnet build src/LogTailPro/LogTailPro.csproj
```

### Manual Test Checklist
- [ ] Ctrl+F opens search panel
- [ ] Typing shows live highlight, no lag on 100K lines
- [ ] Invalid regex shows red border + tooltip error, does not crash
- [ ] Regex mode: pattern ERROR|WARN matches and highlights correctly
- [ ] Capturing groups: pattern \[(\w+)\] shows captured group in results table
- [ ] Search results table appears below log view with Timestamp, Line#, Text, Group columns
- [ ] Results table columns are sortable by clicking header
- [ ] Timestamp column shows arrival time for lines from watcher, not initial load
- [ ] Filter mode "Show matching" hides non-matching lines
- [ ] Filter mode "Hide matching" shows only non-matching lines
- [ ] Match count is correct
- [ ] F3 jumps to next match in main log view, Shift+F3 goes back
- [ ] Save pattern: type a pattern, click Save, give it a name
- [ ] Load pattern: select from dropdown, search fields populate
- [ ] Delete pattern removes it from list
- [ ] Saved patterns persist after restart
- [ ] Escape closes search panel and restores full view
- [ ] New lines from watcher are included in live filter results

---

## Phase 2 Complete When
All checkboxes above are ticked. Then read PHASE_3.md.

---

## Git Commit (Do This Before Moving to Next Phase)

```bash
git add -A
git commit -m "Phase 2 complete: regex search, filter mode, results table, saved patterns"
```

Do not proceed to Phase 3 until this commit succeeds.
