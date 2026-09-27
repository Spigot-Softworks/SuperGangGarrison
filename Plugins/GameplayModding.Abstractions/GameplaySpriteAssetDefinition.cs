namespace OpenGarrison.GameplayModding;

/// <summary>
/// A sprite asset definition.
/// </summary>
/// <param name="Id">The sprite id.</param>
/// <param name="FramePaths">The frame paths.</param>
/// <param name="FrameWidth">The frame width, if any.</param>
/// <param name="FrameHeight">The frame height, if any.</param>
/// <param name="OriginX">The origin X.</param>
/// <param name="OriginY">The origin Y.</param>
/// <param name="Mask">The mask definition, if any.</param>
public sealed record GameplaySpriteAssetDefinition(
    string Id,
    IReadOnlyList<string> FramePaths,
    int? FrameWidth = null,
    int? FrameHeight = null,
    int OriginX = 0,
    int OriginY = 0,
    GameplaySpriteMaskDefinition? Mask = null);

/// <summary>
/// A sprite mask definition.
/// </summary>
/// <param name="Separate">Whether the mask is separate from the sprite.</param>
/// <param name="Shape">The mask shape.</param>
/// <param name="BoundsMode">The bounds mode.</param>
/// <param name="Left">The left bound, if any.</param>
/// <param name="Top">The top bound, if any.</param>
/// <param name="Right">The right bound, if any.</param>
/// <param name="Bottom">The bottom bound, if any.</param>
public sealed record GameplaySpriteMaskDefinition(
    bool Separate = false,
    string Shape = "",
    string BoundsMode = "",
    int? Left = null,
    int? Top = null,
    int? Right = null,
    int? Bottom = null);
