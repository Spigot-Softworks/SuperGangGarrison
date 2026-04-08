using OpenGarrison.PluginHost;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server.Plugins;

public interface IOpenGarrisonServerPluginContext : IOpenGarrisonPluginHostContext
{
    string MapsDirectory { get; }

    IOpenGarrisonServerReadOnlyState ServerState { get; }

    IOpenGarrisonServerAdminOperations AdminOperations { get; }

    IOpenGarrisonServerCvarRegistry Cvars { get; }

    IOpenGarrisonServerScheduler Scheduler { get; }

    void SendMessageToClient(
        byte slot,
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion);

    void BroadcastMessageToClients(
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion);

    void SendMessageToClient(byte slot, string targetPluginId, string messageType, string payload)
    {
        SendMessageToClient(slot, targetPluginId, messageType, payload, PluginMessagePayloadFormat.Text, schemaVersion: 1);
    }

    void BroadcastMessageToClients(string targetPluginId, string messageType, string payload)
    {
        BroadcastMessageToClients(targetPluginId, messageType, payload, PluginMessagePayloadFormat.Text, schemaVersion: 1);
    }

    bool SetPlayerReplicatedStateInt(byte slot, string stateKey, int value);

    bool SetPlayerReplicatedStateFloat(byte slot, string stateKey, float value);

    bool SetPlayerReplicatedStateBool(byte slot, string stateKey, bool value);

    bool ClearPlayerReplicatedState(byte slot, string stateKey);

    void RegisterCommand(IOpenGarrisonServerCommand command, OpenGarrisonServerAdminPermissions requiredPermissions);

    void RegisterCommand(IOpenGarrisonServerCommand command)
    {
        RegisterCommand(command, OpenGarrisonServerAdminPermissions.None);
    }
}
