using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

public sealed record GameplayPrimaryLoadoutDefinition(
    string DefaultItemId,
    IReadOnlyList<string>? ItemIds = null,
    string SwitchPolicy = "primary_swap_station",
    string SelectionPersistence = "same_class_loadout")
{
    public IReadOnlyList<string> ItemIds { get; init; } = ItemIds ?? [];
}

public sealed record GameplaySecondaryLoadoutDefinition(string ItemId);
