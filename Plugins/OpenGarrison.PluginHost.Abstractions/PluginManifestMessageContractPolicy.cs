namespace OpenGarrison.PluginHost;

/// <summary>
/// Validates outgoing cross-plugin messages against the declared message contracts.
/// </summary>
public static class OpenGarrisonPluginManifestMessageContractPolicy
{
    /// <summary>The client-to-server message direction.</summary>
    public const string DirectionClientToServer = "ClientToServer";

    /// <summary>The server-to-client message direction.</summary>
    public const string DirectionServerToClient = "ServerToClient";

    /// <summary>The both-directions message direction.</summary>
    public const string DirectionBoth = "Both";

    /// <summary>
    /// Checks whether an outgoing message is allowed by the manifest's message contracts.
    /// </summary>
    /// <param name="manifest">The plugin manifest.</param>
    /// <param name="targetPluginId">The target plugin id.</param>
    /// <param name="messageType">The message type.</param>
    /// <param name="payloadFormat">The payload format.</param>
    /// <param name="schemaVersion">The schema version.</param>
    /// <param name="direction">The message direction.</param>
    /// <param name="error">When this method returns false, a description of the mismatch.</param>
    /// <returns>True when a declared contract allows the message; otherwise false.</returns>
    public static bool TryValidateOutgoing(
        OpenGarrisonPluginManifest manifest,
        string targetPluginId,
        string messageType,
        string payloadFormat,
        ushort schemaVersion,
        string direction,
        out string error)
    {
        error = string.Empty;
        if (manifest.MessageContracts.Count == 0)
        {
            return true;
        }

        foreach (var contract in manifest.MessageContracts)
        {
            if (!string.Equals(contract.TargetPluginId, targetPluginId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(contract.MessageType, messageType, StringComparison.Ordinal)
                || !string.Equals(contract.PayloadFormat, payloadFormat, StringComparison.OrdinalIgnoreCase)
                || contract.SchemaVersion != schemaVersion)
            {
                continue;
            }

            if (string.Equals(contract.Direction, DirectionBoth, StringComparison.OrdinalIgnoreCase)
                || string.Equals(contract.Direction, direction, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        error = $"No manifest message contract allows {direction} message \"{messageType}\" to \"{targetPluginId}\" with {payloadFormat} schema {schemaVersion}.";
        return false;
    }
}
