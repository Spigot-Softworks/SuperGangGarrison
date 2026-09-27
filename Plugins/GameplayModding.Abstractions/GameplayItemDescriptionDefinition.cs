namespace OpenGarrison.GameplayModding;

/// <summary>
/// Player-facing description of a gameplay item.
/// </summary>
/// <param name="Summary">The summary text, if any.</param>
/// <param name="PositiveAttributes">The positive attribute lines, if any.</param>
/// <param name="NegativeAttributes">The negative attribute lines, if any.</param>
/// <param name="Notes">Additional notes, if any.</param>
public sealed record GameplayItemDescriptionDefinition(
    string? Summary = null,
    IReadOnlyList<string>? PositiveAttributes = null,
    IReadOnlyList<string>? NegativeAttributes = null,
    IReadOnlyList<string>? Notes = null);
