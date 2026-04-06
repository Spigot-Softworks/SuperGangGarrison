using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class ServerPluginContext(
    string pluginId,
    string pluginDirectory,
    string configDirectory,
    string mapsDirectory,
    IOpenGarrisonServerReadOnlyState serverState,
    IOpenGarrisonServerAdminOperations adminOperations,
    Action<byte, string, string, string> sendMessageToClient,
    Action<string, string, string> broadcastMessageToClients,
    PluginCommandRegistry commandRegistry,
    Action<string> log) : IOpenGarrisonServerPluginContext
{
    public string PluginId { get; } = pluginId;

    public string PluginDirectory { get; } = pluginDirectory;

    public string ConfigDirectory { get; } = configDirectory;

    public string MapsDirectory { get; } = mapsDirectory;

    public IOpenGarrisonServerReadOnlyState ServerState { get; } = serverState;

    public IOpenGarrisonServerAdminOperations AdminOperations { get; } = adminOperations;

    public void SendMessageToClient(byte slot, string targetPluginId, string messageType, string payload)
    {
        sendMessageToClient(slot, targetPluginId, messageType, payload);
    }

    public void BroadcastMessageToClients(string targetPluginId, string messageType, string payload)
    {
        broadcastMessageToClients(targetPluginId, messageType, payload);
    }

    public void RegisterCommand(IOpenGarrisonServerCommand command)
    {
        commandRegistry.RegisterPluginCommand(command, PluginId);
    }

    public void Log(string message)
    {
        log($"[plugin:{PluginId}] {message}");
    }
}
