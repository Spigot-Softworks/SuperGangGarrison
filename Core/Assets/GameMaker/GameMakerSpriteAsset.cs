using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed record GameMakerSpriteAsset(
    string Name,
    string MetadataPath,
    IReadOnlyList<string> FramePaths,
    int OriginX,
    int OriginY,
    bool Preload,
    bool Transparent,
    GameMakerSpriteMask Mask);

public sealed record GameMakerSpriteMask(
    bool Separate,
    string Shape,
    string BoundsMode,
    int? Left,
    int? Top,
    int? Right,
    int? Bottom);
