namespace OpenGarrison.Core;

public sealed record GameMakerSoundAsset(
    string Name,
    string MetadataPath,
    string AudioPath,
    string FileType,
    string Kind,
    float Pan,
    float Volume,
    bool Preload);
