namespace OpenGarrison.Core;

public sealed record MatchRules(
    GameModeKind Mode,
    int TimeLimitMinutes,
    int TimeLimitTicks,
    int CapLimit);
