namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimePhaseController
    {
        private readonly SimulationWorld _world;
        private readonly RuntimeEntityPhaseController _entityPhaseController;
        private readonly RuntimeMatchPhaseController _matchPhaseController;

        public RuntimePhaseController(SimulationWorld world)
        {
            _world = world;
            _entityPhaseController = new RuntimeEntityPhaseController(world);
            _matchPhaseController = new RuntimeMatchPhaseController(world);
        }

        public void AdvanceClientPredictionPhase()
        {
            // Client prediction: projectiles and deterministic map motion only.
            // Server remains authoritative for all players (including local player).
            _entityPhaseController.AdvanceProjectileAndTransientEntityPhase();
            _world.AdvanceMovingPlatforms();
            _entityPhaseController.AdvanceRemoteSnapshotPlayerTauntStates();
        }

        public void AdvanceProjectilePhaseOnly()
        {
            // Only advance projectiles and transient entities
            _entityPhaseController.AdvanceProjectileAndTransientEntityPhase();
        }

        public void AdvancePrePlayerSimulationPhase()
        {
            _matchPhaseController.AdvancePrePlayerMatchPhase();
            _entityPhaseController.AdvanceProjectileAndTransientEntityPhase();
            _matchPhaseController.AdvancePresentationAndChatPhase();
        }

        public void AdvancePlayerSimulationPhase()
        {
            _entityPhaseController.AdvancePlayerSimulationPhase();
        }

        public void AdvancePostPlayerSimulationPhase()
        {
            _entityPhaseController.AdvancePostPlayerEntityPhase();
            _matchPhaseController.AdvancePostPlayerMatchPhase();
        }

        public void AdvanceLegacyMatchState()
        {
            _matchPhaseController.AdvanceLegacyMatchState();
        }

        public void AdvanceLegacyControlPointMatchState()
        {
            _matchPhaseController.AdvanceLegacyControlPointMatchState();
        }

        public void AdvanceLegacyKothMatchState()
        {
            _matchPhaseController.AdvanceLegacyKothMatchState();
        }

        public void AdvanceLegacyGeneratorMatchState()
        {
            _matchPhaseController.AdvanceLegacyGeneratorMatchState();
        }

        public void AdvanceLegacyCaptureTheFlagState()
        {
            _matchPhaseController.AdvanceLegacyCaptureTheFlagState();
        }

        public void AdvanceLegacyArenaState()
        {
            _matchPhaseController.AdvanceLegacyArenaState();
        }
    }
}
