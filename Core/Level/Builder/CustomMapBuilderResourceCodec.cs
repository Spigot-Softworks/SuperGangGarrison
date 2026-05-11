using System.Collections.ObjectModel;

namespace OpenGarrison.Core;

public static class CustomMapBuilderResourceCodec
{
    public static CustomMapBuilderResource FromFile(
        string name,
        string sourcePath,
        CustomMapBuilderResourceKind kind = CustomMapBuilderResourceKind.GenericImage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var bytes = File.ReadAllBytes(sourcePath);
        if (!IsSupportedImage(bytes))
        {
            throw new InvalidOperationException($"Resource \"{sourcePath}\" is not a PNG or GIF image.");
        }

        return new CustomMapBuilderResource(name, sourcePath, kind, bytes).NormalizeForEditing();
    }

    public static bool TryDecodeLegacyString(
        string name,
        string legacyValue,
        CustomMapBuilderResourceKind kind,
        out CustomMapBuilderResource resource)
    {
        var bytes = DecodeLegacyString(legacyValue);
        if (!IsSupportedImage(bytes))
        {
            resource = default;
            return false;
        }

        resource = new CustomMapBuilderResource(name, string.Empty, kind, bytes).NormalizeForEditing();
        return true;
    }

    public static string EncodeLegacyString(CustomMapBuilderResource resource)
    {
        var bytes = GetResourceBytes(resource);
        return EncodeLegacyString(bytes);
    }

    public static string EncodeLegacyString(byte[] bytes)
    {
        if (!IsSupportedImage(bytes))
        {
            throw new InvalidOperationException("Only PNG and GIF builder resources can be embedded in GG2 metadata.");
        }

        var characters = new char[bytes.Length];
        for (var index = 0; index < bytes.Length; index += 1)
        {
            characters[index] = (char)bytes[index];
        }

        return new string(characters);
    }

    public static byte[] DecodeLegacyString(string legacyValue)
    {
        var bytes = new byte[legacyValue.Length];
        for (var index = 0; index < legacyValue.Length; index += 1)
        {
            bytes[index] = (byte)(legacyValue[index] & 0xff);
        }

        return bytes;
    }

    public static bool TryGetResourceBytes(CustomMapBuilderResource resource, out byte[] bytes)
    {
        try
        {
            bytes = GetResourceBytes(resource);
            return true;
        }
        catch (IOException)
        {
            bytes = [];
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            bytes = [];
            return false;
        }
    }

    public static byte[] GetResourceBytes(CustomMapBuilderResource resource)
    {
        if (resource.EmbeddedBytes is { Length: > 0 })
        {
            return (byte[])resource.EmbeddedBytes.Clone();
        }

        if (!string.IsNullOrWhiteSpace(resource.SourcePath))
        {
            return File.ReadAllBytes(resource.SourcePath);
        }

        return [];
    }

    public static string WriteDecompiledFile(CustomMapBuilderResource resource, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var bytes = GetResourceBytes(resource);
        if (!IsSupportedImage(bytes))
        {
            throw new InvalidOperationException($"Resource \"{resource.Name}\" is not a PNG or GIF image.");
        }

        Directory.CreateDirectory(outputDirectory);
        var extension = IsGif(bytes) ? ".GIF" : ".PNG";
        var outputPath = Path.Combine(outputDirectory, $"{SanitizeFileName(resource.Name)}{extension}");
        File.WriteAllBytes(outputPath, bytes);
        return outputPath;
    }

    public static bool IsSupportedImage(byte[] bytes) => IsPng(bytes) || IsGif(bytes);

    public static IReadOnlyDictionary<string, CustomMapBuilderResource> DecodeResourcesFromMetadata(
        IReadOnlyDictionary<string, string> metadata)
    {
        var resources = new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in metadata)
        {
            var kind = GetMetadataResourceKind(pair.Key);
            if (TryDecodeLegacyString(pair.Key, pair.Value, kind, out var resource))
            {
                resources[resource.Name] = resource;
            }
        }

        return new ReadOnlyDictionary<string, CustomMapBuilderResource>(resources);
    }

    public static CustomMapBuilderResourceKind GetMetadataResourceKind(string metadataKey)
    {
        if (metadataKey.Equals("bg_foreground", StringComparison.OrdinalIgnoreCase))
        {
            return CustomMapBuilderResourceKind.Foreground;
        }

        if (metadataKey.StartsWith("bg_layer", StringComparison.OrdinalIgnoreCase))
        {
            return CustomMapBuilderResourceKind.ParallaxLayer;
        }

        return CustomMapBuilderResourceKind.GenericImage;
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

    private static bool IsGif(byte[] bytes)
    {
        return bytes.Length >= 6
            && bytes[0] == 'G'
            && bytes[1] == 'I'
            && bytes[2] == 'F'
            && bytes[3] == '8'
            && (bytes[4] == '7' || bytes[4] == '9')
            && bytes[5] == 'a';
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return sanitized.Length == 0 ? "resource" : sanitized;
    }
}
