namespace OpenGarrison.GameplayModding;

/// <summary>
/// A sound asset definition: a WAV or OGG file in the pack, played by name like stock sounds.
/// </summary>
/// <param name="Id">The sound name (at most 64 UTF-8 bytes, so it fits in sound events).</param>
/// <param name="Path">The pack-relative file path (.wav or .ogg).</param>
public sealed record GameplaySoundAssetDefinition(
    string Id,
    string Path);
