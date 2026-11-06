namespace LogTailPro.Models;

/// <summary>A saved workspace — the full set of open tabs, timeline definitions, filters, highlights, and docking layout.</summary>
public sealed class Workspace
{
    /// <summary>Schema version (1 = legacy; 2 = full workspaces with timelines, correlation, layout, scroll).</summary>
    public int SchemaVersion { get; set; } = 2;

    public string Name { get; set; } = "Workspace";

    /// <summary>Identifier (FilePath or FileName) of the active document when saved.</summary>
    public string? ActiveTabId { get; set; }

    /// <summary>Legacy fallback index if ActiveTabId is not matched.</summary>
    public int ActiveTabIndex { get; set; }

    /// <summary>Tabs in display order.</summary>
    public List<WorkspaceTab> Tabs { get; set; } = new();

    /// <summary>Workspace-specific highlight rules (optional; null uses user global rules).</summary>
    public List<HighlightRule>? HighlightRules { get; set; }

    /// <summary>AvalonDock docking layout XML (persists panel positions and docks).</summary>
    public string? DockLayoutXml { get; set; }
}
