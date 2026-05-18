using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;

namespace LogTailPro.ViewModels;

/// <summary>ViewModel for the Log Parser configuration dialog.</summary>
public sealed partial class LogParserViewModel : ObservableObject
{
    public ObservableCollection<LogParserConfig> Presets { get; } = new();

    [ObservableProperty] private LogParserConfig? _selectedPreset;
    [ObservableProperty] private string _customPattern = string.Empty;
    [ObservableProperty] private string _sampleLine = string.Empty;
    [ObservableProperty] private string _parseResult = string.Empty;

    public string ActivePattern => SelectedPreset?.Pattern ?? CustomPattern;

    public LogParserViewModel(string currentPreset)
    {
        foreach (LogParserConfig preset in LogParserConfig.BuiltInPresets())
            Presets.Add(preset);

        Presets.Add(new LogParserConfig { Name = "Custom…", Pattern = string.Empty });

        SelectedPreset = Presets.FirstOrDefault(p => p.Name == currentPreset)
                         ?? Presets.First();
    }

    partial void OnSelectedPresetChanged(LogParserConfig? value)
    {
        if (value != null && value.Name != "Custom…")
            CustomPattern = value.Pattern;
        TestPattern();
    }

    partial void OnSampleLineChanged(string value) => TestPattern();
    partial void OnCustomPatternChanged(string value) => TestPattern();

    [RelayCommand]
    private void TestPattern()
    {
        string pattern = ActivePattern;
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(SampleLine))
        {
            ParseResult = string.Empty;
            return;
        }

        try
        {
            Regex rx = new(pattern, RegexOptions.None, TimeSpan.FromSeconds(2));
            Match m = rx.Match(SampleLine);
            if (!m.Success) { ParseResult = "No match."; return; }

            System.Text.StringBuilder sb = new();
            foreach (string name in new[] { "Timestamp", "Level", "Thread", "Class", "Message" })
            {
                Group g = m.Groups[name];
                if (g.Success) sb.AppendLine($"{name,-12}: {g.Value}");
            }
            ParseResult = sb.Length > 0 ? sb.ToString() : "Matched — no named groups captured.";
        }
        catch (Exception ex)
        {
            ParseResult = $"Pattern error: {ex.Message}";
        }
    }
}
