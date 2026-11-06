using System.IO;
using LogTailPro.Models;
using Newtonsoft.Json;

namespace LogTailPro.Services;

/// <summary>Result of loading a workspace file, including any missing files or warnings.</summary>
public sealed record WorkspaceLoadResult(
    Workspace? Workspace,
    bool Success,
    string? ErrorMessage,
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> UnrestorableTabs);

/// <summary>
/// Handles atomic, versioned serialization and deserialization of LogTailPro workspaces (.ltpw).
/// </summary>
public sealed class WorkspaceService
{
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// Atomically saves a workspace to the specified path.
    /// Writes to a temporary file first and replaces atomically to prevent corruption on interruption.
    /// </summary>
    public async Task SaveAsync(string filePath, Workspace workspace, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(workspace);

        workspace.SchemaVersion = CurrentSchemaVersion;

        string directory = Path.GetDirectoryName(Path.GetFullPath(filePath))
            ?? throw new DirectoryNotFoundException($"Directory not found for path: {filePath}");
        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            string json = JsonConvert.SerializeObject(workspace, Formatting.Indented);
            ct.ThrowIfCancellationRequested();

            await File.WriteAllTextAsync(tempPath, json, System.Text.Encoding.UTF8, ct);
            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw;
        }
    }

    /// <summary>
    /// Loads and validates a workspace from disk, checking for missing files and legacy format compatibility.
    /// </summary>
    public async Task<WorkspaceLoadResult> LoadAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return new WorkspaceLoadResult(null, false, $"File not found: {filePath}", Array.Empty<string>(), Array.Empty<string>());
        }

        try
        {
            string json = await File.ReadAllTextAsync(filePath, ct);
            ct.ThrowIfCancellationRequested();

            Workspace? ws = JsonConvert.DeserializeObject<Workspace>(json);
            if (ws == null)
            {
                return new WorkspaceLoadResult(null, false, "The workspace file is empty or invalid.", Array.Empty<string>(), Array.Empty<string>());
            }

            // Upgrade legacy schema if necessary
            if (ws.SchemaVersion < 2)
            {
                ws.SchemaVersion = 2;
                foreach (WorkspaceTab tab in ws.Tabs)
                {
                    // Legacy tabs were all file tabs
                    tab.TabType = WorkspaceTabType.File;
                }
            }

            // Detect missing files
            List<string> missingFiles = new();
            foreach (WorkspaceTab tab in ws.Tabs)
            {
                if (tab.TabType == WorkspaceTabType.File)
                {
                    if (!string.IsNullOrWhiteSpace(tab.FilePath) && !File.Exists(tab.FilePath))
                    {
                        missingFiles.Add(tab.FilePath);
                    }
                }
            }

            return new WorkspaceLoadResult(ws, true, null, missingFiles, Array.Empty<string>());
        }
        catch (JsonException ex)
        {
            return new WorkspaceLoadResult(null, false, $"Failed to parse workspace JSON: {ex.Message}", Array.Empty<string>(), Array.Empty<string>());
        }
        catch (Exception ex)
        {
            return new WorkspaceLoadResult(null, false, $"Error reading workspace file: {ex.Message}", Array.Empty<string>(), Array.Empty<string>());
        }
    }
}
