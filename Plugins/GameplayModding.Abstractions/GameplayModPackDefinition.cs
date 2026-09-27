using System;
using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

/// <summary>
/// The definition of a gameplay mod pack.
/// </summary>
/// <param name="Id">The mod pack id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Version">The version.</param>
/// <param name="Items">The item definitions keyed by item id.</param>
/// <param name="Classes">The class definitions keyed by class id.</param>
/// <param name="Assets">The asset catalog.</param>
/// <param name="SchemaVersion">The schema version.</param>
public sealed record GameplayModPackDefinition(
    string Id,
    string DisplayName,
    Version Version,
    IReadOnlyDictionary<string, GameplayItemDefinition> Items,
    IReadOnlyDictionary<string, GameplayClassDefinition> Classes,
    GameplayModPackAssetCatalog Assets,
    int SchemaVersion = 1);
