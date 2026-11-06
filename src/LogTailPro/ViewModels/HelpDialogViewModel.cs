using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;
using LogTailPro.Services;

namespace LogTailPro.ViewModels;

/// <summary>
/// ViewModel driving the in-depth Help Guide and Feature Manual dialog.
/// </summary>
public sealed partial class HelpDialogViewModel : ObservableObject
{
    private readonly HelpContentService _helpService;
    private readonly List<HelpTopic> _allTopics;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All Categories";

    [ObservableProperty]
    private HelpTopic? _selectedTopic;

    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<HelpTopic> FilteredTopics { get; } = new();

    public HelpDialogViewModel(HelpContentService? helpService = null, string? initialTarget = null)
    {
        _helpService = helpService ?? new HelpContentService();
        _allTopics = _helpService.GetAllTopics().ToList();

        Categories.Add("All Categories");
        foreach (string cat in _helpService.GetCategories())
        {
            Categories.Add(cat);
        }

        ApplyFilter(initialTarget);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    private void ApplyFilter(string? preferredTarget = null)
    {
        IEnumerable<HelpTopic> matches = _helpService.Search(SearchText, SelectedCategory);

        FilteredTopics.Clear();
        foreach (HelpTopic topic in matches)
        {
            FilteredTopics.Add(topic);
        }

        if (!string.IsNullOrWhiteSpace(preferredTarget))
        {
            HelpTopic? target = FilteredTopics.FirstOrDefault(t =>
                string.Equals(t.Id, preferredTarget, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.Category, preferredTarget, StringComparison.OrdinalIgnoreCase) ||
                t.Title.Contains(preferredTarget, StringComparison.OrdinalIgnoreCase));

            if (target != null)
            {
                SelectedTopic = target;
                return;
            }
        }

        if (SelectedTopic == null || !FilteredTopics.Contains(SelectedTopic))
        {
            SelectedTopic = FilteredTopics.FirstOrDefault();
        }
    }

    [RelayCommand]
    public void SelectTopic(HelpTopic? topic)
    {
        if (topic != null)
        {
            SelectedTopic = topic;
        }
    }

    [RelayCommand]
    public void SelectCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return;
        SelectedCategory = Categories.FirstOrDefault(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase)) ?? "All Categories";
    }

    [RelayCommand]
    private void CopyTopicDetails()
    {
        if (SelectedTopic == null) return;

        string formatted =
            $"============================================================\n" +
            $"{SelectedTopic.Title} — {SelectedTopic.Category}\n" +
            $"Location: {SelectedTopic.UiLocation}\n" +
            $"Shortcut: {(SelectedTopic.HasShortcut ? SelectedTopic.Shortcut : "None")}\n" +
            $"============================================================\n\n" +
            $"OVERVIEW:\n{SelectedTopic.Overview}\n\n" +
            $"DEMO & SCENARIO:\n{SelectedTopic.DemoScenario}\n\n" +
            $"HOW TO WORK IT (STEP-BY-STEP):\n{SelectedTopic.StepByStep}\n\n" +
            $"UNDER THE HOOD:\n{SelectedTopic.UnderTheHood}\n";

        Clipboard.SetText(formatted);
    }

    [RelayCommand]
    private void OpenMarkdownGuide()
    {
        HelpContentService.OpenMarkdownGuide();
    }
}
