namespace OpenGarrison.GameplayModding;

public sealed record GameplayItemOwnershipDefinition(
    bool TrackOwnership = false,
    bool DefaultGranted = true,
    bool GrantOnAcquire = false,
    string? GrantKey = null);
