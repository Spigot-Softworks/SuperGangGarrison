using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OpenGarrison.Core;

public static class CustomMapPackageImporter
{
    public sealed record PackageContentFile(string RelativePath, string FullPath);

    public static CustomMapPngImporter.Result? Import(string manifestPath)
    {
        if (!TryCreateDocument(manifestPath, out var document, out _))
        {
            return null;
        }

        var levelData = CustomMapPngExporter.BuildLevelData(document);
        return CustomMapPngImporter.ImportLevelData(
            document.Name,
            document.BackgroundImagePath,
            levelData,
            document.Resources);
    }

    public static CustomMapBuilderDocument? ImportDocument(string manifestPath)
    {
        return TryCreateDocument(manifestPath, out var document, out _)
            ? document
            : null;
    }

    public static bool TryReadManifest(string manifestPath, out CustomMapPackageManifest manifest, out string error)
    {
        manifest = new CustomMapPackageManifest();
        error = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !PackageFileExists(manifestPath))
            {
                error = "Package manifest was not found.";
                return false;
            }

            if (!Path.GetExtension(manifestPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                error = "Package manifest must be a JSON file.";
                return false;
            }

            if (!TryReadPackageText(manifestPath, out var manifestJson))
            {
                error = "Package manifest could not be read.";
                return false;
            }

            manifest = JsonSerializer.Deserialize<CustomMapPackageManifest>(
                manifestJson,
                CustomMapPackageManifest.JsonOptions) ?? new CustomMapPackageManifest();
            if (manifest.FormatVersion != 1)
            {
                error = $"Unsupported custom map package format version {manifest.FormatVersion}.";
                return false;
            }

            var fileName = Path.GetFileNameWithoutExtension(manifestPath);
            if (!string.IsNullOrWhiteSpace(manifest.Name)
                && !manifest.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            {
                error = "Package manifest name must match its file name.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = $"Package manifest could not be parsed: {ex.Message}";
            return false;
        }
        catch (IOException ex)
        {
            error = $"Package manifest could not be read: {ex.Message}";
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = $"Package manifest could not be read: {ex.Message}";
            return false;
        }
    }

    public static bool TryFindManifestInDirectory(string packageDirectory, out string manifestPath)
    {
        manifestPath = string.Empty;
        if (string.IsNullOrWhiteSpace(packageDirectory) || !Directory.Exists(packageDirectory))
        {
            return false;
        }

        var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(packageDirectory)));
        if (!string.IsNullOrWhiteSpace(folderName))
        {
            var preferredManifestPath = Path.Combine(packageDirectory, $"{folderName}.json");
            if (File.Exists(preferredManifestPath)
                && TryReadManifest(preferredManifestPath, out _, out _))
            {
                manifestPath = preferredManifestPath;
                return true;
            }
        }

        foreach (var candidatePath in Directory
            .EnumerateFiles(packageDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (!TryReadManifest(candidatePath, out _, out _))
            {
                continue;
            }

            manifestPath = candidatePath;
            return true;
        }

        return false;
    }

