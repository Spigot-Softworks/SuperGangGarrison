using System.Text;

namespace OpenGarrison.Protocol;

/// <summary>
/// Creates, normalizes, and validates plugin message compatibility headers and contracts.
/// </summary>
public static class PluginMessageContract
{
    /// <summary>
    /// Creates a compatibility header for a plugin message.
    /// </summary>
    /// <param name="sourcePluginId">The source plugin id.</param>
    /// <param name="targetPluginId">The target plugin id.</param>
    /// <param name="messageType">The message type.</param>
    /// <param name="payloadFormat">The payload format.</param>
    /// <param name="schemaVersion">The schema version.</param>
    /// <returns>The new compatibility header.</returns>
    public static PluginMessageCompatibilityHeader CreateCompatibilityHeader(
        string sourcePluginId,
        string targetPluginId,
        string messageType,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion)
    {
        return new PluginMessageCompatibilityHeader(
            sourcePluginId,
            targetPluginId,
            messageType,
            payloadFormat,
            schemaVersion);
    }

    /// <summary>
    /// Normalizes an outgoing plugin message, trimming ids and enforcing protocol limits.
    /// </summary>
    /// <param name="targetPluginId">The target plugin id.</param>
    /// <param name="messageType">The message type.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="payloadFormat">The payload format.</param>
    /// <param name="schemaVersion">The schema version.</param>
    /// <param name="normalizedTargetPluginId">When this method returns true, the normalized target plugin id.</param>
    /// <param name="normalizedMessageType">When this method returns true, the normalized message type.</param>
    /// <param name="normalizedPayload">When this method returns true, the normalized payload.</param>
    /// <param name="error">When this method returns false, a description of the failure.</param>
    /// <returns>True when the message was normalized; otherwise false.</returns>
    public static bool TryNormalizeOutgoing(
        string? targetPluginId,
        string? messageType,
        string? payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion,
        out string normalizedTargetPluginId,
        out string normalizedMessageType,
        out string normalizedPayload,
        out string error)
    {
        return TryNormalizeCore(
            sourcePluginId: null,
            targetPluginId,
            messageType,
            payload,
            payloadFormat,
            schemaVersion,
            requireSourcePluginId: false,
            out _,
            out normalizedTargetPluginId,
            out normalizedMessageType,
            out normalizedPayload,
            out error);
    }

    /// <summary>
    /// Normalizes an incoming plugin message, trimming ids and enforcing protocol limits.
    /// </summary>
    /// <param name="sourcePluginId">The source plugin id.</param>
    /// <param name="targetPluginId">The target plugin id.</param>
    /// <param name="messageType">The message type.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="payloadFormat">The payload format.</param>
    /// <param name="schemaVersion">The schema version.</param>
    /// <param name="normalizedSourcePluginId">When this method returns true, the normalized source plugin id.</param>
    /// <param name="normalizedTargetPluginId">When this method returns true, the normalized target plugin id.</param>
    /// <param name="normalizedMessageType">When this method returns true, the normalized message type.</param>
    /// <param name="normalizedPayload">When this method returns true, the normalized payload.</param>
    /// <param name="error">When this method returns false, a description of the failure.</param>
    /// <returns>True when the message was normalized; otherwise false.</returns>
    public static bool TryNormalizeIncoming(
        string? sourcePluginId,
        string? targetPluginId,
        string? messageType,
        string? payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion,
        out string normalizedSourcePluginId,
        out string normalizedTargetPluginId,
        out string normalizedMessageType,
        out string normalizedPayload,
        out string error)
    {
        return TryNormalizeCore(
            sourcePluginId,
            targetPluginId,
            messageType,
            payload,
            payloadFormat,
            schemaVersion,
            requireSourcePluginId: true,
            out normalizedSourcePluginId,
            out normalizedTargetPluginId,
            out normalizedMessageType,
            out normalizedPayload,
            out error);
    }

