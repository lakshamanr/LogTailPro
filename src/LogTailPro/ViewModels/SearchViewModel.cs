using CommunityToolkit.Mvvm.ComponentModel;

namespace LogTailPro.ViewModels;

/// <summary>ViewModel for the search/filter panel (Phase 2 will expand this).</summary>
public sealed partial class SearchViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isRegex;

    [ObservableProperty]
    private bool _isCaseSensitive;

    [ObservableProperty]
    private bool _isPanelVisible;
}