    public static IReadOnlyList<PackageContentFile> GetPackageContentFiles(string manifestPath)
    {
        if (!TryReadManifest(manifestPath, out var manifest, out _))
        {
            return Array.Empty<PackageContentFile>();
        }

        var packageDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? string.Empty;
        var files = new Dictionary<string, PackageContentFile>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetFileName(manifestPath)] = new(Path.GetFileName(manifestPath), Path.GetFullPath(manifestPath)),
        };

        foreach (var reference in manifest.EnumerateImageReferences())
        {
            if (!TryResolvePackageImagePath(packageDirectory, reference, requireFileExists: true, out var fullPath, out var normalizedRelativePath))
            {
                return Array.Empty<PackageContentFile>();
            }

            files[normalizedRelativePath] = new PackageContentFile(normalizedRelativePath, fullPath);
        }

        return files.Values
            .OrderBy(static file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> GetReferencedImagePaths(CustomMapPackageManifest manifest)
    {
        return manifest.EnumerateImageReferences()
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool TryResolvePackageImagePath(
        string packageDirectory,
        string relativePath,
        bool requireFileExists,
        out string fullPath,
        out string normalizedRelativePath)
    {
        fullPath = string.Empty;
        normalizedRelativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(packageDirectory) || string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var trimmedPath = relativePath.Trim();
        if (Path.IsPathRooted(trimmedPath)
            || trimmedPath.Contains(':', StringComparison.Ordinal)
            || !Path.GetExtension(trimmedPath).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = trimmedPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\'],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0
            || segments.Any(static segment => segment.Equals("..", StringComparison.Ordinal)
                || segment.Equals(".", StringComparison.Ordinal)))
        {
            return false;
        }

        var packageRoot = Path.GetFullPath(packageDirectory);
        var candidatePath = Path.GetFullPath(Path.Combine(new[] { packageRoot }.Concat(segments).ToArray()));
        var rootWithSeparator = Path.TrimEndingDirectorySeparator(packageRoot) + Path.DirectorySeparatorChar;
        if (!candidatePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (requireFileExists && !PackageFileExists(candidatePath))
        {
            return false;
        }

        normalizedRelativePath = string.Join('/', segments);
        fullPath = candidatePath;
        return true;
    }

    private static bool TryCreateDocument(string manifestPath, out CustomMapBuilderDocument document, out string error)
    {
        document = CustomMapBuilderDocument.CreateEmpty();
        if (!TryReadManifest(manifestPath, out var manifest, out error))
        {
            return false;
        }

        var packageDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? string.Empty;
        if (!TryResolvePackageImagePath(packageDirectory, manifest.BackgroundImage, requireFileExists: true, out var backgroundPath, out _))
        {
            error = "Package backgroundImage must be an existing relative PNG path inside the package folder.";
            return false;
        }

        if (!TryResolvePackageImagePath(packageDirectory, manifest.WalkmaskImage, requireFileExists: true, out var walkmaskPath, out _))
        {
            error = "Package walkmaskImage must be an existing relative PNG path inside the package folder.";
            return false;
        }

        var metadata = new Dictionary<string, string>(manifest.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "meta",
            ["scale"] = NormalizeScale(manifest.Scale).ToString(CultureInfo.InvariantCulture),
            ["walkmaskScale"] = NormalizeScale(manifest.Scale).ToString(CultureInfo.InvariantCulture),
            ["visualScale"] = NormalizeScale(manifest.VisualScale > 0f ? manifest.VisualScale : manifest.Scale).ToString(CultureInfo.InvariantCulture),
        };
        metadata.TryAdd("background", CustomMapBuilderDocument.DefaultBackgroundColor);
        metadata.TryAdd("void", CustomMapBuilderDocument.DefaultVoidColor);

        var resources = new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifestResource in manifest.Resources ?? [])
        {
            if (string.IsNullOrWhiteSpace(manifestResource.Name)
                || !TryResolvePackageImagePath(packageDirectory, manifestResource.Path, requireFileExists: true, out var resourcePath, out _))
            {
                error = "Package resources must have a name and an existing relative PNG path inside the package folder.";
                return false;
            }

            var kind = ParseResourceKind(manifestResource.Kind);
            if (!TryCreateResource(manifestResource.Name.Trim(), resourcePath, kind, out var resource))
            {
                error = $"Package resource \"{manifestResource.Name}\" must reference a PNG image.";
                return false;
            }

            resources[resource.Name] = resource;
        }

        var entities = new List<CustomMapBuilderEntity>();
        foreach (var manifestEntity in manifest.Entities ?? [])
        {
            if (string.IsNullOrWhiteSpace(manifestEntity.Type))
            {
                continue;
            }

            var properties = new Dictionary<string, string>(
                manifestEntity.Properties ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);
            CopyExtensionScalarProperties(manifestEntity.ExtensionData, properties);
            entities.Add(CustomMapBuilderEntity.Create(
                manifestEntity.Type,
                manifestEntity.X,
                manifestEntity.Y,
                properties,
                manifestEntity.XScale,
                manifestEntity.YScale).NormalizeForEditing());
        }

        var manifestLayers = new List<CustomMapBuilderParallaxLayer>();
        foreach (var layer in manifest.ParallaxLayers ?? [])
        {
            manifestLayers.Add(new CustomMapBuilderParallaxLayer(
                layer.Index,
                layer.ResourceName,
                layer.XFactor,
                layer.YFactor).NormalizeForEditing());
        }

        var layers = CustomMapBuilderParallaxLayers.Merge(manifestLayers, metadata, resources);

        var mapName = string.IsNullOrWhiteSpace(manifest.Name)
            ? Path.GetFileNameWithoutExtension(manifestPath)
            : manifest.Name.Trim();
        document = new CustomMapBuilderDocument(
            Name: mapName,
            BackgroundImagePath: backgroundPath,
            WalkmaskImagePath: walkmaskPath,
            Scale: NormalizeScale(manifest.Scale),
            VisualScale: NormalizeScale(manifest.VisualScale > 0f ? manifest.VisualScale : manifest.Scale),
            Metadata: new ReadOnlyDictionary<string, string>(metadata),
            Entities: entities,
            Resources: new ReadOnlyDictionary<string, CustomMapBuilderResource>(resources),
            ParallaxLayers: layers).NormalizeForEditing();
        return true;
    }

    private static void CopyExtensionScalarProperties(
        IReadOnlyDictionary<string, JsonElement>? extensionData,
        Dictionary<string, string> properties)
    {
        if (extensionData is null)
        {
            return;
        }

        foreach (var pair in extensionData)
        {
            if (pair.Key.Equals("type", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("x", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("y", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("xScale", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("yScale", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("xscale", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("yscale", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = pair.Value.ValueKind switch
            {
                JsonValueKind.String => pair.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => pair.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => string.Empty,
            };
            if (value.Length > 0)
            {
                properties[pair.Key] = value;
            }
        }
    }

    private static CustomMapBuilderResourceKind ParseResourceKind(string? kind)
    {
        return kind?.Trim().ToLowerInvariant() switch
        {
            "parallaxlayer" or "parallax_layer" or "parallax" => CustomMapBuilderResourceKind.ParallaxLayer,
            "foreground" or "fg" => CustomMapBuilderResourceKind.Foreground,
            "entitysprite" or "entity_sprite" => CustomMapBuilderResourceKind.EntitySprite,
            "customsprite" or "custom_sprite" => CustomMapBuilderResourceKind.CustomSprite,
            _ => CustomMapBuilderResourceKind.GenericImage,
        };
    }

    private static bool TryCreateResource(
        string name,
        string path,
        CustomMapBuilderResourceKind kind,
        out CustomMapBuilderResource resource)
    {
        resource = default;
        if (!TryReadPackageBytes(path, out var bytes)
            || !CustomMapBuilderResourceCodec.IsSupportedImage(bytes))
        {
            return false;
        }

        resource = new CustomMapBuilderResource(name, path, kind, bytes).NormalizeForEditing();
        return true;
    }

    private static bool PackageFileExists(string path)
    {
        return File.Exists(path)
            || BrowserContentCatalog.TryGetBinaryForPath(path, out _);
    }

    private static bool TryReadPackageText(string path, out string text)
    {
        text = string.Empty;
        if (File.Exists(path))
        {
            text = File.ReadAllText(path);
            return true;
        }

        if (!BrowserContentCatalog.TryGetBinaryForPath(path, out var bytes) || bytes.Length == 0)
        {
            return false;
        }

        text = Encoding.UTF8.GetString(bytes);
        return true;
    }

    private static bool TryReadPackageBytes(string path, out byte[] bytes)
    {
        if (File.Exists(path))
        {
            bytes = File.ReadAllBytes(path);
            return true;
        }

        return BrowserContentCatalog.TryGetBinaryForPath(path, out bytes);
    }

    private static float NormalizeScale(float scale)
    {
        return float.IsFinite(scale) && scale > 0f
            ? scale
            : CustomMapBuilderDocument.DefaultScale;
    }
}
