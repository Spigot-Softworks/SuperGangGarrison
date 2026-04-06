namespace OpenGarrison.Client.Plugins;

public readonly record struct ClientPluginMessageEnvelope(
    string SourcePluginId,
    string TargetPluginId,
    string MessageType,
    string Payload);

public interface IOpenGarrisonClientPluginMessageHooks
{
    void OnServerPluginMessage(ClientPluginMessageEnvelope e);
}
