using System;
using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

/// <summary>
/// The sprite asset catalog of a gameplay mod pack.
/// </summary>
/// <param name="Sprites">The sprite definitions keyed by sprite name.</param>
public sealed record GameplayModPackAssetCatalog(
    IReadOnlyDictionary<string, GameplaySpriteAssetDefinition> Sprites)
{
    /// <summary>
    /// Gets an empty asset catalog.
    /// </summary>
    public static GameplayModPackAssetCatalog Empty { get; } = new(new Dictionary<string, GameplaySpriteAssetDefinition>(0, StringComparer.Ordinal));
}
