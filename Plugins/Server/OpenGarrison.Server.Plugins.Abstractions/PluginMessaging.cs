namespace OpenGarrison.Server.Plugins;

public readonly record struct OpenGarrisonServerPluginMessageEnvelope(
    byte SourceSlot,
    string SourcePlayerName,
    string SourcePluginId,
    string TargetPluginId,
    string MessageType,
    string Payload);

public interface IOpenGarrisonServerPluginMessageHooks
{
    void OnClientPluginMessage(OpenGarrisonServerPluginMessageEnvelope e);
}
