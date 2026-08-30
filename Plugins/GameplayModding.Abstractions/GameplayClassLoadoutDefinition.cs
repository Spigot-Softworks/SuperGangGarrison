using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

public sealed record GameplayClassLoadoutDefinition(
    string Id,
    string DisplayName,
    string PrimaryItemId = "",
    string? SecondaryItemId = null,
    string? UtilityItemId = null,
    IReadOnlyList<string>? AbilityItemIds = null)
{
    public GameplayPrimaryLoadoutDefinition? Primary { get; init; }

    public GameplaySecondaryLoadoutDefinition? Secondary { get; init; }

    public IReadOnlyList<string> Abilities { get; init; } = [];
}
