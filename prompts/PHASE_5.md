# PHASE 5 — Highlight Rules & Settings UI

## Goal
By the end of this phase:
- Users can add color highlight rules (pattern + foreground + background color)
- Rules can be plain text or regex, case-sensitive or not
- Rules can be reordered (priority) by drag-and-drop
- Rules are toggled on/off without deleting
- All rules persist across restarts
- Full Settings dialog: font, font size, max lines, theme, follow-tail default
- Highlight rules apply in real-time as log lines are rendered

---

## Pre-flight Checklist
- [ ] Phase 4 is fully working and built without errors
- [ ] Re-read SKILL.md sections 3 (HighlightService) and 5 (WPF ItemContainerStyle)

---

## Step 1: HighlightService

Create `Services/HighlightService.cs`:

```csharp
public sealed class HighlightService
{
    private List<(HighlightRule Rule, Regex Pattern)> _compiledRules = new();

    /// <summary>
    /// Call this whenever rules change (load, add, edit, delete, reorder).
    /// Pre-compiles all enabled rules so per-line matching is fast.
    /// </summary>
    public void SetRules(IEnumerable<HighlightRule> rules)
    {
        _compiledRules = rules
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.Priority)
            .Select(r =>
            {
                var options = RegexOptions.Compiled;
                if (!r.IsCaseSensitive) options |= RegexOptions.IgnoreCase;
                string pattern = r.IsRegex ? r.Pattern : Regex.Escape(r.Pattern);
                try
                {
                    return (r, new Regex(pattern, options, TimeSpan.FromSeconds(1)));
                }
                catch
                {
                    return (r, new Regex(Regex.Escape(r.Pattern), options, TimeSpan.FromSeconds(1)));
                }
            })
            .ToList();
    }

    /// <summary>Returns the first matching rule for this line, or null.</summary>
    public HighlightRule? GetMatch(string lineText)
    {
        foreach (var (rule, pattern) in _compiledRules)
        {
            try { if (pattern.IsMatch(lineText)) return rule; }
            catch (RegexMatchTimeoutException) { /* skip */ }
        }
        return null;
    }
}
```

---

## Step 2: Apply Highlighting in LogTabViewModel

After loading lines and after new lines arrive from watcher:
```csharp
private void ApplyHighlighting(LogLine line)
{
    line.MatchedRule = _highlightService.GetMatch(line.Text);
}
```

When rules change (from SettingsViewModel), refresh highlighting on all loaded lines:
```csharp
public void RefreshHighlighting()
{
    _highlightService.SetRules(Settings.HighlightRules);
    foreach (var line in _allLines)
        line.MatchedRule = _highlightService.GetMatch(line.Text);

    // Refresh the ListView bindings
    App.Current.Dispatcher.Invoke(() =>
    {
        var temp = VisibleLines.ToList();
        VisibleLines.Clear();
        foreach (var l in temp) VisibleLines.Add(l);
    });
}
```

---

## Step 3: HighlightColorConverter

Create `Converters/HighlightColorConverter.cs`:

```csharp
public sealed class HighlightColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not HighlightRule rule) return DependencyProperty.UnsetValue;

        string colorHex = parameter?.ToString() == "Foreground"
            ? rule.ForegroundColor
            : rule.BackgroundColor;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            return new SolidColorBrush(color);
        }
        catch
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

---

## Step 4: Highlight Rules Dialog

Create `Views/HighlightRulesDialog.xaml`:

Layout:
```
┌─────────────────────────────────────────────────────┐
│  Highlight Rules                                     │
│ ┌─────────────────────────────────────┐ [Add]        │
│ │ ☑ ERROR  |  .*ERROR.*  | Regex  | █ fg | █ bg │    │
│ │ ☑ WARN   |  WARN       | Text   | █ fg | █ bg │    │
│ │ ☐ INFO   |  \[INFO\]   | Regex  | █ fg | █ bg │    │
│ └─────────────────────────────────────┘ [Delete]     │
│                                                      │
│ Pattern: [________________] [.*] [Aa]                │
│ Foreground: [████] Background: [████]                │
│                          [OK]  [Cancel]              │
└─────────────────────────────────────────────────────┘
```

Key behaviors:
- Selecting a rule in the list populates the edit fields below
- Color picker: use `System.Windows.Forms.ColorDialog` (add reference: `UseWindowsForms`)
  OR display a color grid with 16 preset colors + hex input
- Drag handles on left of each row for reordering (update `Priority` on drop)
- Checkbox toggles `IsEnabled` without deleting
- Changes are previewed live on open log files

ViewModel: `ViewModels/HighlightRulesViewModel.cs`
```csharp
[ObservableObject]
public partial class HighlightRulesViewModel
{
    [ObservableProperty]
    private ObservableCollection<HighlightRule> _rules;

