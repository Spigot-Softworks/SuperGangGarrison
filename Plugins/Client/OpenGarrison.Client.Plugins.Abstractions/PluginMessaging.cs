using System.Text.Json;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client.Plugins;

public readonly record struct ClientPluginMessageEnvelope(
    string SourcePluginId,
    string TargetPluginId,
    string MessageType,
    string Payload,
    PluginMessagePayloadFormat PayloadFormat,
    ushort SchemaVersion);

public static class ClientPluginMessageSerializer
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

public interface IOpenGarrisonClientPluginMessageHooks
{
    void OnServerPluginMessage(ClientPluginMessageEnvelope e) { }
}
