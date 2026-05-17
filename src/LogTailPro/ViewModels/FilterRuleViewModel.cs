using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;

namespace LogTailPro.ViewModels;

/// <summary>Observable wrapper for a <see cref="FilterRule"/> in the filter chain UI.</summary>
public sealed partial class FilterRuleViewModel : ObservableObject
{
    private readonly Action<FilterRuleViewModel> _onRemove;
    private readonly Action _onChanged;

    public FilterRuleViewModel(Action<FilterRuleViewModel> onRemove, Action onChanged)
    {
        _onRemove = onRemove;
        _onChanged = onChanged;
    }

    [ObservableProperty]
    private string _pattern = string.Empty;

    [ObservableProperty]
    private bool _isRegex;

    [ObservableProperty]
    private bool _isCaseSensitive;

    [ObservableProperty]
    private FilterOperator _operator = FilterOperator.AND;

    [ObservableProperty]
    private bool _isNegated;

    [ObservableProperty]
    private bool _isEnabled = true;

    partial void OnPatternChanged(string value) => _onChanged();
    partial void OnIsRegexChanged(bool value) => _onChanged();
    partial void OnIsCaseSensitiveChanged(bool value) => _onChanged();
    partial void OnOperatorChanged(FilterOperator value) => _onChanged();
    partial void OnIsNegatedChanged(bool value) => _onChanged();
    partial void OnIsEnabledChanged(bool value) => _onChanged();

    /// <summary>Converts to the serializable model type.</summary>
    public FilterRule ToModel() => new()
    {
        Pattern = Pattern,
        IsRegex = IsRegex,
        IsCaseSensitive = IsCaseSensitive,
        Operator = Operator,
        IsNegated = IsNegated,
        IsEnabled = IsEnabled
    };

    /// <summary>Removes this rule from the parent chain.</summary>
    [RelayCommand]
    private void Remove() => _onRemove(this);

    /// <summary>Display label for the AND/OR toggle button.</summary>
    public string OperatorLabel => Operator == FilterOperator.AND ? "AND" : "OR";

    /// <summary>Toggles between AND and OR.</summary>
    [RelayCommand]
    private void ToggleOperator()
    {
        Operator = Operator == FilterOperator.AND ? FilterOperator.OR : FilterOperator.AND;
        OnPropertyChanged(nameof(OperatorLabel));
    }
}
