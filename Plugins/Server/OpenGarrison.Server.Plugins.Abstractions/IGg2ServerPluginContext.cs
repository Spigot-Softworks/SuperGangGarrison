namespace OpenGarrison.Server.Plugins;

public interface IOpenGarrisonServerPluginContext
{
    string PluginId { get; }

    string PluginDirectory { get; }

    string ConfigDirectory { get; }

    string MapsDirectory { get; }

    IOpenGarrisonServerReadOnlyState ServerState { get; }

    IOpenGarrisonServerAdminOperations AdminOperations { get; }

    void SendMessageToClient(byte slot, string targetPluginId, string messageType, string payload);

    void BroadcastMessageToClients(string targetPluginId, string messageType, string payload);

    void RegisterCommand(IOpenGarrisonServerCommand command);

    void Log(string message);
}
