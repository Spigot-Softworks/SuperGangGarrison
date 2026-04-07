namespace OpenGarrison.GameplayModding;

public sealed record GameplaySpriteAssetDefinition(
    string Id,
    IReadOnlyList<string> FramePaths,
    int? FrameWidth = null,
    int? FrameHeight = null,
    int OriginX = 0,
    int OriginY = 0);
