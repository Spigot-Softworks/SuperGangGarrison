namespace OpenGarrison.Core;

public enum DamageTargetKind : byte
{
    None = 0,
    Player = 1,
    Sentry = 2,
    Generator = 3,
}

public readonly record struct WorldDamageEvent(
    int Amount,
    int AttackerPlayerId,
    int AssistedByPlayerId,
    DamageTargetKind TargetKind,
    int TargetEntityId,
    float X,
    float Y,
    bool WasFatal,
    ulong EventId = 0,
    ulong SourceFrame = 0);
