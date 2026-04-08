namespace OpenGarrison.Protocol;

public readonly record struct PluginMessageCompatibilityHeader(
    string SourcePluginId,
    string TargetPluginId,
    string MessageType,
    PluginMessagePayloadFormat PayloadFormat,
    ushort SchemaVersion)
{
    public const ushort CurrentHeaderVersion = 1;

    public ushort HeaderVersion { get; } = CurrentHeaderVersion;
}

public readonly record struct PluginMessageCompatibilityContract(
    string TargetPluginId,
    string MessageType,
    PluginMessagePayloadFormat PayloadFormat,
    ushort MinimumSchemaVersion = 1,
    ushort MaximumSchemaVersion = ushort.MaxValue);
