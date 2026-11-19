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

        // 100,000 was the old built-in cap that truncated long logs; it now means "unlimited".
        if (settings.MaxLinesInMemory == 100_000)
            settings.MaxLinesInMemory = 0;

        if (settings.HighlightRules.Count == 0)
            settings.HighlightRules = CreateDefaultRules();
        else
            MigrateLegacyDefaultRules(settings);

        return settings;
    }

    private static List<HighlightRule> CreateDefaultRules() =>
    [
        new HighlightRule { Pattern = @"\b(ERROR|FATAL|CRITICAL)\b", IsRegex = true, Priority = 0, ForegroundColor = "#991B1B", BackgroundColor = "#FEE2E2" },
        new HighlightRule { Pattern = @"\bEXCEPTION\b",              IsRegex = true, Priority = 1, ForegroundColor = "#991B1B", BackgroundColor = "#FEE2E2" },
        new HighlightRule { Pattern = @"\bWARN(ING)?\b",             IsRegex = true, Priority = 2, ForegroundColor = "#92400E", BackgroundColor = "#FEF3C7" },
        new HighlightRule { Pattern = @"\bINFO\b",                   IsRegex = true, Priority = 3, ForegroundColor = "#166534", BackgroundColor = "#00FFFFFF" },
        new HighlightRule { Pattern = @"\bDEBUG\b",                  IsRegex = true, Priority = 4, ForegroundColor = "#6B7280", BackgroundColor = "#00FFFFFF" },
    ];

    /// <summary>
    /// Upgrades legacy space-padded patterns and legacy dark-theme default colors.
    /// Rules the user has customised are preserved.
    /// </summary>
    private static void MigrateLegacyDefaultRules(AppSettings settings)
    {
        foreach (HighlightRule r in settings.HighlightRules)
        {
            // Upgrade space-padded patterns that failed on standard log formats ([ERROR], ERROR:, {"@l":"Error"})
            if (r.Pattern == @" ERROR ")      r.Pattern = @"\b(ERROR|FATAL|CRITICAL)\b";
            if (r.Pattern == @" EXCEPTION ")  r.Pattern = @"\bEXCEPTION\b";
            if (r.Pattern == @" WARN(ING)? ") r.Pattern = @"\bWARN(ING)?\b";
            if (r.Pattern == @" INFO ")       r.Pattern = @"\bINFO\b";
            if (r.Pattern == @" DEBUG ")      r.Pattern = @"\bDEBUG\b";

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
