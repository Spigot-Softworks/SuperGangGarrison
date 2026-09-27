namespace OpenGarrison.PluginHost;

/// <summary>
/// Compatibility requirements declared in a manifest. Lua plugins should treat
/// <paramref name="HostApiVersion"/> as the required
/// <c>OpenGarrisonPluginHostApi.ApiVersion</c> /
/// <c>OpenGarrisonPluginRuntimeSurface.ApiVersion</c> contract rather than a loose
/// compatibility hint.
/// </summary>
/// <param name="HostApiVersion">The required host API version.</param>
/// <param name="MinimumGameVersion">The minimum game version, if any.</param>
/// <param name="MaximumGameVersion">The maximum game version, if any.</param>
public sealed record OpenGarrisonPluginManifestCompatibility(
    string HostApiVersion,
    string? MinimumGameVersion = null,
    string? MaximumGameVersion = null);

/// <summary>
/// A plugin dependency declared in a manifest.
/// </summary>
public sealed record OpenGarrisonPluginManifestDependency
{
    /// <summary>
    /// Gets the dependency plugin id.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the required version, if any.
    /// </summary>
    public string? Version { get; init; }
}

/// <summary>
/// Load order hints declared in a manifest.
/// </summary>
public sealed record OpenGarrisonPluginManifestLoadOrderHints
{
    /// <summary>
    /// Gets the plugin ids that should load before this plugin.
    /// </summary>
    public IReadOnlyList<string> Before { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the plugin ids that should load after this plugin.
    /// </summary>
    public IReadOnlyList<string> After { get; init; } = Array.Empty<string>();
}

/// <summary>
/// A permission declared in a manifest.
/// </summary>
public sealed record OpenGarrisonPluginManifestPermissionDeclaration
{
    /// <summary>
    /// Gets the permission id.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the permission description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the permission is required.
    /// </summary>
    public bool Required { get; init; } = true;
}

/// <summary>
/// A declared cross-plugin message contract.
/// </summary>
public sealed record OpenGarrisonPluginManifestMessageContract
{
    /// <summary>
    /// Gets the target plugin id.
    /// </summary>
    public string TargetPluginId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the message type.
    /// </summary>
    public string MessageType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the payload format.
    /// </summary>
    public string PayloadFormat { get; init; } = "Text";

    /// <summary>
    /// Gets the schema version.
    /// </summary>
    public ushort SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Gets the message direction.
    /// </summary>
    public string Direction { get; init; } = "Both";
}

/// <summary>
/// A gameplay pack bundled with a plugin.
/// </summary>
public sealed record OpenGarrisonPluginManifestGameplayPack
{
    /// <summary>
    /// Gets the path to the pack directory, relative to the plugin directory.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether runtime class binding overrides are allowed for this pack.
    /// </summary>
    public bool AllowRuntimeClassBindingOverride { get; init; }
}

/// <summary>
/// A plugin manifest (plugin.json).
/// </summary>
public sealed record OpenGarrisonPluginManifest
{
    /// <summary>The current manifest schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Gets the schema version.
    /// </summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>
    /// Gets the plugin id.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the version.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Gets the plugin type.
    /// </summary>
    public OpenGarrisonPluginType Type { get; init; }

    /// <summary>
    /// Gets the runtime kind.
    /// </summary>
    public OpenGarrisonPluginRuntimeKind Runtime { get; init; } = OpenGarrisonPluginRuntimeKind.Clr;

    /// <summary>
    /// Gets the entry point.
    /// </summary>
    public string EntryPoint { get; init; } = string.Empty;

    /// <summary>
    /// Gets the entry class, if any.
    /// </summary>
    public string? EntryClass { get; init; }

    /// <summary>
    /// Gets the description, if any.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the compatibility requirements.
    /// </summary>
    public OpenGarrisonPluginManifestCompatibility Compatibility { get; init; } = new("1.0");

    /// <summary>
    /// Gets the asset directories.
    /// </summary>
    public IReadOnlyList<string> AssetDirectories { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the config schema path, if any.
    /// </summary>
    public string? ConfigSchemaPath { get; init; }

    /// <summary>
    /// Gets the required dependencies.
    /// </summary>
    public IReadOnlyList<OpenGarrisonPluginManifestDependency> Dependencies { get; init; } = Array.Empty<OpenGarrisonPluginManifestDependency>();

    /// <summary>
    /// Gets the optional dependencies.
    /// </summary>
    public IReadOnlyList<OpenGarrisonPluginManifestDependency> OptionalDependencies { get; init; } = Array.Empty<OpenGarrisonPluginManifestDependency>();

    /// <summary>
    /// Gets the conflicting plugin ids.
    /// </summary>
    public IReadOnlyList<string> Conflicts { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the load order hints.
    /// </summary>
    public OpenGarrisonPluginManifestLoadOrderHints LoadOrder { get; init; } = new();

    /// <summary>
    /// Gets the declared permissions.
    /// </summary>
    public IReadOnlyList<OpenGarrisonPluginManifestPermissionDeclaration> Permissions { get; init; } = Array.Empty<OpenGarrisonPluginManifestPermissionDeclaration>();

    /// <summary>
    /// Gets the message contracts.
    /// </summary>
    public IReadOnlyList<OpenGarrisonPluginManifestMessageContract> MessageContracts { get; init; } = Array.Empty<OpenGarrisonPluginManifestMessageContract>();

    /// <summary>
    /// Gets the bundled gameplay packs.
    /// </summary>
    public IReadOnlyList<OpenGarrisonPluginManifestGameplayPack> GameplayPacks { get; init; } = Array.Empty<OpenGarrisonPluginManifestGameplayPack>();

    /// <summary>
    /// Gets the tags.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a CLR plugin manifest.
    /// </summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="version">The version.</param>
    /// <param name="type">The plugin type.</param>
    /// <param name="entryPoint">The entry point.</param>
    /// <param name="entryClass">The entry class.</param>
    /// <returns>The new manifest.</returns>
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
