namespace OpenGarrison.Core;

public readonly record struct WorldHealingEvent(
    int TargetPlayerId,
    int Amount,
    ulong SourceFrame = 0);
