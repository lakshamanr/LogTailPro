using System.Collections.ObjectModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;

namespace LogTailPro.ViewModels;

/// <summary>ViewModel for the Highlight Rules editor dialog.</summary>
public sealed partial class HighlightRulesViewModel : ObservableObject
{
    private bool _loading;

    [ObservableProperty] private ObservableCollection<HighlightRule> _rules;
    [ObservableProperty] private HighlightRule? _selectedRule;

    /// <summary>True when a rule is selected; used to enable/disable the edit panel.</summary>
    public bool IsRuleSelected => SelectedRule != null;

    // Edit panel — mirrored from selected rule
    [ObservableProperty] private string _editPattern = string.Empty;
    [ObservableProperty] private bool _editIsRegex;
    [ObservableProperty] private bool _editIsCaseSensitive;
    [ObservableProperty] private bool _editIsEnabled = true;
    [ObservableProperty] private string _editForeground = "#FFFFFF";
    [ObservableProperty] private string _editBackground = "#FF4444";
    [ObservableProperty] private bool _editNotifyOnMatch;
    [ObservableProperty] private string _editNotifyTitle = "LogTailPro Alert";

    public HighlightRulesViewModel(IEnumerable<HighlightRule> existing)
    {
        // Work on deep copies so Cancel discards all changes
        _rules = new ObservableCollection<HighlightRule>(existing.Select(Clone));
    }

    partial void OnSelectedRuleChanged(HighlightRule? value)
    {
        OnPropertyChanged(nameof(IsRuleSelected));
        if (value == null) return;
        _loading = true;
        EditPattern = value.Pattern;
        EditIsRegex = value.IsRegex;
        EditIsCaseSensitive = value.IsCaseSensitive;
        EditIsEnabled = value.IsEnabled;
        EditForeground = value.ForegroundColor;
        EditBackground = value.BackgroundColor;
        EditNotifyOnMatch = value.NotifyOnMatch;
        EditNotifyTitle = value.NotifyTitle;
        _loading = false;
    }

    partial void OnEditPatternChanged(string value) => ApplyToSelected();
    partial void OnEditIsRegexChanged(bool value) => ApplyToSelected();
    partial void OnEditIsCaseSensitiveChanged(bool value) => ApplyToSelected();
    partial void OnEditIsEnabledChanged(bool value) => ApplyToSelected();
    partial void OnEditForegroundChanged(string value) => ApplyToSelected();
    partial void OnEditBackgroundChanged(string value) => ApplyToSelected();
    partial void OnEditNotifyOnMatchChanged(bool value) => ApplyToSelected();
    partial void OnEditNotifyTitleChanged(string value) => ApplyToSelected();

    private void ApplyToSelected()
    {
        if (_loading || SelectedRule == null) return;
        SelectedRule.Pattern = EditPattern;
        SelectedRule.IsRegex = EditIsRegex;
        SelectedRule.IsCaseSensitive = EditIsCaseSensitive;
        SelectedRule.IsEnabled = EditIsEnabled;
        SelectedRule.ForegroundColor = EditForeground;
        SelectedRule.BackgroundColor = EditBackground;
        SelectedRule.NotifyOnMatch = EditNotifyOnMatch;
        SelectedRule.NotifyTitle = EditNotifyTitle;
        CollectionViewSource.GetDefaultView(Rules).Refresh();
    }

    [RelayCommand]
    private void AddRule()
    {
        HighlightRule rule = new()
        {
            Pattern = "new pattern",
            ForegroundColor = "#FFFFFF",
            BackgroundColor = "#4466AA",
            IsEnabled = true,
            Priority = Rules.Count
        };
        Rules.Add(rule);
        SelectedRule = rule;
    }

    [RelayCommand]
    private void DeleteRule()
    {
        if (SelectedRule == null) return;
        int idx = Rules.IndexOf(SelectedRule);
        Rules.Remove(SelectedRule);
        RenumberPriorities();
        SelectedRule = Rules.Count > 0 ? Rules[Math.Min(idx, Rules.Count - 1)] : null;
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedRule == null) return;
        int idx = Rules.IndexOf(SelectedRule);
        if (idx > 0) { Rules.Move(idx, idx - 1); RenumberPriorities(); }
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedRule == null) return;
        int idx = Rules.IndexOf(SelectedRule);
        if (idx < Rules.Count - 1) { Rules.Move(idx, idx + 1); RenumberPriorities(); }
    }

    private void RenumberPriorities()
    {
        for (int i = 0; i < Rules.Count; i++)
            Rules[i].Priority = i;
    }

    /// <summary>Returns the final list of rules to save to settings on dialog OK.</summary>
    public List<HighlightRule> GetResultRules() => Rules.ToList();

    private static HighlightRule Clone(HighlightRule r) => new()
    {
        Id = r.Id,
        Pattern = r.Pattern,
        IsRegex = r.IsRegex,
        IsCaseSensitive = r.IsCaseSensitive,
        ForegroundColor = r.ForegroundColor,
        BackgroundColor = r.BackgroundColor,
        IsEnabled = r.IsEnabled,
        Priority = r.Priority,
        NotifyOnMatch = r.NotifyOnMatch,
        NotifyTitle = r.NotifyTitle
    };
}
