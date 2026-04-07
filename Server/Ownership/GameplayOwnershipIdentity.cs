namespace OpenGarrison.Server;

internal readonly record struct GameplayOwnershipIdentity(
    string Key,
    string DisplayName,
    ulong BadgeMask);
