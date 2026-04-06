namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void AdvanceActiveMatchObjectives()
    {
        switch (MatchRules.Mode)
        {
            case GameModeKind.Arena:
                UpdateArenaState();
                break;
            case GameModeKind.ControlPoint:
                UpdateControlPointState();
                break;
            case GameModeKind.KingOfTheHill:
            case GameModeKind.DoubleKingOfTheHill:
                UpdateControlPointState();
                UpdateKothState();
                break;
            case GameModeKind.Generator:
                UpdateGeneratorState();
                break;
            case GameModeKind.TeamDeathmatch:
                break;
            default:
                UpdateCaptureTheFlagState();
                break;
        }
    }

    private void AdvanceActiveMatchResolution()
    {
        AdvanceMatchState();
    }
}
