using System.Threading.Tasks;

namespace LogTailPro.PluginContract;

/// <summary>
/// Master interface that every LogTailPro plugin must implement.
/// </summary>
public interface ILogTailPlugin
{
    /// <summary>
    /// Unique identifier matching plugin.json (e.g., "com.logtailpro.streamserver.dataflow")
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Human-readable plugin name displayed in the Plugins menu.
    /// </summary>
    string PluginName { get; }

    /// <summary>
    /// SemVer version string of the plugin (e.g. "1.0.0").
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Called when LogTailPro loads the plugin during startup.
    /// </summary>
    Task InitializeAsync(IPluginHost host);

    /// <summary>
    /// Called when the user clicks a menu item or executes a command associated with this plugin.
    /// </summary>
    /// <param name="commandId">The ID of the command defined in plugin.json or registered dynamically.</param>
    /// <param name="context">Snapshot of the currently active editor/tab context.</param>
    void ExecuteCommand(string commandId, ILogContext context);

    /// <summary>
    /// Called when LogTailPro is shutting down or the plugin is being unloaded.
    /// </summary>
    void Shutdown();
}
