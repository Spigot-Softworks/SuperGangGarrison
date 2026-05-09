using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace OpenGarrison.Core;

public sealed record CustomMapBuilderDocument(
    string Name,
    string BackgroundImagePath,
    string WalkmaskImagePath,
    float Scale,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<CustomMapBuilderEntity> Entities,
    IReadOnlyDictionary<string, CustomMapBuilderResource> Resources,
    IReadOnlyList<CustomMapBuilderParallaxLayer> ParallaxLayers,
    string EmbeddedWalkmaskSection = "")
{
    public const float DefaultScale = 6f;
    public const string DefaultBackgroundColor = "ffffff";
    public const string DefaultVoidColor = "000000";

    public static CustomMapBuilderDocument CreateEmpty(string name = "untitled") => new(
        NormalizeName(name),
        BackgroundImagePath: string.Empty,
        WalkmaskImagePath: string.Empty,
        Scale: DefaultScale,
        Metadata: CreateDefaultMetadata(DefaultScale),
        Entities: Array.Empty<CustomMapBuilderEntity>(),
        Resources: new ReadOnlyDictionary<string, CustomMapBuilderResource>(
            new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase)),
        ParallaxLayers: Array.Empty<CustomMapBuilderParallaxLayer>());

    public CustomMapBuilderDocument NormalizeForEditing()
    {
        return this with
        {
            Name = NormalizeName(Name),
            Scale = NormalizeScale(Scale),
            Metadata = NormalizeMetadata(Metadata, Scale),
            Entities = Entities
                .Where(static entity => !string.IsNullOrWhiteSpace(entity.Type))
                .Select(static entity => entity.NormalizeForEditing())
                .ToArray(),
            Resources = NormalizeResources(Resources),
            ParallaxLayers = ParallaxLayers
                .Select(static layer => layer.NormalizeForEditing())
                .OrderBy(static layer => layer.Index)
                .ToArray(),
            EmbeddedWalkmaskSection = EmbeddedWalkmaskSection.Trim(),
        };
    }

    public IReadOnlyDictionary<string, string> BuildExportMetadata()
    {
        var normalized = NormalizeForEditing();
        var merged = new Dictionary<string, string>(normalized.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "meta",
            ["background"] = GetMetadataValue(normalized.Metadata, "background", DefaultBackgroundColor),
            ["void"] = GetMetadataValue(normalized.Metadata, "void", DefaultVoidColor),
            ["scale"] = normalized.Scale.ToString(CultureInfo.InvariantCulture),
        };

        foreach (var layer in normalized.ParallaxLayers)
        {
            if (!string.IsNullOrWhiteSpace(layer.ResourceName))
            {
                merged[$"bg_layer{layer.Index}"] = layer.ResourceName;
            }

            merged[$"layer{layer.Index}xfactor"] = layer.XFactor.ToString(CultureInfo.InvariantCulture);
            merged[$"layer{layer.Index}yfactor"] = layer.YFactor.ToString(CultureInfo.InvariantCulture);
        }

        return new ReadOnlyDictionary<string, string>(merged);
    }

    private static ReadOnlyDictionary<string, string> CreateDefaultMetadata(float scale)
    {
        return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "meta",
            ["background"] = DefaultBackgroundColor,
            ["void"] = DefaultVoidColor,
            ["scale"] = NormalizeScale(scale).ToString(CultureInfo.InvariantCulture),
        });
    }

    private static ReadOnlyDictionary<string, string> NormalizeMetadata(
        IReadOnlyDictionary<string, string>? metadata,
        float scale)
    {
        var normalized = metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);

        normalized["type"] = "meta";
        normalized.TryAdd("background", DefaultBackgroundColor);
        normalized.TryAdd("void", DefaultVoidColor);
        normalized["scale"] = NormalizeScale(scale).ToString(CultureInfo.InvariantCulture);
        return new ReadOnlyDictionary<string, string>(normalized);
    }

    private static ReadOnlyDictionary<string, CustomMapBuilderResource> NormalizeResources(
        IReadOnlyDictionary<string, CustomMapBuilderResource>? resources)
    {
        var normalized = new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase);
        if (resources is null)
        {
            return new ReadOnlyDictionary<string, CustomMapBuilderResource>(normalized);
        }

        foreach (var resource in resources.Values)
        {
            var normalizedResource = resource.NormalizeForEditing();
            if (!string.IsNullOrWhiteSpace(normalizedResource.Name))
            {
                normalized[normalizedResource.Name] = normalizedResource;
            }
        }

        return new ReadOnlyDictionary<string, CustomMapBuilderResource>(normalized);
    }

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.Length == 0 ? "untitled" : trimmed;
    }

    private static float NormalizeScale(float scale)
    {
        return float.IsFinite(scale) && scale > 0f ? scale : DefaultScale;
    }

    private static string GetMetadataValue(IReadOnlyDictionary<string, string> metadata, string key, string fallback)
    {
        return metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }
}
