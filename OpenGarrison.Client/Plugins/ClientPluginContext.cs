using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client;

internal sealed class ClientPluginContext(
    string pluginId,
    string pluginDirectory,
    string configDirectory,
    IOpenGarrisonClientReadOnlyState clientState,
    Action<string> log) : IOpenGarrisonClientPluginContext
{
    public string PluginId { get; } = pluginId;

    public string PluginDirectory { get; } = pluginDirectory;

    public string ConfigDirectory { get; } = configDirectory;

    public IOpenGarrisonClientReadOnlyState ClientState { get; } = clientState;

    public void Log(string message)
    {
        log($"[plugin:{PluginId}] {message}");
    }
}
