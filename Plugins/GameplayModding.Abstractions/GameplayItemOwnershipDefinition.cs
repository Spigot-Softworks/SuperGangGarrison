namespace OpenGarrison.GameplayModding;

/// <summary>
/// Ownership behavior for a gameplay item.
/// </summary>
/// <param name="TrackOwnership">Whether ownership is tracked per player.</param>
/// <param name="DefaultGranted">Whether the item is granted by default.</param>
/// <param name="GrantOnAcquire">Whether the item is granted when acquired.</param>
/// <param name="GrantKey">The grant key, if any.</param>
public sealed record GameplayItemOwnershipDefinition(
    bool TrackOwnership = false,
    bool DefaultGranted = true,
    bool GrantOnAcquire = false,
    string? GrantKey = null);
