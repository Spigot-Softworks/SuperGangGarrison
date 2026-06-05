using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed record BrowserAtlasManifest(
    int Version,
    IReadOnlyList<BrowserAtlasPageManifest> Atlases,
    IReadOnlyDictionary<string, BrowserAtlasSpriteManifest> Sprites);

public sealed record BrowserAtlasPageManifest(
    string Id,
    string ImagePath,
    int Width,
    int Height,
    string Group);

public sealed record BrowserAtlasSpriteManifest(
    int OriginX,
    int OriginY,
    int? FrameWidth,
    int? FrameHeight,
    IReadOnlyList<BrowserAtlasFrameManifest> Frames,
    BrowserAtlasMaskManifest? Mask,
    string SourceHash);

public sealed record BrowserAtlasFrameManifest(
    string AtlasId,
    int X,
    int Y,
    int Width,
    int Height,
    int SourceImageIndex,
    int SourceFrameIndex);

public sealed record BrowserAtlasMaskManifest(
    bool Separate,
    string Shape,
    string BoundsMode,
    int? Left,
    int? Top,
    int? Right,
    int? Bottom);
