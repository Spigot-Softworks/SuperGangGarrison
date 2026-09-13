namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    // KOTH stages stay at zero until survivors own the point. CTF retains
    // survival-by-time, alongside its immediate RED / three-capture BLU wins.
    public bool CanCompleteLastToDieStageOnTimeout =>
        MatchRules.Mode is not (GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
        || (ControlPoints.Count > 0 && ControlPoints.All(point => point.Team == PlayerTeam.Red));
}
