using System.Globalization;
using System.Linq;

namespace OpenGarrison.Core;

public static class CustomMapBuilderParallaxLayers
{
    public static List<CustomMapBuilderParallaxLayer> DecodeFromMetadata(
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyDictionary<string, CustomMapBuilderResource> resources)
    {
        var layers = new List<CustomMapBuilderParallaxLayer>();
        for (var index = CustomMapBuilderParallaxLayer.MinIndex; index <= CustomMapBuilderParallaxLayer.MaxIndex; index += 1)
        {
            var layerKey = $"bg_layer{index}";
            var resourceName = metadata.TryGetValue(layerKey, out var resource) ? resource : string.Empty;
            if (resources.ContainsKey(layerKey))
            {
                resourceName = layerKey;
            }

            var hasResource = !string.IsNullOrWhiteSpace(resourceName);
            var hasXFactor = TryParseFloat(metadata, $"layer{index}xfactor", out var parsedXFactor);
            var hasYFactor = TryParseFloat(metadata, $"layer{index}yfactor", out var parsedYFactor);
            var defaultFactor = hasResource ? ResolveLegacyDefaultFactor(index) : 1f;
            var xFactor = hasXFactor ? parsedXFactor : defaultFactor;
            var yFactor = hasYFactor ? parsedYFactor : defaultFactor;
            if (hasResource || hasXFactor || hasYFactor)
            {
                layers.Add(new CustomMapBuilderParallaxLayer(
                    index,
                    ResolveResourceName(resourceName, index, metadata, resources),
                    xFactor,
                    yFactor).NormalizeForEditing());
            }
        }

        return layers;
    }

    public static IReadOnlyList<CustomMapBuilderParallaxLayer> Merge(
        IReadOnlyList<CustomMapBuilderParallaxLayer> manifestLayers,
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyDictionary<string, CustomMapBuilderResource> resources)
    {
        var byIndex = new Dictionary<int, CustomMapBuilderParallaxLayer>();
        foreach (var layer in manifestLayers)
        {
            byIndex[layer.Index] = layer with
            {
                ResourceName = ResolveResourceName(layer.ResourceName, layer.Index, metadata, resources),
            };
        }

        foreach (var metadataLayer in DecodeFromMetadata(metadata, resources))
        {
            if (!byIndex.TryGetValue(metadataLayer.Index, out var existing)
                || string.IsNullOrWhiteSpace(existing.ResourceName))
            {
                byIndex[metadataLayer.Index] = metadataLayer;
                continue;
            }

            byIndex[metadataLayer.Index] = existing with
            {
                ResourceName = ResolveResourceName(existing.ResourceName, existing.Index, metadata, resources),
                XFactor = existing.XFactor,
                YFactor = existing.YFactor,
            };
        }

        return byIndex.Values
            .OrderBy(static layer => layer.Index)
            .Select(static layer => layer.NormalizeForEditing())
            .ToArray();
    }

    public static string ResolveResourceName(
        string resourceName,
        int layerIndex,
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyDictionary<string, CustomMapBuilderResource> resources)
    {
        var trimmed = resourceName.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed) && resources.ContainsKey(trimmed))
        {
            return trimmed;
        }

        var layerKey = $"bg_layer{layerIndex}";
        if (resources.ContainsKey(layerKey))
        {
            return layerKey;
        }

        if (metadata.TryGetValue(layerKey, out var metadataValue))
        {
            var metadataTrimmed = metadataValue.Trim();
            if (resources.ContainsKey(metadataTrimmed))
            {
                return metadataTrimmed;
            }

            if (!string.IsNullOrWhiteSpace(metadataTrimmed))
            {
                trimmed = metadataTrimmed;
            }
        }

        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            foreach (var resource in resources.Values)
            {
                if (resource.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return resource.Name;
                }

                if (!string.IsNullOrWhiteSpace(resource.SourcePath)
                    && Path.GetFileName(resource.SourcePath).Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return resource.Name;
                }

                if (!string.IsNullOrWhiteSpace(resource.SourcePath)
                    && Path.GetFileNameWithoutExtension(resource.SourcePath).Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return resource.Name;
                }
            }
        }

        return trimmed;
    }

    private static float ResolveLegacyDefaultFactor(int layerIndex)
    {
        return Math.Max(1f, 10f - layerIndex);
    }

    private static bool TryParseFloat(IReadOnlyDictionary<string, string> metadata, string key, out float value)
    {
        value = 0f;
        return metadata.TryGetValue(key, out var text)
            && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