    [ObservableProperty]
    private HighlightRule? _selectedRule;

    public HighlightRulesViewModel(IEnumerable<HighlightRule> existingRules)
    {
        _rules = new ObservableCollection<HighlightRule>(existingRules);
    }

    [RelayCommand]
    private void AddRule()
    {
        var rule = new HighlightRule
        {
            Pattern = "new pattern",
            ForegroundColor = "#FFFFFF",
            BackgroundColor = "#FF4444",
            Priority = Rules.Count
        };
        Rules.Add(rule);
        SelectedRule = rule;
    }

    [RelayCommand]
    private void DeleteRule()
    {
        if (SelectedRule != null)
            Rules.Remove(SelectedRule);
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedRule == null) return;
        int idx = Rules.IndexOf(SelectedRule);
        if (idx > 0) Rules.Move(idx, idx - 1);
        RenumberPriorities();
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedRule == null) return;
        int idx = Rules.IndexOf(SelectedRule);
        if (idx < Rules.Count - 1) Rules.Move(idx, idx + 1);
        RenumberPriorities();
    }

    private void RenumberPriorities()
    {
        for (int i = 0; i < Rules.Count; i++)
            Rules[i].Priority = i;
    }
}
```

---

## Step 5: Settings Dialog

Create `Views/SettingsDialog.xaml` with these sections:

**Appearance:**
- Font Family: ComboBox with: Consolas, Courier New, Lucida Console, Cascadia Mono
- Font Size: NumericUpDown (10–24)
- Theme: RadioButton Dark / Light

**Behavior:**
- Max lines in memory: TextBox (default 100000)
- Follow tail on open: CheckBox

**About:**
- Version, build date, GitHub link

ViewModel: `ViewModels/SettingsViewModel.cs` — wraps `AppSettings`, saves on OK.

---

## Step 6: Wire Everything Together

In `MainViewModel`:
```csharp
[RelayCommand]
private void OpenHighlightRules()
{
    var vm = new HighlightRulesViewModel(Settings.HighlightRules);
    var dialog = new HighlightRulesDialog { DataContext = vm, Owner = Application.Current.MainWindow };
    if (dialog.ShowDialog() != true) return;

    Settings.HighlightRules = vm.Rules.ToList();
    _settingsService.Save(Settings);

    // Refresh highlighting on all open tabs
    foreach (var tab in Tabs)
        tab.RefreshHighlighting();
}
```

---

## Step 7: Default Highlight Rules

Provide these sensible defaults when `Settings.HighlightRules` is empty:

| Pattern | IsRegex | ForegroundColor | BackgroundColor |
|---------|---------|-----------------|-----------------|
| `\bERROR\b` | true | `#FFFFFF` | `#CC2222` |
| `\bEXCEPTION\b` | true | `#FFFFFF` | `#CC2222` |
| `\bWARN(ING)?\b` | true | `#000000` | `#FFA500` |
| `\bINFO\b` | true | `#A8D8A8` | `#1E1E1E` |
| `\bDEBUG\b` | true | `#888888` | `#1E1E1E` |

---

## Step 6: Log Parser — Column Extraction

This is the biggest upgrade from BareTailPro. Parse structured columns out of log lines
so users can filter/sort by Level, Thread, Class, Message individually.

```csharp
/// <summary>
/// Log parser config maps regex named groups to columns.
/// Users define their log format once; it applies to all lines.
/// </summary>
public sealed class LogParserConfig
{
    public string Name { get; set; } = "Custom";
    public string Pattern { get; set; } = string.Empty;
    // Example: @"(?<Timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) (?<Level>\w+) (?<Thread>\[\w+\]) (?<Class>[\w\.]+) - (?<Message>.*)"
    public List<string> ColumnOrder { get; set; } = new(); // e.g. ["Timestamp","Level","Thread","Class","Message"]
}

public sealed class ParsedLogLine
{
    public LogLine Raw { get; init; } = null!;
    public Dictionary<string, string> Columns { get; init; } = new();
}
```

Built-in presets to ship with:
- Log4j / Log4net: `%date [%thread] %-5level %logger - %message`
- NLog: `${longdate}|${level}|${logger}|${message}`
- Serilog JSON: JSON parsed line by line
- Azure App Insights traces
- IIS W3C format
- Plain text (no parsing — fallback)

Parser wizard: Tools → Configure Log Parser → pick preset or define custom regex.

