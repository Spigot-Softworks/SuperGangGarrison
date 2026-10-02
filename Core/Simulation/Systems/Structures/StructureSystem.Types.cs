namespace OpenGarrison.Core;

internal enum OwnedSentryDestroyResult
{
    NoOwnedSentry,
    Destroyed,
}

internal readonly record struct SentryTarget(
    PlayerEntity? Player,
    GeneratorState? Generator,
    SentryEntity? Sentry,
    JumpPadEntity? JumpPad,
    int? DamageableZoneRoomObjectIndex,
    float X,
    float Y,
    int? PlayerId);

