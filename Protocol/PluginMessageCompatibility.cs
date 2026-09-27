namespace OpenGarrison.Protocol;

/// <summary>
/// The compatibility header of a plugin message.
/// </summary>
/// <param name="SourcePluginId">The source plugin id.</param>
/// <param name="TargetPluginId">The target plugin id.</param>
/// <param name="MessageType">The message type.</param>
/// <param name="PayloadFormat">The payload format.</param>
/// <param name="SchemaVersion">The schema version.</param>
public readonly record struct PluginMessageCompatibilityHeader(
    string SourcePluginId,
    string TargetPluginId,
    string MessageType,
    PluginMessagePayloadFormat PayloadFormat,
    ushort SchemaVersion)
{
    /// <summary>The current header version.</summary>
    public const ushort CurrentHeaderVersion = 1;

    /// <summary>
    /// Gets the header version.
    /// </summary>
    public ushort HeaderVersion { get; } = CurrentHeaderVersion;
}

/// <summary>
/// A compatibility contract a plugin message is validated against.
/// </summary>
/// <param name="TargetPluginId">The target plugin id.</param>
/// <param name="MessageType">The message type.</param>
/// <param name="PayloadFormat">The payload format.</param>
/// <param name="MinimumSchemaVersion">The minimum schema version.</param>
/// <param name="MaximumSchemaVersion">The maximum schema version.</param>
public readonly record struct PluginMessageCompatibilityContract(
    string TargetPluginId,
    string MessageType,
    PluginMessagePayloadFormat PayloadFormat,
    ushort MinimumSchemaVersion = 1,
    ushort MaximumSchemaVersion = ushort.MaxValue);
