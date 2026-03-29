namespace OpenGarrison.Core;

public sealed record MatchState(
    MatchPhase Phase,
    int TimeRemainingTicks,
    PlayerTeam? WinnerTeam)
{
    public bool IsOvertime => Phase == MatchPhase.Overtime;

    public bool IsEnded => Phase == MatchPhase.Ended;
}
