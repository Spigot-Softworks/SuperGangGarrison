namespace OpenGarrison.GameplayModding;

public sealed record GameplayClassLoadoutDefinition(
    string Id,
    string DisplayName,
    string PrimaryItemId,
    string? SecondaryItemId = null,
    string? UtilityItemId = null);
