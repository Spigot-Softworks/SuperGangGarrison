namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeMatchPhaseController
    {
        private readonly SimulationWorld _world;
        private readonly RuntimeObjectiveFlowController _objectiveFlowController;

        public RuntimeMatchPhaseController(SimulationWorld world)
        {
            _world = world;
            _objectiveFlowController = new RuntimeObjectiveFlowController(world);
        }

        public void AdvancePrePlayerMatchPhase()
        {
            _world.ApplyExperimentalRageEffects();
            _world.AdvanceMedicUberEffects();
        }

        public void AdvancePresentationAndChatPhase()
        {
            _world.AdvanceKillFeed();
            _world.AdvanceLocalDeathCam();

            for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
            {
                AdvanceNetworkPlayerChatBubbleState(NetworkPlayerSlots[index]);
            }

            if (_world.EnemyPlayerEnabled)
            {
                _world.EnemyPlayer.AdvanceChatBubbleState();
            }

            _world.FriendlyDummy.AdvanceChatBubbleState();
        }

        public void AdvancePostPlayerMatchPhase()
        {
            _world.AdvanceExperimentalRageState();
            _objectiveFlowController.AdvanceObjectives();
            _objectiveFlowController.AdvanceResolution();
        }

        public void AdvanceLegacyMatchState()
        {
            _objectiveFlowController.AdvanceLegacyMatchState();
        }

        public void AdvanceLegacyControlPointMatchState()
        {
            _objectiveFlowController.AdvanceLegacyControlPointMatchState();
        }

        public void AdvanceLegacyKothMatchState()
        {
            _objectiveFlowController.AdvanceResolution();
        }

        public void AdvanceLegacyGeneratorMatchState()
        {
            _objectiveFlowController.AdvanceLegacyGeneratorMatchState();
        }

        public void AdvanceLegacyCaptureTheFlagState()
        {
            _objectiveFlowController.AdvanceLegacyCaptureTheFlagState();
        }

        public void AdvanceLegacyArenaState()
        {
            _objectiveFlowController.AdvanceLegacyArenaState();
        }

        private void AdvanceNetworkPlayerChatBubbleState(byte slot)
        {
            if (_world.TryGetNetworkPlayer(slot, out var player))
            {
                player.AdvanceChatBubbleState();
            }
        }
    }
}
