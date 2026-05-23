using System.Text.Json;

namespace OpenGarrison.PluginHost;

public static class OpenGarrisonPluginManifestLoader
{
    public const string DefaultManifestFileName = "plugin.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public static bool TryLoadFromDirectory(string pluginDirectory, out OpenGarrisonPluginManifest manifest, out string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);
        return TryLoadFromPath(Path.Combine(pluginDirectory, DefaultManifestFileName), out manifest, out error);
    }

    public static bool TryLoadFromPath(string manifestPath, out OpenGarrisonPluginManifest manifest, out string error)
    {
        manifest = default!;
        error = string.Empty;

        try
        {
            if (!File.Exists(manifestPath))
            {
                error = $"manifest file not found at \"{manifestPath}\".";
                return false;
            }

            var json = File.ReadAllText(manifestPath);
            if (!TryLoadFromJson(json, out manifest, out error))
            {
                return false;
            }

            var pluginDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            return TryValidateFileBackedManifestMetadata(manifest, pluginDirectory, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryLoadFromJson(string json, out OpenGarrisonPluginManifest manifest, out string error)
    {
        manifest = default!;
        error = string.Empty;

        try
        {
            manifest = JsonSerializer.Deserialize<OpenGarrisonPluginManifest>(json, JsonOptions) ?? new OpenGarrisonPluginManifest();
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        if (manifest.SchemaVersion != OpenGarrisonPluginManifest.CurrentSchemaVersion)
        {
            error = $"unsupported manifest schema version {manifest.SchemaVersion}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            error = "manifest id is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.DisplayName))
        {
            error = "manifest displayName is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            error = "manifest version is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryPoint))
        {
            error = "manifest entryPoint is required.";
            return false;
        }

        if (manifest.Compatibility is null || string.IsNullOrWhiteSpace(manifest.Compatibility.HostApiVersion))
        {
            error = "manifest compatibility.hostApiVersion is required.";
            return false;
        }

        if (!TryValidateManifestMetadata(manifest, out error))
        {
            return false;
        }

        return true;
    }

    private static bool TryValidateManifestMetadata(OpenGarrisonPluginManifest manifest, out string error)
    {
        error = string.Empty;

        if (!TryValidateDependencyList(manifest.Dependencies, "dependencies", manifest.Id, out error)
            || !TryValidateDependencyList(manifest.OptionalDependencies, "optionalDependencies", manifest.Id, out error)
            || !TryValidateStringList(manifest.Conflicts, "conflicts", allowSelfReference: false, manifest.Id, out error))
        {
            return false;
        }

        if (manifest.LoadOrder is null)
        {
            error = "manifest loadOrder must not be null.";
            return false;
        }

        if (!TryValidateStringList(manifest.LoadOrder.Before, "loadOrder.before", allowSelfReference: false, manifest.Id, out error)
            || !TryValidateStringList(manifest.LoadOrder.After, "loadOrder.after", allowSelfReference: false, manifest.Id, out error))
        {
            return false;
        }

        if (manifest.Permissions is null)
        {
            error = "manifest permissions must not be null.";
            return false;
        }

        foreach (var permission in manifest.Permissions)
        {
            if (permission is null || string.IsNullOrWhiteSpace(permission.Id))
            {
                error = "manifest permissions entries require an id.";
                return false;
            }
        }

        if (manifest.MessageContracts is null)
        {
            error = "manifest messageContracts must not be null.";
            return false;
        }

        foreach (var contract in manifest.MessageContracts)
        {
            if (contract is null)
            {
                error = "manifest messageContracts entries must not be null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(contract.TargetPluginId))
            {
                error = "manifest messageContracts entries require targetPluginId.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(contract.MessageType))
            {
                error = "manifest messageContracts entries require messageType.";
                return false;
            }

            if (contract.SchemaVersion == 0)
            {
                error = $"manifest messageContracts entry {contract.MessageType} must use schemaVersion greater than zero.";
                return false;
            }

            if (!IsValidPayloadFormat(contract.PayloadFormat))
            {
                error = $"manifest messageContracts entry {contract.MessageType} has unsupported payloadFormat \"{contract.PayloadFormat}\".";
                return false;
            }

            if (!IsValidMessageDirection(contract.Direction))
            {
                error = $"manifest messageContracts entry {contract.MessageType} has unsupported direction \"{contract.Direction}\".";
                return false;
            }
        }

        if (manifest.GameplayPacks is null)
        {
            error = "manifest gameplayPacks must not be null.";
            return false;
        }

        foreach (var gameplayPack in manifest.GameplayPacks)
        {
            if (gameplayPack is null || string.IsNullOrWhiteSpace(gameplayPack.Path))
            {
                error = "manifest gameplayPacks entries require a path.";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateDependencyList(
        IReadOnlyList<OpenGarrisonPluginManifestDependency>? dependencies,
        string fieldName,
        string pluginId,
        out string error)
    {
        error = string.Empty;
        if (dependencies is null)
        {
            error = $"manifest {fieldName} must not be null.";
            return false;
        }

        foreach (var dependency in dependencies)
        {
            if (dependency is null || string.IsNullOrWhiteSpace(dependency.Id))
            {
                error = $"manifest {fieldName} entries require an id.";
                return false;
            }

            if (string.Equals(dependency.Id, pluginId, StringComparison.Ordinal))
            {
                error = $"manifest {fieldName} must not reference the plugin itself.";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateStringList(
        IReadOnlyList<string>? values,
        string fieldName,
        bool allowSelfReference,
        string pluginId,
        out string error)
    {
        error = string.Empty;
        if (values is null)
        {
            error = $"manifest {fieldName} must not be null.";
            return false;
        }

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"manifest {fieldName} entries must not be empty.";
                return false;
            }

            if (!allowSelfReference && string.Equals(value, pluginId, StringComparison.Ordinal))
            {
                error = $"manifest {fieldName} must not reference the plugin itself.";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateFileBackedManifestMetadata(
        OpenGarrisonPluginManifest manifest,
        string? pluginDirectory,
        out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(manifest.ConfigSchemaPath) && manifest.GameplayPacks.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            error = "plugin directory is required to validate manifest file paths.";
            return false;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(manifest.ConfigSchemaPath))
            {
                var schemaPath = OpenGarrisonPluginPathContainment.ResolveContainedPath(
                    pluginDirectory,
                    manifest.ConfigSchemaPath,
                    "manifest configSchemaPath escapes plugin directory.");
                if (!File.Exists(schemaPath))
                {
                    error = $"manifest configSchemaPath was not found at \"{schemaPath}\".";
                    return false;
                }

                using var _ = JsonDocument.Parse(File.ReadAllText(schemaPath));
            }

            foreach (var gameplayPack in manifest.GameplayPacks)
            {
                var packPath = OpenGarrisonPluginPathContainment.ResolveContainedPath(
                    pluginDirectory,
                    gameplayPack.Path,
                    "manifest gameplayPacks path escapes plugin directory.");
                if (!Directory.Exists(packPath))
                {
                    error = $"manifest gameplayPacks path was not found at \"{packPath}\".";
                    return false;
                }

                if (!File.Exists(Path.Combine(packPath, "pack.json")))
                {
                    error = $"manifest gameplayPacks path \"{packPath}\" does not contain pack.json.";
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool IsValidPayloadFormat(string? value)
    {
        return string.Equals(value, "Text", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidMessageDirection(string? value)
    {
        return string.Equals(value, "Both", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "ClientToServer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "ServerToClient", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetManifestPath(string pluginDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);
        return Path.Combine(pluginDirectory, DefaultManifestFileName);
    }

    public static bool TryResolveEntryPointPath(OpenGarrisonPluginManifest manifest, string pluginDirectory, out string entryPointPath, out string error)
    {
        entryPointPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            error = "plugin directory is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryPoint))
        {
            error = "manifest entryPoint is required.";
            return false;
        }

        try
        {
            entryPointPath = OpenGarrisonPluginPathContainment.ResolveContainedPath(
                pluginDirectory,
                manifest.EntryPoint,
                "manifest entryPoint escapes plugin directory.");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryValidateHostApiCompatibility(
        OpenGarrisonPluginManifest manifest,
        OpenGarrisonPluginHostApi hostApi,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(hostApi);

        error = string.Empty;
        if (manifest.Compatibility is null || string.IsNullOrWhiteSpace(manifest.Compatibility.HostApiVersion))
        {
            error = "manifest compatibility.hostApiVersion is required.";
            return false;
        }

        var requiredHostApiVersion = manifest.Compatibility.HostApiVersion;
        if (!string.Equals(requiredHostApiVersion, hostApi.ApiVersion, StringComparison.Ordinal))
        {
            error = $"manifest requires host API version {requiredHostApiVersion}, but host provides {hostApi.ApiVersion}.";
            return false;
        }

        if (manifest.Runtime == OpenGarrisonPluginRuntimeKind.Lua)
        {
            var runtimeSurface = hostApi.RuntimeSurfaces.FirstOrDefault(surface => surface.Runtime == OpenGarrisonPluginRuntimeKind.Lua);
            if (runtimeSurface is null)
            {
                error = "host does not expose a Lua runtime surface.";
                return false;
            }

            if (!string.Equals(requiredHostApiVersion, runtimeSurface.ApiVersion, StringComparison.Ordinal))
            {
                error = $"manifest requires Lua API version {requiredHostApiVersion}, but host provides {runtimeSurface.ApiVersion}.";
                return false;
            }
        }

        return true;
    }

    public static string Serialize(OpenGarrisonPluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, JsonOptions);
    }
}
