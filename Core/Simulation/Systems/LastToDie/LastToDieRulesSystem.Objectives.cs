namespace OpenGarrison.Core;

internal sealed partial class LastToDieRulesSystem
{
    // KOTH stages stay at zero until survivors own the point. CTF retains
    // survival-by-time, alongside its immediate RED / three-capture BLU wins.
    internal bool CanCompleteLastToDieStageOnTimeout =>
        _host.MatchRules.Mode is not (GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
        || (_host.ControlPoints.Count > 0 && _host.ControlPoints.All(point => point.Team == PlayerTeam.Red));
}
