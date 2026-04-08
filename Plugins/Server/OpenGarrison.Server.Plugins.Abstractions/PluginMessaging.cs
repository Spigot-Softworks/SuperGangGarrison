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
    ushort SchemaVersion)
{
    public PluginMessageCompatibilityHeader CompatibilityHeader => PluginMessageContract.CreateCompatibilityHeader(
        SourcePluginId,
        TargetPluginId,
        MessageType,
        PayloadFormat,
        SchemaVersion);
}

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

    public static bool TryDeserializeCompatibleJsonPayload<T>(
        OpenGarrisonServerPluginMessageEnvelope envelope,
        PluginMessageCompatibilityContract contract,
        out T? value,
        out string error,
        JsonSerializerOptions? options = null)
    {
        value = default;
        if (!PluginMessageContract.TryValidateAgainstCompatibilityContract(envelope.CompatibilityHeader, contract, out error))
        {
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<T>(envelope.Payload, options);
            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSON payload deserialization failed: {ex.Message}";
            return false;
        }
    }
}

public interface IOpenGarrisonServerPluginMessageHooks
{
    void OnClientPluginMessage(OpenGarrisonServerPluginMessageEnvelope e) { }
}
