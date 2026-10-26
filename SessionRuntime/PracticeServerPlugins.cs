using OpenGarrison.Core;
using OpenGarrison.Server;

namespace OpenGarrison.SessionRuntime;

/// <summary>
/// Runs server Lua plugins against an offline practice world. Plugin content is removed
/// again on dispose.
/// </summary>
public sealed class PracticeServerPlugins : IDisposable
{
    private readonly PracticePluginRuntime _runtime;

    public PracticeServerPlugins(
        SimulationWorld world,
        string pluginsDirectory,
        string pluginConfigRoot,
        string mapsDirectory,
        Action<string> log)
    {
        _runtime = new PracticePluginRuntime(world, pluginsDirectory, pluginConfigRoot, mapsDirectory, log);
    }

    public IReadOnlyList<string> LoadedPluginIds => _runtime.LoadedPluginIds;

    public void Start() => _runtime.Start();

    /// <summary>Call once per practice simulation tick.</summary>
    public void Tick() => _runtime.Tick();

    public void Dispose() => _runtime.Dispose();
}
