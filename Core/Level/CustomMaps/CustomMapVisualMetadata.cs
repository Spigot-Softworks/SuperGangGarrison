namespace OpenGarrison.Core;

public sealed record CustomMapVisualMetadata(
    float ImageScale,
    string? BackgroundColor,
    string? VoidColor,
    IReadOnlyList<CustomMapParallaxLayer> ParallaxLayers,
    CustomMapVisualResource? Foreground,
    IReadOnlyDictionary<string, CustomMapVisualResource> SpriteResources,
    IReadOnlyDictionary<int, ForegroundSpriteHitMask> ForegroundSpriteJungleHitMasks)
{
    public static CustomMapVisualMetadata Empty { get; } = new(
        ImageScale: 1f,
        BackgroundColor: null,
        VoidColor: null,
        ParallaxLayers: Array.Empty<CustomMapParallaxLayer>(),
        Foreground: null,
        SpriteResources: EmptySpriteResources,
        ForegroundSpriteJungleHitMasks: EmptyHitMasks);

    private static readonly IReadOnlyDictionary<string, CustomMapVisualResource> EmptySpriteResources =
        new Dictionary<string, CustomMapVisualResource>(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<int, ForegroundSpriteHitMask> EmptyHitMasks =
        new Dictionary<int, ForegroundSpriteHitMask>();
}

public sealed record CustomMapParallaxLayer(
    int Index,
    float XFactor,
    float YFactor,
    CustomMapVisualResource Resource);

public sealed record CustomMapVisualResource(
    string Name,
    byte[] Bytes);
