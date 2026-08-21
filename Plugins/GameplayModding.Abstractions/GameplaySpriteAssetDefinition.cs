namespace OpenGarrison.GameplayModding;

public sealed record GameplaySpriteAssetDefinition(
    string Id,
    IReadOnlyList<string> FramePaths,
    int? FrameWidth = null,
    int? FrameHeight = null,
    int OriginX = 0,
    int OriginY = 0,
    GameplaySpriteMaskDefinition? Mask = null);

public sealed record GameplaySpriteMaskDefinition(
    bool Separate = false,
    string Shape = "",
    string BoundsMode = "",
    int? Left = null,
    int? Top = null,
    int? Right = null,
    int? Bottom = null);
