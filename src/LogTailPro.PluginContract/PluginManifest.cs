using System.Collections.Generic;

namespace LogTailPro.PluginContract;

/// <summary>
/// Deserialized representation of a plugin's plugin.json metadata file.
/// </summary>
public class PluginManifest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EntryAssembly { get; set; } = string.Empty;
    public string? EntryClass { get; set; }
    public string MinLogTailProVersion { get; set; } = "2.10.0";
    public PluginMenuDefinition? Menu { get; set; }
}

public class PluginMenuDefinition
{
    public string Header { get; set; } = string.Empty;
    public List<PluginMenuItem> Items { get; set; } = new();
}

public class PluginMenuItem
{
    public string Id { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string? Shortcut { get; set; }
    public string? IconResourceKey { get; set; }
    public bool ShowInContextMenu { get; set; } = true;
    public bool SeparatorBefore { get; set; }
}
