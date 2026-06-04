using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.Core;

public sealed class CustomMapPackageManifest
{
    public int FormatVersion { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public float Scale { get; set; } = CustomMapBuilderDocument.DefaultScale;

    public float VisualScale { get; set; }

    public string BackgroundImage { get; set; } = string.Empty;

    public string WalkmaskImage { get; set; } = string.Empty;

    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<CustomMapPackageEntity> Entities { get; set; } = [];

    public List<CustomMapPackageResource> Resources { get; set; } = [];

    public List<CustomMapPackageParallaxLayer> ParallaxLayers { get; set; } = [];

    internal static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public IEnumerable<string> EnumerateImageReferences()
    {
        if (!string.IsNullOrWhiteSpace(BackgroundImage))
        {
            yield return BackgroundImage;
        }

        if (!string.IsNullOrWhiteSpace(WalkmaskImage))
        {
            yield return WalkmaskImage;
        }

        foreach (var resource in Resources)
        {
            if (!string.IsNullOrWhiteSpace(resource.Path))
            {
                yield return resource.Path;
            }
        }
    }
}

public sealed class CustomMapPackageEntity
{
    public string Type { get; set; } = string.Empty;

    public float X { get; set; }

    public float Y { get; set; }

    public float XScale { get; set; } = 1f;

    public float YScale { get; set; } = 1f;

    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class CustomMapPackageResource
{
    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = "genericImage";

    public string Path { get; set; } = string.Empty;
}

public sealed class CustomMapPackageParallaxLayer
{
    public int Index { get; set; }

    public string ResourceName { get; set; } = string.Empty;

    public float XFactor { get; set; } = 1f;

    public float YFactor { get; set; } = 1f;
}
