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
            _world.AdvanceVipState();
        }

        public void AdvancePresentationAndChatPhase()
        {
            _world.AdvanceKillFeed();
            _world.AdvanceLocalDeathCam();

            AdvanceNetworkPlayerChatBubbleState(SimulationWorld.LocalPlayerSlot);
            foreach (var slot in _world._enabledAdditionalNetworkPlayerSlots)
            {
                AdvanceNetworkPlayerChatBubbleState(slot);
            }

            if (_world.EnemyPlayerEnabled)
            {
                _world.EnemyPlayer.AdvanceChatBubbleState();
            }

            _world.FriendlyDummy.AdvanceChatBubbleState();
        }

        public void AdvancePostPlayerMatchPhase()
        {
            _world.EmitPendingMedicUberReadyPresentation();
            _world.AdvanceExperimentalRageState();
            _objectiveFlowController.AdvanceObjectives();
            _world.UpdateAuxiliaryControlPointStateIfNeeded();
            _world.TickForegroundSpriteJungle();
            _world.TickSpritesheetPlayback();
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

        public void AdvanceLegacyScrState()
        {
            _objectiveFlowController.AdvanceLegacyScrState();
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
