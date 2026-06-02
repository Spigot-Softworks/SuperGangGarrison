namespace OpenGarrison.Core;

public sealed record CustomMapVisualMetadata(
    float ImageScale,
    string? BackgroundColor,
    string? VoidColor,
    IReadOnlyList<CustomMapParallaxLayer> ParallaxLayers,
    CustomMapVisualResource? Foreground,
    IReadOnlyDictionary<string, CustomMapVisualResource> Resources)
{
    public float ForegroundOffsetX { get; init; }

    public float ForegroundOffsetY { get; init; }

    public static CustomMapVisualMetadata Empty { get; } = new(
        ImageScale: 1f,
        BackgroundColor: null,
        VoidColor: null,
        ParallaxLayers: Array.Empty<CustomMapParallaxLayer>(),
        Foreground: null,
        Resources: new Dictionary<string, CustomMapVisualResource>(StringComparer.OrdinalIgnoreCase));
}

public sealed record CustomMapParallaxLayer(
    int Index,
    float XFactor,
    float YFactor,
    CustomMapVisualResource Resource);

public sealed record CustomMapVisualResource(
    string Name,
    byte[] Bytes);
