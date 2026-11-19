using System;
using System.Threading.Tasks;
using LogTailPro.PluginContract;

namespace TestPlugins;

public class ValidTestPlugin : ILogTailPlugin
{
    public static int InitializeCount;
    public static int ShutdownCount;
    public static int CommandExecutedCount;
    public static bool LinesAppendedReceived;
    public static string? LastCommandId;

    public string PluginId => "com.logtailpro.test.valid";
    public string PluginName => "Valid Test Plugin";
    public string Version => "1.0.0";

    public Task InitializeAsync(IPluginHost host)
    {
        InitializeCount++;
        host.RegisterMenuItem(PluginId, new PluginMenuItem
        {
            Id = "valid_cmd",
            Header = "Execute Valid Command"
        });

        host.LinesAppended += (sender, args) =>
        {
            LinesAppendedReceived = true;
        };

        return Task.CompletedTask;
    }

    public void ExecuteCommand(string commandId, ILogContext context)
    {
        CommandExecutedCount++;
        LastCommandId = commandId;
    }

    public void Shutdown()
    {
        ShutdownCount++;
    }
}

public class FailingTestPlugin : ILogTailPlugin
{
    public string PluginId => "com.logtailpro.test.failing";
    public string PluginName => "Failing Test Plugin";
    public string Version => "1.0.0";

    public Task InitializeAsync(IPluginHost host)
    {
        throw new InvalidOperationException("Intentional failure in FailingTestPlugin.InitializeAsync");
    }

    public void ExecuteCommand(string commandId, ILogContext context) { }
    public void Shutdown() { }
}

public class TimeoutTestPlugin : ILogTailPlugin
{
    public static bool PostTimeoutCallbackAttempted;

    public string PluginId => "com.logtailpro.test.timeout";
    public string PluginName => "Timeout Test Plugin";
    public string Version => "1.0.0";

    public async Task InitializeAsync(IPluginHost host)
    {
        // Deliberately wait longer than the configured timeout
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Attempt callback after timeout to verify it gets blocked by PluginHostSession
        try
        {
            host.RegisterMenuItem(PluginId, new PluginMenuItem { Id = "late_cmd", Header = "Late Command" });
            PostTimeoutCallbackAttempted = true;
        }
        catch
        {
            // Suppress
        }
    }

    public void ExecuteCommand(string commandId, ILogContext context) { }
    public void Shutdown() { }
}

public class SiblingAPlugin : ILogTailPlugin
{
    public string PluginId => "com.logtailpro.test.siblinga";
    public string PluginName => "Sibling Plugin A";
    public string Version => "1.0.0";

    public Task InitializeAsync(IPluginHost host) => Task.CompletedTask;
    public void ExecuteCommand(string commandId, ILogContext context) { }
    public void Shutdown() { }
}

public class SiblingBPlugin : ILogTailPlugin
{
    public string PluginId => "com.logtailpro.test.siblingb";
    public string PluginName => "Sibling Plugin B";
    public string Version => "1.0.0";

    public Task InitializeAsync(IPluginHost host) => Task.CompletedTask;
    public void ExecuteCommand(string commandId, ILogContext context) { }
    public void Shutdown() { }
}

public class FallbackValidPlugin : ILogTailPlugin
{
    public static bool InitializedSuccessfully;

    public string PluginId => "com.logtailpro.test.fallback";
    public string PluginName => "Fallback Valid Plugin";
    public string Version => "1.0.0";

    public Task InitializeAsync(IPluginHost host)
    {
        InitializedSuccessfully = true;
        return Task.CompletedTask;
    }

    public void ExecuteCommand(string commandId, ILogContext context) { }
    public void Shutdown() { }
}

public class FallbackFailingPlugin : ILogTailPlugin
{
    public string PluginId => "com.logtailpro.test.fallback";
    public string PluginName => "Fallback Failing Plugin";
    public string Version => "1.0.0";

    public Task InitializeAsync(IPluginHost host)
    {
        throw new InvalidOperationException("Higher-priority copy failed intentionally");
    }

    public void ExecuteCommand(string commandId, ILogContext context) { }
    public void Shutdown() { }
}

public class RootDiscoveryPlugin : ILogTailPlugin
{
    public string PluginId => "com.logtailpro.test.rootdiscovery";
    public string PluginName => "Root Discovery Plugin";
    public string Version => "1.0.0";
    public Task InitializeAsync(IPluginHost host) => Task.CompletedTask;
    public void ExecuteCommand(string commandId, ILogContext context) { }
    public void Shutdown() { }
}

public class SharedChildAPlugin : ILogTailPlugin
{
    public string PluginId => "com.logtailpro.test.sharedchilda";
    public string PluginName => "Shared Child A Plugin";
    public string Version => "1.0.0";
    public Task InitializeAsync(IPluginHost host) => Task.CompletedTask;
    public void ExecuteCommand(string commandId, ILogContext context) { }
    public void Shutdown() { }
}

public class SharedChildBPlugin : ILogTailPlugin
{
    public string PluginId => "com.logtailpro.test.sharedchildb";
    public string PluginName => "Shared Child B Plugin";
    public string Version => "1.0.0";
    public Task InitializeAsync(IPluginHost host) => Task.CompletedTask;
    public void ExecuteCommand(string commandId, ILogContext context) { }
    public void Shutdown() { }
}

public class SlowSyncInitPlugin : ILogTailPlugin
{
    public string PluginId => "com.logtailpro.test.slowsync";
    public string PluginName => "Slow Sync Init Plugin";
    public string Version => "1.0.0";

    public SlowSyncInitPlugin()
    {
        // Deliberately delay constructor slightly to verify instrumentation
        System.Threading.Thread.Sleep(60);
    }

    public Task InitializeAsync(IPluginHost host)
    {
        // Deliberately delay synchronous portion to verify instrumentation
        System.Threading.Thread.Sleep(60);
        return Task.CompletedTask;
    }

    public void ExecuteCommand(string commandId, ILogContext context) { }
    public void Shutdown()
    {
        System.Threading.Thread.Sleep(60);
    }
}

public class ManagedTestPlugin : ILogTailPlugin
{
    public string PluginId => "com.logtailpro.test.managed";
    public string PluginName => "Managed Test Plugin";
    public string Version => "1.0.0";
    public Task InitializeAsync(IPluginHost host) => Task.CompletedTask;
    public void ExecuteCommand(string commandId, ILogContext context) { }
    public void Shutdown() { }
}

public class ManagedLockedPlugin : ILogTailPlugin
{
    public string PluginId => "com.logtailpro.test.managed.locked";
    public string PluginName => "Managed Locked Plugin";
    public string Version => "1.0.0";
    public Task InitializeAsync(IPluginHost host) => Task.CompletedTask;
    public void ExecuteCommand(string commandId, ILogContext context) { }
    public void Shutdown() { }
}

public class ManagedCleanPlugin : ILogTailPlugin
{
    public string PluginId => "com.logtailpro.test.managed.clean";
    public string PluginName => "Managed Clean Plugin";
    public string Version => "1.0.0";
    public Task InitializeAsync(IPluginHost host) => Task.CompletedTask;
    public void ExecuteCommand(string commandId, ILogContext context) { }
    public void Shutdown() { }
}
