namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeObjectiveFlowController
    {
        private readonly SimulationWorld _world;
        private readonly RuntimeArenaObjectiveController _arenaObjectiveController;
        private readonly RuntimeControlPointObjectiveController _controlPointObjectiveController;
        private readonly RuntimeKothObjectiveController _kothObjectiveController;
        private readonly RuntimeCaptureTheFlagUpdateController _captureTheFlagUpdateController;
        private readonly RuntimeCaptureTheFlagResolutionController _captureTheFlagResolutionController;
        private readonly RuntimeScoreLimitResolutionController _scoreLimitResolutionController;
        private readonly RuntimeScrObjectiveController _scrObjectiveController;
        private readonly RuntimeScrResolutionController _scrResolutionController;

        public RuntimeObjectiveFlowController(SimulationWorld world)
        {
            _world = world;
            _arenaObjectiveController = new RuntimeArenaObjectiveController(world);
            _controlPointObjectiveController = new RuntimeControlPointObjectiveController(world);
            _kothObjectiveController = new RuntimeKothObjectiveController(world);
            _captureTheFlagUpdateController = new RuntimeCaptureTheFlagUpdateController(world);
            _captureTheFlagResolutionController = new RuntimeCaptureTheFlagResolutionController(world);
            _scoreLimitResolutionController = new RuntimeScoreLimitResolutionController(world);
            _scrObjectiveController = new RuntimeScrObjectiveController(world);
            _scrResolutionController = new RuntimeScrResolutionController(world);
        }

        public void AdvanceObjectives()
        {
            if (_world.CompetitiveObjectivesLocked)
            {
                return;
            }

            switch (_world.MatchRules.Mode)
            {
                case GameModeKind.Arena:
                    _arenaObjectiveController.AdvanceObjectives();
                    break;
                case GameModeKind.ControlPoint:
                    _controlPointObjectiveController.AdvanceObjectives();
                    break;
                case GameModeKind.KingOfTheHill:
                case GameModeKind.DoubleKingOfTheHill:
                    _kothObjectiveController.AdvanceObjectives();
                    break;
                case GameModeKind.Generator:
                    SimulationWorld.UpdateGeneratorState();
                    break;
                case GameModeKind.TeamDeathmatch:
                    break;
                case GameModeKind.Scr:
                    _scrObjectiveController.AdvanceObjectives();
                    break;
                default:
                    _captureTheFlagUpdateController.AdvanceObjectives();
                    break;
            }
        }

        public void AdvanceResolution()
        {
            if (_world.CompetitiveObjectivesLocked)
            {
                return;
            }

            switch (_world.MatchRules.Mode)
            {
                case GameModeKind.Arena:
                    _arenaObjectiveController.AdvanceResolution();
                    break;
                case GameModeKind.ControlPoint:
                    _controlPointObjectiveController.AdvanceResolution();
                    break;
                case GameModeKind.KingOfTheHill:
                case GameModeKind.DoubleKingOfTheHill:
                    _kothObjectiveController.AdvanceResolution();
                    break;
                case GameModeKind.Generator:
                case GameModeKind.TeamDeathmatch:
                    _scoreLimitResolutionController.AdvanceResolution();
                    break;
                case GameModeKind.Scr:
                    _scrResolutionController.AdvanceResolution();
                    break;
                default:
                    _captureTheFlagResolutionController.AdvanceResolution();
                    break;
            }
        }

        public void AdvanceLegacyMatchState()
        {
            AdvanceResolution();
        }

        public void AdvanceLegacyControlPointMatchState()
        {
            _controlPointObjectiveController.AdvanceResolution();
        }

        public void AdvanceLegacyGeneratorMatchState()
        {
            _scoreLimitResolutionController.AdvanceResolution();
        }

        public void AdvanceLegacyCaptureTheFlagState()
        {
            _captureTheFlagUpdateController.AdvanceObjectives();
        }

        public void AdvanceLegacyScrState()
        {
            _scrObjectiveController.AdvanceObjectives();
        }

        public void AdvanceLegacyArenaState()
        {
            _arenaObjectiveController.AdvanceObjectives();
        }
    }
}
