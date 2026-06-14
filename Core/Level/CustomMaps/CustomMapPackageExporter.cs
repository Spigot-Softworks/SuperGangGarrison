using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenGarrison.Core;

public static class CustomMapPackageExporter
{
    public static void Export(CustomMapBuilderDocument document, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var normalized = document.NormalizeForEditing();
        if (string.IsNullOrWhiteSpace(normalized.BackgroundImagePath))
        {
            throw new InvalidOperationException("A background PNG is required before exporting a custom map package.");
        }

        if (string.IsNullOrWhiteSpace(normalized.WalkmaskImagePath)
            && string.IsNullOrWhiteSpace(normalized.EmbeddedWalkmaskSection))
        {
            throw new InvalidOperationException("A walkmask image or embedded walkmask section is required before exporting a custom map package.");
        }

        var manifestPath = ResolveManifestOutputPath(normalized, outputPath);
        var packageDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))
            ?? throw new InvalidOperationException("A package output directory is required.");
        Directory.CreateDirectory(packageDirectory);

        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var backgroundFileName = ReserveFileName(reservedNames, "background.png");
        var backgroundPath = Path.Combine(packageDirectory, backgroundFileName);
        CopyPng(normalized.BackgroundImagePath, backgroundPath, "background");

        var walkmaskFileName = ReserveFileName(reservedNames, "walkmask.png");
        var walkmaskPath = Path.Combine(packageDirectory, walkmaskFileName);
        if (!string.IsNullOrWhiteSpace(normalized.WalkmaskImagePath))
        {
            CopyPng(normalized.WalkmaskImagePath, walkmaskPath, "walkmask");
        }
        else
        {
            WriteWalkmaskPng(normalized.EmbeddedWalkmaskSection, walkmaskPath);
        }

        var resources = new List<CustomMapPackageResource>();
        foreach (var resource in normalized.Resources.Values.OrderBy(static resource => resource.Name, StringComparer.OrdinalIgnoreCase))
        {
            var bytes = CustomMapBuilderResourceCodec.GetResourceBytes(resource);
            if (!IsPng(bytes))
            {
                throw new InvalidOperationException($"Resource \"{resource.Name}\" must be a PNG image for package export.");
            }

            var resourceFileName = ReserveFileName(reservedNames, $"{SanitizeFileName(resource.Name)}.png");
            File.WriteAllBytes(Path.Combine(packageDirectory, resourceFileName), bytes);
            resources.Add(new CustomMapPackageResource
            {
                Name = resource.Name,
                Kind = FormatResourceKind(resource.Kind),
                Path = resourceFileName,
            });
        }

        var manifest = new CustomMapPackageManifest
        {
            FormatVersion = 1,
            Name = Path.GetFileNameWithoutExtension(manifestPath),
            Scale = normalized.Scale,
            VisualScale = normalized.VisualScale,
            BackgroundImage = backgroundFileName,
            WalkmaskImage = walkmaskFileName,
            Metadata = BuildManifestMetadata(normalized),
            Entities = normalized.Entities.Select(ToManifestEntity).ToList(),
            Resources = resources,
            ParallaxLayers = normalized.ParallaxLayers.Select(ToManifestLayer).ToList(),
        };

        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(manifest, CustomMapPackageManifest.JsonOptions));
    }

    public static bool IsPackageOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return false;
        }

        return Directory.Exists(outputPath)
            || Path.GetExtension(outputPath).Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveManifestOutputPath(CustomMapBuilderDocument document, string outputPath)
    {
        var trimmed = outputPath.Trim().Trim('"');

        // A package manifest is always a ".json" file. Anything else - an existing
        // directory, a staging folder whose name contains a dot, a map name with a
        // dot, and so on - is treated as the package directory the manifest lives in.
        if (Path.GetExtension(trimmed).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return Path.Combine(trimmed, $"{document.NormalizeForEditing().Name}.json");
    }

    /// <summary>
    /// Resolves the manifest path for a user-chosen save location, ensuring the package
    /// is written into its own folder named after the map. This matches the runtime
    /// convention of one package directory per map (see
    /// <see cref="CustomMapPackageDirectoryPublisher"/>), so saving never dumps loose
    /// files into a shared maps folder.
    /// </summary>
    public static string ResolvePackageManifestPath(CustomMapBuilderDocument document, string outputPath)
    {
        var trimmed = outputPath.Trim().Trim('"');
        var documentName = document.NormalizeForEditing().Name;

        string parentDirectory;
        string manifestName;
        if (Path.GetExtension(trimmed).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            var manifestFullPath = Path.GetFullPath(trimmed);
            parentDirectory = Path.GetDirectoryName(manifestFullPath) ?? trimmed;
            manifestName = Path.GetFileNameWithoutExtension(manifestFullPath);
        }
        else
        {
            parentDirectory = Path.GetFullPath(trimmed);
            manifestName = documentName;
        }

        if (string.IsNullOrWhiteSpace(manifestName))
        {
            manifestName = documentName;
        }

        // If the chosen folder is already this package's own folder, keep it instead of
        // nesting another level deep (so repeated saves stay in the same directory).
        if (string.Equals(Path.GetFileName(parentDirectory), manifestName, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(parentDirectory, $"{manifestName}.json");
        }

        return Path.Combine(parentDirectory, manifestName, $"{manifestName}.json");
    }

    private static Dictionary<string, string> BuildManifestMetadata(CustomMapBuilderDocument document)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in document.Metadata.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Key.Equals("type", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("scale", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("walkmaskScale", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("visualScale", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("bg_foreground", StringComparison.OrdinalIgnoreCase)
                || pair.Key.StartsWith("bg_layer", StringComparison.OrdinalIgnoreCase)
                || pair.Key.EndsWith("xfactor", StringComparison.OrdinalIgnoreCase)
                || pair.Key.EndsWith("yfactor", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            metadata[pair.Key] = pair.Value;
        }

        metadata.TryAdd("background", CustomMapBuilderDocument.DefaultBackgroundColor);
        metadata.TryAdd("void", CustomMapBuilderDocument.DefaultVoidColor);
        return metadata;
    }

    private static CustomMapPackageEntity ToManifestEntity(CustomMapBuilderEntity entity)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in entity.Properties.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Key.Equals("type", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("x", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("y", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("xscale", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("yscale", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            properties[pair.Key] = pair.Value;
        }

        return new CustomMapPackageEntity
        {
            Type = entity.Type,
            X = entity.X,
            Y = entity.Y,
            XScale = entity.XScale,
            YScale = entity.YScale,
            Properties = properties,
        };
    }

    private static CustomMapPackageParallaxLayer ToManifestLayer(CustomMapBuilderParallaxLayer layer)
    {
        return new CustomMapPackageParallaxLayer
        {
            Index = layer.Index,
            ResourceName = layer.ResourceName,
            XFactor = layer.XFactor,
            YFactor = layer.YFactor,
        };
    }

    private static string ReserveFileName(HashSet<string> reservedNames, string preferredName)
    {
        var sanitized = SanitizeFileName(Path.GetFileNameWithoutExtension(preferredName));
        var extension = Path.GetExtension(preferredName);
        var candidate = $"{sanitized}{extension}";
        var suffix = 2;
        while (!reservedNames.Add(candidate))
        {
            candidate = $"{sanitized}-{suffix.ToString(CultureInfo.InvariantCulture)}{extension}";
            suffix += 1;
        }

        return candidate;
    }

    private static void CopyPng(string sourcePath, string outputPath, string label)
    {
        var bytes = File.ReadAllBytes(sourcePath);
        if (!IsPng(bytes))
        {
            throw new InvalidOperationException($"The package {label} image must be a PNG file.");
        }

        if (PathsEqual(sourcePath, outputPath))
        {
            return;
        }

        using var image = Image.Load<Rgba32>(bytes);
        image.Save(outputPath, new PngEncoder());
    }

    private static void WriteWalkmaskPng(string embeddedWalkmaskSection, string outputPath)
    {
        var lines = embeddedWalkmaskSection
            .Trim()
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3
            || !int.TryParse(lines[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
            || !int.TryParse(lines[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
            || width <= 0
            || height <= 0)
        {
            throw new InvalidOperationException("Embedded walkmask section is invalid.");
        }

        var packed = string.Concat(lines.Skip(2));
        using var image = new Image<Rgba32>(width, height);
        var pixelIndex = 0;
        foreach (var character in packed)
        {
            var value = character - 32;
            for (var bit = 5; bit >= 0 && pixelIndex < width * height; bit -= 1)
            {
                if (((value >> bit) & 1) != 0)
                {
                    var x = pixelIndex % width;
                    var y = pixelIndex / width;
                    image[x, y] = new Rgba32(255, 255, 255, 255);
                }

                pixelIndex += 1;
            }
        }

        using var output = File.Create(outputPath);
        image.Save(output, new PngEncoder());
    }

    private static bool IsPng(byte[] bytes)
    {
        return bytes.Length >= 8
            && bytes[0] == 137
            && bytes[1] == 80
            && bytes[2] == 78
            && bytes[3] == 71
            && bytes[4] == 13
            && bytes[5] == 10
            && bytes[6] == 26
            && bytes[7] == 10;
    }

    private static string FormatResourceKind(CustomMapBuilderResourceKind kind)
    {
        return kind switch
        {
            CustomMapBuilderResourceKind.ParallaxLayer => "parallaxLayer",
            CustomMapBuilderResourceKind.Foreground => "foreground",
            CustomMapBuilderResourceKind.EntitySprite => "entitySprite",
            CustomMapBuilderResourceKind.CustomSprite => "customSprite",
            _ => "genericImage",
        };
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return sanitized.Length == 0 ? "resource" : sanitized;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
