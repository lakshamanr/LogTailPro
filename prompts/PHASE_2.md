# PHASE 2 — Search, Filter & Incremental Find

## Goal
By the end of this phase, the user can:
- Open a search bar with Ctrl+F
- Type text or regex and see matching lines highlighted in real-time (incremental search)
- Switch between: plain text search and regex search
- Toggle filter mode: show ONLY matching lines OR hide matching lines
- Clear search and return to full log view
- Jump between search matches with F3 / Shift+F3

---

## Pre-flight Checklist
- [ ] Phase 1 is fully working and built without errors
- [ ] Re-read SKILL.md sections 3 (SearchService) and 4 (LogTabViewModel) before coding

---

## Step 1: SearchService

Create `Services/SearchService.cs`:

```csharp
public sealed class SearchService
{
    /// <summary>Compiles and validates a search pattern. Returns null if invalid.</summary>
    public (Regex? Pattern, string? Error) CompilePattern(
        string text, bool isRegex, bool caseSensitive)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, null);

        var options = RegexOptions.Compiled;
        if (!caseSensitive) options |= RegexOptions.IgnoreCase;

        string pattern = isRegex ? text : Regex.Escape(text);

        try
        {
            // ALWAYS set matchTimeout — user input can be pathological
            return (new Regex(pattern, options, TimeSpan.FromSeconds(2)), null);
        }
        catch (ArgumentException ex)
        {
            return (null, $"Invalid regex: {ex.Message}");
        }
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

    private static bool SafeIsMatch(Regex pattern, string text)
    {
        try { return pattern.IsMatch(text); }
        catch (RegexMatchTimeoutException) { return false; }
    }
}
```

---

## Step 2: Extend LogTabViewModel

Add these properties and methods to `LogTabViewModel`:

```csharp
// Search state
[ObservableProperty] private string _searchText = string.Empty;
[ObservableProperty] private bool _isRegexMode = false;
[ObservableProperty] private bool _isCaseSensitive = false;
[ObservableProperty] private bool _isSearchPanelVisible = false;
[ObservableProperty] private string _searchError = string.Empty;
[ObservableProperty] private FilterMode _filterMode = FilterMode.None;
[ObservableProperty] private int _matchCount = 0;
[ObservableProperty] private int _currentMatchIndex = -1;

// All raw lines (unfiltered). VisibleLines is the filtered view.
private readonly List<LogLine> _allLines = new();

// Trigger search when any of these change
partial void OnSearchTextChanged(string value) => _ = ApplySearchAsync();
partial void OnIsRegexModeChanged(bool value) => _ = ApplySearchAsync();
partial void OnIsCaseSensitiveChanged(bool value) => _ = ApplySearchAsync();
partial void OnFilterModeChanged(FilterMode value) => _ = ApplySearchAsync();
```

### Core search method

```csharp
private readonly SearchService _searchService = new();
private Regex? _activePattern;
private CancellationTokenSource? _searchCts;

private async Task ApplySearchAsync()
{
    // Cancel any in-progress search
    _searchCts?.Cancel();
    _searchCts = new CancellationTokenSource();
    var ct = _searchCts.Token;

    SearchError = string.Empty;

    if (string.IsNullOrWhiteSpace(SearchText))
    {
        _activePattern = null;
        await RefreshVisibleLinesAsync(ct);
        MatchCount = 0;
        return;
    }

    var (pattern, error) = _searchService.CompilePattern(
        SearchText, IsRegexMode, IsCaseSensitive);

    if (error != null)
    {
        SearchError = error;
        return;
    }

    _activePattern = pattern;

    // Run filtering on background thread for large files
    await Task.Run(() =>
    {
        var filtered = FilterMode != FilterMode.None && pattern != null
            ? _searchService.ApplyFilter(_allLines, pattern, FilterMode).ToList()
            : _allLines.ToList();

        if (ct.IsCancellationRequested) return;

        App.Current.Dispatcher.Invoke(() =>
        {
            VisibleLines.Clear();
            foreach (var line in filtered)
                VisibleLines.Add(line);
            MatchCount = filtered.Count(l => pattern?.IsMatch(l.Text) == true);
        });
    }, ct);
}
```

### Jump between matches

```csharp
[RelayCommand]
private void NextMatch()
{
    if (_activePattern == null || VisibleLines.Count == 0) return;
    // Find next line after CurrentMatchIndex that matches
    // Set selected item in ListView via SelectedLogLine property
}

[RelayCommand]
private void PreviousMatch()
{
    // Find previous match before CurrentMatchIndex
}
```

---

## Step 3: SearchPanel.xaml

Create `Views/SearchPanel.xaml` as a UserControl:

