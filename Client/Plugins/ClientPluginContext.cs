using OpenGarrison.Client.Plugins;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client;

internal sealed class ClientPluginContext(
    string pluginId,
    string pluginDirectory,
    string configDirectory,
    GraphicsDevice graphicsDevice,
    IOpenGarrisonClientReadOnlyState clientState,
    Action<string> log) : IOpenGarrisonClientPluginContext
{
    public string PluginId { get; } = pluginId;

    public string PluginDirectory { get; } = pluginDirectory;

    public string ConfigDirectory { get; } = configDirectory;

    public GraphicsDevice GraphicsDevice { get; } = graphicsDevice;

    public IOpenGarrisonClientReadOnlyState ClientState { get; } = clientState;

    public void Log(string message)
    {
        log($"[plugin:{PluginId}] {message}");
    }
}
