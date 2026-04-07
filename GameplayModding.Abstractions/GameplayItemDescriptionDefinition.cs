namespace OpenGarrison.GameplayModding;

public sealed record GameplayItemDescriptionDefinition(
    string? Summary = null,
    IReadOnlyList<string>? PositiveAttributes = null,
    IReadOnlyList<string>? NegativeAttributes = null,
    IReadOnlyList<string>? Notes = null);
