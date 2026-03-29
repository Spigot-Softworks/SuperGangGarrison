namespace OpenGarrison.Core;

public sealed record GameMakerBackgroundAsset(
    string Name,
    string MetadataPath,
    string ImagePath,
    bool Preload,
    bool Transparent);
