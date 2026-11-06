using System.IO;

namespace LogTailPro.Services;

/// <summary>Persists the docking layout (AvalonDock XML) under %AppData%\LogTailPro.</summary>
public sealed class DockLayoutService
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LogTailPro", "layout.v2.xml"); // v2: Search and Results are separate stacked panes

    /// <summary>Returns the saved layout file path if it exists, otherwise null.</summary>
    public string? GetSavedLayoutPath() => File.Exists(_path) ? _path : null;

    /// <summary>Full path the layout is written to.</summary>
    public string LayoutPath => _path;

    /// <summary>Ensures the target directory exists before a layout is written.</summary>
    public void EnsureDirectory() => Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

    /// <summary>Deletes the saved layout so the default layout is used next time.</summary>
    public void Delete()
    {
        try { if (File.Exists(_path)) File.Delete(_path); }
        catch (IOException) { /* best effort */ }
    }
}
