using System.IO;
using LogTailPro.Models;
using Newtonsoft.Json;

namespace LogTailPro.Services;

/// <summary>Loads and saves application settings to %AppData%\LogTailPro\settings.json.</summary>
public sealed class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LogTailPro",
        "settings.json");

    /// <summary>Loads settings from disk, returning defaults if the file is missing or corrupt.</summary>
    public AppSettings Load()
    {
        AppSettings settings;

        if (!File.Exists(SettingsPath))
        {
            settings = new AppSettings();
        }
        else
        {
            try
            {
                string json = File.ReadAllText(SettingsPath);
                settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                settings = new AppSettings();
            }
        }

        if (settings.HighlightRules.Count == 0)
            settings.HighlightRules = CreateDefaultRules();

        return settings;
    }

    private static List<HighlightRule> CreateDefaultRules() =>
    [
        new HighlightRule { Pattern = @"\bERROR\b",      IsRegex = true, Priority = 0, ForegroundColor = "#FFFFFF", BackgroundColor = "#CC2222" },
        new HighlightRule { Pattern = @"\bEXCEPTION\b",  IsRegex = true, Priority = 1, ForegroundColor = "#FFFFFF", BackgroundColor = "#CC2222" },
        new HighlightRule { Pattern = @"\bWARN(ING)?\b", IsRegex = true, Priority = 2, ForegroundColor = "#000000", BackgroundColor = "#FFA500" },
        new HighlightRule { Pattern = @"\bINFO\b",       IsRegex = true, Priority = 3, ForegroundColor = "#A8D8A8", BackgroundColor = "#1E1E1E" },
        new HighlightRule { Pattern = @"\bDEBUG\b",      IsRegex = true, Priority = 4, ForegroundColor = "#888888", BackgroundColor = "#1E1E1E" },
    ];

    /// <summary>Saves settings to disk, creating directories as needed.</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
    }
}
