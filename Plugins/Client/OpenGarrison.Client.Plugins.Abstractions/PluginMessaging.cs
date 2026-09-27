using System.Text.Json;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// A plugin message received from a server-side plugin.
/// </summary>
/// <param name="SourcePluginId">The sending server plugin id.</param>
/// <param name="TargetPluginId">The receiving client plugin id.</param>
/// <param name="MessageType">The message type.</param>
/// <param name="Payload">The message payload.</param>
/// <param name="PayloadFormat">The payload format (text or JSON).</param>
/// <param name="SchemaVersion">The message schema version.</param>
public readonly record struct ClientPluginMessageEnvelope(
    string SourcePluginId,
    string TargetPluginId,
    string MessageType,
    string Payload,
    PluginMessagePayloadFormat PayloadFormat,
    ushort SchemaVersion)
{
    /// <summary>
    /// Gets the compatibility header describing this envelope for contract validation.
    /// </summary>
    public PluginMessageCompatibilityHeader CompatibilityHeader => PluginMessageContract.CreateCompatibilityHeader(
        SourcePluginId,
        TargetPluginId,
        MessageType,
        PayloadFormat,
        SchemaVersion);
}

/// <summary>
/// Helpers for serializing and deserializing plugin message payloads.
/// </summary>
public static class ClientPluginMessageSerializer
{
    /// <summary>
    /// Serializes a value to a JSON payload string.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">The serializer options, or null for defaults.</param>
    /// <returns>The JSON payload.</returns>
    public static string SerializeJsonPayload<T>(T value, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(value, options);
    }

    /// <summary>
    /// Deserializes a JSON payload string to a value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="payload">The JSON payload.</param>
    /// <param name="options">The serializer options, or null for defaults.</param>
    /// <returns>The deserialized value.</returns>
    public static T? DeserializeJsonPayload<T>(string payload, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(payload, options);
    }

    /// <summary>
    /// Tries to deserialize a JSON payload after validating it against a compatibility contract.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="envelope">The received message envelope.</param>
    /// <param name="contract">The expected compatibility contract.</param>
    /// <param name="value">The deserialized value.</param>
    /// <param name="error">The error message when deserialization fails.</param>
    /// <param name="options">The serializer options, or null for defaults.</param>
    /// <returns>True when the payload is compatible and deserializes successfully.</returns>
    public static bool TryDeserializeCompatibleJsonPayload<T>(
        ClientPluginMessageEnvelope envelope,
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

/// <summary>
/// Hooks a client plugin can implement to receive messages from server-side plugins.
/// </summary>
public interface IOpenGarrisonClientPluginMessageHooks
{
    /// <summary>
    /// Called when a message arrives from a server-side plugin.
    /// </summary>
    /// <param name="e">The message envelope.</param>
    void OnServerPluginMessage(ClientPluginMessageEnvelope e) { }
}
