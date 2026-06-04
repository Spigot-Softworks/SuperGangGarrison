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
    float VisualScale,
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
        VisualScale: DefaultScale,
        Metadata: CreateDefaultMetadata(DefaultScale),
        Entities: Array.Empty<CustomMapBuilderEntity>(),
        Resources: new ReadOnlyDictionary<string, CustomMapBuilderResource>(
            new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase)),
        ParallaxLayers: Array.Empty<CustomMapBuilderParallaxLayer>());

    public CustomMapBuilderDocument NormalizeForEditing()
    {
        var walkmaskScale = NormalizeScale(Scale);
        var visualScale = NormalizeScale(VisualScale > 0f ? VisualScale : walkmaskScale);
        return this with
        {
            Name = NormalizeName(Name),
            Scale = walkmaskScale,
            VisualScale = visualScale,
            Metadata = NormalizeMetadata(Metadata, walkmaskScale, visualScale),
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
            ["walkmaskScale"] = normalized.Scale.ToString(CultureInfo.InvariantCulture),
            ["visualScale"] = normalized.VisualScale.ToString(CultureInfo.InvariantCulture),
        };

        foreach (var layer in normalized.ParallaxLayers)
        {
            if (!string.IsNullOrWhiteSpace(layer.ResourceName))
            {
                merged[$"bg_layer{layer.Index}"] = TryEncodeResource(normalized.Resources, layer.ResourceName, out var encodedResource)
                    ? encodedResource
                    : layer.ResourceName;
            }

            merged[$"layer{layer.Index}xfactor"] = layer.XFactor.ToString(CultureInfo.InvariantCulture);
            merged[$"layer{layer.Index}yfactor"] = layer.YFactor.ToString(CultureInfo.InvariantCulture);
        }

        foreach (var resource in normalized.Resources.Values)
        {
            if (!CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out var bytes)
                || !CustomMapBuilderResourceCodec.IsSupportedImage(bytes))
            {
                continue;
            }

            var encodedResource = CustomMapBuilderResourceCodec.EncodeLegacyString(bytes);
            if (resource.Kind == CustomMapBuilderResourceKind.Foreground)
            {
                merged["bg_foreground"] = encodedResource;
            }
            else
            {
                merged[resource.Name] = encodedResource;
            }
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
        float walkmaskScale,
        float visualScale)
    {
        var normalized = metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);

        normalized["type"] = "meta";
        normalized.TryAdd("background", DefaultBackgroundColor);
        normalized.TryAdd("void", DefaultVoidColor);
        normalized["scale"] = NormalizeScale(walkmaskScale).ToString(CultureInfo.InvariantCulture);
        normalized["walkmaskScale"] = NormalizeScale(walkmaskScale).ToString(CultureInfo.InvariantCulture);
        normalized["visualScale"] = NormalizeScale(visualScale).ToString(CultureInfo.InvariantCulture);
        ControlPointMapSettingsMetadata.StripLegacyMetadataKeys(normalized);
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

    public static float ResolveWalkmaskScale(IReadOnlyDictionary<string, string> metadata, float fallback = DefaultScale)
    {
        if (TryReadMetadataScale(metadata, "walkmaskScale", out var walkmaskScale))
        {
            return walkmaskScale;
        }

        return TryReadMetadataScale(metadata, "scale", out var legacyScale)
            ? legacyScale
            : fallback;
    }

    public static float ResolveVisualScale(IReadOnlyDictionary<string, string> metadata, float walkmaskScale)
    {
        return TryReadMetadataScale(metadata, "visualScale", out var visualScale)
            ? visualScale
            : walkmaskScale;
    }

    private static bool TryReadMetadataScale(IReadOnlyDictionary<string, string> metadata, string key, out float scale)
    {
        scale = DefaultScale;
        return metadata.TryGetValue(key, out var scaleText)
            && float.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedScale)
            && parsedScale > 0f
            && float.IsFinite(parsedScale)
            && (scale = parsedScale) > 0f;
    }

    private static string GetMetadataValue(IReadOnlyDictionary<string, string> metadata, string key, string fallback)
    {
        return metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static bool TryEncodeResource(
        IReadOnlyDictionary<string, CustomMapBuilderResource> resources,
        string name,
        out string encodedResource)
    {
        if (resources.TryGetValue(name, out var resource)
            && CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out var bytes)
            && CustomMapBuilderResourceCodec.IsSupportedImage(bytes))
        {
            encodedResource = CustomMapBuilderResourceCodec.EncodeLegacyString(bytes);
            return true;
        }

        encodedResource = string.Empty;
        return false;
    }
}
