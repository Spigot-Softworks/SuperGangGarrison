using System;
using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

public sealed record GameplayModPackAssetCatalog(
    IReadOnlyDictionary<string, GameplaySpriteAssetDefinition> Sprites)
{
    public static GameplayModPackAssetCatalog Empty { get; } = new(new Dictionary<string, GameplaySpriteAssetDefinition>(0, StringComparer.Ordinal));
}
