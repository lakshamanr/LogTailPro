using CommunityToolkit.Mvvm.ComponentModel;
using LogTailPro.Models;

namespace LogTailPro.ViewModels;

/// <summary>ViewModel for the Settings dialog.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel(AppSettings settings)
    {
        FontFamily = settings.FontFamily;
        FontSize = settings.FontSize;
        MaxLinesInMemory = settings.MaxLinesInMemory;
        FollowTailOnOpen = settings.FollowTailOnOpen;
        IsDarkTheme = settings.Theme == "Dark";
    }

    [ObservableProperty] private string _fontFamily = "Consolas";
    [ObservableProperty] private double _fontSize = 13.0;
    [ObservableProperty] private int _maxLinesInMemory = 100_000;
    [ObservableProperty] private bool _followTailOnOpen = true;
    [ObservableProperty] private bool _isDarkTheme = true;

    partial void OnIsDarkThemeChanged(bool value) => OnPropertyChanged(nameof(IsLightTheme));

    public bool IsLightTheme
    {
        get => !IsDarkTheme;
        set => IsDarkTheme = !value;
    }

    /// <summary>Applies edited values back to the live settings object.</summary>
    public void ApplyTo(AppSettings settings)
    {
        settings.FontFamily = FontFamily;
        settings.FontSize = FontSize;
        settings.MaxLinesInMemory = MaxLinesInMemory;
        settings.FollowTailOnOpen = FollowTailOnOpen;
        settings.Theme = IsDarkTheme ? "Dark" : "Light";
    }
}
