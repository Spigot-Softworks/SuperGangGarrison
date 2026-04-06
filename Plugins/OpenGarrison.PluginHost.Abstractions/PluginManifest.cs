namespace OpenGarrison.PluginHost;

public sealed record OpenGarrisonPluginManifestCompatibility(
    // Lua plugins should treat this as the required OpenGarrisonPluginHostApi.ApiVersion /
    // OpenGarrisonPluginRuntimeSurface.ApiVersion contract rather than a loose compatibility hint.
    string HostApiVersion,
    string? MinimumGameVersion = null,
    string? MaximumGameVersion = null);

public sealed record OpenGarrisonPluginManifest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Version { get; init; } = "1.0.0";

    public OpenGarrisonPluginType Type { get; init; }

    public OpenGarrisonPluginRuntimeKind Runtime { get; init; } = OpenGarrisonPluginRuntimeKind.Clr;

    public string EntryPoint { get; init; } = string.Empty;

    public string? EntryClass { get; init; }

    public string? Description { get; init; }

    public OpenGarrisonPluginManifestCompatibility Compatibility { get; init; } = new("1.0");

    public IReadOnlyList<string> AssetDirectories { get; init; } = Array.Empty<string>();

    public string? ConfigSchemaPath { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public static OpenGarrisonPluginManifest CreateClr(
        string id,
        string displayName,
        Version version,
        OpenGarrisonPluginType type,
        string entryPoint,
        string? entryClass)
    {
        return new OpenGarrisonPluginManifest
        {
            Id = id,
            DisplayName = displayName,
            Version = version.ToString(),
            Type = type,
            Runtime = OpenGarrisonPluginRuntimeKind.Clr,
            EntryPoint = entryPoint,
            EntryClass = entryClass,
            Compatibility = new OpenGarrisonPluginManifestCompatibility("1.0"),
        };
    }
}
