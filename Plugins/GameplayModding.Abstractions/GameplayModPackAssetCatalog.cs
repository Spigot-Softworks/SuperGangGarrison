using System;
using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

/// <summary>
/// The sprite and sound asset catalog of a gameplay mod pack.
/// </summary>
/// <param name="Sprites">The sprite definitions keyed by sprite name.</param>
/// <param name="Sounds">The sound definitions keyed by sound name, if any.</param>
public sealed record GameplayModPackAssetCatalog(
    IReadOnlyDictionary<string, GameplaySpriteAssetDefinition> Sprites,
    IReadOnlyDictionary<string, GameplaySoundAssetDefinition>? Sounds = null)
{
    /// <summary>
    /// Gets an empty asset catalog.
    /// </summary>
    public static GameplayModPackAssetCatalog Empty { get; } = new(new Dictionary<string, GameplaySpriteAssetDefinition>(0, StringComparer.Ordinal));

    /// <summary>
    /// Gets the sound definitions, or an empty map when the pack has none.
    /// </summary>
    public IReadOnlyDictionary<string, GameplaySoundAssetDefinition> SoundsOrEmpty
        => Sounds ?? EmptySounds;

    private static readonly IReadOnlyDictionary<string, GameplaySoundAssetDefinition> EmptySounds
        = new Dictionary<string, GameplaySoundAssetDefinition>(0, StringComparer.Ordinal);
}
