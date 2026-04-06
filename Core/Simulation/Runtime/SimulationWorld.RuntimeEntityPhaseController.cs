namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeEntityPhaseController
    {
        private readonly SimulationWorld _world;

        public RuntimeEntityPhaseController(SimulationWorld world)
        {
            _world = world;
        }

        public void AdvanceProjectileAndTransientEntityPhase()
        {
            _world.AdvanceCombatTraces();
            _world.AdvanceShots();
            _world.AdvanceBubbles();
            _world.AdvanceBlades();
            _world.AdvanceNeedles();
            _world.AdvanceRevolverShots();
            _world.AdvanceStabAnimations();
            _world.AdvanceStabMasks();
            _world.AdvanceFlames();
            _world.AdvanceFlares();
            _world.AdvanceRockets();
            _world.AdvanceMines();
            _world.AdvancePlayerGibs();
            _world.AdvanceBloodDrops();
            _world.AdvanceDeadBodies();
            _world.AdvanceSentryGibs();
        }

        public void AdvancePlayerSimulationPhase()
        {
            for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
            {
                _world.AdvancePlayableNetworkPlayer(NetworkPlayerSlots[index]);
            }

            _world.AdvanceEnemyDummy();

            if (_world.FriendlyDummyEnabled && _world.FriendlyDummy.IsAlive)
            {
                _world.ApplyRoomForces(_world.FriendlyDummy);
                _world.FriendlyDummy.Advance(default, false, _world.Level, _world.FriendlyDummy.Team, _world.Config.FixedDeltaSeconds);
                _world.UpdateSpawnRoomState(_world.FriendlyDummy);
                _world.TryActivatePendingSpyBackstab(_world.FriendlyDummy);
                _world.ApplyHealingCabinets(_world.FriendlyDummy);
                _world.ApplyRoomHazards(_world.FriendlyDummy);
            }
        }

        public void AdvancePostPlayerEntityPhase()
        {
            _world.AdvanceHealthPacks();
            _world.AdvanceDroppedWeapons();
            _world.AdvanceAfterburnAlertBubbles();
            _world.AdvanceSentries();
        }
    }
}
