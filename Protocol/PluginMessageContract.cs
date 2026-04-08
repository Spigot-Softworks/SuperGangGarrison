using System.Text;

namespace OpenGarrison.Protocol;

public static class PluginMessageContract
{
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
