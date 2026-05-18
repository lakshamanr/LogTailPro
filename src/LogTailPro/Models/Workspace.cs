namespace LogTailPro.Models;

/// <summary>A saved workspace — the full set of open tabs and their search/filter state.</summary>
public sealed class Workspace
{
    public string Name { get; set; } = "Workspace";
    public List<WorkspaceTab> Tabs { get; set; } = new();
    public int ActiveTabIndex { get; set; }
}
