#nullable enable

using System;
using System.IO;
using OpenGarrison.Core;
using OpenGarrison.SessionRuntime;

namespace OpenGarrison.Client;

public partial class Game1
{
    // Practice has no server process, so server Lua plugins run here against the practice world.
    private PracticeServerPlugins? _practicePluginRuntime;

    public void RestartPracticePluginRuntime()
    {
        StopPracticePluginRuntime();
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        var runtime = new PracticeServerPlugins(
            _world,
            Path.Combine(RuntimePaths.ApplicationRoot, "Plugins"),
            Path.Combine(RuntimePaths.ConfigDirectory, "plugins"),
            Path.Combine(RuntimePaths.ApplicationRoot, "Maps"),
            AddConsoleLine);
        try
        {
            runtime.Start();
        }
        catch (Exception ex)
        {
            AddConsoleLine($"[practice] server plugins failed to start: {ex.Message}");
            runtime.Dispose();
            return;
        }

        _practicePluginRuntime = runtime;
        if (runtime.LoadedPluginIds.Count > 0)
        {
            AddConsoleLine($"[practice] server plugins: {string.Join(", ", runtime.LoadedPluginIds)}");
        }
    }

    public void StopPracticePluginRuntime()
    {
        _practicePluginRuntime?.Dispose();
        _practicePluginRuntime = null;
    }

    private void TickPracticePluginRuntime()
    {
        _practicePluginRuntime?.Tick();
    }
}
