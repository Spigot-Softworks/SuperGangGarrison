namespace OpenGarrison.Core;

public readonly record struct WorldRocketSpawnEvent(
    int Id,
    byte Team,
    int OwnerId,
    float X,
    float Y,
    float PreviousX,
    float PreviousY,
    float DirectionRadians,
    float Speed,
    int TicksRemaining,
    float ReducedKnockbackSourceTicksRemaining,
    float ZeroKnockbackSourceTicksRemaining,
    int RangeAnchorOwnerId,
    float LastKnownRangeOriginX,
    float LastKnownRangeOriginY,
    float DistanceToTravel,
    bool IsFading,
    float FadeSourceTicksRemaining,
    bool ExplodeImmediately,
    bool IsCritical);
