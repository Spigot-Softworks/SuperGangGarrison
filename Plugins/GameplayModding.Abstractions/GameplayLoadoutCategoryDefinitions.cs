using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

public static class GameplayLoadoutPolicies
{
    public const string PrimarySwapStation = "primary_swap_station";

    public const string SameClassLoadout = "same_class_loadout";
}

public sealed record GameplayPrimaryLoadoutDefinition(
    string DefaultItemId,
    IReadOnlyList<string>? ItemIds = null,
    string SwitchPolicy = GameplayLoadoutPolicies.PrimarySwapStation,
    string SelectionPersistence = GameplayLoadoutPolicies.SameClassLoadout)
{
    public IReadOnlyList<string> ItemIds { get; init; } = ItemIds ?? [];
}

public sealed record GameplaySecondaryLoadoutDefinition(string ItemId);
