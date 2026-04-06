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

        if (string.IsNullOrWhiteSpace(manifest.Compatibility.HostApiVersion))
        {
            error = "manifest compatibility.hostApiVersion is required.";
            return false;
        }

        return true;
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

        entryPointPath = Path.GetFullPath(Path.Combine(pluginDirectory, manifest.EntryPoint));
        return true;
    }

    public static string Serialize(OpenGarrisonPluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, JsonOptions);
    }
}
