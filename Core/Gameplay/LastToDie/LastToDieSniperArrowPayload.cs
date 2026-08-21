namespace OpenGarrison.Core;

/// <summary>
/// Release-time Huntsman payload shared by the first arrow and any queued
/// Menage A Trois arrows. It deliberately does not consult the owner's later
/// perk or critical-boost state.
/// </summary>
public readonly record struct LastToDieSniperArrowPayload(
    bool AppliesGuardian,
    bool PiercesPlayers,
    bool AppliesTranqDarts,
    float PoisonDamagePerSecond,
    float GhostDamageMultiplier,
    bool AppliesDecapitator,
    bool IsDecapitatorFullyCharged,
    bool AppliesExplosiveTip,
    bool IsCritical,
    float CriticalDamageMultiplier = 1f);

public readonly record struct LastToDieSniperVolleyState(
    byte QueuedArrowCount,
    byte DueArrowCount,
    byte SourceTicksUntilNextArrow,
    float VelocityX,
    float VelocityY,
    int Damage,
    float FakeSpeedMultiplier,
    LastToDieSniperArrowPayload Payload)
{
    public bool IsActive => QueuedArrowCount > 0 || DueArrowCount > 0;
}
