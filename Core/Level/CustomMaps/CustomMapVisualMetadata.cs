namespace OpenGarrison.Core;

public sealed record CustomMapVisualMetadata(
    float ImageScale,
    string? BackgroundColor,
    string? VoidColor,
    IReadOnlyList<CustomMapParallaxLayer> ParallaxLayers,
    CustomMapVisualResource? Foreground,
    IReadOnlyDictionary<string, CustomMapVisualResource> Resources,
    IReadOnlyDictionary<string, CustomMapVisualResource> SpriteResources,
    IReadOnlyDictionary<int, ForegroundSpriteHitMask> ForegroundSpriteJungleHitMasks)
{
    public float ForegroundOffsetX { get; init; }

    public float ForegroundOffsetY { get; init; }

    private static readonly IReadOnlyDictionary<string, CustomMapVisualResource> EmptyResources =
        new Dictionary<string, CustomMapVisualResource>(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, CustomMapVisualResource> EmptySpriteResources =
        new Dictionary<string, CustomMapVisualResource>(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<int, ForegroundSpriteHitMask> EmptyHitMasks =
        new Dictionary<int, ForegroundSpriteHitMask>();

    public static CustomMapVisualMetadata Empty { get; } = new(
        ImageScale: 1f,
        BackgroundColor: null,
        VoidColor: null,
        ParallaxLayers: Array.Empty<CustomMapParallaxLayer>(),
        Foreground: null,
        Resources: EmptyResources,
        SpriteResources: EmptySpriteResources,
        ForegroundSpriteJungleHitMasks: EmptyHitMasks);
}

public sealed record CustomMapParallaxLayer(
    int Index,
    float XFactor,
    float YFactor,
    CustomMapVisualResource Resource);

public sealed record CustomMapVisualResource(
    string Name,
    byte[] Bytes);
