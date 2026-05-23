namespace OpenGarrison.PluginHost;

public sealed record OpenGarrisonPluginManifestCompatibility(
    // Lua plugins should treat this as the required OpenGarrisonPluginHostApi.ApiVersion /
    // OpenGarrisonPluginRuntimeSurface.ApiVersion contract rather than a loose compatibility hint.
    string HostApiVersion,
    string? MinimumGameVersion = null,
    string? MaximumGameVersion = null);

public sealed record OpenGarrisonPluginManifestDependency
{
    public string Id { get; init; } = string.Empty;

    public string? Version { get; init; }
}

public sealed record OpenGarrisonPluginManifestLoadOrderHints
{
    public IReadOnlyList<string> Before { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> After { get; init; } = Array.Empty<string>();
}

public sealed record OpenGarrisonPluginManifestPermissionDeclaration
{
    public string Id { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool Required { get; init; } = true;
}

public sealed record OpenGarrisonPluginManifestMessageContract
{
    public string TargetPluginId { get; init; } = string.Empty;

    public string MessageType { get; init; } = string.Empty;

    public string PayloadFormat { get; init; } = "Text";

    public ushort SchemaVersion { get; init; } = 1;

    public string Direction { get; init; } = "Both";
}

public sealed record OpenGarrisonPluginManifestGameplayPack
{
    public string Path { get; init; } = string.Empty;

    public bool AllowRuntimeClassBindingOverride { get; init; }
}

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

    public IReadOnlyList<OpenGarrisonPluginManifestDependency> Dependencies { get; init; } = Array.Empty<OpenGarrisonPluginManifestDependency>();

    public IReadOnlyList<OpenGarrisonPluginManifestDependency> OptionalDependencies { get; init; } = Array.Empty<OpenGarrisonPluginManifestDependency>();

    public IReadOnlyList<string> Conflicts { get; init; } = Array.Empty<string>();

    public OpenGarrisonPluginManifestLoadOrderHints LoadOrder { get; init; } = new();

    public IReadOnlyList<OpenGarrisonPluginManifestPermissionDeclaration> Permissions { get; init; } = Array.Empty<OpenGarrisonPluginManifestPermissionDeclaration>();

    public IReadOnlyList<OpenGarrisonPluginManifestMessageContract> MessageContracts { get; init; } = Array.Empty<OpenGarrisonPluginManifestMessageContract>();

    public IReadOnlyList<OpenGarrisonPluginManifestGameplayPack> GameplayPacks { get; init; } = Array.Empty<OpenGarrisonPluginManifestGameplayPack>();

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
