# LogTailPro Plugin Architecture & SDK Standard (v1.0)

This standard defines the mandatory technical specifications, lifecycle rules, directory layouts, and API contracts for developing external plugins for **LogTailPro**.

---

## 1. Core Principles & Philosophy

LogTailPro follows the **Notepad++ Plugin Philosophy**:
1. **Zero-Coupling:** Plugins live in their own repositories and compile into standalone binaries. They reference only `LogTailPro.PluginContract.dll`.
2. **Crash Isolation:** Plugins run inside an isolated .NET `AssemblyLoadContext`. A failure or unhandled exception within a plugin must never crash the main LogTailPro window.
3. **Multi-Plugin Concurrency:** LogTailPro supports an unlimited number of concurrent plugins. Each plugin registers its own submenu under the top-level **Plugins** menu.
4. **Hot Reloadability:** Plugins can be added, updated, or reloaded at runtime via **Plugins > Plugins Manager…** or **Reload All Plugins** without restarting LogTailPro.

---

## 2. Directory & Packaging Standard

LogTailPro scans for plugins in three designated locations (in order of priority):

1. **Custom Plugin Directories** (configured in **Plugins > Plugins Manager… > Add Plugin / Folder…**, persisted in `%APPDATA%\LogTailPro\settings.json`)
2. **User Plugins Directory:** `%LOCALAPPDATA%\LogTailPro\plugins\<PluginFolder>\`
3. **Application Directory:** `<LogTailPro_InstallDir>\plugins\<PluginFolder>\`

### File Layout inside a Plugin Folder

```text
<PluginFolder>/
├── plugin.json                          # (Required) Plugin Manifest
├── <MainAssembly>.dll                   # (Required) Compiled Plugin DLL implementing ILogTailPlugin
├── <MainAssembly>.deps.json             # (Recommended) Dependency manifest for isolated loading
├── <MainAssembly>.pdb                   # (Optional) Debug symbols
└── [Third-Party Dependencies].dll       # (Optional) OxyPlot, SkiaSharp, Newtonsoft.Json, etc.
```

---

## 3. Plugin Manifest Specification (`plugin.json`)

Every plugin must contain a valid `plugin.json` at its folder root:

```json
{
  "id": "com.company.pluginid",
  "name": "Human Readable Plugin Name",
  "version": "1.0.0",
  "author": "Your Name / Organization",
  "description": "Short summary of what the plugin does.",
  "entryAssembly": "MainAssembly.dll",
  "minAppVersion": "2.10.0",
  "menuItems": [
    {
      "id": "cmd_primary_action",
      "text": "Open Dashboard",
      "shortcut": "Ctrl+Shift+D"
    },
    {
      "id": "cmd_secondary_action",
      "text": "Run Trace Diagnostics",
      "shortcut": ""
    }
  ]
}
```

### Manifest Fields
| Field | Type | Description |
|---|---|---|
| `id` | string | Unique reverse-DNS identifier (e.g. `com.example.streamdataflow`). Used for deduplication. |
| `name` | string | Display name shown on the Plugins submenu and Plugins Manager. |
| `version` | string | SemVer version string (e.g. `1.0.0`). |
| `author` | string | Author name or email. |
| `description` | string | Description shown in the Plugins Manager dialog. |
| `entryAssembly` | string | File name of the entry assembly DLL. |
| `minAppVersion` | string | Minimum compatible LogTailPro version (e.g. `2.10.0`). |
| `menuItems` | array | Array of menu action descriptors (`id`, `text`, `shortcut`). |

---

## 4. Contract Interfaces (`LogTailPro.PluginContract.dll`)

### `ILogTailPlugin`
The entry class in `entryAssembly` must implement `ILogTailPlugin`:

```csharp
namespace LogTailPro.PluginContract;

public interface ILogTailPlugin
{
    string PluginId { get; }
    string PluginName { get; }
    string Version { get; }

    Task InitializeAsync(IPluginHost host);
    void ExecuteCommand(string commandId, ILogContext context);
    void Shutdown();
}
```

### `IPluginHost` (Host Services provided to Plugin)
```csharp
public interface IPluginHost
{
    // Navigation
    void NavigateToLine(int lineNumber);
    void OpenLogFile(string filePath);

    // Notifications
    void SetStatusMessage(string message);

    // Real-Time Events
    event EventHandler<LinesAppendedEventArgs>? LinesAppended;
}
```

### `ILogContext` (Editor Context passed during command invocation)
```csharp
public interface ILogContext
{
    string? FilePath { get; }
    int CurrentLineNumber { get; }
    string? SelectedText { get; }
    IReadOnlyList<string> SelectedLines { get; }
    string? CurrentFilterExpression { get; }
    int TotalLineCount { get; }
}
```

---

## 5. UI & Threading Architecture Rules

1. **WPF UI Windows:**
   - Any window created by a plugin (`new StudioWindow().Show()`) executes on the WPF UI Dispatcher thread.
   - Do NOT run heavy data parsing or file I/O on the UI thread. Use `Task.Run()` for background processing.
2. **Real-time Tail Streaming (`LinesAppended`):**
   - The `host.LinesAppended` event fires whenever new log chunks are ingested.
   - Handle this event asynchronously or dispatch updates to UI controls using `Dispatcher.InvokeAsync()`.
3. **Resource Dictionaries & Themes:**
   - Plugin controls can define their own dark/light styles or use standard WPF controls.
   - Use `WindowStartupLocation="CenterOwner"` or `CenterScreen`.

---

## 6. Project Setup Guide (`.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows7.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="LogTailPro.PluginContract">
      <HintPath>..\..\LogTailPro\publish\LogTailPro.PluginContract.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <None Update="plugin.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```
> **Note:** Set `<Private>false</Private>` on `LogTailPro.PluginContract` so the contract DLL is not redundantly copied into the plugin directory.

---

## 7. Verification & Compliance Checklist

Before publishing or deploying any plugin:
- [ ] Assembly targets `net9.0-windows7.0`.
- [ ] Implements `ILogTailPlugin` with a public parameterless constructor.
- [ ] `plugin.json` is located in the output folder alongside the assembly.
- [ ] All `commandId` strings in `plugin.json` match `ExecuteCommand(string commandId, ...)` switch cases.
- [ ] `Shutdown()` cleanly disposes timers, background workers, and event handlers (`host.LinesAppended -= OnLinesAppended`).
- [ ] Can be loaded via **Plugins > Plugins Manager… > Add Plugin / Folder…**.
