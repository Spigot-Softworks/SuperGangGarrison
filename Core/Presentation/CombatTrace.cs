namespace OpenGarrison.Core;

public readonly record struct CombatTrace(
    float StartX,
    float StartY,
    float EndX,
    float EndY,
    int TicksRemaining,
    bool HitCharacter,
    PlayerTeam Team,
    bool IsSniperTracer);
