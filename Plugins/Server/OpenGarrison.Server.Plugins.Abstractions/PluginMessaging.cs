using System.Text.Json;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server.Plugins;

public readonly record struct OpenGarrisonServerPluginMessageEnvelope(
    byte SourceSlot,
    string SourcePlayerName,
    string SourcePluginId,
    string TargetPluginId,
    string MessageType,
    string Payload,
    PluginMessagePayloadFormat PayloadFormat,
    ushort SchemaVersion);

public static class ServerPluginMessageSerializer
{
    public static string SerializeJsonPayload<T>(T value, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(value, options);
    }

    public static T? DeserializeJsonPayload<T>(string payload, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(payload, options);
    }
}

public interface IOpenGarrisonServerPluginMessageHooks
{
    void OnClientPluginMessage(OpenGarrisonServerPluginMessageEnvelope e) { }
}