    /// <summary>
    /// Validates a compatibility header against a compatibility contract.
    /// </summary>
    /// <param name="header">The compatibility header.</param>
    /// <param name="contract">The compatibility contract.</param>
    /// <param name="error">When this method returns false, a description of the mismatch.</param>
    /// <returns>True when the header satisfies the contract; otherwise false.</returns>
    public static bool TryValidateAgainstCompatibilityContract(
        PluginMessageCompatibilityHeader header,
        PluginMessageCompatibilityContract contract,
        out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(contract.TargetPluginId))
        {
            error = "Compatibility contract target plugin id must be non-empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(contract.MessageType))
        {
            error = "Compatibility contract message type must be non-empty.";
            return false;
        }

        if (contract.MinimumSchemaVersion == 0 || contract.MaximumSchemaVersion == 0)
        {
            error = "Compatibility contract schema versions must be greater than zero.";
            return false;
        }

        if (contract.MinimumSchemaVersion > contract.MaximumSchemaVersion)
        {
            error = "Compatibility contract minimum schema version cannot exceed the maximum schema version.";
            return false;
        }

        if (!string.Equals(header.TargetPluginId, contract.TargetPluginId, StringComparison.Ordinal))
        {
            error = $"Compatibility header target plugin id \"{header.TargetPluginId}\" did not match expected target \"{contract.TargetPluginId}\".";
            return false;
        }

        if (!string.Equals(header.MessageType, contract.MessageType, StringComparison.Ordinal))
        {
            error = $"Compatibility header message type \"{header.MessageType}\" did not match expected message type \"{contract.MessageType}\".";
            return false;
        }

        if (header.PayloadFormat != contract.PayloadFormat)
        {
            error = $"Compatibility header payload format {header.PayloadFormat} did not match expected format {contract.PayloadFormat}.";
            return false;
        }

        if (header.SchemaVersion < contract.MinimumSchemaVersion || header.SchemaVersion > contract.MaximumSchemaVersion)
        {
            error = $"Compatibility header schema version {header.SchemaVersion} is outside the supported range {contract.MinimumSchemaVersion}-{contract.MaximumSchemaVersion}.";
            return false;
        }

        return true;
    }

    private static bool TryNormalizeCore(
        string? sourcePluginId,
        string? targetPluginId,
        string? messageType,
        string? payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion,
        bool requireSourcePluginId,
        out string normalizedSourcePluginId,
        out string normalizedTargetPluginId,
        out string normalizedMessageType,
        out string normalizedPayload,
        out string error)
    {
        normalizedSourcePluginId = sourcePluginId?.Trim() ?? string.Empty;
        normalizedTargetPluginId = targetPluginId?.Trim() ?? string.Empty;
        normalizedMessageType = messageType?.Trim() ?? string.Empty;
        normalizedPayload = payload ?? string.Empty;
        error = string.Empty;

        if (requireSourcePluginId && normalizedSourcePluginId.Length == 0)
        {
            error = "Source plugin id must be non-empty.";
            return false;
        }

        if (normalizedTargetPluginId.Length == 0 || normalizedMessageType.Length == 0)
        {
            error = "Target plugin id and message type must be non-empty.";
            return false;
        }

        if (!Enum.IsDefined(payloadFormat))
        {
            error = "Payload format must be a defined protocol value.";
            return false;
        }

        if (schemaVersion == 0)
        {
            error = "Schema version must be greater than zero.";
            return false;
        }

        if (requireSourcePluginId && !IsWithinUtf8ByteLimit(normalizedSourcePluginId, ProtocolCodec.MaxPluginIdBytes))
        {
            error = $"Source plugin id exceeds protocol byte limit of {ProtocolCodec.MaxPluginIdBytes} bytes.";
            return false;
        }

        if (!IsWithinUtf8ByteLimit(normalizedTargetPluginId, ProtocolCodec.MaxPluginIdBytes))
        {
            error = $"Target plugin id exceeds protocol byte limit of {ProtocolCodec.MaxPluginIdBytes} bytes.";
            return false;
        }

        if (!IsWithinUtf8ByteLimit(normalizedMessageType, ProtocolCodec.MaxPluginMessageTypeBytes))
        {
            error = $"Message type exceeds protocol byte limit of {ProtocolCodec.MaxPluginMessageTypeBytes} bytes.";
            return false;
        }

        if (!IsWithinUtf8ByteLimit(normalizedPayload, ProtocolCodec.MaxPluginPayloadBytes))
        {
            error = $"Payload exceeds protocol byte limit of {ProtocolCodec.MaxPluginPayloadBytes} bytes.";
            return false;
        }

        return true;
    }

    private static bool IsWithinUtf8ByteLimit(string value, int maxBytes)
    {
        return Encoding.UTF8.GetByteCount(value) <= maxBytes;
    }
}