When a parser is active:
- GridView shows extracted columns instead of raw text
- Filter chains can target specific columns: "Level equals ERROR"
- Timestamp column drives the Elapsed Time calculation
- Export to CSV uses parsed columns as headers

---

## Step 7: Workspace Save/Restore

Save and restore exactly which files are open, with their filter/search state.

```csharp
public sealed class Workspace
{
    public string Name { get; set; } = "Default";
    public List<WorkspaceTab> Tabs { get; set; } = new();
    public DateTime SavedAt { get; set; }
}

public sealed class WorkspaceTab
{
    public string FilePath { get; set; } = string.Empty;
    public bool IsFollowTail { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public bool IsRegexMode { get; set; }
    public FilterMode FilterMode { get; set; }
    public List<FilterRule> FilterRules { get; set; } = new();
    public List<long> BookmarkedLineNumbers { get; set; } = new();
}
```

File → Save Workspace → prompts for name → saves to AppData.
File → Load Workspace → lists saved workspaces → restores all tabs.
Auto-save the last workspace on exit; auto-restore on startup (optional setting).

---

## Step 8: JSON / XML Pretty-Print

When the user selects a log line and presses Ctrl+P (or double-clicks),
open a detail panel below the log view that pretty-prints any JSON or XML found in the line.

```csharp
private string TryPrettyPrint(string lineText)
{
    // Try JSON first
    int jsonStart = lineText.IndexOfAny(['{', '[']);
    if (jsonStart >= 0)
    {
        try
        {
            string jsonPart = lineText[jsonStart..];
            var obj = JsonConvert.DeserializeObject(jsonPart);
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }
        catch { /* not valid JSON */ }
    }

    // Try XML
    int xmlStart = lineText.IndexOf('<');
    if (xmlStart >= 0)
    {
        try
        {
            string xmlPart = lineText[xmlStart..];
            var doc = XDocument.Parse(xmlPart);
            return doc.ToString();
        }
        catch { /* not valid XML */ }
    }

    return lineText; // return raw if neither
}
```

Display in a collapsible detail panel (bottom of LogTabView) with syntax coloring.

---

## Step 9: Build and Test

```bash
dotnet build src/LogTailPro/LogTailPro.csproj
```

### Manual Test Checklist
- [ ] Tools → Highlight Rules opens dialog
- [ ] Default rules are pre-loaded on first launch
- [ ] ERROR lines show red background in log view
- [ ] WARN lines show orange background
- [ ] Adding a new rule and clicking OK applies it immediately to all open tabs
- [ ] Disabling a rule (unchecking) removes its highlight from all lines
- [ ] Rule order can be changed with Move Up/Move Down
- [ ] Rules persist after restarting the app
- [ ] Settings dialog changes font and font size live
- [ ] Theme switches between Dark and Light
- [ ] Settings persist after restart
- [ ] Tools → Configure Log Parser opens parser setup dialog
- [ ] Selecting a Log4net preset parses a real log file into columns
- [ ] Parsed columns (Timestamp, Level, Thread, Message) appear as separate GridView columns
- [ ] Filter chain can target "Level equals ERROR" specifically
- [ ] File → Save Workspace saves current tabs and filter state
- [ ] File → Load Workspace restores all tabs and their state
- [ ] Selecting a line containing JSON and pressing Ctrl+P shows formatted JSON in detail panel
- [ ] Selecting a line containing XML and pressing Ctrl+P shows formatted XML

---

## Phase 5 Complete When
All checkboxes above are ticked. Then read `PHASE_6.md`.

---

## Step 8b: Additional Settings (from Official Docs)

Add these to SettingsDialog.xaml:

**Text Rendering:**
- TAB width: NumericUpDown (1-16, default 8) -- expands \t to spaces
- Line spacing multiplier: Slider (0.8-2.0, default 1.0)
- Font character offset: NumericUpDown (-5 to 5 px)

**Preferences Storage:**
- RadioButtons: "Save to File" | "Save to Registry" | "Don't save on exit"
- "Load preferences from file" button -- loads a shared .cfg
- "Save preferences to file" button -- saves a sharable .cfg

**Network:**
- Polling interval for network files (ms): NumericUpDown (500-10000, default 2000)

**Startup:**
- Automatically open last file on startup: CheckBox (default true)

Implement TAB expansion in LogParserService as specified in SKILL.md section 11.
Apply tab expansion BEFORE adding a line to _allLines.

---

## Git Commit (Do This Before Moving to Next Phase)

```bash
git add -A
git commit -m "Phase 5 complete: highlight rules, TAB expansion, IIS format, settings dialog"
```

Do not proceed to Phase 6 until this commit succeeds.
