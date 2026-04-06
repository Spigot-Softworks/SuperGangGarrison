using OpenGarrison.Server.Plugins;
using OpenGarrison.PluginHost;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal sealed class ServerPluginContext(
    string pluginId,
    string pluginDirectory,
    string configDirectory,
    OpenGarrisonPluginManifest manifest,
    OpenGarrisonPluginHostApi hostApi,
    string mapsDirectory,
    IOpenGarrisonServerReadOnlyState serverState,
    IOpenGarrisonServerAdminOperations adminOperations,
    Action<byte, string, string, string, PluginMessagePayloadFormat, ushort> sendMessageToClient,
    Action<string, string, string, PluginMessagePayloadFormat, ushort> broadcastMessageToClients,
    Func<string, byte, string, int, bool> setPlayerReplicatedStateInt,
    Func<string, byte, string, float, bool> setPlayerReplicatedStateFloat,
    Func<string, byte, string, bool, bool> setPlayerReplicatedStateBool,
    Func<string, byte, string, bool> clearPlayerReplicatedState,
    PluginCommandRegistry commandRegistry,
    Action<string> log) : IOpenGarrisonServerPluginContext
{
    public string PluginId { get; } = pluginId;

    public string PluginDirectory { get; } = pluginDirectory;

    public string ConfigDirectory { get; } = configDirectory;

    public OpenGarrisonPluginManifest Manifest { get; } = manifest;

    public OpenGarrisonPluginHostApi HostApi { get; } = hostApi;

    public string MapsDirectory { get; } = mapsDirectory;

    public IOpenGarrisonServerReadOnlyState ServerState { get; } = serverState;

    public IOpenGarrisonServerAdminOperations AdminOperations { get; } = adminOperations;

    public void SendMessageToClient(byte slot, string targetPluginId, string messageType, string payload, PluginMessagePayloadFormat payloadFormat, ushort schemaVersion)
    {
        sendMessageToClient(slot, targetPluginId, messageType, payload, payloadFormat, schemaVersion);
    }

    public void BroadcastMessageToClients(string targetPluginId, string messageType, string payload, PluginMessagePayloadFormat payloadFormat, ushort schemaVersion)
    {
        broadcastMessageToClients(targetPluginId, messageType, payload, payloadFormat, schemaVersion);
    }

    public bool SetPlayerReplicatedStateInt(byte slot, string stateKey, int value)
    {
        return setPlayerReplicatedStateInt(PluginId, slot, stateKey, value);
    }

    public bool SetPlayerReplicatedStateFloat(byte slot, string stateKey, float value)
    {
        return setPlayerReplicatedStateFloat(PluginId, slot, stateKey, value);
    }

    public bool SetPlayerReplicatedStateBool(byte slot, string stateKey, bool value)
    {
        return setPlayerReplicatedStateBool(PluginId, slot, stateKey, value);
    }

    public bool ClearPlayerReplicatedState(byte slot, string stateKey)
    {
        return clearPlayerReplicatedState(PluginId, slot, stateKey);
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
