using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

/// <summary>
/// Well-known loadout policies.
/// </summary>
public static class GameplayLoadoutPolicies
{
    /// <summary>The primary swap station policy.</summary>
    public const string PrimarySwapStation = "primary_swap_station";

    /// <summary>The same-class loadout selection persistence policy.</summary>
    public const string SameClassLoadout = "same_class_loadout";
}

/// <summary>
/// The primary loadout definition of a class.
/// </summary>
/// <param name="DefaultItemId">The default item id.</param>
/// <param name="ItemIds">The available item ids, if any.</param>
/// <param name="SwitchPolicy">The switch policy.</param>
/// <param name="SelectionPersistence">The selection persistence policy.</param>
public sealed record GameplayPrimaryLoadoutDefinition(
    string DefaultItemId,
    IReadOnlyList<string>? ItemIds = null,
    string SwitchPolicy = GameplayLoadoutPolicies.PrimarySwapStation,
    string SelectionPersistence = GameplayLoadoutPolicies.SameClassLoadout)
{
    /// <summary>
    /// Gets the available item ids.
    /// </summary>
    public IReadOnlyList<string> ItemIds { get; init; } = ItemIds ?? [];
}

/// <summary>
/// The secondary loadout definition of a class.
/// </summary>
/// <param name="ItemId">The item id.</param>
public sealed record GameplaySecondaryLoadoutDefinition(string ItemId);
