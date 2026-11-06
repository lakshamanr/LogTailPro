namespace LogTailPro.Models;

/// <summary>
/// Represents a documented feature or option in LogTailPro with an in-depth demo and usage details.
/// </summary>
public sealed class HelpTopic
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required string Title { get; init; }
    public required string ShortTitle { get; init; }
    public string Shortcut { get; init; } = string.Empty;
    public string UiLocation { get; init; } = string.Empty;
    public required string Overview { get; init; }
    public required string DemoScenario { get; init; }
    public required string StepByStep { get; init; }
    public required string UnderTheHood { get; init; }

    public bool HasShortcut => !string.IsNullOrWhiteSpace(Shortcut);
}
