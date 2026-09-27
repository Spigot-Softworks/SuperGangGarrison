using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

/// <summary>
/// The definition of a class loadout.
/// </summary>
/// <param name="Id">The loadout id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="PrimaryItemId">The primary item id.</param>
/// <param name="SecondaryItemId">The secondary item id, if any.</param>
/// <param name="UtilityItemId">The utility item id, if any.</param>
/// <param name="AbilityItemIds">The ability item ids, if any.</param>
public sealed record GameplayClassLoadoutDefinition(
    string Id,
    string DisplayName,
    string PrimaryItemId = "",
    string? SecondaryItemId = null,
    string? UtilityItemId = null,
    IReadOnlyList<string>? AbilityItemIds = null)
{
    /// <summary>
    /// Gets the primary loadout definition, if any.
    /// </summary>
    public GameplayPrimaryLoadoutDefinition? Primary { get; init; }

    /// <summary>
    /// Gets the secondary loadout definition, if any.
    /// </summary>
    public GameplaySecondaryLoadoutDefinition? Secondary { get; init; }

    /// <summary>
    /// Gets the ability item ids.
    /// </summary>
    public IReadOnlyList<string> Abilities { get; init; } = [];
}
