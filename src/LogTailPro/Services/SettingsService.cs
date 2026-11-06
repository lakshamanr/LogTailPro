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
        else
            MigrateLegacyDefaultColors(settings);

        return settings;
    }

    private static List<HighlightRule> CreateDefaultRules() =>
    [
        new HighlightRule { Pattern = @"ERROR",      IsRegex = true, Priority = 0, ForegroundColor = "#991B1B", BackgroundColor = "#FEE2E2" },
        new HighlightRule { Pattern = @"EXCEPTION",  IsRegex = true, Priority = 1, ForegroundColor = "#991B1B", BackgroundColor = "#FEE2E2" },
        new HighlightRule { Pattern = @"WARN(ING)?", IsRegex = true, Priority = 2, ForegroundColor = "#92400E", BackgroundColor = "#FEF3C7" },
        new HighlightRule { Pattern = @"INFO",       IsRegex = true, Priority = 3, ForegroundColor = "#166534", BackgroundColor = "#00FFFFFF" },
        new HighlightRule { Pattern = @"DEBUG",      IsRegex = true, Priority = 4, ForegroundColor = "#6B7280", BackgroundColor = "#00FFFFFF" },
    ];

    /// <summary>
    /// Upgrades the untouched colours of the old dark-theme default rules to the soft-light palette.
    /// Rules the user has customised (any other colour combination) are left alone.
    /// </summary>
    private static void MigrateLegacyDefaultColors(AppSettings settings)
    {
        foreach (HighlightRule r in settings.HighlightRules)
        {
            string fg = r.ForegroundColor.ToUpperInvariant();
            string bg = r.BackgroundColor.ToUpperInvariant();
            (string Fg, string Bg)? upgraded = (fg, bg) switch
            {
                ("#FFFFFF", "#CC2222") => ("#991B1B", "#FEE2E2"),
                ("#000000", "#FFA500") => ("#92400E", "#FEF3C7"),
                ("#A8D8A8", "#1E1E1E") => ("#166534", "#00FFFFFF"),
                ("#888888", "#1E1E1E") => ("#6B7280", "#00FFFFFF"),
                _ => null
            };
            if (upgraded is { } u)
            {
                r.ForegroundColor = u.Fg;
                r.BackgroundColor = u.Bg;
            }
        }
    }

    /// <summary>Saves settings to disk, creating directories as needed.</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
    }
}