```xml
<UserControl Visibility="{Binding IsSearchPanelVisible,
             Converter={StaticResource BoolToVisibilityConverter}}">
  <Grid Background="{DynamicResource SearchBarBackground}">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="Auto"/>  <!-- label -->
      <ColumnDefinition Width="300"/>   <!-- search box -->
      <ColumnDefinition Width="Auto"/>  <!-- regex toggle -->
      <ColumnDefinition Width="Auto"/>  <!-- case toggle -->
      <ColumnDefinition Width="Auto"/>  <!-- filter mode -->
      <ColumnDefinition Width="Auto"/>  <!-- match count -->
      <ColumnDefinition Width="Auto"/>  <!-- prev/next -->
      <ColumnDefinition Width="Auto"/>  <!-- close -->
    </Grid.ColumnDefinitions>

    <Label Content="Search:" Grid.Column="0"/>

    <TextBox Grid.Column="1"
             Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"
             Background="Transparent"
             Foreground="{DynamicResource ForegroundBrush}">
      <!-- Red border when SearchError is not empty -->
    </TextBox>

    <ToggleButton Grid.Column="2" Content=".*"
                  IsChecked="{Binding IsRegexMode}"
                  ToolTip="Use regular expression"/>

    <ToggleButton Grid.Column="3" Content="Aa"
                  IsChecked="{Binding IsCaseSensitive}"
                  ToolTip="Match case"/>

    <ComboBox Grid.Column="4"
              SelectedValue="{Binding FilterMode}"
              SelectedValuePath="Tag">
      <ComboBoxItem Content="Highlight" Tag="{x:Static local:FilterMode.None}"/>
      <ComboBoxItem Content="Show only matching" Tag="{x:Static local:FilterMode.IncludeMatching}"/>
      <ComboBoxItem Content="Hide matching" Tag="{x:Static local:FilterMode.ExcludeMatching}"/>
    </ComboBox>

    <TextBlock Grid.Column="5"
               Text="{Binding MatchCount, StringFormat='{}{0} matches'}"
               Foreground="{DynamicResource ForegroundBrush}"
               VerticalAlignment="Center" Margin="8,0"/>

    <Button Grid.Column="6" Content="▲"
            Command="{Binding PreviousMatchCommand}"
            ToolTip="Previous match (Shift+F3)"/>
    <Button Grid.Column="6" Content="▼"
            Command="{Binding NextMatchCommand}"
            ToolTip="Next match (F3)"/>

    <Button Grid.Column="7" Content="✕"
            Command="{Binding CloseSearchCommand}"
            ToolTip="Close search (Escape)"/>
  </Grid>
</UserControl>
```

Place SearchPanel inside `LogTabView.xaml` above the ListView (DockPanel.Dock="Top").

---

## Step 4: Keyboard Shortcuts

In `MainWindow.xaml`, add InputBindings:

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

## Step 5: AND/OR Combined Filter Chains

This is a significant upgrade over BareTailPro. Instead of one filter, users can chain multiple
filters with AND/OR/NOT logic.

```csharp
public sealed class FilterRule
{
    public string Pattern { get; set; } = string.Empty;
    public bool IsRegex { get; set; }
    public bool IsCaseSensitive { get; set; }
    public FilterOperator Operator { get; set; } = FilterOperator.AND;
    public bool IsNegated { get; set; }  // NOT
    public bool IsEnabled { get; set; } = true;
}

public enum FilterOperator { AND, OR }
```

Filter chain evaluation:
```csharp
public bool LineMatchesChain(string lineText, IReadOnlyList<FilterRule> rules)
{
    if (!rules.Any(r => r.IsEnabled)) return true;

    bool result = true;
    bool firstRule = true;

    foreach (var rule in rules.Where(r => r.IsEnabled))
    {
        bool matches = SafeIsMatch(_compiledPatterns[rule], lineText);
        if (rule.IsNegated) matches = !matches;

        if (firstRule)
        {
            result = matches;
            firstRule = false;
        }
        else
        {
            result = rule.Operator == FilterOperator.AND
                ? result && matches
                : result || matches;
        }
    }
    return result;
}
```

Add a filter chain panel in `SearchPanel.xaml`:
```
[+] Add Filter
─────────────────────────────────────────────────────
  AND  [NOT] Pattern: [ERROR|WARN  ] [.*] [Aa] [✕]
  OR   [NOT] Pattern: [Exception   ] [.*] [Aa] [✕]
─────────────────────────────────────────────────────
```

Each filter row has: AND/OR toggle, NOT toggle, pattern text box, regex toggle, case toggle, delete.

---

## Step 6: Build and Test

```bash
dotnet build src/LogTailPro/LogTailPro.csproj
```

### Manual Test Checklist
- [ ] Ctrl+F opens the search panel
- [ ] Typing in the search box filters/highlights in real-time (no lag up to 100K lines)
- [ ] Regex mode toggle works — `ERROR|WARN` matches multiple words
- [ ] Invalid regex shows error message, does not crash
- [ ] "Show only matching" hides non-matching lines
- [ ] "Hide matching" shows only non-matching lines
- [ ] MatchCount shows correct number
- [ ] F3 jumps to next match, Shift+F3 jumps to previous
- [ ] Escape closes the search panel and restores full view
- [ ] New lines added by file watcher are also searched when filter is active
- [ ] Clearing the search box restores all lines
- [ ] Two filter rules with AND logic: both must match
- [ ] Two filter rules with OR logic: either match is shown
- [ ] NOT toggle inverts a rule (excludes matching lines)
- [ ] Filter rules can be added and removed dynamically

---

## Phase 2 Complete When
All checkboxes above are ticked. Then read `PHASE_3.md`.
