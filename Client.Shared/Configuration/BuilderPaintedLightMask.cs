using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.ClientShared;

/// <summary>Retains a light's painted layer separately from its currently active mask.</summary>
public static class BuilderPaintedLightMask
{
    public const string ResourceKey = "paintedMask";
    public const string PixelSizeKey = "paintedMaskPixelSize";
    public const string BrushSizeKey = "paintMaskBrushSize";

    public static void RememberActive(IDictionary<string, string> properties)
    {
        if (properties.TryGetValue(MapLightMetadata.MaskKey, out var name) && !string.IsNullOrWhiteSpace(name)
            && properties.TryGetValue(MapLightMetadata.MaskPixelSizeKey, out var rawSize)
            && float.TryParse(rawSize, NumberStyles.Float, CultureInfo.InvariantCulture, out var size)
            && float.IsFinite(size) && size > 0)
        {
            properties[ResourceKey] = name;
            properties[PixelSizeKey] = rawSize;
        }
    }

    public static (string? Resource, float PixelSize) GetLayer(IReadOnlyDictionary<string, string> properties)
    {
        var active = MapLightMetadata.FromProperties(0, 0, properties);
        if (active.MaskPixelSize > 0 && active.Mask is not null) return (active.Mask, active.MaskPixelSize);
        if (properties.TryGetValue(ResourceKey, out var resource) && !string.IsNullOrWhiteSpace(resource)
            && properties.TryGetValue(PixelSizeKey, out var rawSize)
            && float.TryParse(rawSize, NumberStyles.Float, CultureInfo.InvariantCulture, out var size)
            && float.IsFinite(size) && size > 0)
            return (resource, size);
        return (null, 0);
    }

    public static int GetBrushSize(IReadOnlyDictionary<string, string> properties) =>
        properties.TryGetValue(BrushSizeKey, out var value) && int.TryParse(value, out var size)
            ? Math.Clamp(size, 2, 4) : 2;
}
