namespace OpenGarrison.Core;

public sealed record CustomMapVisualMetadata(
    float ImageScale,
    string? BackgroundColor,
    string? VoidColor,
    IReadOnlyList<CustomMapParallaxLayer> ParallaxLayers,
    CustomMapVisualResource? Foreground)
{
    public static CustomMapVisualMetadata Empty { get; } = new(
        ImageScale: 1f,
        BackgroundColor: null,
        VoidColor: null,
        ParallaxLayers: Array.Empty<CustomMapParallaxLayer>(),
        Foreground: null);
}

public sealed record CustomMapParallaxLayer(
    int Index,
    float XFactor,
    float YFactor,
    CustomMapVisualResource Resource);

public sealed record CustomMapVisualResource(
    string Name,
    byte[] Bytes);
