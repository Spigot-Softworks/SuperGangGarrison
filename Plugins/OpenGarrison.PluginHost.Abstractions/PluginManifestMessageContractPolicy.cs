namespace OpenGarrison.PluginHost;

public static class OpenGarrisonPluginManifestMessageContractPolicy
{
    public const string DirectionClientToServer = "ClientToServer";
    public const string DirectionServerToClient = "ServerToClient";
    public const string DirectionBoth = "Both";

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
